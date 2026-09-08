using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// A match recorder that only counts the intents each player slot declares, per spell.
/// </summary>
internal sealed class IntentCounter : IMatchRecorder
{
    private readonly Dictionary<PlayerSlot, Dictionary<string, int>> _usage = new()
    {
        [PlayerSlot.Player1] = new Dictionary<string, int>(StringComparer.Ordinal),
        [PlayerSlot.Player2] = new Dictionary<string, int>(StringComparer.Ordinal),
    };

    public IReadOnlyDictionary<string, int> UsageOf(PlayerSlot slot) => _usage[slot];

    public IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent) => new CountingAgent(agent, this);

    public Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private void Count(PlayerSlot slot, SpellId spell)
    {
        var usage = _usage[slot];
        usage[spell.Value] = usage.GetValueOrDefault(spell.Value) + 1;
    }

    private sealed class CountingAgent(IPlayerAgent inner, IntentCounter counter) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => inner.DecideSpeed(board, creature);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            ArgumentNullException.ThrowIfNull(board);
            var spell = inner.DecideIntent(board, intentOption);
            counter.Count(board.Slot, spell);
            return spell;
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }
}
