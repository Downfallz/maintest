namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Implemented by each concrete <see cref="VersionedId"/> so the parsing logic can be shared.
/// </summary>
public interface IVersionedId<TSelf> : IContentKind
    where TSelf : VersionedId, IVersionedId<TSelf>
{
    static abstract TSelf Create(string value, string name, int version);
}
