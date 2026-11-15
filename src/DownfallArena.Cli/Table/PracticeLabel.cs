namespace DownfallArena.Cli.Table;

/// <summary>
/// What a seat's view says of a table that is a match scenario's practice (ADR 0179): the page shows it as practice,
/// says where it was cut, and offers to start it again. A table that is a game has none.
/// </summary>
/// <param name="Scenario">The scenario's id, which starts it again.</param>
/// <param name="Title">What the scenario is called.</param>
/// <param name="Goal">What it asks the person to try, if anything.</param>
/// <param name="Round">The round it was cut at the top of.</param>
internal sealed record PracticeLabel(string Scenario, string Title, string? Goal, int Round);
