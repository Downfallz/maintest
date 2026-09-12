using System.Collections.Concurrent;
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
    /// <summary>
    /// One tally per side of one match. The outer map is concurrent because matches may play at the same
    /// time; each inner one is touched by a single match, which plays its own rounds in order, so it needs
    /// nothing.
    /// </summary>
    private readonly ConcurrentDictionary<(MatchId Match, PlayerSlot Slot), Dictionary<string, int>> _perMatch = new();

    /// <summary>
    /// What one slot declared over the whole batch, summed from the matches rather than kept alongside them.
    /// <para>
    /// It used to be its own running total, which is the one thing here two matches would have contended
    /// over: every side of every match increments the same row. Summing on read costs a walk of a few
    /// hundred tallies once, removes the contention instead of locking it, and leaves one source of truth
    /// where there were two that had to agree.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, int> UsageOf(PlayerSlot slot)
    {
        var usage = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, tally) in _perMatch)
        {
            if (key.Slot != slot)
            {
                continue;
            }

            foreach (var (spell, count) in tally)
            {
                usage[spell] = usage.GetValueOrDefault(spell) + count;
            }
        }

        // By spell id, because this is walked out of a concurrent map whose order is not the insertion
        // order and need not be the same twice. The totals would be right either way; the key order lands
        // in `evaluation.json`, and an artifact whose bytes move while its numbers do not is a bad artifact.
        return usage.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    /// <summary>Every side this counter saw declare something, as (match, slot, what it declared).</summary>
    public IEnumerable<(MatchId Match, PlayerSlot Slot, IReadOnlyDictionary<string, int> Usage)> Sides() =>
        _perMatch.Select(entry => (entry.Key.Match, entry.Key.Slot, (IReadOnlyDictionary<string, int>)entry.Value));

    public IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent) => new CountingAgent(matchId, agent, this);

    public Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private void Count(MatchId matchId, PlayerSlot slot, SpellId spell)
    {
        var perMatch = _perMatch.GetOrAdd((matchId, slot), _ => new Dictionary<string, int>(StringComparer.Ordinal));
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
