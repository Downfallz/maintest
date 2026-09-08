using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// One reason a targeting is wrong. A <c>null</c> target means the failure concerns the whole action.
/// </summary>
public sealed record TargetingFailure(CreatureId? Target, DomainError Error)
{
    public bool IsGlobal => Target is null;
}
