namespace DownfallArena.Application.Accounts;

/// <summary>
/// One creature a seat played in a finished match, kept with the result for a player's statistics: which creature
/// it was, the mutations it bought, whether it was still standing when the match ended, and the spells it cast that
/// resolved, each with how many times. A fizzle cast nothing. Ids, never names: a name is the catalogue's to give.
/// </summary>
public sealed record PlayedCreature(string Seat, string Creature, IReadOnlyList<string> Mutations, bool Survived, IReadOnlyDictionary<string, int> Casts);
