using DownfallArena.Application.Agents;
using DownfallArena.Infrastructure.Agents;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Agents;

public sealed class JsonPolicySourceTests
{
    private const string Policy = """
        {
          "kind": "clone",
          "stamp": { "engineVersion": "abcdef123456", "contentHash": "c", "ruleSet": {}, "featureSchema": "features:v5+0123456789ab", "player1Agent": "Greedy", "player2Agent": "Greedy", "baseSeed": 1 },
          "schemaId": "features:v5+0123456789ab",
          "schemaVersion": "features:v5",
          "featureNames": ["round_fraction", "phase"],
          "actionKeys": ["pass", "intent:0:spell:a:v1"],
          "weights": [[0.5, -1.0], [0.0, 2.0]],
          "bias": [0.1, -0.2],
          "fallback": -1000000000.0,
          "trainedAt": "2026-09-09T00:00:00+00:00",
          "metrics": { "accuracy": 0.9 }
        }
        """;

    /// <summary>ADR 0051: a policy may weigh the candidate terms, named in the engine's order; a file without them reads as before.</summary>
    [Fact]
    public void A_policy_may_carry_candidate_weights_and_scores_a_candidates_terms_with_them()
    {
        var names = string.Join(", ", ScoreTerms.Names.Select(name => $"\"{name}\""));
        var fallback = "\"fallback\": -1000000000.0,";
        var weighted = Policy.Replace(fallback, fallback + $" \"candidateTermNames\": [{names}], \"candidateWeights\": [1.0, 5.0, 0, 0, 0, 0, 0, 0, 0],", StringComparison.Ordinal);
        var reordered = Policy.Replace(fallback, fallback + $" \"candidateTermNames\": [\"kill\", \"damage\"], \"candidateWeights\": [5.0, 1.0],", StringComparison.Ordinal);
        using var directory = new ContentDirectory().WithFile("weighted.json", weighted).WithFile("reordered.json", reordered).WithFile("plain.json", Policy);
        var source = new JsonPolicySource();

        var policy = source.Load(Path.Combine(directory.Path, "weighted.json"));
        var terms = new float[ScoreTerms.Count];
        terms[0] = 3f;
        terms[1] = 1f;

        policy.ReadsCandidateTerms.ShouldBeTrue();
        policy.CandidateWeights.ShouldBe([1.0, 5.0, 0, 0, 0, 0, 0, 0, 0]);
        policy.Score("intent:0:spell:a:v1", [0.5f, 1f], terms).ShouldBe(1.8 + 3 + 5, 1e-9);
        source.Load(Path.Combine(directory.Path, "plain.json")).ReadsCandidateTerms.ShouldBeFalse();
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "reordered.json"))).Message.ShouldContain("this engine reads");
    }

    [Fact]
    public void A_policy_written_by_the_python_side_loads_with_its_fingerprint()
    {
        using var directory = new ContentDirectory().WithFile("policy.json", Policy).WithFile("other.json", Policy.Replace("0.1", "0.3", StringComparison.Ordinal));
        var source = new JsonPolicySource();

        var policy = source.Load(Path.Combine(directory.Path, "policy.json"));

        policy.Kind.ShouldBe("clone");
        policy.SchemaId.ShouldBe("features:v5+0123456789ab");
        policy.ActionKeys.ShouldBe(["pass", "intent:0:spell:a:v1"]);
        policy.Weights[1].ShouldBe([0.0, 2.0]);
        policy.Bias.ShouldBe([0.1, -0.2]);
        policy.Fallback.ShouldBe(-1e9);
        policy.Fingerprint.ShouldMatch("^[0-9a-f]{8}$");
        source.Load(Path.Combine(directory.Path, "policy.json")).Fingerprint.ShouldBe(policy.Fingerprint);
        source.Load(Path.Combine(directory.Path, "other.json")).Fingerprint.ShouldNotBe(policy.Fingerprint);
        policy.Score("intent:0:spell:a:v1", [0.5f, 1f]).ShouldBe(1.8, 1e-9);
        policy.Score("unknown", [0.5f, 1f]).ShouldBe(-1e9);
        policy.Baseline.ShouldBeNull("a file written before ADR 0016 carries none");
    }

    [Fact]
    public void A_baseline_is_read_when_the_file_carries_one_and_lifts_every_score()
    {
        var fallback = "\"fallback\": -1000000000.0,";
        var withBaseline = Policy.Replace(fallback, fallback + " \"baseline\": { \"weights\": [1.0, 0.0], \"bias\": 0.25 },", StringComparison.Ordinal);
        using var directory = new ContentDirectory()
            .WithFile("baseline.json", withBaseline)
            .WithFile("wide.json", withBaseline.Replace("[1.0, 0.0]", "[1.0]", StringComparison.Ordinal))
            .WithFile("scalar.json", withBaseline.Replace("{ \"weights\": [1.0, 0.0], \"bias\": 0.25 }", "0.25", StringComparison.Ordinal));
        var source = new JsonPolicySource();

        var policy = source.Load(Path.Combine(directory.Path, "baseline.json"));

        var baseline = policy.Baseline.ShouldNotBeNull();
        baseline.Weights.ShouldBe([1.0, 0.0]);
        baseline.Bias.ShouldBe(0.25);
        policy.Score("intent:0:spell:a:v1", [0.5f, 1f]).ShouldBe(2.55, 1e-9, "0.5 x 1.0 + 0.25 on top of 1.8");
        policy.Score("unknown", [0.5f, 1f]).ShouldBe(-1e9 + 0.75, 1e-9);
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "wide.json"))).Message.ShouldContain("one weight per feature");
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "scalar.json"))).Message.ShouldContain("baseline");
    }

    [Fact]
    public void Incomplete_or_foreign_files_are_refused()
    {
        using var directory = new ContentDirectory()
            .WithFile("no-bias.json", Policy.Replace("\"bias\"", "\"offsets\"", StringComparison.Ordinal))
            .WithFile("old-version.json", Policy.Replace("features:v5", "features:v0", StringComparison.Ordinal))
            .WithFile("empty.json", "null");
        var source = new JsonPolicySource();

        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "no-bias.json"))).Message.ShouldContain("bias");
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "old-version.json"))).Message.ShouldContain("features:v0");
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "empty.json")));
        Should.Throw<FileNotFoundException>(() => source.Load(Path.Combine(directory.Path, "missing.json")));
        Should.Throw<ArgumentException>(() => source.Load(" "));
    }
}
