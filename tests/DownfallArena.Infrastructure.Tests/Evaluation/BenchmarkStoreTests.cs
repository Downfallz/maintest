using DownfallArena.Application.Evaluation;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Evaluation;

public sealed class BenchmarkStoreTests
{
    [Fact]
    public void The_committed_seed_list_holds_two_hundred_distinct_seeds()
    {
        var seeds = new BenchmarkStore(Path.Combine(AppContext.BaseDirectory, "benchmarks")).LoadSeeds();

        seeds.Count.ShouldBe(200);
        seeds.Distinct().Count().ShouldBe(200);
        seeds.ShouldAllBe(seed => seed > 0);
    }

    [Fact]
    public void A_digest_is_written_and_read_back_as_written()
    {
        using var directory = new ContentDirectory();
        var store = new BenchmarkStore(directory.Path);
        var digest = new BenchmarkDigest
        {
            ContentHash = "abc",
            EngineVersion = "abc123def456-dirty",
            AgentA = "Random",
            AgentB = "Random",
            Entries = [new BenchmarkEntry(1, "AB", "Player1", "Elimination", 3, 10, 0), new BenchmarkEntry(1, "BA", "Draw", "RoundCap", 5, 4, 4)],
        };

        var path = store.WriteDigest(digest);
        var read = store.TryLoadDigest("abc");

        path.ShouldBe(store.DigestPath("abc"));
        File.Exists(path).ShouldBeTrue();
        read.ShouldNotBeNull();
        read.ContentHash.ShouldBe("abc");
        read.EngineVersion.ShouldBe("abc123def456-dirty");
        read.Entries.ShouldBe(digest.Entries);
        digest.DifferencesFrom(read).ShouldBeEmpty();
        store.TryLoadDigest("missing").ShouldBeNull();
        BenchmarkStore.Serialize(digest).ShouldContain("\"contentHash\": \"abc\"");
    }

    [Fact]
    public void Seed_files_are_validated()
    {
        using var directory = new ContentDirectory()
            .WithFile("empty.json", """{ "seeds": [] }""")
            .WithFile("repeated.json", """{ "seeds": [1, 2, 1] }""")
            .WithFile("fine.json", """{ "seeds": [3, 1, 2] }""");

        BenchmarkStore.LoadSeeds(Path.Combine(directory.Path, "fine.json")).ShouldBe([3, 1, 2]);
        Should.Throw<InvalidDataException>(() => BenchmarkStore.LoadSeeds(Path.Combine(directory.Path, "empty.json")));
        Should.Throw<InvalidDataException>(() => BenchmarkStore.LoadSeeds(Path.Combine(directory.Path, "repeated.json")));
        Should.Throw<ArgumentException>(() => BenchmarkStore.LoadSeeds(" "));
        Should.Throw<ArgumentException>(() => new BenchmarkStore(" "));
        Should.Throw<ArgumentException>(() => new BenchmarkStore(directory.Path).DigestPath(" "));
    }
}
