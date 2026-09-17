using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Catalogue;

/// <summary>
/// The catalogue as the table serves it. What these hold is the promise the app is built on: a tuning pass is
/// a rebuild and a restart, never an app change — which is only true while every word and every number a
/// player reads is computed here.
/// </summary>
public sealed class CatalogueProjectionTests
{
    private static readonly RuleSet Rules = RuleSet.Create(3, 2, 2, 12, 2.0);

    private static CatalogueView View => CatalogueProjection.Build(TestContent.Resources, Rules);

    [Fact]
    public void Every_spell_the_match_can_cast_has_exactly_one_card()
    {
        var view = View;

        view.Cards.Select(card => card.Id).ShouldBe(TestContent.Resources.Spells.Select(spell => spell.Id), ignoreOrder: true);
        view.Cards.Select(card => card.Id).Distinct().Count().ShouldBe(view.Cards.Count);
    }

    /// <summary>Everything needed to resolve a cast, without the rulebook and without the engine.</summary>
    [Fact]
    public void A_card_carries_what_it_costs_when_it_acts_who_it_hits_and_what_it_does()
    {
        var slam = View.Cards.Single(card => card.Id == TestContent.Slam);

        slam.Name.ShouldBe("Slam");
        slam.Cost.ShouldBe(2);
        slam.Initiative.ShouldBe(1);
        slam.Targeting.ShouldBe("Up to 2 enemies");
        slam.Effects.ShouldBe(["Damage 2", "Stun, 1 round"]);
    }

    [Fact]
    public void A_lasting_effect_prints_its_duration_because_a_player_has_to_track_it()
    {
        var rend = View.Cards.Single(card => card.Id == TestContent.Rend);

        rend.Effects.ShouldBe(["Damage 1", "Bleed 19 a round, 1 round"]);
    }

    [Theory]
    [InlineData(TargetOrigin.Self, TargetScope.SingleTarget, null, "Self")]
    [InlineData(TargetOrigin.Enemy, TargetScope.SingleTarget, null, "One enemy")]
    [InlineData(TargetOrigin.Ally, TargetScope.SingleTarget, null, "One ally")]
    [InlineData(TargetOrigin.Enemy, TargetScope.Multi, 2, "Up to 2 enemies")]
    [InlineData(TargetOrigin.Ally, TargetScope.Multi, null, "Every ally")]
    [InlineData(TargetOrigin.Ally, TargetScope.Multi, 3, "Up to 3 allies")]
    public void Targeting_is_one_sentence(TargetOrigin origin, TargetScope scope, int? maxTargets, string expected)
    {
        var spell = Spell(TestContent.Strike, scope == TargetScope.SingleTarget ? TargetingSpec.SingleTarget(origin) : TargetingSpec.Multi(origin, maxTargets));

        Card(spell).Targeting.ShouldBe(expected);
    }

    /// <summary>
    /// A twentieth prints the face a d20 crits on, because that is the die the table rolls
    /// (docs/tabletop/d20-criticals.md). One is a legal twentieth: `21 - 20 x 1.00` is a spell that always
    /// crits, which nothing authors today, which is exactly why a rule that stopped at 2 would go unnoticed.
    /// </summary>
    [Theory]
    [InlineData(0.05, 20)]
    [InlineData(0.35, 14)]
    [InlineData(0.5, 11)]
    [InlineData(1.0, 1)]
    public void A_chance_that_is_a_twentieth_prints_the_face_a_d20_crits_on(double chance, int expected)
    {
        var threshold = CatalogueProjection.Threshold(chance).ShouldNotBeNull();

        threshold.ShouldBe(expected);
        threshold.ShouldBeInRange(1, 20);
    }

    /// <summary>A spell that never crits has no face to roll, and the card says the chance instead.</summary>
    [Fact]
    public void A_chance_of_zero_has_no_threshold()
    {
        CatalogueProjection.Threshold(0).ShouldBeNull();
    }

    /// <summary>
    /// The fallback the print generator takes too: until the catalogue is snapped to the grid, a chance that is
    /// not a twentieth prints as a percentage and no die line. It is the one thing in this projection that the
    /// content can turn on by itself, with no change here.
    /// </summary>
    [Theory]
    [InlineData(0.33)]
    [InlineData(0.07)]
    [InlineData(0.125)]
    public void A_chance_that_is_not_a_twentieth_has_no_threshold(double chance)
    {
        CatalogueProjection.Threshold(chance).ShouldBeNull();
    }

    [Fact]
    public void A_card_prints_the_chance_whether_or_not_it_is_a_twentieth()
    {
        var card = Card(Spell(TestContent.Strike, TargetingSpec.SingleTarget(TargetOrigin.Enemy), critical: 0.05));

        card.Critical.ShouldBe("5%");
        card.CriticalThreshold.ShouldBe(20);
    }

    /// <summary>
    /// The pair that says which game this is: a session is reproducible against a content hash and a rule set,
    /// or it is not reproducible at all.
    /// </summary>
    [Fact]
    public void The_view_names_the_content_and_the_rules_it_was_built_from()
    {
        var view = View;

        view.ContentHash.ShouldBe(TestContent.Resources.Version);
        view.Rules.RoundCap.ShouldBe(12);
        view.Rules.TeamSize.ShouldBe(3);
        view.Rules.CriticalMultiplier.ShouldBe(2.0);
    }

    [Fact]
    public void A_card_says_which_tree_it_is_unlocked_from()
    {
        var guard = View.Cards.Single(card => card.Id == TestContent.Guard);

        guard.Tree.ShouldNotBeNullOrWhiteSpace();
        View.Trees.ShouldContain(band => band.Spells.Contains(TestContent.Guard));
    }

    /// <summary>
    /// The tier is how far into the tree a spell sits, and inside one node that is what its own prerequisites
    /// say: the test content offers Strike, then Guard behind it, then Slam behind Guard, all from one node.
    /// A tier copied from the node would have read the same for all three, which on the real content prints
    /// "Warlord · Warlord" and says nothing at all.
    /// </summary>
    [Fact]
    public void The_tier_of_a_spell_counts_the_ones_it_is_unlocked_behind()
    {
        var view = View;

        Tier(view, TestContent.Strike).ShouldBe(1);
        Tier(view, TestContent.Guard).ShouldBe(2);
        Tier(view, TestContent.Slam).ShouldBe(3);
    }

    /// <summary>A band knows its own depth, which is the row the talent mat draws it on.</summary>
    [Fact]
    public void A_band_carries_the_depth_of_its_node()
    {
        View.Trees.ShouldAllBe(band => band.Depth >= 1);
        View.Trees.ShouldContain(band => band.Depth == 1);
    }

    /// <summary>The gate is on the card, so a pick can be checked without the talent mat.</summary>
    [Fact]
    public void A_card_names_the_spells_that_gate_it_by_name_rather_than_by_id()
    {
        var slam = View.Cards.Single(card => card.Id == TestContent.Slam);

        slam.Requires.ShouldNotBeNull();
        slam.Requires.ShouldContain("Guard");
        slam.Requires.ShouldNotContain("spell:");
    }

    /// <summary>
    /// The one thing a gate has to say is which combination is legal. Crushing Stomp on the real content wants
    /// Full Plate <em>and</em> one of two others; flattening the two groups into one list would name all three
    /// and leave a player guessing which they need (<c>TalentPrerequisites.AreSatisfiedBy</c>).
    /// </summary>
    [Fact]
    public void A_gate_of_two_groups_keeps_them_apart()
    {
        var gated = SpellId.Parse("spell:gated:v1");
        var tree = TalentTree.Create(
            TestContent.Tree,
            "Base",
            new TalentNode(
                "base",
                "Base",
                TalentPrerequisites.None,
                [
                    new TalentSpell(TestContent.Strike, TalentPrerequisites.None),
                    new TalentSpell(TestContent.Guard, TalentPrerequisites.None),
                    new TalentSpell(gated, TalentPrerequisites.Of([TestContent.Strike], [TestContent.Guard, TestContent.Slam])),
                ],
                []));

        var requires = Cards(tree, [Named(TestContent.Strike, "Strike"), Named(TestContent.Guard, "Guard"), Named(TestContent.Slam, "Slam"), Named(gated, "Gated")])
            .Single(card => card.Id == gated)
            .Requires;

        requires.ShouldBe("Strike; one of Guard or Slam");
    }

    [Fact]
    public void A_gate_of_several_spells_reads_as_a_sentence()
    {
        var gated = SpellId.Parse("spell:gated:v1");
        var tree = TalentTree.Create(
            TestContent.Tree,
            "Base",
            new TalentNode(
                "base",
                "Base",
                TalentPrerequisites.None,
                [
                    new TalentSpell(TestContent.Strike, TalentPrerequisites.None),
                    new TalentSpell(TestContent.Guard, TalentPrerequisites.None),
                    new TalentSpell(gated, TalentPrerequisites.Of([TestContent.Strike, TestContent.Guard], [])),
                ],
                []));

        var requires = Cards(tree, [Named(TestContent.Strike, "Strike"), Named(TestContent.Guard, "Guard"), Named(gated, "Gated")])
            .Single(card => card.Id == gated)
            .Requires;

        requires.ShouldBe("Guard and Strike");
    }

    private static int Tier(CatalogueView view, SpellId spell) => view.Cards.Single(card => card.Id == spell).Tier;

    private static Domain.Resources.Spell Named(SpellId id, string name) =>
        Domain.Resources.Spell.Create(
            id,
            name,
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.None),
            TargetingSpec.SingleTarget(TargetOrigin.Enemy),
            [Damage.Of(1)]);

    /// <summary>One tree and its spells, through the projection.</summary>
    private static IReadOnlyList<CardFace> Cards(TalentTree tree, IReadOnlyList<Domain.Resources.Spell> spells)
    {
        var creature = CreatureDefinition.Create(
            TestContent.Main,
            "Main",
            CreatureClass.Creature,
            new CreatureStats(Health.Of(20), Energy.Of(0), Defense.Of(0), Initiative.Of(5), CriticalChance.None),
            TestContent.Tree,
            [spells[0].Id]);

        return CatalogueProjection.Build(GameResources.Create("probe", [creature], spells, [tree]), Rules).Cards;
    }

    /// <summary>
    /// The set of effects is closed (ADR 0012). This list is written out rather than derived, and then held
    /// against the assembly: a kind added to the domain without a line here fails this test instead of printing
    /// a blank line on a card nobody can resolve.
    /// </summary>
    [Fact]
    public void Every_effect_the_domain_defines_has_a_line_a_card_can_print()
    {
        Effect[] rendered =
        [
            Damage.Of(7),
            Heal.Of(4),
            EnergyGain.Of(2),
            EnergyDrain.Of(2),
            Bleed.Of(4, rounds: 2),
            Regeneration.Of(3, rounds: 2),
            EnergyRegeneration.Of(2, rounds: 3),
            Stun.For(2),
            DefenseBuff.Of(3, Duration.Permanent),
            DefenseDebuff.Of(2, Duration.OfRounds(1)),
            InitiativeBuff.Of(2, Duration.OfRounds(1)),
            InitiativeDebuff.Of(2, Duration.OfRounds(2)),
        ];

        var kinds = typeof(Effect).Assembly.GetTypes()
            .Where(type => type.IsSealed && typeof(Effect).IsAssignableFrom(type))
            .ToList();

        rendered.Select(effect => effect.GetType()).ShouldBe(kinds, ignoreOrder: true);
        rendered.Select(EffectLine.Of).ShouldBe(
        [
            "Damage 7",
            "Heal 4",
            "Energy +2",
            "Energy -2",
            "Bleed 4 a round, 2 rounds",
            "Regeneration 3 a round, 2 rounds",
            "Energy regeneration 2 a round, 3 rounds",
            "Stun, 2 rounds",
            "Defense +3, permanent",
            "Defense -2, 1 round",
            "Initiative +2, 1 round",
            "Initiative -2, 2 rounds",
        ]);
    }

    /// <summary>
    /// The printed deck writes "1 round" and the widths components.md §2.3 measured were measured with it, so
    /// a card that said "1 rounds" would be both wrong and wider than the face it was measured for.
    /// `Duration.ToString()` is always plural, which is why the wording is the projection's and not its.
    /// </summary>
    [Fact]
    public void One_round_is_singular_and_no_rounds_at_all_is_permanent()
    {
        EffectLine.Of(Bleed.Of(4, rounds: 1)).ShouldBe("Bleed 4 a round, 1 round");
        EffectLine.Of(Bleed.Of(4, rounds: 2)).ShouldBe("Bleed 4 a round, 2 rounds");
        EffectLine.Of(DefenseBuff.Of(3, Duration.OfRounds(1))).ShouldBe("Defense +3, 1 round");
        EffectLine.Of(DefenseBuff.Of(3, Duration.Permanent)).ShouldBe("Defense +3, permanent");
        EffectLine.Of(Stun.For(1)).ShouldBe("Stun, 1 round");
    }

    /// <summary>
    /// A caster effect resolves once however many targets the cast reached (ADR 0031), so it must not read as
    /// one more thing done to the target.
    /// </summary>
    [Fact]
    public void A_caster_effect_is_marked_as_the_caster_s_own()
    {
        var spell = Spell(TestContent.Strike, TargetingSpec.SingleTarget(TargetOrigin.Enemy), casterEffects: [Heal.Of(2)]);

        Card(spell).CasterEffects.ShouldBe(["Caster: Heal 2"]);
    }

    private static Domain.Resources.Spell Spell(
        SpellId id,
        TargetingSpec targeting,
        double critical = 0,
        IReadOnlyList<Effect>? casterEffects = null) =>
        Domain.Resources.Spell.Create(
            id,
            "Probe",
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Initiative.Of(1), Energy.Of(0), CriticalChance.Of(critical)),
            targeting,
            [Damage.Of(1)],
            casterEffects);

    /// <summary>
    /// One spell, through the projection: what the route would answer for it and nothing else. The creature and
    /// the tree are the smallest content the engine will accept around it.
    /// </summary>
    private static CardFace Card(Domain.Resources.Spell spell)
    {
        var tree = TalentTree.Create(
            TestContent.Tree,
            "Base",
            new TalentNode("base", "Base", TalentPrerequisites.None, [new TalentSpell(spell.Id, TalentPrerequisites.None)], []));
        var creature = CreatureDefinition.Create(
            TestContent.Main,
            "Main",
            CreatureClass.Creature,
            new CreatureStats(Health.Of(20), Energy.Of(0), Defense.Of(0), Initiative.Of(5), CriticalChance.None),
            TestContent.Tree,
            [spell.Id]);

        return CatalogueProjection
            .Build(GameResources.Create("probe", [creature], [spell], [tree]), Rules)
            .Cards
            .ShouldHaveSingleItem();
    }
}
