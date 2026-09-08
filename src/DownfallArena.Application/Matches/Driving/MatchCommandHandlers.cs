using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// The player-action commands of a match, bundled so a driver takes them as one dependency.
/// </summary>
public sealed record MatchCommandHandlers(
    ICommandHandler<SubmitEvolutionChoice, Result> SubmitEvolutionChoice,
    ICommandHandler<PassEvolution, Result> PassEvolution,
    ICommandHandler<SubmitSpeedChoice, Result> SubmitSpeedChoice,
    ICommandHandler<SubmitIntent, Result> SubmitIntent,
    ICommandHandler<SubmitAction, Result> SubmitAction,
    ICommandHandler<ResolveNextAction, Result<CombatStep>> ResolveNextAction);
