using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What one seat reads, as one moment: the question its person is on, the board and the options that go with it
/// -- both carrying the picks, speeds and intents the seat has answered and the match does not have yet (ADR
/// 0111, ADR 0117) -- and how many intents the other side still holds face down, as a number and nothing else.
/// </summary>
internal sealed record SeatSnapshot(Asking? Waiting, PlayerBoardState Board, PlayerOptions Options, int OpponentIntents);
