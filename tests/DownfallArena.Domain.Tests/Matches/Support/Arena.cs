using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A small match setup: two creatures per player, a three-spell talent tree, and helpers to walk a round.
/// Strike is known at spawn; Guard requires Strike; Slam sits in a node that requires Guard.
/// </summary>
internal static class Arena
{
    public static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    public static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");
    public static readonly SpellId Slam = SpellId.Parse("spell:slam:v1");

    public static readonly CreatureId Knight = CreatureId.From(1);
    public static readonly CreatureId Archer = CreatureId.From(2);
    public static readonly CreatureId Ghoul = CreatureId.From(4);
    public static readonly CreatureId Wraith = CreatureId.From(5);

    public static GameResources Resources { get; } = GameResources.Create(
        "test",
        [Content.Creature()],
        [Content.Spell("spell:strike:v1"), Content.Spell("spell:guard:v1"), Content.Spell("spell:slam:v1")],
        [TalentTree.Create(
            Content.TreeId,
            "Base",
            new TalentNode(
                "root",
                "Root",
                TalentPrerequisites.None,
                [new TalentSpell(Strike, TalentPrerequisites.None), Content.TalentSpell("spell:guard:v1", "spell:strike:v1")],
                [Content.Node("brawler", Content.TalentSpell("spell:slam:v1")) with { Prerequisites = TalentPrerequisites.Of([Guard], []) }]))]);

    public static Creature Spawn(CreatureId id, PlayerSlot owner) => Creature.Spawn(id, owner, Content.Creature());

    /// <summary>
    /// Four creatures: Knight and Archer for Player1, Ghoul and Wraith for Player2.
    /// </summary>
    public static List<Creature> FourCreatures() =>
        [Spawn(Knight, PlayerSlot.Player1), Spawn(Archer, PlayerSlot.Player1), Spawn(Ghoul, PlayerSlot.Player2), Spawn(Wraith, PlayerSlot.Player2)];

    public static IReadOnlyList<CreatureSnapshot> Snapshots(IEnumerable<Creature> creatures) => [.. creatures.Select(creature => creature.Snapshot())];

    public static Round RoundAt(RoundSubPhase subPhase)
    {
        var round = Round.First();
        while (round.SubPhase != subPhase)
        {
            round.Advance();
        }

        return round;
    }
}
