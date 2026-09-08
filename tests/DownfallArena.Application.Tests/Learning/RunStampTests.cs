using DownfallArena.Application.Learning;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Tests.Learning;

public sealed class RunStampTests
{
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());

    [Fact]
    public void A_stamp_names_every_axis_of_a_run()
    {
        var stamp = Stamp();

        stamp.EngineVersion.ShouldBe("abc123def456-dirty");
        stamp.ContentHash.ShouldBe("test-content");
        stamp.RuleSet.ShouldBe(new RuleSetStamp(2, 2, 2, 30, 2.0));
        stamp.FeatureSchema.ShouldBe(Schema.Id);
        stamp.Player1Agent.ShouldBe("random");
        stamp.Player2Agent.ShouldBe("random");
        stamp.BaseSeed.ShouldBe(42);
    }

    [Fact]
    public void Identical_stamps_differ_on_no_axis()
    {
        Stamp().DifferencesFrom(Stamp()).ShouldBeEmpty();
        Stamp().ShouldBe(Stamp());
    }

    [Fact]
    public void Each_axis_is_reported_when_it_differs()
    {
        var stamp = Stamp();

        stamp.DifferencesFrom(stamp with { ContentHash = "other" }).ShouldBe(["content"]);
        stamp.DifferencesFrom(stamp with { EngineVersion = "abc123def456" }).ShouldBe(["engine"]);
        stamp.DifferencesFrom(stamp with { RuleSet = RuleSetStamp.Of(RuleSet.Default) }).ShouldBe(["rules"]);
        stamp.DifferencesFrom(stamp with { FeatureSchema = "features:v2" }).ShouldBe(["schema"]);
        stamp.DifferencesFrom(stamp with { FeatureSchema = FeatureSchema.Build(TestContent.Resources, RuleSet.Default).Id }).ShouldBe(["schema"]);
        stamp.DifferencesFrom(stamp with { Player2Agent = "greedy" }).ShouldBe(["agents"]);
        stamp.DifferencesFrom(stamp with { BaseSeed = 7 }).ShouldBe(["seed"]);
        stamp.DifferencesFrom(stamp with { ContentHash = "other", BaseSeed = 7 }).ShouldBe(["content", "seed"]);
    }

    [Fact]
    public void A_rule_set_stamp_copies_the_rule_set_values()
    {
        RuleSetStamp.Of(RuleSet.Create(3, 1, 2, 10, 1.5)).ShouldBe(new RuleSetStamp(3, 1, 2, 10, 1.5));
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var engine = new EngineVersion("abc123def456", false);
        var rules = MatchStore.TwoOnTwo();

        Should.Throw<ArgumentNullException>(() => RunStamp.Create(null!, TestContent.Resources, rules, Schema, "random", "random", 1));
        Should.Throw<ArgumentNullException>(() => RunStamp.Create(engine, null!, rules, Schema, "random", "random", 1));
        Should.Throw<ArgumentNullException>(() => RunStamp.Create(engine, TestContent.Resources, null!, Schema, "random", "random", 1));
        Should.Throw<ArgumentNullException>(() => RunStamp.Create(engine, TestContent.Resources, rules, null!, "random", "random", 1));
        Should.Throw<ArgumentException>(() => RunStamp.Create(engine, TestContent.Resources, rules, Schema, " ", "random", 1));
        Should.Throw<ArgumentException>(() => RunStamp.Create(engine, TestContent.Resources, rules, Schema, "random", "", 1));
        Should.Throw<ArgumentNullException>(() => Stamp().DifferencesFrom(null!));
        Should.Throw<ArgumentNullException>(() => RuleSetStamp.Of(null!));
    }

    private static RunStamp Stamp() =>
        RunStamp.Create(new EngineVersion("abc123def456", true), TestContent.Resources, MatchStore.TwoOnTwo(), Schema, "random", "random", 42);
}
