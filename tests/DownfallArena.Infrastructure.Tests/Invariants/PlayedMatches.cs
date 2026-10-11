using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>
/// Matches played once for every invariant test: the repository's own content built from <c>data/</c>, the
/// default rules, and three ways of playing it that between them reach most of the rules.
/// <list type="bullet">
/// <item>A random mirror casts what no scorer would: a stun on a creature immune to it, which the scored
/// agents price at nothing and so never try. Without it the stun-immunity invariant saw no case at all, and
/// removing both of that rule's gates left every test green.</item>
/// <item>An exploring mirror, Greedy with one decision in four taken at random, reaches the choices a single
/// agent never makes: odd speed choices, spells no argmax picks, ties both sides hold.</item>
/// <item>The <c>search-23</c> mirror reaches the far end of a match. It stacks permanent defense past the
/// ceiling of ten, trades stuns, and runs to the round cap in most of its matches (journal, 2026-09-24).</item>
/// <item>A random and an exploring mirror again, under a rule set that declares no intent (ADR 0110): every
/// invariant holds whichever rule chose the spell, and a skipped slot replaces a fizzle there.</item>
/// </list>
/// Seeds are fixed, so a failing invariant names a match that replays.
/// </summary>
public sealed class PlayedMatches : IAsyncLifetime
{
    public static readonly AgentSpec Exploring = AgentSpec.Parse("explore:0.25");

    public static readonly AgentSpec Stacking = AgentSpec.Parse($"heuristic:{Path.Combine(AppContext.BaseDirectory, "weights", "search-23.json")}");

    public IGameResources Resources { get; } = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Path.Combine(AppContext.BaseDirectory, "data")));

    public static RuleSet Rules => RuleSet.Default;

    /// <summary>The same rules with the spell chosen at the slot (ADR 0110).</summary>
    public static RuleSet RulesAtActivation => Rules.WithCombatDeclaration(CombatDeclaration.AtActivation);

    public IReadOnlyList<PlayedMatch> Matches { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var random = await PlayAsync(AgentSpec.Random, matches: 120, baseSeed: 5000);
        var exploring = await PlayAsync(Exploring, matches: 80, baseSeed: 7000);
        var stacking = await PlayAsync(Stacking, matches: 30, baseSeed: 9000);
        var randomAtActivation = await PlayAsync(AgentSpec.Random, matches: 60, baseSeed: 11000, RulesAtActivation);
        var exploringAtActivation = await PlayAsync(Exploring, matches: 40, baseSeed: 13000, RulesAtActivation);
        Matches = [.. random, .. exploring, .. stacking, .. randomAtActivation, .. exploringAtActivation];
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// A mirror of <paramref name="agent"/>, one match a seed from <paramref name="baseSeed"/>, on
    /// <paramref name="rules"/> or the default ones.
    /// </summary>
    public async Task<IReadOnlyList<PlayedMatch>> PlayAsync(AgentSpec agent, int matches, int baseSeed, RuleSet? rules = null)
    {
        rules ??= Rules;
        var log = new MatchEventLog();
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: baseSeed);
        services.AddSingleton(Resources);
        services.AddSingleton<IDomainEventListener>(log);
        await using var provider = services.BuildServiceProvider();

        var roster = Enumerable.Repeat(Resources.Creatures.First().Id, rules.TeamSize).ToList();
        var scenario = new SimulationScenario
        {
            RuleSet = rules,
            Player1Roster = roster,
            Player2Roster = roster,
            Player1Agent = agent,
            Player2Agent = agent,
            Matches = matches,
            BaseSeed = baseSeed,
        };
        var batch = await provider.GetRequiredService<BatchRunner>().RunAsync(scenario);

        return [.. batch.Results.OrderBy(result => result.Index).Select(result => new PlayedMatch(result, log.Of(result.MatchId), rules.CombatDeclaration))];
    }
}
