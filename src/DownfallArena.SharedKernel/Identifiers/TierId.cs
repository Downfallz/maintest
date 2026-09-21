using System.Diagnostics.CodeAnalysis;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a tier in the game resources: <c>tier:name:vN</c>.
/// </summary>
/// <remarks>
/// A tier is bought, a spell is cast, and the two are addressed separately from here on. They were the same
/// identity while evolution unlocked one spell at a time, which is why the learning encoders reach for a
/// spell index when they mean a purchase; giving the purchase its own kind is what stops that reading.
/// </remarks>
public sealed record TierId : VersionedId, IVersionedId<TierId>
{
    private TierId(string value, string name, int version)
        : base(value, name, version)
    {
    }

    public static string Kind => "tier";

    public static TierId Parse(string text) => Parse<TierId>(text);

    public static bool TryParse(string? text, [NotNullWhen(true)] out TierId? id) => TryParse<TierId>(text, out id);

    static TierId IVersionedId<TierId>.Create(string value, string name, int version) => new(value, name, version);
}
