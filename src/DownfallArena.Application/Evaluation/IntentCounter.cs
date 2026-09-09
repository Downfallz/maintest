using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// A match recorder that only counts the intents each player slot declares, per spell: over the whole batch,
/// and per match, which is what a spell can be correlated with the outcome of that match through.
/// </summary>
internal sealed class IntentCounter : IMatchRecorder
{
    private readonly Dictionary<PlayerSlot, Dictionary<string, int>> _usage = new()
    {
        [PlayerSlot.Player1] = new Dictionary<string, int>(StringComparer.Ordinal),
        [PlayerSlot.Player2] = new Dictionary<string, int>(StringComparer.Ordinal),
    };

    private readonly Dictionary<(MatchId Match, PlayerSlot Slot), Dictionary<string, int>> _perMatch = [];

    public IReadOnlyDictionary<string, int> UsageOf(PlayerSlot slot) => _usage[slot];

    /// <summary>Every side this counter saw declare something, as (match, slot, what it declared).</summary>
    public IEnumerable<(MatchId Match, PlayerSlot Slot, IReadOnlyDictionary<string, int> Usage)> Sides() =>
        _perMatch.Select(entry => (entry.Key.Match, entry.Key.Slot, (IReadOnlyDictionary<string, int>)entry.Value));

    public IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent) => new CountingAgent(matchId, agent, this);

    public Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private void Count(MatchId matchId, PlayerSlot slot, SpellId spell)
    {
        var usage = _usage[slot];
        usage[spell.Value] = usage.GetValueOrDefault(spell.Value) + 1;

        var key = (matchId, slot);
        if (!_perMatch.TryGetValue(key, out var perMatch))
        {
            perMatch = new Dictionary<string, int>(StringComparer.Ordinal);
            _perMatch[key] = perMatch;
        }

        perMatch[spell.Value] = perMatch.GetValueOrDefault(spell.Value) + 1;
    }

    private sealed class CountingAgent(MatchId matchId, IPlayerAgent inner, IntentCounter counter) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => inner.DecideSpeed(board, creature);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            ArgumentNullException.ThrowIfNull(board);
            var spell = inner.DecideIntent(board, intentOption);
            counter.Count(matchId, board.Slot, spell);
            return spell;
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }
}
