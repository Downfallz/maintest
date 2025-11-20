namespace DA.Game.Shared.Utilities;

public abstract record ValueObject
{
    protected static Result Validate(params (bool ok, string error)[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var (ok, error) in rules)
            if (!ok) return Result.Fail(error);
        return Result.Ok();
    }

    protected static Result ValidateRange(double value, double min, double max, string name)
        => value < min || value > max
            ? Result.Fail($"{name} doit être entre {min} et {max}.")
            : Result.Ok();

    protected static Result ValidateNonNegative(double value, string name)
        => value < 0
            ? Result.Fail($"{name} doit être ≥ 0.")
            : Result.Ok();

    protected static Result ValidateAll(params (bool ok, string error)[] rules)
    {
        var errors = rules.Where(r => !r.ok).Select(r => r.error).ToArray();
        return errors.Length == 0
            ? Result.Ok()
            : Result.Fail(string.Join(" | ", errors));
    }
}

