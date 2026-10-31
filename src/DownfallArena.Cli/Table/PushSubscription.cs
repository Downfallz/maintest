using System.Buffers.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Where a browser asked a seat's notices to be sent (ADR 0118): the push service address its
/// <c>PushManager.subscribe</c> answered with, and the two keys that encrypt a notice to that browser alone.
/// One of the seat's keys (<see cref="SeatKeys" />): it belongs to the browser holding the seat's token, and
/// goes with it when the token is replaced.
/// </summary>
internal sealed record PushSubscription(string Endpoint, string P256dh, string Auth)
{
    /// <summary>
    /// The subscription as the browser serialises it, or <c>null</c> when it is not one: an endpoint that is not
    /// an https address, a <c>p256dh</c> that is not an uncompressed P-256 point, an <c>auth</c> that is not
    /// sixteen bytes. A page that posts something else posts a bug, not a subscription, and the host keeps
    /// nothing it could not encrypt to: a key of the wrong shape fails inside the sender, at every asking.
    /// </summary>
    public static PushSubscription? From(string? endpoint, string? p256dh, string? auth) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps
            && Decodes(p256dh, 65, out var point) && point[0] == 0x04 && Decodes(auth, 16, out _)
            ? new PushSubscription(endpoint!, p256dh!, auth!)
            : null;

    private static bool Decodes(string? text, int length, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || !Base64Url.IsValid(text))
        {
            return false;
        }

        bytes = Base64Url.DecodeFromChars(text);
        return bytes.Length == length;
    }
}
