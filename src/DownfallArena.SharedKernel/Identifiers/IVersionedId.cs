namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Declares the <c>kind</c> segment a content identifier accepts, e.g. <c>spell</c>.
/// </summary>
public interface IContentKind
{
    static abstract string Kind { get; }
}

/// <summary>
/// Implemented by each concrete <see cref="VersionedId"/> so the parsing logic can be shared.
/// </summary>
public interface IVersionedId<TSelf> : IContentKind
    where TSelf : VersionedId, IVersionedId<TSelf>
{
    static abstract TSelf Create(string value, string name, int version);
}
