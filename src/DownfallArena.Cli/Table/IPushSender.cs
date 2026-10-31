namespace DownfallArena.Cli.Table;

/// <summary>
/// Sends one notice to one browser through its push service (ADR 0117). The port is the host's own rather than
/// the application's: nothing below the host knows a seat, a browser or a push service, and what is behind it is
/// one HTTP request signed with the host's keys.
/// </summary>
internal interface IPushSender
{
    /// <summary>
    /// Sends the payload, a JSON text the service worker reads. True when the push service took it; false when
    /// the subscription is gone (the browser unsubscribed, or the service forgot it), which is the one answer
    /// that means the seat should forget it too. Anything else is thrown: a service that is out is a line on
    /// the console and a notice that did not arrive, never a reason to drop the subscription.
    /// </summary>
    Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default);
}
