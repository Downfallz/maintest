namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Implemented by each concrete <see cref="VersionedId"/> so the parsing logic can be shared.
/// </summary>
public interface IVersionedId<TSelf>
    where TSelf : VersionedId, IVersionedId<TSelf>
{
    /// <summary>
    /// The <c>kind</c> segment this identifier accepts, e.g. <c>spell</c>.
    /// </summary>
    static abstract string Kind { get; }

    static abstract TSelf Create(string value, string name, int version);
}
