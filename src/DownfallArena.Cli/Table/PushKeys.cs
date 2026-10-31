using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The host's VAPID key pair (RFC 8292), which is what lets a push service believe a notice comes from this
/// host and nobody else (ADR 0117). The public key goes to every page that subscribes; the private key signs
/// every notice sent. One pair outlives every table: a subscription a browser holds is bound to the public
/// key it was made with, so changing the pair silently unsubscribes everybody.
/// </summary>
/// <remarks>
/// Keys are the uncompressed P-256 point and the scalar, base64url without padding, which is what the browser's
/// <c>applicationServerKey</c> and the push services expect. The subject is who a push service may write to
/// about abuse: a <c>mailto:</c> or an https address.
/// </remarks>
internal sealed record PushKeys(string PublicKey, string PrivateKey, string Subject = PushKeys.DefaultSubject)
{
    /// <summary>The variable the hosted table reads its keys from, as the JSON <see cref="ToJson" /> writes (ADR 0080).</summary>
    public const string Environment = "DOWNFALL_PUSH_KEYS";

    public const string DefaultSubject = "https://github.com/Downfallz/maintest";

    private const int PointLength = 65;

    private const int ScalarLength = 32;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>A fresh pair, for a host that has none: printed once by <c>push-keys</c> and kept where the host reads it.</summary>
    public static PushKeys Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        var point = new byte[PointLength];
        point[0] = 0x04;
        Pad(parameters.Q.X!).CopyTo(point, 1);
        Pad(parameters.Q.Y!).CopyTo(point, 1 + ScalarLength);
        return new PushKeys(Base64Url.EncodeToString(point), Base64Url.EncodeToString(Pad(parameters.D!)));
    }

    /// <summary>
    /// The keys a host starts with: the file <c>--push-keys</c> names, else the JSON in <see cref="Environment" />,
    /// else none, which is a host that offers no notices. A pair that is not a P-256 pair is refused in one line
    /// here rather than by a push service later.
    /// </summary>
    public static PushKeys? Load(string? path, string? environment)
    {
        var text = path is not null ? File.ReadAllText(path) : environment;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var keys = JsonSerializer.Deserialize<PushKeys>(text, Json) ?? throw new InvalidDataException("The push keys are not a JSON object.");
        return keys.Checked();
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private PushKeys Checked()
    {
        if (!Decodes(PublicKey, PointLength, out var point) || point[0] != 0x04)
        {
            throw new InvalidDataException("The push public key is not an uncompressed P-256 point in base64url.");
        }

        if (!Decodes(PrivateKey, ScalarLength, out _))
        {
            throw new InvalidDataException("The push private key is not a 32-byte P-256 scalar in base64url.");
        }

        return string.IsNullOrWhiteSpace(Subject) ? this with { Subject = DefaultSubject } : this;
    }

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

    /// <summary>A big-endian scalar as the export gives it, which may drop leading zero bytes; the point needs all thirty-two.</summary>
    private static byte[] Pad(byte[] scalar)
    {
        if (scalar.Length == ScalarLength)
        {
            return scalar;
        }

        var padded = new byte[ScalarLength];
        scalar.CopyTo(padded, ScalarLength - scalar.Length);
        return padded;
    }
}
