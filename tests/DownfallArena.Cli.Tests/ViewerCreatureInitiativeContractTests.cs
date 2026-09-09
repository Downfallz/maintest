using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// A creature card shows "I 4 of 8" when a debuff pulls a creature off its base initiative and "I 8" when it
/// does not, which means reading two fields the engine serializes on a <see cref="CreatureSnapshot"/>. Renaming
/// either would leave the tag reading a base of <c>undefined</c> for good — the viewer treats a missing base as
/// "whole", by design, so older traces still render — and nothing would fail. This holds the two together
/// against the real <c>viewer/index.html</c>.
/// </summary>
public sealed class ViewerCreatureInitiativeContractTests
{
    private static readonly string Viewer = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "viewer", "index.html"));

    [Theory]
    [InlineData(nameof(CreatureSnapshot.BaseInitiative))]
    [InlineData(nameof(CreatureSnapshot.CurrentInitiative))]
    public void The_initiative_tag_reads_a_creature_by_the_names_the_engine_writes(string property)
    {
        var written = ArtifactJson.DocumentOptions.PropertyNamingPolicy.ShouldNotBeNull().ConvertName(property);

        BodyOf("initiativeTag").ShouldContain(written);
    }

    private static string BodyOf(string function)
    {
        var start = Viewer.IndexOf($"function {function}(", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"the viewer no longer declares {function}");
        var open = Viewer.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < Viewer.Length; index++)
        {
            depth += Viewer[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return Viewer[(open + 1)..index];
            }
        }

        throw new InvalidDataException($"The body of {function} is not closed in the viewer.");
    }
}
