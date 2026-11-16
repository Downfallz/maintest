using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>A step taken over a journal: what the step did, and the match as it left it.</summary>
public sealed record JournaledStep(MatchStep Step, JournaledState State);
