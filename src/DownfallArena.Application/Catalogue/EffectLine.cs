using System.Globalization;
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
            Bleed bleed => $"Bleed {bleed.AmountPerRound} a round, {Lasting(bleed.Duration)}",
            Regeneration regeneration => $"Regeneration {regeneration.AmountPerRound} a round, {Lasting(regeneration.Duration)}",
            EnergyRegeneration energy => $"Energy regeneration {energy.AmountPerRound} a round, {Lasting(energy.Duration)}",
            Stun stun => $"Stun, {Lasting(stun.Duration)}",
            DefenseBuff buff => $"Defense +{buff.Amount}, {Lasting(buff.Duration)}",
            DefenseDebuff debuff => $"Defense -{debuff.Amount}, {Lasting(debuff.Duration)}",
            InitiativeBuff buff => $"Initiative +{buff.Amount}, {Lasting(buff.Duration)}",
            InitiativeDebuff debuff => $"Initiative -{debuff.Amount}, {Lasting(debuff.Duration)}",
            _ => throw new NotSupportedException($"No card line is written for {effect.GetType().Name}. The set of effects is closed (ADR 0012), so a new one is added here as well."),
        };
    }

    /// <summary>
    /// How long it lasts, as a card says it: <c>permanent</c>, <c>1 round</c>, <c>2 rounds</c>.
    /// <see cref="Duration.ToString" /> is always plural, and the printed deck is not — "1 rounds" is on no
    /// card of <c>docs/tabletop/components.md</c> §2.2, and the widths §2.3 measured were measured without it.
    /// </summary>
    private static string Lasting(Duration duration) => duration.Rounds switch
    {
        null => "permanent",
        1 => "1 round",
        var rounds => $"{rounds.Value.ToString(CultureInfo.InvariantCulture)} rounds",
    };
}
