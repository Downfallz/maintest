using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a host may send the match while the driver plays it, behind the driver's own lock: the read side, the
/// one command of the host's own (ADR 0087), and the lock itself. Bundled so the session takes them as one
/// dependency.
/// </summary>
internal sealed record TableHandlers(MatchQueryHandlers Queries, ICommandHandler<Concede, Result> Concede, TableGate Gate);
