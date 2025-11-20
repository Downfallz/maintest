namespace DA.Game.Shared.Utilities;

public readonly record struct Result<T>(bool IsSuccess, T? Value, string? Error = null)
{
    public static Result<T> Ok(T v) => new(true, v);
    public static Result<T> Fail(string e) => new(false, default, e);
}

