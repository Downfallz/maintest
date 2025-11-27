namespace DA.Game.Shared.Utilities;

public readonly record struct Result<T>(bool IsSuccess, T? Value, string? Error = null, bool IsInvariant = false)
{
    public static Result<T> Ok(T v) => new(true, v);

    // Legacy: game-flow / validation failure
    public static Result<T> Fail(string e) => new(false, default, e, false);

    // New: invariant failure
    public static Result<T> InvariantFail(string e) => new(false, default, e, true);
}
