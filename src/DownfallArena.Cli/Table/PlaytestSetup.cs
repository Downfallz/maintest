using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a session is playing and who is playing it: everything <see cref="PlaytestRun" /> needs to stamp a run
/// and to encode a board, and nothing about where the files go.
/// </summary>
/// <remarks>
/// The rule set is the table's own rather than the engine default, and the feature schema is built from it: a
/// dataset whose schema describes a different rule set than the match played is a dataset that trains on a
/// mislabelled board (<c>docs/tabletop/plan.md</c>).
/// </remarks>
/// <param name="Player1Agent">
/// What the run stamp calls each seat: an agent's own name for a bot, and <c>human</c> or
/// <c>human:&lt;initials&gt;</c> for a person, so <c>compare-stamps</c> reports the agents axis between two
/// sessions played by different people rather than calling them the same player.
/// </param>
internal sealed record PlaytestSetup(
    IGameResources Resources,
    RuleSet Rules,
    int Seed,
    string Player1Agent,
    string Player2Agent);
