namespace DownfallArena.Domain.Matches;

/// <summary>
/// When a <see cref="RuleSet" /> has a Creature choose the Spell it casts in combat (ADR 0110).
/// </summary>
/// <remarks>
/// Two rules are kept playable side by side so that a playtest can compare them and the one that loses can be
/// removed in one change (<c>docs/domain/free-activation-plan.md</c>). Neither touches resolution: an action is
/// resolved the same way whichever of them chose its Spell.
/// </remarks>
public enum CombatDeclaration
{
    /// <summary>
    /// Today's rule and the default: every Creature on the timeline declares a hidden Intent before combat, and at
    /// its slot its owner chooses only the targets (ADR 0083, 0107).
    /// </summary>
    HiddenIntent,

    /// <summary>
    /// No Intent: at its slot, a Creature's owner chooses a Spell it can cast now and the Spell's targets together,
    /// and the action resolves on confirmation.
    /// </summary>
    AtActivation,
}
