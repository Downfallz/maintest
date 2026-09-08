using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// One match from one player's point of view: how many steps they took, how it ended for them, and their return.
/// </summary>
public sealed record EpisodeRecord
{
    public required MatchId MatchId { get; init; }

    public int? Seed { get; init; }

    public required PlayerSlot Slot { get; init; }

    public required int Steps { get; init; }

    public required int Rounds { get; init; }

    public required MatchOutcome Outcome { get; init; }

    public required int RemainingHealth { get; init; }

    public required int EnemyRemainingHealth { get; init; }

    public required double Return { get; init; }

    /// <summary>
    /// The episode of one player, read from the final board as player 1 sees it (allies are player 1's team).
    /// </summary>
    public static EpisodeRecord Of(PlayerSlot slot, PlayerBoardState player1Board, int steps, int? seed)
    {
        ArgumentNullException.ThrowIfNull(player1Board);
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        if (player1Board.Slot != PlayerSlot.Player1)
        {
            throw new ArgumentException("The final board must be the one player 1 sees.", nameof(player1Board));
        }

        var outcome = player1Board.Outcome ?? throw new ArgumentException("The final board has no outcome: the match has not ended.", nameof(player1Board));
        var (own, enemy) = slot == PlayerSlot.Player1 ? (player1Board.Allies, player1Board.Enemies) : (player1Board.Enemies, player1Board.Allies);
        var ownHealth = own.Sum(creature => creature.Health.Value);
        var enemyHealth = enemy.Sum(creature => creature.Health.Value);
        var totalMaxHealth = own.Concat(enemy).Sum(creature => creature.MaxHealth.Value);

        return new EpisodeRecord
        {
            MatchId = player1Board.MatchId,
            Seed = seed,
            Slot = slot,
            Steps = steps,
            Rounds = player1Board.RoundNumber ?? 0,
            Outcome = outcome,
            RemainingHealth = ownHealth,
            EnemyRemainingHealth = enemyHealth,
            Return = Returns.Of(outcome, slot, ownHealth, enemyHealth, totalMaxHealth),
        };
    }
}
