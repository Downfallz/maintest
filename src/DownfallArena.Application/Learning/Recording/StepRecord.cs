using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// One decision as a dataset sees it: where it happened, what the player saw, what they could do, and what they did.
/// </summary>
public sealed record StepRecord
{
    public required MatchId MatchId { get; init; }

    public required PlayerSlot Slot { get; init; }

    public int? Round { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    public required ActionKind Kind { get; init; }

    public required Observation Observation { get; init; }

    /// <summary>The keys of every action the options offered, in candidate order.</summary>
    public required IReadOnlyList<string> Candidates { get; init; }

    /// <summary>
    /// The scorer's terms of every candidate, one vector per entry of <see cref="Candidates"/> in the same order,
    /// each in the order the manifest's term names list (ADR 0051).
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<float>> CandidateTerms { get; init; }

    /// <summary>The key of the chosen action, always one of <see cref="Candidates"/>.</summary>
    public required string Action { get; init; }

    public required ActionCode Code { get; init; }

    /// <summary>
    /// Who decided this step, named the way a run stamp names an agent: an agent by its spec
    /// (<c>Greedy</c>), a person as <c>human</c> or <c>human:&lt;initials&gt;</c>. Null when nothing told the
    /// recorder.
    /// </summary>
    /// <remarks>
    /// A seat can change hands while a match runs -- a handover seats a bot for the early rounds and a person
    /// from the tenth (<c>docs/tabletop/app-roadmap.md</c>, stage 6) -- and the run stamp is one string for the
    /// whole run, so without this a fast-forwarded session's steps cannot be separated and a clone would learn
    /// the bot's opening as human play. It is null on every run recorded before this field existed, which is
    /// the one thing a reader must be able to tell apart from "a bot decided it": not knowing is not a claim.
    /// </remarks>
    public string? DecidedBy { get; init; }
}
