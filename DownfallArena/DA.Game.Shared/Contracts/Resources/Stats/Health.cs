using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Health(int Value) : ValueObject, IComparable<Health>
{
    public static Health Of(int v)
    {
        var res = Validate((v >= 0, "Health >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }

    public int CompareTo(Health? other)
            => other is null ? 1 : Value.CompareTo(other.Value);

    public static bool operator <(Health left, Health right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Health left, Health right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Health left, Health right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Health left, Health right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) >= 0;
    }

    public bool IsDead() => Value <= 0;

    public override string ToString() => Value.ToString();
}
