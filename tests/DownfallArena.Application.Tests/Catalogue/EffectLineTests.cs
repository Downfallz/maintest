using DownfallArena.Application.Catalogue;
using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Application.Tests.Catalogue;

public sealed class EffectLineTests
{
    /// <summary>
    /// Every effect of the closed set (ADR 0012), with the numbers a narrow face shows beside its icon: a sign slip
    /// on one arm would show a debuff as a gain. The rounds carry the field chips' words (<c>/rd</c>, <c>rds</c>,
    /// <c>perm.</c>), so a stun's <c>1</c> is not read as an amount nor a bleed's <c>4 · 2</c> either way (#1026).
    /// </summary>
    [Fact]
    public void Each_effect_s_figures_are_its_amount_with_its_sign_then_how_many_rounds_it_lasts()
    {
        (Effect Effect, string Figures)[] cases =
        [
            (Damage.Of(7), "7"),
            (Heal.Of(4), "4"),
            (EnergyGain.Of(2), "+2"),
            (EnergyDrain.Of(2), "-2"),
            (Bleed.Of(4, 2), "4/rd · 2 rds"),
            (Regeneration.Of(3, 2), "3/rd · 2 rds"),
            (EnergyRegeneration.Of(1, 3), "1/rd · 3 rds"),
            (Stun.For(1), "1 rd"),
            (DefenseBuff.Of(1, Duration.OfRounds(3)), "+1 · 3 rds"),
            (DefenseBuff.Of(3, Duration.Permanent), "+3 · perm."),
            (DamageBuff.Of(2, Duration.OfRounds(2)), "+2 · 2 rds"),
            (DefenseDebuff.Of(2, Duration.OfRounds(1)), "-2 · 1 rd"),
            (InitiativeBuff.Of(5, Duration.OfRounds(2)), "+5 · 2 rds"),
            (InitiativeDebuff.Of(5, Duration.Permanent), "-5 · perm."),
        ];

        cases.Select(one => EffectLine.Figures(one.Effect)).ShouldBe(cases.Select(one => one.Figures));
    }
}
