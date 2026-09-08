namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Declares the <c>kind</c> segment a content identifier accepts, e.g. <c>spell</c>.
/// </summary>
public interface IContentKind
{
    static abstract string Kind { get; }
}
