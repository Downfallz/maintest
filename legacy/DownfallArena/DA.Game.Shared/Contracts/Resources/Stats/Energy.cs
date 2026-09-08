using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Energy(int Value) : NonNegativeIntStat<Energy>(Value), IComparable<Energy>
{
    public static Energy Of(int v)
    {
        var res = Validate((v >= 0, "Energy >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }

    public int CompareTo(Energy? other)
        => other is null ? 1 : Value.CompareTo(other.Value);

    public static bool operator <(Energy left, Energy right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Energy left, Energy right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Energy left, Energy right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Energy left, Energy right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) >= 0;
    }

    public override string ToString() => Value.ToString();
}
