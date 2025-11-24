using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public abstract record NonNegativeIntStat<TSelf>(int Value) : ValueObject
    where TSelf : NonNegativeIntStat<TSelf>
{
    protected static int EnsureNonNegative(int v, string name)
    {
        var res = Validate((v >= 0, $"{name} >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return v;
    }

    public TSelf WithAdded(int amount)
    {
        var res = Validate((amount >= 0, "Added amount must be >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        var newValue = Value + amount;
        return (TSelf)this with { Value = newValue };
    }

    public TSelf WithSubstracted(int amount)
    {
        var res = Validate((amount >= 0, "Substract amount must be >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        var newValue = Value - amount;
        if (newValue < 0)
            newValue = 0;

        return (TSelf)this with { Value = newValue };
    }

    public override string ToString() => Value.ToString();
}
