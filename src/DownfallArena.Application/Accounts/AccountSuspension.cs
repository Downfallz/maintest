using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// Why and since when the operator suspended an account (ADR 0178): the player is told the reason when they try to
/// sign in, and its seats in unfinished games are offered to other players a day later unless it is lifted first.
/// </summary>
public sealed record AccountSuspension(string Reason, string By, DateTimeOffset At)
{
    public const int ShortestReason = 10;

    public const int LongestReason = 300;

    /// <summary>How long a suspended account's seats in unfinished games are kept for it, should it be restored.</summary>
    public static readonly TimeSpan SeatsKeptFor = TimeSpan.FromDays(1);

    /// <summary>A suspension without a reason its player can read.</summary>
    public static readonly DomainError Invalid = new(
        "Admin.SuspensionReason",
        $"Say why, in {ShortestReason} to {LongestReason} characters: the player reads it when they try to sign in.");

    /// <summary>The suspension the operator asked for, its reason trimmed; refused without a reason of a readable length.</summary>
    public static Result<AccountSuspension> Create(string? reason, string by, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var said = reason?.Trim() ?? string.Empty;
        return said.Length is < ShortestReason or > LongestReason
            ? Result.Failure<AccountSuspension>(Invalid)
            : Result.Success(new AccountSuspension(said, by, at));
    }

    /// <summary>Whether the account's seats are due to be offered to other players: kept a day for it first.</summary>
    public bool SeatsDue(DateTimeOffset now) => now - At >= SeatsKeptFor;
}
