using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Hosting;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A notice to an account, on every device it registered (friends plan, stage 1): what a seat's own notice
/// cannot carry, because the player it is for holds no seat yet -- a friend's request, an invitation, the end of
/// a match. Each device is told once; a device whose push service says it is gone is forgotten, and its endpoint
/// remembered as gone with the account's devices (ADR 0203), so the page that still holds it is answered 410 and makes
/// a new one whichever process heard it. A host with a notices queue puts one item a device on it
/// (<see cref="EnqueueAsync" />), which <see cref="NoticeWorker" /> sends one at a time (<see cref="SendOneAsync" />).
/// </summary>
internal sealed class AccountNotices(IPushSender sender, AccountDevices devices)
{
    // A notice says it is a test only when it is one: the worker reads the field's presence.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Whether a push service said this endpoint of the account is gone: a device the page should not send again.</summary>
    public Task<bool> IsGoneAsync(AccountId account, string endpoint, CancellationToken cancellationToken = default) =>
        devices.IsGoneAsync(account, endpoint, cancellationToken);

    /// <summary>
    /// One item a device of the account on the notices queue, but the endpoints <paramref name="except" /> names (ADR
    /// 0203): each is sent, tried again and given up on by itself, so a phone that is out delays no other. How many
    /// were put on it.
    /// </summary>
    public async Task<int> EnqueueAsync(IWorkChannel outbox, AccountId to, string payload, string topic, IReadOnlyCollection<string>? except, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var queued = 0;
        var endpoints = (await devices.ListAsync(to, cancellationToken))
            .Select(device => device.Endpoint)
            .Distinct(StringComparer.Ordinal)
            .Where(endpoint => except is null || !except.Contains(endpoint, StringComparer.Ordinal));
        foreach (var endpoint in endpoints)
        {
            await outbox.SendAsync(QueuedNotice.Of(new DevicePush(to.Value, endpoint, payload, topic)).ToJson(), KeptFor, cancellationToken: cancellationToken);
            queued++;
        }

        return queued;
    }

    /// <summary>How long a device's item waits on the queue: no longer than the push service keeps a notice for a phone that is off.</summary>
    public static readonly TimeSpan KeptFor = WebPushSender.KeptFor;

    /// <summary>
    /// One queued item: the payload to the device at that endpoint, if it is still one of the account's -- a browser
    /// turned off or signed out since is told nothing. A device whose push service says it is gone is forgotten and its
    /// endpoint remembered as gone. A service that is out is thrown as a <see cref="PushRefusedException" />, for the
    /// queue to try again; the store's own failure is thrown as it is.
    /// </summary>
    public async Task<DeviceSend> SendOneAsync(DevicePush item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var account = AccountId.Parse(item.Account);
        var device = (await devices.ListAsync(account, cancellationToken)).FirstOrDefault(device => string.Equals(device.Endpoint, item.Endpoint, StringComparison.Ordinal));
        if (device is null)
        {
            return DeviceSend.NotADevice;
        }

        PushSent sent;
        try
        {
            sent = await sender.SendAsync(Subscription(device), item.Payload, item.Topic, cancellationToken);
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested && failure is not PushRefusedException)
        {
            // The push service's own failure, told apart from the store's around it (N6): only this one is a refusal.
            throw new PushRefusedException(failure.Message, failure);
        }

        if (sent is PushSent.Taken)
        {
            return DeviceSend.Sent;
        }

        await ForgetAsync(account, device.Endpoint, cancellationToken);
        return DeviceSend.Gone;
    }

    /// <summary>
    /// What the service worker is handed: a title, a line, where a tap goes, and a tag that replaces an earlier
    /// notice of the same thing on the phone. A name is public on every card already; nothing else travels.
    /// </summary>
    public static string Payload(string title, string body, string url, string tag, bool test = false) =>
        JsonSerializer.Serialize(new { title, body, url, tag, test = test ? true : (bool?)null }, Web);

    /// <summary>
    /// Sends the payload to every device of the account, under one topic a kind of notice. How many took it. A
    /// device that is gone is forgotten; a service that is out is a line on the console and a notice that did
    /// not arrive, never a reason to drop the device.
    /// </summary>
    public Task<int> SendAsync(AccountId to, string payload, string topic, CancellationToken cancellationToken = default) =>
        SendAsync(to, payload, topic, except: null, cancellationToken);

    /// <summary>As <see cref="SendAsync(AccountId, string, string, CancellationToken)" />, to every device but the endpoints <paramref name="except" /> names.</summary>
    public async Task<int> SendAsync(AccountId to, string payload, string topic, IReadOnlyCollection<string>? except, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var reached = 0;
        foreach (var device in (await devices.ListAsync(to, cancellationToken)).DistinctBy(device => device.Endpoint, StringComparer.Ordinal))
        {
            if (except is not null && except.Contains(device.Endpoint, StringComparer.Ordinal))
            {
                continue;
            }

            try
            {
                if (await sender.SendAsync(Subscription(device), payload, topic, cancellationToken) is PushSent.Taken)
                {
                    reached++;
                    continue;
                }

                await ForgetAsync(to, device.Endpoint, cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Azure.RequestFailedException or System.Security.Cryptography.CryptographicException)
            {
                Console.WriteLine($"  Account {to}: a device could not be notified: {failure.Message}");
            }
        }

        return reached;
    }

    /// <summary>
    /// A notice to one of the account's devices now, on request, whatever its page is doing: what a player taps to
    /// see whether this browser can be reached at all, and what the push service answers when it cannot.
    /// </summary>
    public async Task<PushTest> TestAsync(AccountId account, string? endpoint, CancellationToken cancellationToken = default)
    {
        var device = (await devices.ListAsync(account, cancellationToken)).FirstOrDefault(device => string.Equals(device.Endpoint, endpoint, StringComparison.Ordinal));
        if (device is null)
        {
            return new PushTest(false, "This browser is not one of your devices: turn on \"Pop-ups on this device\" first.");
        }

        var payload = Payload("Notifications work", "This device is told about your friends, your invitations and your games.", "/lobby", "test:account", test: true);
        try
        {
            var sent = await sender.SendAsync(Subscription(device), payload, "test", cancellationToken);
            if (sent is PushSent.Taken)
            {
                Console.WriteLine($"  Account {account}: sent a test notice.");
                return new PushTest(true, "Sent. If nothing showed, check the device's notification settings for this browser.");
            }

            await ForgetAsync(account, device.Endpoint, cancellationToken);
            // A device kept from before the host checked push services is never sent to. Turning it on again would be
            // refused on that service too, so answer with that refusal, not "gone" (#1176, #1209).
            return sent is PushSent.UnknownService
                ? PushTest.OnUnknownService
                : new PushTest(false, PushTest.DeviceTestGoneMessage);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Azure.RequestFailedException or System.Security.Cryptography.CryptographicException)
        {
            Console.WriteLine($"  Account {account}: could not be sent a test notice: {HttpHost.OneLine(failure)}");
            return new PushTest(false, PushTest.Refused(failure, PushTest.DeviceAgain));
        }
    }

    private Task<bool> ForgetAsync(AccountId account, string endpoint, CancellationToken cancellationToken) =>
        devices.MarkGoneAsync(account, endpoint, cancellationToken);

    private static PushSubscription Subscription(PushDevice device) => new(device.Endpoint, device.P256dh, device.Auth);
}
