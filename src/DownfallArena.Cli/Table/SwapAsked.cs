using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a pilot's request turned out to be: a swap to make, or the refusal that says why there is none.
/// </summary>
/// <remarks>
/// One value rather than a bag of <c>out</c> parameters, for the reason this whole path converged on: a
/// reading is a reading, and splitting one across several places to be filled in is how the pieces stop
/// agreeing. <see cref="Refusal" /> and the rest are exclusive — when there is a refusal there is nothing to
/// read, and when there is not, all three are the request.
/// </remarks>
internal sealed record SwapAsked(StudioResponse? Refusal, PlayerSlot Slot = default, string Agent = "", int Round = 0)
{
    public static SwapAsked No(StudioResponse refusal) => new(refusal);
}
