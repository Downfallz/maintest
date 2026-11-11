using System.Globalization;
using Azure.Data.Tables;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Results in Azure Table Storage (ADR 0168), in the account that holds the index of the tables: every result in
/// the partition <c>match</c>, which the leaderboard reads whole, and again in the partition of each account that
/// held a seat, which that player's pages read alone. A row key sorts by when the match finished, then by its id,
/// so a partition reads oldest first. Each entity holds the result in the shape the blobs kept it in.
/// </summary>
public sealed class StorageMatchResults : IMatchResults
{
    /// <summary>The name of the table results are kept in.</summary>
    public const string Name = "matchresults";

    private const string Matches = "match";
    private const string AccountPrefix = "account-";
    private const string Meta = "meta";
    private const string Copied = "copied-blobs";
    private const string Document = "Result";

    private readonly TableClient _table;

    private StorageMatchResults(TableClient table) => _table = table;

    /// <summary>
    /// The results in this table, made if it is not there yet. The first opening copies the results kept before
    /// this table into it, then marks the copy done: two processes opening at once both copy, which upserts the
    /// same entities twice.
    /// </summary>
    /// <param name="before">Where results were kept before this table, read once (ADR 0168); none to copy nothing.</param>
    public static async Task<StorageMatchResults> OpenAsync(TableClient table, IMatchResults? before = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        await table.CreateIfNotExistsAsync(cancellationToken);
        var results = new StorageMatchResults(table);
        if (before is not null && !(await table.GetEntityIfExistsAsync<TableEntity>(Meta, Copied, cancellationToken: cancellationToken)).HasValue)
        {
            foreach (var result in await before.ListAsync(cancellationToken))
            {
                await results.RecordAsync(result, cancellationToken);
            }

            await table.UpsertEntityAsync(new TableEntity(Meta, Copied), TableUpdateMode.Replace, cancellationToken);
        }

        return results;
    }

    public async Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var row = RowOf(result);
        var document = ResultDocuments.Write(result);

        // The players' partitions first: a result the leaderboard counts is then always on its players' pages.
        foreach (var account in result.Seats.Select(seat => seat.Account).OfType<AccountId>().Distinct())
        {
            await _table.UpsertEntityAsync(new TableEntity(PartitionOf(account), row) { [Document] = document }, TableUpdateMode.Replace, cancellationToken);
        }

        await _table.UpsertEntityAsync(new TableEntity(Matches, row) { [Document] = document }, TableUpdateMode.Replace, cancellationToken);
    }

    public Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default) =>
        PartitionAsync(Matches, cancellationToken);

    public Task<IReadOnlyList<MatchResult>> ListForAsync(AccountId account, CancellationToken cancellationToken = default) =>
        PartitionAsync(PartitionOf(account), cancellationToken);

    private async Task<IReadOnlyList<MatchResult>> PartitionAsync(string partition, CancellationToken cancellationToken)
    {
        var results = new List<MatchResult>();
        await foreach (var entity in _table.QueryAsync<TableEntity>(
            TableClient.CreateQueryFilter($"PartitionKey eq {partition}"),
            select: [Document],
            cancellationToken: cancellationToken))
        {
            results.Add(ResultDocuments.Read(entity.GetString(Document)));
        }

        // Already in row-key order, which is this one; sorted again so a row written under another clock's
        // rounding cannot put two results of the same instant out of the order every store lists them in.
        return [.. results.OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal)];
    }

    private static string PartitionOf(AccountId account) => AccountPrefix + account.Value;

    /// <summary>When the match finished, as ticks wide enough to sort as text, then the match's own id.</summary>
    private static string RowOf(MatchResult result) =>
        $"{result.FinishedAt.UtcTicks.ToString("D19", CultureInfo.InvariantCulture)}-{ResultDocuments.IdOf(result):D}";
}
