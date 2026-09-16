using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// The conditions on one creature, applying each effect's stacking policy.
/// </summary>
internal sealed class ConditionSet
{
    private readonly List<Condition> _conditions = [];

    public ConditionSet()
    {
    }

    /// <summary>
    /// The conditions a snapshot was taken of, in the order they were applied: the stacking policy reads the
    /// first of a kind, so the order is part of the state. A kind that refreshes or ignores is carried at most
    /// once, because <see cref="Apply"/> never adds a second; two of them is a snapshot no creature took.
    /// </summary>
    public ConditionSet(IEnumerable<ConditionSnapshot> snapshots)
    {
        _conditions.AddRange(snapshots.Select(Condition.Restore));

        var twice = _conditions
            .Where(condition => condition.Effect.Stacking != StackingPolicy.Stack)
            .GroupBy(condition => condition.Effect.GetType())
            .FirstOrDefault(kind => kind.Count() > 1);
        if (twice is not null)
        {
            throw new ArgumentException($"No creature carries two {twice.Key.Name} conditions: the kind does not stack.", nameof(snapshots));
        }
    }

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
    public Condition? Apply(LastingEffect effect, ConditionSource? source)
    {
        var existing = _conditions.Find(condition => condition.Effect.GetType() == effect.GetType());
        if (existing is null || effect.Stacking == StackingPolicy.Stack)
        {
            var condition = new Condition(effect, source);
            _conditions.Add(condition);
            return condition;
        }

        if (effect.Stacking == StackingPolicy.Refresh)
        {
            existing.Refresh(source);
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
