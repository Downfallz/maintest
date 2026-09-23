using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
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

    /// <summary>A band knows its own depth, which is the row the talent mat draws it on.</summary>
    [Fact]
    public void A_band_carries_the_depth_of_its_node()
    {
        View.Trees.ShouldAllBe(band => band.Depth >= 1);
        View.Trees.ShouldContain(band => band.Depth == 1);
    }

    [Fact]
    public void Bands_carry_their_actual_parent_within_each_tree()
    {
        var bands = View.Trees;

        foreach (var tree in TestContent.Resources.TalentTrees)
        {
            bands.Single(band => band.Tree == tree.Id && band.Code == tree.Root.Code).ParentCode.ShouldBeNull();
            foreach (var node in tree.Nodes)
            {
                foreach (var child in node.Children)
                {
                    var band = bands.Single(candidate => candidate.Tree == tree.Id && candidate.Code == child.Code);
                    band.ParentCode.ShouldBe(node.Code);
                    band.Depth.ShouldBe(bands.Single(parent => parent.Tree == tree.Id && parent.Code == node.Code).Depth + 1);
                }
            }
        }
    }

    /// <summary>
    /// What it takes to acquire a spell is on the package that sells it and nowhere else. The talent gate and
    /// the per-spell unlock bonus were both rules once; neither is one now (ADR 0056), and a card that still
    /// carried them would hand a table requirements nobody has to meet and an initiative gain nobody is paid.
    /// The gate lives on in the authored content, which is what the packages are derived from -- so the test
    /// that matters is that the projection does not republish it as a rule.
    /// </summary>
    [Fact]
    public void A_card_carries_no_acquisition_rule_because_the_package_that_teaches_it_does()
    {
        var gate = TestContent.Resources.GetTalentTree(TestContent.Tree).Nodes
            .Single(node => node.Spells.Any(offered => offered.Id == TestContent.Slam))
            .Prerequisites;
        var slam = View.Cards.Single(card => card.Id == TestContent.Slam);
        var package = View.Packages.Single(card => card.Id == TestContent.SlamPack);

        gate.ReferencedSpells.ShouldContain(TestContent.Guard);
        var face = typeof(CardFace).GetProperties().Select(property => property.Name).ToList();
        face.ShouldNotContain("Requires");
        face.ShouldNotContain("Tier");
        face.ShouldNotContain("Initiative");

        package.Prerequisites.ShouldBe([TestContent.GuardPack]);
        package.InitiativeBonus.ShouldBe(1);
        slam.Tree.ShouldNotBeNullOrWhiteSpace();
    }

    private static Domain.Resources.Spell Named(SpellId id, string name) =>
        Domain.Resources.Spell.Create(
            id,
            name,
            SpellType.Offensive,
            CreatureClass.Creature,
            new SpellStats(Energy.Of(0), CriticalChance.None),
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
            new SpellStats(Energy.Of(0), CriticalChance.Of(critical)),
            targeting,
            [Damage.Of(1)],
            casterEffects);

    [Fact]
    public void Visual_cues_distinguish_target_damage_caster_healing_and_a_real_critical_chance()
    {
        var spell = Spell(TestContent.Strike, TargetingSpec.SingleTarget(TargetOrigin.Enemy), critical: 0.25, casterEffects: [Heal.Of(2)]);
        var card = Card(spell);

        card.Cues.ShouldNotBeNull().ShouldBe([new CardCue("harm", "Damage"), new CardCue("recovery", "Caster: Healing"), new CardCue("critical", "Crit · Standard only")]);
        card.CriticalNote.ShouldBe("Quick cannot crit. In Standard, crits multiply only direct damage and healing on targets.");
        Card(Spell(TestContent.Strike, TargetingSpec.SingleTarget(TargetOrigin.Enemy))).Cues.ShouldNotBeNull().ShouldNotContain(cue => cue.Tone == "critical");
    }

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

    /// <summary>
    /// The round strip the table prints. The steps come off the enum rather than a list here, so a step added
    /// to the round cannot be one the screen forgets — that is the whole reason the client is served them.
    /// </summary>
    [Fact]
    public void The_round_is_served_as_its_steps_in_play_order_and_the_orderings_inside_them()
    {
        View.Round.SubPhases.ShouldBe(Enum.GetNames<RoundSubPhase>());
        View.Round.SubPhases[0].ShouldBe(nameof(RoundSubPhase.EnergyGain));
        View.Round.SubPhases[^1].ShouldBe(nameof(RoundSubPhase.Finalization));
        View.Round.SubPhases.ShouldContain(nameof(RoundSubPhase.RevealAndTarget));

        // The two the player aid calls load-bearing, because they are the two a player gets wrong.
        View.Round.Orderings.Count.ShouldBe(2);
        View.Round.Orderings.ShouldAllBe(ordering => ordering.EndsWith('.'));
    }
}
