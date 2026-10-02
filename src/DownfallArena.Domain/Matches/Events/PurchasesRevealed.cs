using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The Evolution sub-phase ended and every package picked in it was bought at once, both players' together
/// (ADR 0089): Player 1's picks first, then Player 2's, each in the order it was made. Raised only when someone
/// picked; a round where both passed reveals nothing.
/// </summary>
public sealed record PurchasesRevealed(MatchId MatchId, RoundId RoundId, IReadOnlyList<EvolutionChoice> Choices) : IMatchEvent;
