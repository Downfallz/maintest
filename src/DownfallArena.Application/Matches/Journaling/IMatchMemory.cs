using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Something that keeps, by match, what the match's events told it. A match rebuilt from its journal raises
/// every event again, so whatever remembers them is told to forget the match first, or it would hold them twice.
/// </summary>
public interface IMatchMemory
{
    /// <summary>Lets go of what was kept for a match. Forgetting a match never heard of is nothing.</summary>
    void Forget(MatchId matchId);
}
