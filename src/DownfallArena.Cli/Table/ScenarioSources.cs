using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Whether the games a kept scenario was cut from are over (#833). A scenario cut before #736 from a table still played
/// holds the other seat's face-down choices of its round, which its opponent follows: it is not opened, listed under
/// Practice or reproduced from until that game is over. A missing record passes, as every guard of #814 lets it: a
/// repository scenario's source run names a table of another host. A record that cannot be parsed refuses, since
/// nothing then shows the game is over.
/// </summary>
internal static class ScenarioSources
{
    public static readonly DomainError UnderWay = new("Scenario.UnderWay", "The game this scenario was cut from is still being played: the scenario is opened, listed and reproduced from once that game is over, so nothing a seat holds face down there reaches it.");

    public static readonly DomainError SourceUnread = new("Scenario.SourceUnread", "The record of the game this scenario was cut from cannot be read: nothing shows that game is over, so the scenario is not opened, listed or reproduced from.");

    /// <summary>Why the scenario is held back, or none when every table it was cut from is over or not this host's.</summary>
    public static async Task<DomainError?> HeldBackAsync(StoredSessions stored, ScenarioDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(document);
        foreach (var source in new[] { document.Session, document.Scenario.Source.Run }.OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (!TableKeys.IsTableId(source))
            {
                continue;
            }

            var read = await stored.ReadRecordAsync(source, cancellationToken);
            if (read.Unparseable)
            {
                return SourceUnread;
            }

            if (read.Record is { UnderWay: true })
            {
                return UnderWay;
            }
        }

        return null;
    }
}
