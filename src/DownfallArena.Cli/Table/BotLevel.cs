using DownfallArena.Application.Agents;

namespace DownfallArena.Cli.Table;

/// <summary>One level: its id, the word a player reads, the bot it seats, and the bots it seated before.</summary>
internal sealed record BotLevel(string Level, string Name, string Agent, IReadOnlyList<string> Formerly)
{
    /// <summary>Whether this level seats <paramref name="spec" /> now.</summary>
    public bool Seats(string spec) => Same(Agent, spec);

    /// <summary>Whether this level seated <paramref name="spec" /> before.</summary>
    public bool Seated(string spec) => Formerly.Any(one => Same(one, spec));

    // A seat names its bot as the spec writes itself back ("Greedy" for "greedy"), so two are compared as parsed.
    private static bool Same(string one, string other) =>
        string.Equals(one, other, StringComparison.OrdinalIgnoreCase) || string.Equals(Canonical(one), Canonical(other), StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string spec)
    {
        try
        {
            return AgentSpec.Parse(spec).ToString();
        }
        catch (ArgumentException)
        {
            return spec;
        }
    }
}
