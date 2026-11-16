using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// A scenario's match, rebuilt under its own id to the top of the scenario's round and found to stand where its
/// source stood: a match of its own from here, which nothing it does writes back to the source.
/// </summary>
/// <param name="Match">How the new match was created: the source's seed, rules and roster under a new id and run.</param>
/// <param name="Entries">The first lines of its own journal: the entries it was rebuilt from, without the source's seat changes.</param>
public sealed record ScenarioStart(JournaledMatch Match, IReadOnlyList<IJournalEntry> Entries);
