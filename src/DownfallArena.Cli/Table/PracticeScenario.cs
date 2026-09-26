using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>Recipes are catalogue-specific examples, not new rules or hand-edited match snapshots.</summary>
internal sealed record PracticeScenario(string Id, string Name, string Goal, int Round, PlayerOptionsKind Question)
{
    public const int Seed = 17;

    public static RuleSet Rules { get; } = RuleSet.Create(3, 2, 2, 10, 2.0);

    public static IReadOnlyList<PracticeScenario> All { get; } =
    [
        new("targeting", "Choose your target", "Compare Lightning Bolt on each enemy. Select a target, read the preview, then confirm and follow the resolution.", 1, PlayerOptionsKind.Target),
        new("multiclass", "Build a multiclass creature", "Round 3: your first creature already owns Brute. Compare another level-1 package with its level-2 upgrades in the talent atlas.", 3, PlayerOptionsKind.Evolution),
        new("stun", "Interrupt an action", "Cast Tranquilizer Dart before the enemy acts. Follow its skipped action, then inspect stun and the immunity left after it expires.", 5, PlayerOptionsKind.Target),
        new("resolution", "Read a layered resolution", "Choose the targets of Toxic Waves. Follow damage, bleed and caster effects with Before / After and Next.", 5, PlayerOptionsKind.Target),
    ];
}
