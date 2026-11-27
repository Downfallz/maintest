using System.Globalization;
using DownfallArena.Domain.Resources;
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
    /// <summary>
    /// What a package's passive does, a line per property, in the order a card prints them (ADR 0101). Empty for
    /// a package that gives nothing through it.
    /// </summary>
    public static IReadOnlyList<string> Of(Passive passive)
    {
        ArgumentNullException.ThrowIfNull(passive);
        var lines = new List<string>();
        if (passive.StunImmunity)
        {
            lines.Add("Immune to stun");
        }

        if (passive.UpkeepEnergy > 0)
        {
            lines.Add($"Energy +{passive.UpkeepEnergy} at the start of each round");
        }

        if (passive.DamageBonus > 0)
        {
            lines.Add($"Damage +{passive.DamageBonus} on every hit");
        }

        if (passive.Sunder > 0)
        {
            lines.Add($"Every hit ignores {passive.Sunder} defense");
        }

        return lines;
    }

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
            DamageBuff buff => $"Damage +{buff.Amount} on every hit, {Lasting(buff.Duration)}",
            DefenseDebuff debuff => $"Defense -{debuff.Amount}, {Lasting(debuff.Duration)}",
            InitiativeBuff buff => $"Initiative +{buff.Amount}, {Lasting(buff.Duration)}",
            InitiativeDebuff debuff => $"Initiative -{debuff.Amount}, {Lasting(debuff.Duration)}",
            _ => throw new NotSupportedException($"No card line is written for {effect.GetType().Name}. The set of effects is closed (ADR 0012), so a new one is added here as well."),
        };
    }

    /// <summary>
    /// The line's numbers, as a narrow face shows them beside the stat's icon, with the field chips' words for the
    /// rounds (<c>battle/src/target-preview.ts</c>) so that no number reads two ways (#1026): <c>Damage 7</c> is
    /// <c>7</c>, <c>Defense +1, 3 rounds</c> is <c>+1 · 3 rds</c>, <c>Bleed 4 a round, 2 rounds</c> is
    /// <c>4/rd · 2 rds</c>, <c>Stun, 1 round</c> is <c>1 rd</c>, <c>Defense +3, permanent</c> is <c>+3 · perm.</c>.
    /// </summary>
    public static string Figures(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect switch
        {
            Damage damage => Number(damage.Amount),
            Heal heal => Number(heal.Amount),
            EnergyGain energy => $"+{Number(energy.Amount)}",
            EnergyDrain drain => $"-{Number(drain.Amount)}",
            Bleed bleed => $"{Number(bleed.AmountPerRound)}/rd · {Span(bleed.Duration)}",
            Regeneration regeneration => $"{Number(regeneration.AmountPerRound)}/rd · {Span(regeneration.Duration)}",
            EnergyRegeneration energy => $"{Number(energy.AmountPerRound)}/rd · {Span(energy.Duration)}",
            Stun stun => Span(stun.Duration),
            DefenseBuff buff => $"+{Number(buff.Amount)} · {Span(buff.Duration)}",
            DamageBuff buff => $"+{Number(buff.Amount)} · {Span(buff.Duration)}",
            DefenseDebuff debuff => $"-{Number(debuff.Amount)} · {Span(debuff.Duration)}",
            InitiativeBuff buff => $"+{Number(buff.Amount)} · {Span(buff.Duration)}",
            InitiativeDebuff debuff => $"-{Number(debuff.Amount)} · {Span(debuff.Duration)}",
            _ => throw new NotSupportedException($"No card figures are written for {effect.GetType().Name}. The set of effects is closed (ADR 0012), so a new one is added here as well."),
        };
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Span(Duration duration) => duration.Rounds switch
    {
        null => "perm.",
        1 => "1 rd",
        var rounds => $"{Number(rounds.Value)} rds",
    };

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
