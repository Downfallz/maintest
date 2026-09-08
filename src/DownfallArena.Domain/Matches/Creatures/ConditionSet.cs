using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// The conditions on one creature, applying each effect's stacking policy.
/// </summary>
internal sealed class ConditionSet
{
    private readonly List<Condition> _conditions = [];

    public IReadOnlyList<Condition> Active => _conditions;

    public bool Has<TEffect>()
        where TEffect : LastingEffect =>
        _conditions.Exists(condition => condition.Effect is TEffect);

    public int Sum<TEffect>(Func<TEffect, int> amount)
        where TEffect : LastingEffect =>
        _conditions.Select(condition => condition.Effect).OfType<TEffect>().Sum(amount);

    /// <summary>
    /// Applies an effect and returns the resulting condition: a new one, the refreshed existing one, or
    /// <c>null</c> when the stacking policy ignores the application.
    /// </summary>
    public Condition? Apply(LastingEffect effect)
    {
        var existing = _conditions.Find(condition => condition.Effect.GetType() == effect.GetType());
        if (existing is null || effect.Stacking == StackingPolicy.Stack)
        {
            var condition = new Condition(effect);
            _conditions.Add(condition);
            return condition;
        }

        if (effect.Stacking == StackingPolicy.Refresh)
        {
            existing.Refresh();
            return existing;
        }

        return null;
    }

    /// <summary>
    /// Counts one round down on every condition and removes the ones that expired, returning them.
    /// </summary>
    public IReadOnlyList<Condition> Tick()
    {
        foreach (var condition in _conditions)
        {
            condition.Tick();
        }

        var expired = _conditions.FindAll(condition => condition.IsExpired);
        _conditions.RemoveAll(condition => condition.IsExpired);
        return expired;
    }
}
