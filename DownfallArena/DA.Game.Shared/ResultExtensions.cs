namespace DA.Game.Shared;

public static class ResultExtensions
{
    public static Result<T> To<T>(this Result result)
    {
        if (result.IsSuccess)
            return Result<T>.Ok(default!);

        return Result<T>.Fail(result.Error!);
    }
}