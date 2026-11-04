using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A notice to an account, on every device it registered (friends plan, stage 1): what a seat's own notice
/// cannot carry, because the player it is for holds no seat yet -- a friend's request, an invitation, the end of
/// a match. Each device is told once; a device whose push service says it is gone is forgotten, and its endpoint
/// remembered as dead so the page that still holds it is answered 410 and makes a new one, as a seat's is
/// (<see cref="PushNotices" />).
/// </summary>
internal sealed class AccountNotices(IPushSender sender, AccountDevices devices)
{
    // A notice says it is a test only when it is one: the worker reads the field's presence.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly ConcurrentDictionary<string, byte> _dead = new(StringComparer.Ordinal);

    /// <summary>Whether a push service said this endpoint is gone: a device the page should not send again.</summary>
    public bool IsDead(string endpoint) => _dead.ContainsKey(endpoint);

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
    public async Task<int> SendAsync(AccountId to, string payload, string topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var reached = 0;
        foreach (var device in (await devices.ListAsync(to, cancellationToken)).DistinctBy(device => device.Endpoint, StringComparer.Ordinal))
        {
            try
            {
                if (await sender.SendAsync(Subscription(device), payload, topic, cancellationToken))
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
            return new PushTest(false, "This browser is not one of your devices: tap \"Notify me on this device\" first.");
        }

        var payload = Payload("Notifications work", "This device is told about your friends, your invitations and your games.", "/lobby", "test:account", test: true);
        try
        {
            if (await sender.SendAsync(Subscription(device), payload, "test", cancellationToken))
            {
                Console.WriteLine($"  Account {account}: sent a test notice.");
                return new PushTest(true, "Sent. If nothing showed, check the device's notification settings for this browser.");
            }

            await ForgetAsync(account, device.Endpoint, cancellationToken);
            return new PushTest(false, "The push service says this subscription is gone. Tap \"Notify me on this device\" again.");
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Azure.RequestFailedException or System.Security.Cryptography.CryptographicException)
        {
            Console.WriteLine($"  Account {account}: could not be sent a test notice: {failure.Message}");
            return new PushTest(false, $"The push service refused it: {failure.Message}");
        }
    }

    private async Task ForgetAsync(AccountId account, string endpoint, CancellationToken cancellationToken)
    {
        _dead[endpoint] = 0;
        await devices.RemoveAsync(account, endpoint, cancellationToken);
    }

    private static PushSubscription Subscription(PushDevice device) => new(device.Endpoint, device.P256dh, device.Auth);
}
