using System.Buffers.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Where a browser asked a seat's notices to be sent (ADR 0117): the push service address its
/// <c>PushManager.subscribe</c> answered with, and the two keys that encrypt a notice to that browser alone.
/// One of the seat's keys (<see cref="SeatKeys" />): it belongs to the browser holding the seat's token, and
/// goes with it when the token is replaced.
/// </summary>
internal sealed record PushSubscription(string Endpoint, string P256dh, string Auth)
{
    /// <summary>
    /// The subscription as the browser serialises it, or <c>null</c> when it is not one: an endpoint that is not
    /// an https address, or keys that are not base64url. A page that posts something else posts a bug, not a
    /// subscription, and the host keeps nothing it could not send to.
    /// </summary>
    public static PushSubscription? From(string? endpoint, string? p256dh, string? auth) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps
            && IsKey(p256dh) && IsKey(auth)
            ? new PushSubscription(endpoint!, p256dh!, auth!)
            : null;

    private static bool IsKey(string? text) => !string.IsNullOrEmpty(text) && Base64Url.IsValid(text);
}
