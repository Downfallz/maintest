using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// The packages one evolution pick buys, through the builder that validates them. Nothing in the engine plays
/// a tier yet, so these are the only thing standing between authored tier content and a silent mistake.
/// </summary>
public sealed class TierContentTests
{
    private const string Opener = """
        {
          "id": "tier:brute:v1", "name": "Brute", "level": 1,
          "prerequisites": [], "spells": ["spell:strike"], "initiativeBonus": 1
        }
        """;

    private const string Advanced = """
        {
          "id": "tier:marauder:v1", "name": "Marauder", "level": 2,
          "prerequisites": ["tier:brute:v1"], "spells": ["spell:guard"], "initiativeBonus": 0
        }
        """;

    [Fact]
    public void A_package_reaches_the_catalogue_with_its_spells_and_its_one_bonus()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", Opener).WithFile("Tiers/marauder.v1.json", Advanced);

        var resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path));

        resources.Tiers.Count.ShouldBe(2);
        var marauder = resources.GetTier(TierId.Parse("tier:marauder:v1"));
        marauder.Name.ShouldBe("Marauder");
        marauder.Level.ShouldBe(2);
        marauder.Prerequisites.ShouldBe([TierId.Parse("tier:brute:v1")]);
        marauder.Spells.ShouldBe([SpellId.Parse("spell:guard:v1")]);
        marauder.InitiativeBonus.Value.ShouldBe(0);
    }

    /// <summary>The tree may name a spell unversioned, and so may a tier: both go through the same resolver.</summary>
    [Fact]
    public void A_package_may_name_its_spells_unversioned()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", Opener);

        var resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path));

        resources.GetTier(TierId.Parse("tier:brute:v1")).Spells.ShouldBe([SpellId.Parse("spell:strike:v1")]);
    }

    [Fact]
    public void A_package_that_teaches_an_unknown_spell_is_refused()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", """
            { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:nope:v1"], "initiativeBonus": 1 }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("unknown spell");
    }

    [Fact]
    public void A_package_that_requires_an_unknown_package_is_refused()
    {
        using var content = Content().WithFile("Tiers/marauder.v1.json", Advanced);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("unknown tier");
    }

    /// <summary>
    /// A prerequisite level with or below what it opens cannot be reached first, so the pair is unbuyable and
    /// the content is wrong however it was meant.
    /// </summary>
    [Fact]
    public void A_prerequisite_has_to_sit_above_what_it_opens()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", Opener)
            .WithFile("Tiers/sibling.v1.json", """
                { "id": "tier:sibling:v1", "name": "Sibling", "level": 1, "prerequisites": ["tier:brute:v1"], "spells": ["spell:guard"], "initiativeBonus": 1 }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("has to sit above");
    }

    /// <summary>
    /// Sitting above what it opens is not enough. A level-3 package whose only prerequisite opens the family is
    /// bought with two picks rather than three, so the depth it is priced and paced at is not the depth a player
    /// pays -- and the pacing is the whole point of levels.
    /// </summary>
    [Fact]
    public void An_advanced_package_cannot_skip_the_level_below_it()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", Opener)
            .WithFile("Tiers/warmonger.v1.json", """
                { "id": "tier:warmonger:v1", "name": "Warmonger", "level": 3, "prerequisites": ["tier:brute:v1"], "spells": ["spell:guard"], "initiativeBonus": 2 }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("no prerequisite at level 2");
    }

    /// <summary>The same rule read from the other end: nothing above the first level is bought straight away.</summary>
    [Fact]
    public void A_package_above_the_first_level_needs_a_prerequisite()
    {
        using var content = Content().WithFile("Tiers/marauder.v1.json", """
            { "id": "tier:marauder:v1", "name": "Marauder", "level": 2, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 0 }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("no prerequisite at level 1");
    }

    /// <summary>
    /// One mistake is one problem: a package that names a prerequisite nobody authored is missing that
    /// prerequisite, not missing a level, and reporting both would send the author looking for two.
    /// </summary>
    [Fact]
    public void An_unknown_prerequisite_is_not_also_reported_as_a_skipped_level()
    {
        using var content = Content().WithFile("Tiers/marauder.v1.json", Advanced);

        var message = Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path))).Message;

        message.ShouldContain("unknown tier");
        message.ShouldNotContain("no prerequisite at level");
    }

    /// <summary>
    /// Refused by <c>Initiative</c> rather than by a content rule. An earlier draft of the catalogue check
    /// repeated it and could never fire, which reads like a guarantee that is actually made somewhere else.
    /// </summary>
    [Fact]
    public void A_package_worth_negative_initiative_is_refused()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", """
            { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:strike"], "initiativeBonus": -1 }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("non-negative");
    }

    [Fact]
    public void A_package_that_teaches_nothing_is_refused()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", """
            { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": [], "initiativeBonus": 1 }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)))
            .Message.ShouldContain("at least one spell");
    }

    /// <summary>
    /// A package is bought whole, so quietly dropping a disabled spell from it would sell something nobody
    /// authored. The author disables the package too, or re-authors it.
    /// </summary>
    [Fact]
    public void Disabling_a_spell_a_package_teaches_is_refused_rather_than_shrinking_the_package()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", Opener)
            .WithFile("Spells/strike.v1.json", """
                {
                  "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
                  "initiative": 1, "energyCost": 0, "criticalChance": 0, "enabled": false,
                  "targeting": { "origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1 },
                  "effects": [ { "kind": "Damage", "amount": 1 } ]
                }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Message.ShouldContain("teaches disabled spell");
    }

    /// <summary>
    /// Dropping a disabled prerequisite would leave the package with nothing in front of it, which opens a
    /// descendant instead of closing it -- the same failure the empty <c>anyOf</c> gate has.
    /// </summary>
    [Fact]
    public void Disabling_a_prerequisite_package_is_refused_rather_than_opening_its_descendant()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", """
                { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:strike"], "initiativeBonus": 1, "enabled": false }
                """)
            .WithFile("Tiers/marauder.v1.json", Advanced);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Message.ShouldContain("requires disabled tier");
    }

    /// <summary>The switch is authoring-only: it may not reach the consolidated schema or move its hash.</summary>
    [Fact]
    public void A_disabled_package_leaves_the_build_and_its_switch_leaves_the_schema()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", Opener)
            .WithFile("Tiers/spare.v1.json", """
                { "id": "tier:spare:v1", "name": "Spare", "level": 1, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 1, "enabled": false }
                """);

        var schema = GameSchemaBuilder.Build(content.Path);

        schema.Tiers.ShouldNotBeNull().Select(tier => tier.Id).ShouldBe(["tier:brute:v1"]);
        schema.Tiers.ShouldAllBe(tier => tier.Enabled == null);
    }

    /// <summary>
    /// Two packages reaching the same prerequisite is not a cycle, and the pruning that makes the cycle walk
    /// terminate must not report one. This is the shape that pruning gets wrong when it is threaded badly: the
    /// second path to a shared ancestor is skipped, and a check that confused skipping with finding would fail
    /// the content here.
    /// </summary>
    [Fact]
    public void Two_packages_sharing_a_prerequisite_is_not_a_cycle()
    {
        using var content = Content()
            .WithFile("Tiers/brute.v1.json", Opener)
            .WithFile("Tiers/marauder.v1.json", Advanced)
            .WithFile("Tiers/ironbound.v1.json", """
                { "id": "tier:ironbound:v1", "name": "Ironbound", "level": 2, "prerequisites": ["tier:brute:v1"], "spells": ["spell:strike"], "initiativeBonus": 1 }
                """)
            .WithFile("Tiers/warmonger.v1.json", """
                { "id": "tier:warmonger:v1", "name": "Warmonger", "level": 3, "prerequisites": ["tier:marauder:v1", "tier:ironbound:v1"], "spells": ["spell:guard"], "initiativeBonus": 2 }
                """);

        var resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path));

        resources.Tiers.Count.ShouldBe(4);
    }

    /// <summary>
    /// Catalogues without tiers are what exists today, and the migration is not finished, so the builder has
    /// to keep accepting them rather than demanding content nobody has authored yet.
    /// </summary>
    [Fact]
    public void A_catalogue_with_no_packages_still_builds()
    {
        using var content = Content();

        GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path)).Tiers.ShouldBeEmpty();
    }

    private static ContentDirectory Content() => new ContentDirectory().WithValidContent();
}
