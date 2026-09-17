namespace DownfallArena.Application.Agents;

/// <summary>
/// What an action is expected to do, before any weight is applied: one quantity per scoring weight, in the
/// order <see cref="ScoringWeights.Named"/> lists them, each already signed (for the actor's side, against
/// it). The scorer's score of an action is exactly <see cref="ScoringWeights.Apply"/> over its terms, so the
/// terms are what the heuristic reads and a policy can read the same numbers (ADR 0051).
/// </summary>
public readonly record struct ScoreTerms(
    double Damage,
    double Kill,
    double Heal,
    double Stun,
    double Bleed,
    double Defense,
    double Energy,
    double Initiative,
    double Pressure)
{
    public static ScoreTerms Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>The names of the terms, in order: the weight names, since a term is what its weight multiplies.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. ScoringWeights.Default.Named.Select(weight => weight.Name)];

    public static int Count => Names.Count;

    public static ScoreTerms operator +(ScoreTerms left, ScoreTerms right) => new(
        left.Damage + right.Damage,
        left.Kill + right.Kill,
        left.Heal + right.Heal,
        left.Stun + right.Stun,
        left.Bleed + right.Bleed,
        left.Defense + right.Defense,
        left.Energy + right.Energy,
        left.Initiative + right.Initiative,
        left.Pressure + right.Pressure);

    public static ScoreTerms operator *(double factor, ScoreTerms terms) => new(
        factor * terms.Damage,
        factor * terms.Kill,
        factor * terms.Heal,
        factor * terms.Stun,
        factor * terms.Bleed,
        factor * terms.Defense,
        factor * terms.Energy,
        factor * terms.Initiative,
        factor * terms.Pressure);

    public static ScoreTerms Add(ScoreTerms left, ScoreTerms right) => left + right;

    public static ScoreTerms Multiply(double factor, ScoreTerms terms) => factor * terms;

    /// <summary>The terms as a vector, in the order of <see cref="Names"/>: what a dataset records per candidate.</summary>
    public double[] ToArray() => [Damage, Kill, Heal, Stun, Bleed, Defense, Energy, Initiative, Pressure];
}
