using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Something that decides for a player: a bot, a scripted test, a UI adapter. It only ever picks among the
/// options the match offers, so its decisions are accepted by construction.
/// </summary>
public interface IPlayerAgent
{
    EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options);

    Speed DecideSpeed(PlayerBoardState board, CreatureId creature);

    /// <summary>
    /// The player's tied creatures, first to act first, each moved only within its own tie (ADR 0063).
    /// <see cref="TieOrderOptions.AsRolled"/> is the order that changes nothing.
    /// </summary>
    IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options);

    SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption);

    IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options);

    /// <summary>
    /// A spell and its targets at once, for the creature whose slot has come up under a rule set that declares
    /// no intent (ADR 0110). By default, the spell this agent would declare among the ones on offer, then the
    /// targets it would bind for it: right for an agent that reads the board as it stands. An agent that wraps
    /// another must forward this to it, or the wrapped agent's own reading is never asked.
    /// </summary>
    ChosenAction DecideAction(PlayerBoardState board, ActionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var spell = DecideIntent(board, new IntentOption(options.Actor, options.SpellIds));
        var offered = options.For(spell)
            ?? throw new InvalidOperationException($"The agent chose {spell}, which the options do not offer.");
        return new ChosenAction(spell, DecideTargets(board, offered));
    }
}
