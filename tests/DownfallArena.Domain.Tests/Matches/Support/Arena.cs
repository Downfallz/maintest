using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A small match setup: two creatures per player, a three-spell talent tree, two packages, and helpers to
/// walk a round. Strike is known at spawn; the Guard package teaches Guard and the Slam package, which
/// requires it, teaches Slam.
/// Strike deals 3 to one enemy for free; Guard buffs the caster's defense by 2 for one round and costs 1;
/// Slam deals 2 and stuns up to two enemies for one round and costs 2.
/// </summary>
internal static class Arena
{
    public static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    public static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");
    public static readonly SpellId Slam = SpellId.Parse("spell:slam:v1");

    /// <summary>The two packages of this arena: Guard opens, Slam sits behind it (ADR 0056).</summary>
    public static readonly TierId GuardPack = TierId.Parse("tier:guard:v1");

    public static readonly TierId SlamPack = TierId.Parse("tier:slam:v1");

    public static readonly CreatureId Knight = CreatureId.From(1);
    public static readonly CreatureId Archer = CreatureId.From(2);
    public static readonly CreatureId Ghoul = CreatureId.From(4);
    public static readonly CreatureId Wraith = CreatureId.From(5);

    public static GameResources Resources { get; } = GameResources.Create(
        "test",
        [Content.Creature()],
        [
            Content.Spell("spell:strike:v1", TargetingSpec.SingleTarget(TargetOrigin.Enemy), cost: 0, criticalChance: 0, Damage.Of(3)),
            Content.Spell("spell:guard:v1", TargetingSpec.SingleTarget(TargetOrigin.Self), cost: 1, criticalChance: 0, DefenseBuff.Of(2, Duration.OfRounds(1))),
            Content.Spell("spell:slam:v1", TargetingSpec.Multi(TargetOrigin.Enemy, 2), cost: 2, criticalChance: 0, Damage.Of(2), Stun.For(1)),
        ],
        [TalentTree.Create(
            Content.TreeId,
            "Base",
            new TalentNode(
                "root",
                "Root",
                TalentPrerequisites.None,
                [new TalentSpell(Strike, TalentPrerequisites.None), Content.TalentSpell("spell:guard:v1", "spell:strike:v1")],
                [Content.Node("brawler", Content.TalentSpell("spell:slam:v1")) with { Prerequisites = TalentPrerequisites.Of([Guard], []) }]))],
        [
            Tier.Create(GuardPack, "Guard", 1, [], [Guard], Initiative.Of(1)),
            Tier.Create(SlamPack, "Slam", 2, [GuardPack], [Slam], Initiative.Of(2)),
        ]);

    /// <summary>The spell behind an id, for the unlocks that now need the whole spell and not just its name.</summary>
    public static Spell SpellOf(SpellId id) => Resources.GetSpell(id);

    /// <summary>The one talent tree. It gates nothing a pick buys any more (ADR 0056); the content audit reads it.</summary>
    public static TalentTree Tree => Resources.GetTalentTree(Content.TreeId);

    /// <summary>The packages a snapshot owns, resolved, which is what restoring one needs.</summary>
    public static IReadOnlyList<Tier> TiersOf(CreatureSnapshot snapshot) => [.. snapshot.AcquiredTiers.Select(Resources.GetTier)];

    public static Creature Spawn(CreatureId id, PlayerSlot owner) => Creature.Spawn(id, owner, Content.Creature());

    /// <summary>
    /// Four creatures: Knight and Archer for Player1, Ghoul and Wraith for Player2.
    /// </summary>
    public static List<Creature> FourCreatures() =>
        [Spawn(Knight, PlayerSlot.Player1), Spawn(Archer, PlayerSlot.Player1), Spawn(Ghoul, PlayerSlot.Player2), Spawn(Wraith, PlayerSlot.Player2)];

    public static IReadOnlyList<CreatureSnapshot> Snapshots(IEnumerable<Creature> creatures) => [.. creatures.Select(creature => creature.Snapshot())];

    /// <summary>
    /// A creature by id among the given ones.
    /// </summary>
    public static Creature Find(IEnumerable<Creature> creatures, CreatureId id) => creatures.First(creature => creature.Id == id);

    /// <summary>
    /// A round in the given combat sub-phase with the four creatures on the timeline, Knight first, then Archer,
    /// Ghoul, Wraith (all Standard, equal initiative, ordered by player slot then creature id).
    /// </summary>
    public static Round CombatRoundAt(RoundSubPhase subPhase)
    {
        var round = RoundAt(RoundSubPhase.TurnOrderResolution);
        round.SetTimeline(CombatTimeline.Of(FourCreatures().Select(creature =>
            new ActivationSlot(creature.Owner, creature.Id, Speed.Standard, creature.CurrentInitiative))));
        while (round.SubPhase != subPhase)
        {
            round.Advance();
        }

        return round;
    }

    public static Round RoundAt(RoundSubPhase subPhase) => RoundAt(subPhase, number: 1);

    /// <summary>
    /// A round by number, at a sub-phase. The number matters to anything that reads the evolution schedule:
    /// round 1 offers an opportunity under the default rule set and round 2 does not (ADR 0056).
    /// </summary>
    public static Round RoundAt(RoundSubPhase subPhase, int number)
    {
        var round = Round.First();
        while (round.Number < number)
        {
            round = round.Next();
        }

        while (round.SubPhase != subPhase)
        {
            round.Advance();
        }

        return round;
    }
}
