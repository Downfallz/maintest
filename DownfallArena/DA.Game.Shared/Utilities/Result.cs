namespace DA.Game.Shared.Utilities;

public readonly record struct Result(bool IsSuccess, string? Error = null, bool IsInvariant = false) : IResult
{
    public static Result Ok() => new(true);

    // Legacy: game-flow / validation style failure (par défaut)
    public static Result Fail(string e) => new(false, e, false);

    // New: invariant / impossible-state failure
    public static Result InvariantFail(string e) => new(false, e, true);
}
