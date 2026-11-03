namespace DownfallArena.Infrastructure.Accounts;

/// <summary>The URL-safe base64 of JSON Web Tokens and Keys, without its padding (RFC 7515, section 2).</summary>
internal static class Base64Url
{
    public static byte[] Decode(string text)
    {
        var standard = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(standard.PadRight(standard.Length + ((4 - (standard.Length % 4)) % 4), '='));
    }
}
