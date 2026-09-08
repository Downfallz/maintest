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

    SpellId DecideIntent(PlayerBoardState board, IntentOption option);

    IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options);
}
