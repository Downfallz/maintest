using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Accounts as blobs of their own container (ADR 0122), not the recordings' one: a store of sessions reads every
/// top-level prefix as a session. An index entry is uploaded with <c>If-None-Match: *</c>, so the service, not this
/// host, refuses the second of two registrations of one name; a conditional removal deletes only the version it
/// read (<c>If-Match</c>), so a name taken in between is left alone.
/// </summary>
public sealed class BlobAccountStore : IAccountStore
{
    private readonly BlobContainerClient _container;

    private BlobAccountStore(BlobContainerClient container)
    {
        _container = container;
    }

    public Uri Uri => _container.Uri;

    public static async Task<BlobAccountStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobAccountStore(container);
    }

    public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.AccountPath(id), cancellationToken) is { } read ? AccountDocuments.ReadAccount(read.Json) : null;

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        var accounts = new List<Account>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, AccountDocuments.AccountsPrefix, cancellationToken))
        {
            // An account deleted between the listing and its read is not listed.
            if (await ReadAsync(item.Name, cancellationToken) is { } read)
            {
                accounts.Add(AccountDocuments.ReadAccount(read.Json));
            }
        }

        return accounts;
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await _container.GetBlobClient(AccountDocuments.AccountPath(account.Id))
            .UploadAsync(BinaryData.FromString(AccountDocuments.Write(account)), Options(conditions: null), cancellationToken);
    }

    public async Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) =>
        (await _container.DeleteBlobIfExistsAsync(AccountDocuments.AccountPath(id), cancellationToken: cancellationToken)).Value;

    public async Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.SubjectPath(identity), cancellationToken) is { } read ? AccountDocuments.ReadSubject(read.Json) : null;

    public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        CreateAsync(AccountDocuments.SubjectPath(identity), AccountDocuments.WriteSubject(account), cancellationToken);

    public async Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default)
    {
        var path = AccountDocuments.SubjectPath(identity);
        return await ReadAsync(path, cancellationToken) is { } read
            && AccountDocuments.ReadSubject(read.Json) == account
            && await DeleteIfUnchangedAsync(path, read.ETag, cancellationToken);
    }

    public async Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.NamePath(username), cancellationToken) is { } read ? AccountDocuments.ReadName(read.Json) : null;

    public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return CreateAsync(AccountDocuments.NamePath(reservation.Username), AccountDocuments.Write(reservation), cancellationToken);
    }

    public async Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        var path = AccountDocuments.NamePath(reservation.Username);
        return await ReadAsync(path, cancellationToken) is { } read
            && AccountDocuments.ReadName(read.Json).Account == reservation.Account
            && await DeleteIfUnchangedAsync(path, read.ETag, cancellationToken);
    }

    /// <summary>Created once with <c>If-None-Match: *</c>, so two replicas starting together agree on one key.</summary>
    public async Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default)
    {
        if (await ReadAsync(AccountDocuments.SessionKeyPath, cancellationToken) is not { } read)
        {
            await CreateAsync(AccountDocuments.SessionKeyPath, AccountDocuments.NewSessionKey(), cancellationToken);
            read = (await ReadAsync(AccountDocuments.SessionKeyPath, cancellationToken))!.Value;
        }

        return AccountDocuments.ReadSessionKey(read.Json);
    }

    private static BlobUploadOptions Options(BlobRequestConditions? conditions) => new()
    {
        Conditions = conditions,
        HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
    };

    private async Task<(string Json, ETag ETag)?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _container.GetBlobClient(path).DownloadContentAsync(cancellationToken);
            return (download.Value.Content.ToString(), download.Value.Details.ETag);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async Task<bool> CreateAsync(string path, string json, CancellationToken cancellationToken)
    {
        try
        {
            await _container.GetBlobClient(path).UploadAsync(
                BinaryData.FromString(json),
                Options(new BlobRequestConditions { IfNoneMatch = ETag.All }),
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            return false;
        }
    }

    private async Task<bool> DeleteIfUnchangedAsync(string path, ETag etag, CancellationToken cancellationToken)
    {
        try
        {
            await _container.GetBlobClient(path).DeleteAsync(conditions: new BlobRequestConditions { IfMatch = etag }, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return false;
        }
    }
}
