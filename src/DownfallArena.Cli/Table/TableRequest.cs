using System.Text.Json;
using DownfallArena.Application.Agents;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a table is asked to be before it is composed: who sits in each seat, who the people are, when they
/// take over, and the seed. It is what the command line says of the table it starts with and what the lobby
/// says of every table after it, read into one shape so both are composed the same way (ADR 0081).
/// </summary>
/// <param name="Player1">The agent in seat 1, or <c>null</c> for a person.</param>
/// <param name="Player2">The agent in seat 2, or <c>null</c> for a person.</param>
/// <param name="Who">The people's initials, for the run stamp (<c>human:mk</c>), or none.</param>
/// <param name="Handover">The round the people take over on, both seats bots until then, or none.</param>
/// <param name="Seed">The match seed. None lets the host draw one.</param>
internal sealed record TableRequest(AgentSpec? Player1, AgentSpec? Player2, string? Who, int? Handover, int? Seed)
{
    /// <summary>The word the lobby uses for a seat a person plays; anything else names an agent.</summary>
    public const string Person = "person";

    /// <summary>The table the command line asked for: a person in every seat no agent was named for.</summary>
    public static TableRequest Of(CliOptions options, int seed)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TableRequest(
            options.Player1Named ? options.Player1 : null,
            options.Player2Named ? options.Player2 : null,
            options.Who,
            options.Handover,
            seed);
    }

    /// <summary>
    /// A request as the lobby posts it, or the reason it is not one. Each seat is <c>person</c> or an agent spec
    /// the CLI understands; a seat left out is a person, as it is on the command line.
    /// </summary>
    public static TableRequest Parse(string body, out string? problem)
    {
        problem = null;
        TableRequestBody? posted;
        try
        {
            posted = string.IsNullOrWhiteSpace(body) ? new TableRequestBody() : JsonSerializer.Deserialize<TableRequestBody>(body, TableRequestBody.Options);
        }
        catch (JsonException exception)
        {
            problem = exception.Message;
            return Nothing;
        }

        if (posted is null)
        {
            problem = "A table names who sits in player1 and player2: 'person', or an agent such as 'greedy'.";
            return Nothing;
        }

        if (posted.Handover is { } round && round < 1)
        {
            problem = $"Round {round} is not a round to hand over on.";
            return Nothing;
        }

        var player1 = Seat(posted.Player1, "player1", ref problem);
        var player2 = Seat(posted.Player2, "player2", ref problem);
        return problem is null
            ? new TableRequest(player1, player2, string.IsNullOrWhiteSpace(posted.Who) ? null : posted.Who.Trim(), posted.Handover, posted.Seed)
            : Nothing;
    }

    private static readonly TableRequest Nothing = new(null, null, null, null, null);

    private static AgentSpec? Seat(string? named, string slot, ref string? problem)
    {
        if (problem is not null || string.IsNullOrWhiteSpace(named) || string.Equals(named.Trim(), Person, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return AgentSpec.Parse(named.Trim());
        }
        catch (ArgumentException exception)
        {
            problem = $"Nobody called '{named}' can sit in {slot}: {exception.Message}";
            return null;
        }
    }

    /// <summary>The JSON the lobby posts, camelCase like everything else on this host's wire.</summary>
    private sealed record TableRequestBody
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public string? Player1 { get; init; }

        public string? Player2 { get; init; }

        public string? Who { get; init; }

        public int? Handover { get; init; }

        public int? Seed { get; init; }
    }
}
