using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>Which bot each level seats, read off the file that puts bots forward, and what a player and the operator call it (ADR 0177).</summary>
public sealed class BotLevelsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"seatable-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void Without_a_file_Easy_is_greedy_exploring_Medium_is_greedy_and_Hard_is_the_strongest()
    {
        var levels = BotLevels.Read(_file, "lookahead");

        levels.All.Select(level => (level.Level, level.Agent)).ShouldBe([("easy", "explore:0.3"), ("medium", "greedy"), ("hard", "lookahead")]);
    }

    [Fact]
    public void The_file_says_which_bot_a_level_seats_and_the_others_keep_theirs()
    {
        File.WriteAllText(_file, """{ "levels": [ { "level": "hard", "agent": "heuristic:learning/weights/search-51.json" } ] }""");

        var levels = BotLevels.Read(_file, "lookahead");

        levels.Named("hard").ShouldNotBeNull().Agent.ShouldBe("heuristic:learning/weights/search-51.json");
        levels.Named("medium").ShouldNotBeNull().Agent.ShouldBe("greedy");
    }

    [Fact]
    public void A_game_against_a_bot_a_level_seated_before_still_says_that_level()
    {
        File.WriteAllText(_file, """{ "levels": [ { "level": "hard", "agent": "lookahead", "formerly": ["heuristic:learning/weights/search-40.json"] } ] }""");

        var levels = BotLevels.Read(_file, "greedy");

        levels.PlayerName("heuristic:learning/weights/search-40.json").ShouldBe("Hard");
        levels.PlayerName("lookahead").ShouldBe("Hard");
    }

    [Fact]
    public void A_file_that_does_not_parse_leaves_the_built_in_levels()
    {
        File.WriteAllText(_file, "{ not json");

        BotLevels.Read(_file, "lookahead").Named("hard").ShouldNotBeNull().Agent.ShouldBe("lookahead");
    }

    [Fact]
    public void A_player_reads_the_level_and_the_operator_reads_the_bot_and_its_level()
    {
        var levels = BotLevels.BuiltIn("lookahead");

        levels.PlayerName("greedy").ShouldBe("Medium");
        levels.PlayerName("Greedy").ShouldBe("Medium", "a seat names its bot as the spec writes itself back");
        levels.InSentence("explore:0.3").ShouldBe("the Easy bot");
        levels.OperatorName("lookahead", "Lookahead").ShouldBe("Lookahead · Hard");
        levels.PlayerName("random").ShouldBe("Bot");
        levels.OperatorName("random", "Random").ShouldBe("Random");
    }
}
