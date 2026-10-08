namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Where an evaluation stands when one more match has finished: how many of its matches are played, and how
/// they went for the two agents. Both seatings are played together, so a partial count is read across both
/// seats rather than leaning on the one that happened to be played first.
/// </summary>
public sealed record EvaluationProgress(int Played, int Total, int WinsOfA, int WinsOfB, int Draws);
