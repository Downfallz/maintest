using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What <c>--record</c> names, as a store: a directory on this machine, or a blob container by its URL
/// (ADR 0080). The composition root decides the credential, which is why this is here and not in
/// Infrastructure: on Azure the container's managed identity, and locally whatever <c>az login</c> left; for
/// the storage emulator, the well-known development account, which is a published constant and not a secret.
/// </summary>
internal static class ArtifactStores
{
    private const string DevelopmentAccount = "devstoreaccount1";

    // Azurite's documented key, the same on every machine that runs the emulator.
    private const string DevelopmentKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public static async Task<IArtifactStore> OpenAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        if (!IsContainerUrl(target))
        {
            return new FileArtifactStore(target);
        }

        var uri = new Uri(target, UriKind.Absolute);
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobArtifactStore.OpenAsync(container, cancellationToken);
    }

    /// <summary>The container accounts are kept in, next to the recordings' one (ADR 0122).</summary>
    /// <summary>
    /// The journal the tables of a store are stepped over (ADR 0159), kept where the store keeps its runs: a
    /// table's <c>decisions.jsonl</c> is one file, written by the journal and listed by the store. A store this
    /// does not know has to be given its journal by whoever made it.
    /// </summary>
    public static IDecisionJournal JournalOf(IArtifactStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        return store switch
        {
            FileArtifactStore files => new FileDecisionJournal(files.RootDirectory, clock),
            BlobArtifactStore blobs => blobs.Journal(clock),
            _ => throw new ArgumentException($"A {store.GetType().Name} does not say where its tables' journals are kept; name the journal beside it.", nameof(store)),
        };
    }

    public const string AccountsContainer = "accounts";

    /// <summary>
    /// Where accounts are kept beside what <c>--record</c> names: a directory <c>accounts</c> next to the runs'
    /// directory, or a container <c>accounts</c> in the same storage account, reached with the same credential.
    /// Never inside the recordings: the session store reads everything at the top of it as a session.
    /// </summary>
    public static async Task<IAccountStore> OpenAccountsAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!IsContainerUrl(target))
        {
            var runs = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return new FileAccountStore(Path.Combine(Path.GetDirectoryName(runs) ?? runs, AccountsContainer));
        }

        var uri = AccountsUri(new Uri(target, UriKind.Absolute));
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobAccountStore.OpenAsync(container, cancellationToken);
    }

    /// <summary>Where finished matches with an account in them are kept (ADR 0122, stage 5): beside the accounts, under <c>results/</c>.</summary>
    public static async Task<IMatchResults> OpenResultsAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!IsContainerUrl(target))
        {
            var runs = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return new FileMatchResults(Path.Combine(Path.GetDirectoryName(runs) ?? runs, AccountsContainer));
        }

        var uri = AccountsUri(new Uri(target, UriKind.Absolute));
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobMatchResults.OpenAsync(container, cancellationToken);
    }

    /// <summary>Where an account's devices are kept (friends plan, stage 1): beside the accounts, under <c>devices/</c>.</summary>
    public static async Task<IDeviceStore> OpenDevicesAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!IsContainerUrl(target))
        {
            var runs = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return new FileDeviceStore(Path.Combine(Path.GetDirectoryName(runs) ?? runs, AccountsContainer));
        }

        var uri = AccountsUri(new Uri(target, UriKind.Absolute));
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobDeviceStore.OpenAsync(container, cancellationToken);
    }

    /// <summary>Where each account's game settings are kept (ADR 0140): beside the accounts, under <c>preferences/</c>.</summary>
    public static async Task<IPreferenceStore> OpenPreferencesAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!IsContainerUrl(target))
        {
            var runs = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return new FilePreferenceStore(Path.Combine(Path.GetDirectoryName(runs) ?? runs, AccountsContainer));
        }

        var uri = AccountsUri(new Uri(target, UriKind.Absolute));
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobPreferenceStore.OpenAsync(container, cancellationToken);
    }

    /// <summary>Where each account's friendships are kept (ADR 0129): beside the accounts, under <c>friends/</c>.</summary>
    public static async Task<IFriendStore> OpenFriendsAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!IsContainerUrl(target))
        {
            var runs = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return new FileFriendStore(Path.Combine(Path.GetDirectoryName(runs) ?? runs, AccountsContainer));
        }

        var uri = AccountsUri(new Uri(target, UriKind.Absolute));
        var container = IsEmulator(uri)
            ? new BlobContainerClient(uri, new StorageSharedKeyCredential(DevelopmentAccount, DevelopmentKey))
            : new BlobContainerClient(uri, new DefaultAzureCredential());
        return await BlobFriendStore.OpenAsync(container, cancellationToken);
    }

    /// <summary>The recordings' container URL with its container swapped for the accounts' one.</summary>
    public static Uri AccountsUri(Uri recordings)
    {
        ArgumentNullException.ThrowIfNull(recordings);
        var path = recordings.AbsolutePath.TrimEnd('/');
        var cut = path.LastIndexOf('/');
        return new UriBuilder(recordings) { Path = $"{path[..cut]}/{AccountsContainer}", Query = string.Empty }.Uri;
    }

    /// <summary>Whether a record target is a container's URL rather than a directory.</summary>
    public static bool IsContainerUrl(string target) =>
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    /// <summary>The emulator: the development account on a loopback host, the one URL a shared key belongs to.</summary>
    public static bool IsEmulator(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsLoopback && uri.AbsolutePath.StartsWith($"/{DevelopmentAccount}/", StringComparison.Ordinal);
    }
}
