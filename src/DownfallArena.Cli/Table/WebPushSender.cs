using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The Web Push protocol (RFC 8030, 8291, 8292) behind <see cref="IPushSender" />: the payload encrypted to the
/// browser's keys, the request signed with the host's VAPID pair, sent to the push service the browser named.
/// The library does the encryption and the signing (ADR 0118); this maps a seat's subscription onto it and
/// reads its one answer that matters.
/// </summary>
internal sealed class WebPushSender : IPushSender, IDisposable
{
    /// <summary>How long a push service keeps a notice for a phone that is off: a turn is worth waiting ten minutes for, not a day.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromMinutes(10);

    private readonly PushServiceClient _client;
    private readonly VapidAuthentication _authentication;
    private readonly HttpClient? _owned;

    /// <param name="http">The client the requests go through; a test hands one aimed at its own listener. Without one the sender makes, and owns, its own.</param>
    public WebPushSender(PushKeys keys, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = keys.Subject };
        _owned = http is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(10) } : null;
        _client = new PushServiceClient(http ?? _owned!)
        {
            DefaultAuthentication = _authentication,
            DefaultTimeToLive = (int)KeptFor.TotalSeconds,
        };
    }

    public async Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(payload);

        var subscription = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = to.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, to.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, to.Auth);

        // One topic a seat: a second notice before the first was read replaces it on the service rather than
        // queueing behind it, so a phone switched back on reads one "your turn", the latest.
        var message = new PushMessage(payload) { Topic = "turn", Urgency = PushMessageUrgency.High };
        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
            return true;
        }
        catch (PushServiceClientException refusal) when (refusal.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return false;
        }
        catch (PushServiceClientException refusal)
        {
            // Every other refusal is the service's, not the subscription's: said as the HTTP failure it is, so
            // the caller keeps the subscription and reports the line.
            throw new HttpRequestException($"The push service answered {(int)refusal.StatusCode}: {refusal.Message}", refusal, refusal.StatusCode);
        }
    }

    public void Dispose()
    {
        _authentication.Dispose();
        _owned?.Dispose();
    }
}
