using DownfallArena.Application.Agents;
using DownfallArena.Cli.Tests.Table;

namespace DownfallArena.Cli.Tests;

/// <summary>The console seat of <c>human</c>, answering from a scripted input.</summary>
public sealed class ConsoleAgentTests
{
    /// <summary>
    /// Under a rule set that declares no intent, the console asks for an action at the slot -- a spell, then its
    /// targets -- and never for an intent (ADR 0110). The first entry of every menu plays a whole match.
    /// </summary>
    [Fact]
    public async Task At_activation_the_console_asks_for_an_action_at_the_slot_and_never_for_an_intent()
    {
        using var hosted = new HostedTables();
        var options = CliOptions.Parse(["human", "--declaration", "at-activation", "--format", "2v2"]);
        var session = new GameSession(hosted.Services, options, seed: 7);
        using var output = new StringWriter();
        var person = new ConsoleAgent(new StringReader(string.Concat(Enumerable.Repeat("1\n", 5000))), output);

        await session.PlayAsync(person, session.Agent(AgentSpec.Random, slot: 2), "person", "random");

        output.ToString().ShouldContain("Action of creature");
        output.ToString().ShouldNotContain("Intent of creature");
    }
}
