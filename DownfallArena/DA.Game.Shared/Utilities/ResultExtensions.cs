using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Utilities;

public static class ResultExtensions
{
    public static Result<T> To<T>(this Result result)
    {
        if (result.IsSuccess)
            return Result<T>.Ok(default!);

        // conserve le message + le flag invariant
        return new Result<T>(
            IsSuccess: false,
            Value: default,
            Error: result.Error,
            IsInvariant: result.IsInvariant);
    }

    public static bool IsInvariantFailure(this Result r) =>
    !r.IsSuccess && r.IsInvariant;
}
