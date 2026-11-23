using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>An account's notice log as one blob of the accounts' container (ADR 0201), written only over the tag it was read at.</summary>
public sealed class BlobNoticeStore : INoticeStore
{
    private readonly BlobContainerClient _container;

    private BlobNoticeStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobNoticeStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobNoticeStore(container);
    }

    public async Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalBlob.ReadAsync(Blob(account), cancellationToken);
        return new NoticeLogRead(text is null ? [] : NoticeDocuments.Read(text), version);
    }

    public Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return ConditionalBlob.WriteAsync(Blob(account), entries.Count == 0 ? null : NoticeDocuments.Write(entries), version, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountId>> AccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = new List<AccountId>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, NoticeDocuments.Prefix, cancellationToken))
        {
            if (NoticeDocuments.AccountOf(item.Name) is { } account)
            {
                accounts.Add(account);
            }
        }

        return accounts;
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(NoticeDocuments.PathOf(account), cancellationToken: cancellationToken);

    private BlobClient Blob(AccountId account) => _container.GetBlobClient(NoticeDocuments.PathOf(account));
}
