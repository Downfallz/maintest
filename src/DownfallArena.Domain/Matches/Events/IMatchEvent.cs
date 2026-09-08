using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A domain event raised by a match, so that a listener following several matches can tell them apart
/// without inspecting each event type.
/// </summary>
public interface IMatchEvent : IDomainEvent
{
    MatchId MatchId { get; }
}
