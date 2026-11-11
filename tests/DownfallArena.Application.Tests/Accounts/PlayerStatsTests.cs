using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class PlayerStatsTests
{
    private static readonly AccountId Mark = AccountId.New();
    private static readonly AccountId Bastien = AccountId.New();
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static MatchResult Game(int minutes, ResultSeat first, ResultSeat second, string? winner = "player1", string format = "3v3", int rounds = 9) =>
        new(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), format, "at-activation", "hash", rounds, winner, [first, second]);

    private static MatchResult Against(int minutes, string? winner = "player1", string format = "3v3", int rounds = 9) =>
        Game(minutes, new("player1", Mark, null), new("player2", Bastien, null), winner, format, rounds);

    private static MatchResult AgainstBot(int minutes, string? winner = "player1") =>
        Game(minutes, new("player1", Mark, null), new("player2", null, "greedy"), winner);

    private static PlayedCreature Creature(string seat, string creature, bool survived = true, string[]? mutations = null, Dictionary<string, int>? casts = null) =>
        new(seat, creature, mutations ?? [], survived, casts ?? []);

    [Fact]
    public void Games_are_counted_from_the_players_side_whichever_seat_they_held_against_players_and_bots_apart()
    {
        MatchResult[] results = [Against(0, winner: "player1"), Against(1, winner: "player2"), Against(2, winner: null),
            Game(3, new("player1", Bastien, null), new("player2", Mark, null), winner: "player2"), AgainstBot(4, winner: "player2")];

        var stats = PlayerStats.Of(results, Mark);

        stats.Overall.ShouldBe(new Tally(5, 2, 2, 1));
        stats.AgainstPlayers.ShouldBe(new Tally(4, 2, 1, 1));
        stats.AgainstBots.ShouldBe(new Tally(1, 0, 1, 0));
    }

    [Fact]
    public void A_game_one_account_held_both_seats_of_is_counted_apart_and_in_nothing_else()
    {
        MatchResult[] results = [Against(0), Game(1, new("player1", Mark, null), new("player2", Mark, null), rounds: 30)];

        var stats = PlayerStats.Of(results, Mark);

        stats.Shared.ShouldBe(1);
        stats.Overall.Games.ShouldBe(1);
        stats.AverageRounds.ShouldBe(9);
        stats.Streak.ShouldBe(new StreakOf(GameOutcome.Won, 1));
    }

    [Fact]
    public void Games_the_player_held_no_seat_in_are_passed_over()
    {
        MatchResult[] results = [Game(0, new("player1", Bastien, null), new("player2", null, "greedy"))];

        var stats = PlayerStats.Of(results, Mark);

        stats.Overall.ShouldBe(Tally.None);
        stats.Streak.ShouldBeNull();
        stats.AverageRounds.ShouldBeNull();
    }

    [Fact]
    public void Each_format_has_its_own_tally_the_most_played_first()
    {
        MatchResult[] results = [Against(0, format: "1v1"), Against(1, format: "3v3", winner: "player2"), Against(2, format: "3v3")];

        PlayerStats.Of(results, Mark).Formats.ShouldBe([new FormatTally("3v3", new Tally(2, 1, 1, 0)), new FormatTally("1v1", new Tally(1, 1, 0, 0))]);
    }

    [Fact]
    public void The_streak_is_the_last_games_outcome_in_a_row_and_the_best_win_streak_the_longest_run_of_wins()
    {
        MatchResult[] results = [Against(5, winner: "player2"), Against(0), Against(1), Against(2), Against(3, winner: "player2"), Against(4)];

        var stats = PlayerStats.Of(results, Mark);

        stats.Streak.ShouldBe(new StreakOf(GameOutcome.Lost, 1));
        stats.BestWinStreak.ShouldBe(3);
    }

    [Fact]
    public void A_games_length_is_averaged_over_the_players_games()
    {
        MatchResult[] results = [Against(0, rounds: 6), Against(1, rounds: 11)];

        PlayerStats.Of(results, Mark).AverageRounds.ShouldBe(8.5);
    }

    [Fact]
    public void What_was_played_counts_the_players_own_creatures_and_only_games_kept_with_their_teams()
    {
        var won = Against(0) with
        {
            Teams =
            [
                Creature("player1", "creature:wolf:v1", survived: true, mutations: ["tier:guard:v1"], casts: new() { ["spell:strike:v1"] = 3 }),
                Creature("player1", "creature:bear:v1", survived: false, casts: new() { ["spell:strike:v1"] = 1, ["spell:roar:v1"] = 2 }),
                Creature("player2", "creature:wolf:v1", mutations: ["tier:fang:v1"], casts: new() { ["spell:bite:v1"] = 5 }),
            ],
        };
        var lost = Against(1, winner: "player2") with
        {
            Teams = [Creature("player1", "creature:wolf:v1", survived: false, mutations: ["tier:guard:v1"], casts: new() { ["spell:roar:v1"] = 1 })],
        };
        var untold = Against(2);

        var stats = PlayerStats.Of([won, lost, untold], Mark);

        stats.WithTeams.ShouldBe(2);
        stats.Creatures.ShouldBe([new CreatureTally("creature:wolf:v1", 2, 1, 1), new CreatureTally("creature:bear:v1", 1, 1, 0)]);
        stats.Mutations.ShouldBe([new MutationTally("tier:guard:v1", 2, 1)]);
        stats.Spells.ShouldBe([new SpellTally("spell:strike:v1", 4, 1, 1), new SpellTally("spell:roar:v1", 3, 2, 1)]);
    }
}
