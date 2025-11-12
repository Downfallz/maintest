namespace DA.Game.Shared;

public readonly record struct Result(bool IsSuccess, string? Error = null) : IResult
{
    public static Result Ok() => new(true);
    public static Result Fail(string e) => new(false, e);
}

