using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What the host gives the API so an operator can change who is playing a seat: the token that says a request
/// is the pilot's, and how to turn the name of an agent into somebody who can sit down.
/// </summary>
/// <remarks>
/// The token is its own, never a seat's. A pilot can move both seats, so a seat token that could also pilot
/// would let either player hand their opponent's seat to a bot — and in hotseat both tokens live in the same
/// browser as the page a player is looking at. Two tokens is what keeps "what this seat may do" and "what the
/// operator may do" separate questions (<c>docs/tabletop/app-roadmap.md</c>, stage 6).
/// </remarks>
/// <param name="Seating">
/// Resolves an agent name for a slot, or null when nothing of that name can sit there. It is a function
/// rather than a factory because the API must not learn how agents are built: which specs exist, what random
/// source they are given and which seat already holds a person are the composition root's business.
/// </param>
internal sealed record TablePilot(string Token, Func<PlayerSlot, string, Occupant?> Seating);
