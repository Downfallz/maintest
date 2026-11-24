namespace DownfallArena.Cli.Table;

/// <summary>
/// Sends one notice to one browser through its push service (ADR 0118). The port is the host's own rather than
/// the application's: nothing below the host knows a seat, a browser or a push service, and what is behind it is
/// one HTTP request signed with the host's keys.
/// </summary>
internal interface IPushSender
{
    /// <summary>
    /// Sends the payload, a JSON text the service worker reads, under a topic: a second notice on the same topic
    /// before the first was read replaces it on the push service rather than queueing behind it, so a phone
    /// switched back on reads one "your turn", the latest, and one notice per kind of thing it was told. A topic is
    /// at most 32 characters of the URL-safe base64 alphabet. <see cref="PushSent.Taken" /> when the push service took it;
    /// <see cref="PushSent.Gone" /> when the subscription is gone (the browser unsubscribed, or the service forgot it), and
    /// <see cref="PushSent.UnknownService" /> when it is on no service this host sends to. After either answer the seat
    /// or the account should forget the subscription. Anything else is thrown: a service that is out is a line on
    /// the console and a notice that did not arrive, never a reason to drop the subscription.
    /// </summary>
    Task<PushSent> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default);
}
