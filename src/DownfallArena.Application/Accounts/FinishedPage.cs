namespace DownfallArena.Application.Accounts;

/// <summary>One page of an account's finished games: how many there are in all, on how many pages, and this one's.</summary>
/// <param name="Page">The page shown, from 0; a page asked for past the end shows the last one.</param>
public sealed record FinishedPage(int Total, int Pages, int Page, IReadOnlyList<MatchResult> Items);
