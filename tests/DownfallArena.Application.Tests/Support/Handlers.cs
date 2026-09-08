using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Simulation;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// The driver and the batch runner wired over a workflow, the way the composition root does it.
/// </summary>
internal static class Handlers
{
    public static MatchDriver Driver(MatchWorkflow workflow) =>
        new(
            new MatchCommandHandlers(
                new SubmitEvolutionChoiceHandler(workflow),
                new PassEvolutionHandler(workflow),
                new SubmitSpeedChoiceHandler(workflow),
                new SubmitIntentHandler(workflow),
                new SubmitActionHandler(workflow),
                new ResolveNextActionHandler(workflow)),
            new MatchQueryHandlers(
                new GetBoardStateForPlayerHandler(workflow),
                new GetPlayerOptionsHandler(workflow, TestContent.Resources)));

    public static BatchRunner Runner(MatchWorkflow workflow, IRandomSourceFactory random) =>
        new(
            new CreateMatchHandler(workflow, TestContent.Resources, random),
            new JoinMatchHandler(workflow),
            new GetBoardStateForPlayerHandler(workflow),
            Driver(workflow),
            random);
}
