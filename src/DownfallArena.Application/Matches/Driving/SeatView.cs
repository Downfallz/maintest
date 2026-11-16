using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// What a seat reads while a match is stepped: its board and its options with the answers it has given and the
/// match does not have yet (<see cref="HeldAnswers" />), and the question it is being asked, if any.
/// </summary>
/// <param name="Board">The seat's board, its held picks, speeds and intents added to its own hidden choices.</param>
/// <param name="Options">The seat's options, its held picks taken out of them.</param>
/// <param name="Asked">The question to answer now, or none while the seat waits.</param>
public sealed record SeatView(PlayerBoardState Board, PlayerOptions Options, SeatQuestion? Asked);
