using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches;

public static class ApplicationErrors
{
    public static readonly DomainError MatchNotFound = new("Match.NotFound", "No match with this id exists.");
}
