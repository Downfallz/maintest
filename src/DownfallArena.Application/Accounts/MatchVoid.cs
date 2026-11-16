using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// Why the operator voided a match (ADR 0176): a category, an explanation the players read, who voided it and when.
/// Kept on the table's record and on its result, if one was kept.
/// </summary>
public sealed record MatchVoid(string Category, string Explanation, string By, DateTimeOffset At)
{
    public const int ShortestExplanation = 10;

    public const int LongestExplanation = 500;

    /// <summary>The categories a void is filed under, as the panel offers them and the record keeps them.</summary>
    public static readonly IReadOnlyList<string> Categories = ["stuck", "engine", "abuse", "mistake", "other"];

    public static readonly DomainError Invalid = new(
        "Admin.VoidReason",
        $"A void needs a category (stuck, engine, abuse, mistake or other) and an explanation of {ShortestExplanation} to {LongestExplanation} characters the players will read.");

    /// <summary>A reason, or <see cref="Invalid" /> when its category is not one or its explanation too short or long.</summary>
    public static Result<MatchVoid> Create(string? category, string? explanation, string by, DateTimeOffset at)
    {
        var filed = category?.Trim().ToLowerInvariant();
        var said = explanation?.Trim() ?? string.Empty;
        return filed is null || !Categories.Contains(filed) || said.Length is < ShortestExplanation or > LongestExplanation
            ? Result.Failure<MatchVoid>(Invalid)
            : Result.Success(new MatchVoid(filed, said, by, at));
    }
}
