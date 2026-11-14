using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A table's match rebuilt from its record and journal on the content and rules it began on, under a match id of its
/// own and forgotten once read (docs/tabletop/scenarios-plan.md, S5b): what the operator's diagnosis checks and reads,
/// without touching the match the table is being played on.
/// </summary>
internal static class TableRebuild
{
    /// <summary>The rebuilt match: how many entries it took, the answers it holds, and each seat's board, hidden choices and all.</summary>
    public sealed record Rebuilt(int Entries, HeldAnswers Held, PlayerBoardState Player1, PlayerBoardState Player2);

    /// <summary>The match rebuilt aside, or the words that say why it did not rebuild.</summary>
    public static async Task<(Rebuilt? Rebuilt, string? Refused)> AsideAsync(TableRecord record, IReadOnlyList<IJournalEntry> entries, TableContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(content);
        if (record.Rules is not { } recorded)
        {
            return (null, "The table was recorded before its rules were kept with it (ADR 0163): it cannot be rebuilt on the rules it began on.");
        }

        var services = content.Services;
        var rules = recorded.ToRuleSet();
        var resources = services.GetRequiredService<IGameResources>();
        var aside = new JournaledMatch(record.Id, MatchId.New(), rules, record.Seed, [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)]);
        var stepper = services.GetRequiredService<MatchStepper>();
        try
        {
            var built = await services.GetRequiredService<MatchReplay>().RebuildAsync(aside, entries, cancellationToken);
            if (built.IsFailure)
            {
                return (null, built.Error.Message);
            }

            var first = await stepper.SeatAsync(aside.MatchId, PlayerSlot.Player1, built.Value, cancellationToken);
            var second = await stepper.SeatAsync(aside.MatchId, PlayerSlot.Player2, built.Value, cancellationToken);
            return first.IsFailure || second.IsFailure
                ? (null, (first.IsFailure ? first.Error : second.Error).Message)
                : (new Rebuilt(entries.Count, built.Value, first.Value.Board, second.Value.Board), null);
        }
        finally
        {
            await services.GetRequiredService<IMatchRepository>().ForgetAsync(aside.MatchId, CancellationToken.None);
            foreach (var memory in services.GetServices<IMatchMemory>())
            {
                memory.Forget(aside.MatchId);
            }
        }
    }
}
