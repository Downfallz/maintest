using System.Globalization;

namespace DownfallArena.SharedKernel.Stats;

/// <summary>
/// Base for integer stats that can never go below zero. Arithmetic returns a new value; subtraction floors at zero.
/// </summary>
public abstract record NonNegativeStat<TSelf> : IComparable<TSelf>
    where TSelf : NonNegativeStat<TSelf>
{
    protected NonNegativeStat(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; private init; }

    public bool IsZero => Value == 0;

    public TSelf Plus(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return (TSelf)(this with { Value = Value + amount });
    }

    public TSelf Minus(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return (TSelf)(this with { Value = Math.Max(0, Value - amount) });
    }

    public int CompareTo(TSelf? other) => other is null ? 1 : Value.CompareTo(other.Value);

    public static bool operator <(NonNegativeStat<TSelf> left, NonNegativeStat<TSelf> right) => Compare(left, right) < 0;

    public static bool operator >(NonNegativeStat<TSelf> left, NonNegativeStat<TSelf> right) => Compare(left, right) > 0;

    public static bool operator <=(NonNegativeStat<TSelf> left, NonNegativeStat<TSelf> right) => Compare(left, right) <= 0;

    public static bool operator >=(NonNegativeStat<TSelf> left, NonNegativeStat<TSelf> right) => Compare(left, right) >= 0;

    public sealed override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private static int Compare(NonNegativeStat<TSelf>? left, NonNegativeStat<TSelf>? right)
    {
        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        return right is null ? 1 : left.Value.CompareTo(right.Value);
    }
}
