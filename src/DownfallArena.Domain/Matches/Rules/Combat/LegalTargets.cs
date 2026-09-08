using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// What a player may pick as targets for a spell right now: the candidates and how many of them.
/// </summary>
public sealed record LegalTargets(int MinTargets, int MaxTargets, IReadOnlyList<CreatureId> Candidates)
{
    public bool IsCastable => Candidates.Count >= MinTargets;
}
