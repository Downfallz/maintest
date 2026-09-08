using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// The return of an episode (ADR 0013, decision C): win +1, loss -1, draw 0, plus a tenth of the remaining-health
/// margin as a fraction of the total maximum health of both teams. Antisymmetric, so the two players' returns of
/// one match sum to zero.
/// </summary>
public static class Returns
{
    public const double HealthMarginWeight = 0.1;

    public static double Of(MatchOutcome outcome, PlayerSlot slot, int ownHealth, int enemyHealth, int totalMaxHealth)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentOutOfRangeException.ThrowIfNegative(ownHealth);
        ArgumentOutOfRangeException.ThrowIfNegative(enemyHealth);
        ArgumentOutOfRangeException.ThrowIfNegative(totalMaxHealth);

        var result = outcome.Winner is null ? 0.0 : outcome.Winner == slot ? 1.0 : -1.0;
        var margin = totalMaxHealth == 0 ? 0.0 : (double)(ownHealth - enemyHealth) / totalMaxHealth;
        return result + (HealthMarginWeight * margin);
    }
}
