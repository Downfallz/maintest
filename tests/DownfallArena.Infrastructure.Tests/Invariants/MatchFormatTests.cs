using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>
/// Every offered format, played on the repository's own content: the engine takes the team size from the rule
/// set and nothing else has to be told (<see cref="MatchFormat" />). These are the tests that would fail if a
/// rule, a projection or an agent had quietly assumed three creatures a side.
/// </summary>
public sealed class MatchFormatTests
{
    private static readonly GameResources Content =
        GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Path.Combine(AppContext.BaseDirectory, "data")));

    public static TheoryData<string> Formats() => new("1v1", "2v2", "3v3");

    /// <summary>
    /// Greedy against Greedy, which exercises the scorer, the lookahead's rollout and every projection a seat
    /// reads. A format that broke one of them never reaches an outcome.
    /// </summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Every_offered_format_plays_to_an_outcome(string format)
    {
        var played = await PlayAsync(format, AgentSpec.Greedy, matches: 12, baseSeed: 31000);

        played.ShouldNotBeEmpty();
        foreach (var match in played)
        {
            match.Result.Outcome.ShouldNotBeNull($"the match on seed {match.Result.Seed} reached no outcome");
            match.Result.Rounds.ShouldBeGreaterThan(0);
        }
    }

    /// <summary>
    /// The picks a player takes in an opportunity, against what the rule set says the format leaves them. A
    /// creature buys at most one package an opportunity (ADR 0066), so 1v1 gives one pick without a rule of
    /// its own -- and this is the test that says so on a played match rather than on an arithmetic helper.
    /// </summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public async Task No_player_takes_more_picks_in_one_opportunity_than_the_format_leaves(string format)
    {
        var rules = RuleSet.Default.InFormat(MatchFormat.Parse(format));
        var played = await PlayAsync(format, PlayedMatches.Exploring, matches: 12, baseSeed: 33000);

        // Grouped by the match too: a RoundId is the round's identity inside its match, so two matches of a
        // batch hold the same one and a key without the seed would add their picks together.
        var taken = played
            .SelectMany(match => match.Events.OfType<EvolutionChoiceSubmitted>().Select(choice => (match.Result.Seed, choice.RoundId, choice.Slot)))
            .GroupBy(choice => (choice.Seed, choice.RoundId, choice.Slot))
            .ToList();

        taken.ShouldNotBeEmpty("no package was ever bought, so this asserts nothing");
        foreach (var opportunity in taken)
        {
            opportunity.Count().ShouldBeLessThanOrEqualTo(
                rules.EvolutionPicksUsableInAnOpportunity,
                $"a player took {opportunity.Count()} picks in one opportunity of a {format} match");
        }
    }

    /// <summary>
    /// A roster is sized by the rule set and checked by the aggregate (<c>Match.WrongTeamSize</c>), so a
    /// format's own roster is the only one its matches accept. Guarded here because every host builds that
    /// roster itself, from the rule set it was handed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public async Task A_format_refuses_a_roster_of_another_format(string format)
    {
        var rules = RuleSet.Default.InFormat(MatchFormat.Parse(format));
        var wrong = rules.TeamSize == 1 ? 2 : rules.TeamSize - 1;

        var failure = await Should.ThrowAsync<Exception>(() => PlayAsync(format, AgentSpec.Greedy, matches: 1, baseSeed: 35000, teamSize: wrong));

        failure.Message.ShouldContain(MatchErrors.WrongTeamSize.Code);
    }

    /// <param name="teamSize">The roster size, when it is deliberately not the format's.</param>
    private static async Task<IReadOnlyList<PlayedMatch>> PlayAsync(string format, AgentSpec agent, int matches, int baseSeed, int? teamSize = null)
    {
        var rules = RuleSet.Default.InFormat(MatchFormat.Parse(format));
        var log = new MatchEventLog();
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: baseSeed);
        services.AddSingleton<IGameResources>(Content);
        services.AddSingleton<IDomainEventListener>(log);
        await using var provider = services.BuildServiceProvider();

        var roster = Enumerable.Repeat(Content.Creatures.First().Id, teamSize ?? rules.TeamSize).ToList();
        var batch = await provider.GetRequiredService<BatchRunner>().RunAsync(new SimulationScenario
        {
            RuleSet = rules,
            Player1Roster = roster,
            Player2Roster = roster,
            Player1Agent = agent,
            Player2Agent = agent,
            Matches = matches,
            BaseSeed = baseSeed,
        });

        return [.. batch.Results.OrderBy(result => result.Index).Select(result => new PlayedMatch(result, log.Of(result.MatchId)))];
    }
}
