using System.Text.Json.Serialization;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Recipes are catalogue-specific examples, not new rules or hand-edited match snapshots. A recipe plays the
/// combat declaration it names (ADR 0110): the ones at the slot set up a board the free-activation playtest asks
/// about (docs/tabletop/free-activation-playtest.md). <paramref name="AllyIndex" /> is the position of the ally whose
/// question hands the seat over, or <see langword="null" /> for the first one asked.
/// </summary>
internal sealed record PracticeScenario(
    string Id,
    string Name,
    string Goal,
    int Round,
    PlayerOptionsKind Question,
    CombatDeclaration Declaration = CombatDeclaration.HiddenIntent,
    int? AllyIndex = 0)
{
    public const int Seed = 17;

    private static readonly RuleSet HiddenIntents = RuleSet.Create(3, 2, 2, 10, 2.0);
    private static readonly RuleSet AtTheSlot = RuleSet.Create(3, 2, 2, 10, 2.0, combatDeclaration: CombatDeclaration.AtActivation);

    // The page lists the recipes and plays the rules the match serves it; a rule set per recipe is not its to read.
    [JsonIgnore]
    public RuleSet Rules => Declaration == CombatDeclaration.AtActivation ? AtTheSlot : HiddenIntents;

    public static IReadOnlyList<PracticeScenario> All { get; } =
    [
        new("targeting", "Choose your target", "Compare Shock on each enemy. Select a target, read the preview, then confirm: it resolves at once.", 1, PlayerOptionsKind.Target),
        new("multiclass", "Build a multiclass creature", "Round 3: your first creature already owns Brute. Compare another level-1 package with its level-2 upgrades in the talent atlas.", 3, PlayerOptionsKind.Evolution),
        new("stun", "Interrupt an action", "Cast Paralyzing Barb before the enemy acts. Watch its action fizzle when its turn comes, then inspect stun and the immunity left after it expires.", 5, PlayerOptionsKind.Target),
        new("resolution", "Read a layered resolution", "Choose the targets of Void Pulse. It resolves at once; replay the round from its recap to step through damage, bleed and caster effects.", 5, PlayerOptionsKind.Target),
        new("slot-armor", "At the slot: answer an armor", "Spells chosen at the slot. The enemy Ironhide casts Carapace before your creature acts. Choose your spell and its target knowing it: strike through the armor, or turn to another enemy.", 3, PlayerOptionsKind.Action, CombatDeclaration.AtActivation),
        new("slot-overdrive", "At the slot: an ally's Overdrive", "Spells chosen at the slot. Your Blighted casts Overdrive before your Frenzied acts, and Fury becomes payable. Choose with the energy it brought.", 3, PlayerOptionsKind.Action, CombatDeclaration.AtActivation),
        new("slot-drain", "At the slot: drained before your turn", "Spells chosen at the slot. The enemy Oppressor's Claim drains an energy from your Frenzied before it acts, and Fury is out of reach. Choose what is left.", 3, PlayerOptionsKind.Action, CombatDeclaration.AtActivation),
        new("slot-stun", "At the slot: stunned before your turn", "Spells chosen at the slot. The enemy Oppressor's Crash stuns your first creature before it acts: its slot will be skipped, with nothing to choose. Play on with the others.", 3, PlayerOptionsKind.Action, CombatDeclaration.AtActivation, AllyIndex: null),
        new("slot-chain", "At the slot: three allies in a row", "Spells chosen at the slot. Your three creatures act one after the other: open with Contagion to lower the enemies' defense, follow up while it holds, then finish.", 5, PlayerOptionsKind.Action, CombatDeclaration.AtActivation, AllyIndex: null),
    ];
}
