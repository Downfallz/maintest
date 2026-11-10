using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What the match is asking this seat. The creature is the one being asked about for a speed choice, an
/// intent, a target binding and an action chosen at the slot, and none for evolution or a tie order, which
/// are asked of the player rather than of one creature.
/// </summary>
/// <param name="Asked">
/// Which asking this is, counted by the seat. It exists so that two questions of the same shape are two
/// questions: a seat with two Evolution picks in a round is asked <c>(Evolution, null)</c> twice, and
/// anything comparing askings by their shape alone -- the decision clock does -- would treat the second as
/// a continuation of the first, keep the first's stamp for it and then take that stamp away when the first
/// was answered. Nothing else reads it; it is identity, not information.
/// </param>
internal sealed record Asking(PlayerOptionsKind Kind, CreatureId? Creature, long Asked = 0)
{
    /// <summary>Whether a decision answers this question rather than the previous one.</summary>
    public bool Answers(PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Kind != Kind)
        {
            return false;
        }

        // Evolution names its own creature and a tie order names several; the other four are asked about one
        // creature, and the answer is for it.
        return Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.TieOrder || Creature is null || decision.Creature is null || decision.Creature == Creature;
    }
}
