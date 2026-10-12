using System.Reflection;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// <see cref="IPlayerAgent.DecideAction" /> has a default, which is right for an agent that reads the board as
/// it stands and wrong for one that wraps another: the default asks the wrapper's own intent and targets, and the
/// wrapped agent's reading of a spell chosen at the slot is never asked (ADR 0110). An agent added later without
/// it would compile and play quietly wrong, so every agent the engine and the hosts ship either declares it or is
/// named below with the reason it does not.
/// </summary>
public sealed class PlayerAgentContractTests
{
    /// <summary>The agents that rely on the default, each for a reason.</summary>
    private static readonly Dictionary<string, string> OnTheDefault = new(StringComparer.Ordinal)
    {
        // They read the board as it stands: their intent then their targets is their action.
        ["RandomAgent"] = "reads the board as it stands",
        ["PolicyAgent"] = "reads the board as it stands until the action encoding of plan PR 4",

        // The table's seats: a table refuses a rule set that declares no intent until plan PR 5 (TableComposer).
        ["HumanSeat"] = "table seat, plan PR 5",
        ["Journaled"] = "table seat, plan PR 5",
        ["PracticeAgent"] = "table seat, plan PR 5",
    };

    [Fact]
    public void Every_agent_declares_how_it_chooses_an_action_or_says_why_not()
    {
        var agents = new[] { typeof(IPlayerAgent).Assembly, typeof(TableComposer).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlayerAgent).IsAssignableFrom(type))
            .ToList();

        var silent = agents
            .Where(type => type.GetMethod(nameof(IPlayerAgent.DecideAction), BindingFlags.Public | BindingFlags.Instance) is null)
            .Select(type => type.Name)
            .Where(name => !OnTheDefault.ContainsKey(name));

        agents.ShouldNotBeEmpty();
        silent.ShouldBeEmpty();
    }

    [Fact]
    public void Every_agent_named_as_relying_on_the_default_still_exists_and_still_does()
    {
        var onTheDefault = new[] { typeof(IPlayerAgent).Assembly, typeof(TableComposer).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlayerAgent).IsAssignableFrom(type))
            .Where(type => type.GetMethod(nameof(IPlayerAgent.DecideAction), BindingFlags.Public | BindingFlags.Instance) is null)
            .Select(type => type.Name);

        OnTheDefault.Keys.ShouldBe(onTheDefault, ignoreOrder: true);
    }
}
