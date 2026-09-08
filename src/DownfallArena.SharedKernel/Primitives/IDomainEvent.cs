namespace DownfallArena.SharedKernel.Primitives;

/// <summary>
/// Something that happened in the domain that other parts of the system may react to.
/// Implement as an immutable <c>record</c>, named in the past tense (e.g. <c>MatchStarted</c>).
/// </summary>
public interface IDomainEvent
{
}
