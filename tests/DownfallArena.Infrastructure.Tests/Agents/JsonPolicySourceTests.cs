using DownfallArena.Infrastructure.Agents;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Agents;

public sealed class JsonPolicySourceTests
{
    private const string Policy = """
        {
          "kind": "clone",
          "stamp": { "engineVersion": "abcdef123456", "contentHash": "c", "ruleSet": {}, "featureSchema": "features:v1+0123456789ab", "player1Agent": "Greedy", "player2Agent": "Greedy", "baseSeed": 1 },
          "schemaId": "features:v1+0123456789ab",
          "schemaVersion": "features:v1",
          "featureNames": ["round_fraction", "phase"],
          "actionKeys": ["pass", "intent:0:spell:a:v1"],
          "weights": [[0.5, -1.0], [0.0, 2.0]],
          "bias": [0.1, -0.2],
          "fallback": -1000000000.0,
          "trainedAt": "2026-09-09T00:00:00+00:00",
          "metrics": { "accuracy": 0.9 }
        }
        """;

    [Fact]
    public void A_policy_written_by_the_python_side_loads_with_its_fingerprint()
    {
        using var directory = new ContentDirectory().WithFile("policy.json", Policy).WithFile("other.json", Policy.Replace("0.1", "0.3", StringComparison.Ordinal));
        var source = new JsonPolicySource();

        var policy = source.Load(Path.Combine(directory.Path, "policy.json"));

        policy.Kind.ShouldBe("clone");
        policy.SchemaId.ShouldBe("features:v1+0123456789ab");
        policy.ActionKeys.ShouldBe(["pass", "intent:0:spell:a:v1"]);
        policy.Weights[1].ShouldBe([0.0, 2.0]);
        policy.Bias.ShouldBe([0.1, -0.2]);
        policy.Fallback.ShouldBe(-1e9);
        policy.Fingerprint.ShouldMatch("^[0-9a-f]{8}$");
        source.Load(Path.Combine(directory.Path, "policy.json")).Fingerprint.ShouldBe(policy.Fingerprint);
        source.Load(Path.Combine(directory.Path, "other.json")).Fingerprint.ShouldNotBe(policy.Fingerprint);
        policy.Score("intent:0:spell:a:v1", [0.5f, 1f]).ShouldBe(1.8, 1e-9);
        policy.Score("unknown", [0.5f, 1f]).ShouldBe(-1e9);
    }

    [Fact]
    public void Incomplete_or_foreign_files_are_refused()
    {
        using var directory = new ContentDirectory()
            .WithFile("no-bias.json", Policy.Replace("\"bias\"", "\"offsets\"", StringComparison.Ordinal))
            .WithFile("old-version.json", Policy.Replace("features:v1", "features:v0", StringComparison.Ordinal))
            .WithFile("empty.json", "null");
        var source = new JsonPolicySource();

        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "no-bias.json"))).Message.ShouldContain("bias");
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "old-version.json"))).Message.ShouldContain("features:v0");
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "empty.json")));
        Should.Throw<FileNotFoundException>(() => source.Load(Path.Combine(directory.Path, "missing.json")));
        Should.Throw<ArgumentException>(() => source.Load(" "));
    }
}
