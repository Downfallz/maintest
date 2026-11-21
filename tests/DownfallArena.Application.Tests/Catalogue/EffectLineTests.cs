using DownfallArena.Application.Catalogue;
using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Application.Tests.Catalogue;

public sealed class EffectLineTests
{
    /// <summary>
    /// Every effect of the closed set (ADR 0012), with the numbers a narrow face shows beside its icon: a sign slip
    /// on one arm would show a debuff as a gain.
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
            (Bleed.Of(4, 2), "4 · 2"),
            (Regeneration.Of(3, 2), "3 · 2"),
            (EnergyRegeneration.Of(1, 3), "1 · 3"),
            (Stun.For(1), "1"),
            (DefenseBuff.Of(1, Duration.OfRounds(3)), "+1 · 3"),
            (DefenseBuff.Of(3, Duration.Permanent), "+3 · perm."),
            (DamageBuff.Of(2, Duration.OfRounds(2)), "+2 · 2"),
            (DefenseDebuff.Of(2, Duration.OfRounds(1)), "-2 · 1"),
            (InitiativeBuff.Of(5, Duration.OfRounds(2)), "+5 · 2"),
            (InitiativeDebuff.Of(5, Duration.Permanent), "-5 · perm."),
        ];

        cases.Select(one => EffectLine.Figures(one.Effect)).ShouldBe(cases.Select(one => one.Figures));
    }
}
