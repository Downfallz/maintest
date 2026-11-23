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

    /// <summary>
    /// How long one send may take before it is given up: a push service answers in well under a second, and the notices
    /// worker sends one look's items side by side, so a slow one must not hold its time (#1132).
    /// </summary>
    public static readonly TimeSpan SendFor = TimeSpan.FromSeconds(5);

    private readonly PushServiceClient _client;
    private readonly VapidAuthentication _authentication;
    private readonly HttpClient? _owned;
    private readonly Func<string, bool> _sendsTo;
    private readonly TimeSpan _sendFor;

    /// <param name="http">The client the requests go through; a test hands one aimed at its own listener. Without one the sender makes, and owns, its own.</param>
    /// <param name="sendsTo">Which endpoints it sends to: the browsers' push services (<see cref="PushSubscription.OnKnownService" />) unless a test says otherwise.</param>
    /// <param name="sendFor">How long one send may take, retries included: <see cref="SendFor" /> unless a test says otherwise.</param>
    public WebPushSender(PushKeys keys, HttpClient? http = null, Func<string, bool>? sendsTo = null, TimeSpan? sendFor = null)
    {
        _sendsTo = sendsTo ?? PushSubscription.OnKnownService;
        _sendFor = sendFor ?? SendFor;
        ArgumentNullException.ThrowIfNull(keys);
        _authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = keys.Subject };
        _owned = http is null ? new HttpClient { Timeout = _sendFor } : null;
        _client = new PushServiceClient(http ?? _owned!)
        {
            DefaultAuthentication = _authentication,
            DefaultTimeToLive = (int)KeptFor.TotalSeconds,
            // The library retries a 429 as long and as often as the service asks, past any timeout of the client's:
            // a refusal to take the notice now is a failure of this send, said as one (#1132).
            AutoRetryAfter = false,
        };
    }

    public async Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        // A subscription kept before the host checked its service at subscribe time is never called, and is forgotten
        // as gone, so its page subscribes again (#1132).
        if (!_sendsTo(to.Endpoint))
        {
            return false;
        }

        var subscription = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = to.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, to.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, to.Auth);

        var message = new PushMessage(payload) { Topic = topic, Urgency = PushMessageUrgency.High };
        // The limit covers the whole send, whatever client it goes through.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_sendFor);
        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, limit.Token);
            return true;
        }
        catch (OperationCanceledException late) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"The push service did not answer within {_sendFor.TotalSeconds:0.#} seconds.", late);
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
