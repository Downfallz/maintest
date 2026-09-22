using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// Hand-made boards for the learning tests: creature snapshots with the test content's defaults, to adjust
/// with <c>with</c>, and a board in the first round of a two-on-two match holding whatever creatures a test needs.
/// </summary>
internal static class Boards
{
    public static CreatureSnapshot Creature(int id, PlayerSlot owner) => new()
    {
        Id = CreatureId.From(id),
        Owner = owner,
        DefinitionId = TestContent.Main,
        Name = $"Creature {id}",
        TalentTree = TestContent.Tree,
        Health = Health.Of(20),
        MaxHealth = Health.Of(20),
        Energy = Energy.Of(0),
        TotalDefense = Defense.Of(0),
        BaseInitiative = Initiative.Of(5),
        CurrentInitiative = Initiative.Of(5),
        CriticalChance = CriticalChance.Of(0.05),
        IsStunned = false,
        KnownSpells = new HashSet<SpellId> { TestContent.Strike },
        Conditions = [],
    };

    /// <summary>
    /// The creature with a package bought: the purchase on the record and every spell it teaches known. That
    /// is the only shape a real board has (ADR 0056), and a snapshot whose spells no package of its own
    /// explains cannot be restored — so a hand-made board that just adds a spell is one no rollout can read.
    /// </summary>
    public static CreatureSnapshot Bought(this CreatureSnapshot creature, TierId tier)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var package = TestContent.Resources.GetTier(tier);
        return creature with
        {
            AcquiredTiers = new HashSet<TierId>(creature.AcquiredTiers) { tier },
            KnownSpells = new HashSet<SpellId>([.. creature.KnownSpells, .. package.Spells]),
        };
    }

    /// <summary>A board as a player sees it at the start of a two-on-two match, with these creatures instead of the match's.</summary>
    public static PlayerBoardState Board(PlayerSlot slot, IReadOnlyList<CreatureSnapshot> allies, IReadOnlyList<CreatureSnapshot> enemies) =>
        PlayerBoardStateProjection.Build(new MatchStore().Started(), slot) with { Allies = allies, Enemies = enemies };
}
