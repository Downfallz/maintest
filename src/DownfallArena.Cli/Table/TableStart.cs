using DownfallArena.Application.Agents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>How a session is started beyond its seats. The default records nothing and keeps nothing.</summary>
/// <param name="Wrap">
/// What the driver plays for a seat, given the match the seat is in. It exists because the match id is
/// created as the session starts and a recorder needs it to wrap a seat (<c>docs/tabletop/app-roadmap.md</c>,
/// stage 5), and because the wrapping has to go <em>around</em> the seat: a <c>RecordingAgent</c> seated
/// inside one would be swapped out by the next handover, and the recording would stop without saying so. Null
/// plays the seat itself, which is every caller that records nothing.
/// </param>
/// <param name="MatchIdWanted">
/// An earlier host's match id (ADR 0091), so every token and page that named the match still does; null lets
/// the match take a fresh one.
/// </param>
internal sealed record TableStart(Func<MatchId, SeatAgent, IPlayerAgent>? Wrap = null, MatchId? MatchIdWanted = null);
