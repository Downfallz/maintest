using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Simulation;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Evaluation;

public sealed class BenchmarkDigestTests
{
    [Fact]
    public void A_digest_holds_one_entry_per_mirrored_match()
    {
        var digest = BenchmarkDigest.Of(Evaluation());

        digest.ContentHash.ShouldBe("test-content");
        digest.EngineVersion.ShouldBe("abc123def456");
        digest.AgentA.ShouldBe("Random");
        digest.Entries.Select(entry => (entry.Seed, entry.Order)).ShouldBe([(1, "AB"), (1, "BA"), (2, "AB"), (2, "BA")]);
        digest.Entries[0].ShouldBe(new BenchmarkEntry(1, "AB", "Player1", "Elimination", 3, 10, 0));
        digest.Entries[1].Winner.ShouldBe(BenchmarkEntry.Draw);
        digest.Entries[0].ToString().ShouldBe("Player1 by Elimination in 3 rounds (10/0)");
    }

    [Fact]
    public void Identical_digests_have_no_difference()
    {
        BenchmarkDigest.Of(Evaluation()).DifferencesFrom(BenchmarkDigest.Of(Evaluation())).ShouldBeEmpty();
    }

    [Fact]
    public void Every_changed_missing_or_extra_entry_is_named()
    {
        var mine = BenchmarkDigest.Of(Evaluation());
        var changed = mine with
        {
            Entries = [mine.Entries[0] with { Rounds = 4 }, mine.Entries[1], mine.Entries[2], new BenchmarkEntry(9, "AB", "Draw", "RoundCap", 1, 1, 1)],
        };

        var differences = mine.DifferencesFrom(changed);

        differences.Count.ShouldBe(3);
        differences[0].ShouldStartWith("seed 1 AB: ");
        differences[0].ShouldContain("in 4 rounds");
        differences[1].ShouldBe("seed 2 BA: missing");
        differences[2].ShouldBe("seed 9 AB: no longer played");
    }

    [Fact]
    public void Content_and_agents_are_compared_too()
    {
        var mine = BenchmarkDigest.Of(Evaluation());

        mine.DifferencesFrom(mine with { ContentHash = "other" })[0].ShouldContain("content hash");
        mine.DifferencesFrom(mine with { AgentB = "Greedy" })[0].ShouldContain("agents");
        Should.Throw<ArgumentNullException>(() => mine.DifferencesFrom(null!));
        Should.Throw<ArgumentNullException>(() => BenchmarkDigest.Of(null!));
    }

    private static EvaluationResult Evaluation()
    {
        var rules = MatchStore.TwoOnTwo();
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, FeatureSchema.Build(TestContent.Resources, rules), "Random", "Random", 1);
        var pairs = new List<SeedPair>
        {
            new(1, Result(1, PlayerSlot.Player1, MatchEndReason.Elimination, 3, 10, 0), Result(1, null, MatchEndReason.RoundCap, 5, 4, 4)),
            new(2, Result(2, PlayerSlot.Player2, MatchEndReason.Elimination, 2, 0, 7), Result(2, PlayerSlot.Player1, MatchEndReason.Elimination, 4, 6, 0)),
        };
        var report = new AgentReport
        {
            Agent = "Random",
            Wins = 2,
            WinRate = new ConfidenceInterval(0.5, 0.2, 0.8),
            Score = new ConfidenceInterval(0.5, 0.2, 0.8),
            AverageRemainingHealth = 5,
            SpellUsage = new Dictionary<string, int>(StringComparer.Ordinal),
            SpellEntropy = 0,
            Actions = 0,
            Fizzles = 0,
            Criticals = 0,
        };
        return new EvaluationResult
        {
            Stamp = stamp,
            AgentA = report,
            AgentB = report,
            Matches = 4,
            Draws = 1,
            AverageRounds = 3.5,
            RoundCapShare = 0.25,
            Pairs = pairs,
        };
    }

    private static MatchResult Result(int seed, PlayerSlot? winner, MatchEndReason reason, int rounds, int player1Health, int player2Health) => new()
    {
        Index = 0,
        Seed = seed,
        MatchId = MatchId.New(),
        ContentHash = "test-content",
        Outcome = new MatchOutcome(winner, reason),
        Rounds = rounds,
        Player1RemainingHealth = player1Health,
        Player2RemainingHealth = player2Health,
    };
}
