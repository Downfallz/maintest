using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// A match scenario: a recorded match cut at the top of a round. Its source's match as it was created, the
/// entries before the cut that rebuild it there, the source's entries after it, and the boards at the cut.
/// </summary>
/// <param name="Source">How the recorded match was created: its seed, rules and roster.</param>
/// <param name="Cut">The round it begins at, and where that round begins in the source's journal.</param>
/// <param name="Before">The entries that rebuild the match to the top of the round.</param>
/// <param name="After">What the source did from there, which a scripted opponent can follow.</param>
/// <param name="Checkpoint">Both seats' boards at the cut, as the source stood there.</param>
public sealed record ScenarioCut(JournaledMatch Source, RoundCut Cut, IReadOnlyList<IJournalEntry> Before, IReadOnlyList<IJournalEntry> After, ScenarioCheckpoint Checkpoint);
