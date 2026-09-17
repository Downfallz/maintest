using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One effect, in the words a card prints: <c>Damage 7</c>, <c>Bleed 4 a round, 2 rounds</c>,
/// <c>Defense +3, permanent</c>.
/// </summary>
/// <remarks>
/// The wording is the printed deck's, line for line (<c>docs/tabletop/components.md</c> §2.2), and that is the
/// point of rendering it here rather than in the page: a card on a screen and a card on the table have to read
/// the same, or a playtest is of neither. It is also what keeps the page free of content — it never learns what
/// a Bleed is.
///
/// The set of effects is closed (ADR 0012), so an effect this does not know is a bug and not a blank line.
/// </remarks>
public static class EffectLine
{
    public static string Of(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect switch
        {
            Damage damage => $"Damage {damage.Amount}",
            Heal heal => $"Heal {heal.Amount}",
            EnergyGain energy => $"Energy +{energy.Amount}",
            EnergyDrain drain => $"Energy -{drain.Amount}",
            Bleed bleed => $"Bleed {bleed.AmountPerRound} a round, {bleed.Duration}",
            Regeneration regeneration => $"Regeneration {regeneration.AmountPerRound} a round, {regeneration.Duration}",
            EnergyRegeneration energy => $"Energy regeneration {energy.AmountPerRound} a round, {energy.Duration}",
            Stun stun => $"Stun, {stun.Duration}",
            DefenseBuff buff => $"Defense +{buff.Amount}, {buff.Duration}",
            DefenseDebuff debuff => $"Defense -{debuff.Amount}, {debuff.Duration}",
            InitiativeBuff buff => $"Initiative +{buff.Amount}, {buff.Duration}",
            InitiativeDebuff debuff => $"Initiative -{debuff.Amount}, {debuff.Duration}",
            _ => throw new NotSupportedException($"No card line is written for {effect.GetType().Name}. The set of effects is closed (ADR 0012), so a new one is added here as well."),
        };
    }
}
