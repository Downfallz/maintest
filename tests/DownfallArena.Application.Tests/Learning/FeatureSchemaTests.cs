using DownfallArena.Application.Learning;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Learning;

public sealed class FeatureSchemaTests
{
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());

    [Fact]
    public void The_schema_is_the_published_version()
    {
        Schema.Version.ShouldBe("features:v6");
        FeatureSchema.CurrentVersion.ShouldBe("features:v6");
        Schema.TeamSize.ShouldBe(2);
        Schema.RoundCap.ShouldBe(30);
    }

    [Fact]
    public void The_id_fingerprints_the_concrete_layout()
    {
        var same = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());
        var otherTeamSize = FeatureSchema.Build(TestContent.Resources, RuleSet.Create(3, 2, 2, 30, 2.0));
        var otherRoundCap = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo(roundCap: 8));
        var otherContent = FeatureSchema.Build(GameResources.Create("other", [], [], []), MatchStore.TwoOnTwo());

        Schema.Id.ShouldBe(same.Id);
        Schema.Id.ShouldMatch("^features:v6\\+[0-9a-f]{12}$");
        new[] { Schema.Id, otherTeamSize.Id, otherRoundCap.Id, otherContent.Id }.Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
        FeatureSchema.Build(TestContent.Resources, RuleSet.Create(2, 9, 9, 30, 9.0)).Id.ShouldBe(Schema.Id);
    }

    [Fact]
    public void The_length_is_the_globals_plus_one_block_per_board_slot()
    {
        // 6 creature features, 8 condition pairs, 5 spells, 4 packages.
        Schema.CreatureLength.ShouldBe(6 + (2 * 8) + 5 + 4);
        Schema.Length.ShouldBe(5 + (2 * 2 * Schema.CreatureLength));
        Schema.FeatureNames.Count.ShouldBe(Schema.Length);
        Schema.FeatureNames.Distinct(StringComparer.Ordinal).Count().ShouldBe(Schema.Length);
    }

    [Fact]
    public void Every_feature_has_a_stable_index()
    {
        FeatureSchema.GlobalFeatures.ShouldBe(["round_fraction", "phase", "sub_phase", "reveal_progress", "revealed_enemy_actions"]);
        FeatureSchema.CreatureFeatures.ShouldBe(["alive", "health_fraction", "energy", "stunned", "defense", "initiative"]);
        Schema.IndexOf("round_fraction").ShouldBe(0);
        Schema.IndexOf("revealed_enemy_actions").ShouldBe(4);
        Schema.IndexOf("own0_alive").ShouldBe(5);
        Schema.IndexOf("own0_initiative").ShouldBe(10);
        Schema.IndexOf("own0_Bleed_amount").ShouldBe(11);
        Schema.IndexOf("own0_Bleed_remaining").ShouldBe(12);
        Schema.IndexOf("own0_Regeneration_amount").ShouldBe(13);
        Schema.IndexOf("own0_EnergyRegeneration_amount").ShouldBe(15);
        Schema.IndexOf("own0_DefenseDebuff_amount").ShouldBe(21);
        Schema.IndexOf("own0_InitiativeBuff_amount").ShouldBe(23);
        Schema.IndexOf("own0_InitiativeDebuff_remaining").ShouldBe(26);
        Schema.IndexOf("own0_knows_spell:guard:v1").ShouldBe(27);
        Schema.IndexOf("own0_knows_spell:strike:v1").ShouldBe(31);
        Schema.IndexOf("own0_owns_tier:both:v1").ShouldBe(32);
        Schema.IndexOf("own0_owns_tier:slam:v1").ShouldBe(35);
        Schema.IndexOf("own1_alive").ShouldBe(36);
        Schema.IndexOf("enemy0_alive").ShouldBe(67);
        Schema.IndexOf("enemy1_alive").ShouldBe(98);
        Schema.IndexOf("enemy1_owns_tier:slam:v1").ShouldBe(128);
        Should.Throw<ArgumentOutOfRangeException>(() => Schema.IndexOf("own2_alive"));
    }

    [Fact]
    public void Creature_offsets_follow_board_slots()
    {
        Schema.CreatureOffset(0).ShouldBe(Schema.IndexOf("own0_alive"));
        Schema.CreatureOffset(1).ShouldBe(Schema.IndexOf("own1_alive"));
        Schema.CreatureOffset(2).ShouldBe(Schema.IndexOf("enemy0_alive"));
        Schema.CreatureOffset(3).ShouldBe(Schema.IndexOf("enemy1_alive"));
        Should.Throw<ArgumentOutOfRangeException>(() => Schema.CreatureOffset(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Schema.CreatureOffset(4));
    }

    [Fact]
    public void Spells_and_packages_are_indexed_in_ordinal_order()
    {
        Schema.Spells.ShouldBe([TestContent.Guard, TestContent.Jab, TestContent.Rend, TestContent.Slam, TestContent.Strike]);
        Schema.SpellIndex(TestContent.Guard).ShouldBe(0);
        Schema.SpellIndex(TestContent.Strike).ShouldBe(4);
        Schema.SpellIndex(SpellId.Parse("spell:unknown:v1")).ShouldBe(-1);
        Schema.Tiers.ShouldBe([TestContent.BothPack, TestContent.GuardPack, TestContent.JabPack, TestContent.SlamPack]);
        Schema.TierIndex(TestContent.BothPack).ShouldBe(0);
        Schema.TierIndex(TestContent.SlamPack).ShouldBe(3);
        Schema.TierIndex(TierId.Parse("tier:unknown:v1")).ShouldBe(-1);
    }

    [Fact]
    public void The_condition_kinds_are_the_domain_taxonomy()
    {
        // A new lasting effect in the domain changes the observation layout: publish a new schema version
        // (docs/learning/features.md) and extend ConditionKinds before this passes again.
        var domainKinds = typeof(LastingEffect).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(LastingEffect)) && !type.IsAbstract)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        FeatureSchema.ConditionKinds.Order(StringComparer.Ordinal).ShouldBe(domainKinds);
        FeatureSchema.ConditionKinds.ShouldBe(["Bleed", "Regeneration", "EnergyRegeneration", "Stun", "DefenseBuff", "DefenseDebuff", "InitiativeBuff", "InitiativeDebuff"]);
        FeatureSchema.ConditionKindIndex("Stun").ShouldBe(3);
        FeatureSchema.ConditionKindIndex("Poison").ShouldBe(-1);
    }

    [Fact]
    public void The_team_size_widens_the_vector()
    {
        var wide = FeatureSchema.Build(TestContent.Resources, RuleSet.Create(3, 2, 2, 10, 2.0));

        wide.Length.ShouldBe(5 + (6 * wide.CreatureLength));
        wide.CreatureLength.ShouldBe(Schema.CreatureLength);
        wide.CreatureOffset(5).ShouldBe(wide.IndexOf("enemy2_alive"));
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => FeatureSchema.Build(null!, MatchStore.TwoOnTwo()));
        Should.Throw<ArgumentNullException>(() => FeatureSchema.Build(TestContent.Resources, null!));
        Should.Throw<ArgumentOutOfRangeException>(() => FeatureSchema.Build(TestContent.Resources, RuleSet.Create(FeatureSchema.MaxTeamSize + 1, 2, 2, 30, 2.0)));
        FeatureSchema.Build(TestContent.Resources, RuleSet.Create(FeatureSchema.MaxTeamSize, 2, 2, 30, 2.0)).CreatureOffset(31).ShouldBe(5 + (31 * Schema.CreatureLength));
    }
}
