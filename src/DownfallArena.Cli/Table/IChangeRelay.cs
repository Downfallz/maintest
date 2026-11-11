using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Where every process says what moved, for the pages and the other processes to hear (ADR 0165): a service that
/// holds the connections, so a page stays told whichever replica answers it, and while none is awake. Like the
/// streams it replaces (ADR 0119, 0164), it says only that something moved, never what: a page reads its own routes,
/// and the host still decides what it may read.
/// </summary>
internal interface IChangeRelay
{
    /// <summary>This process, as the messages it sends to the other processes name it: what it does not answer.</summary>
    string Origin { get; }

    /// <summary>A table moved: its seats' pages are told, and every process that may hold it.</summary>
    void Table(string table);

    /// <summary>The public lists moved: every lobby in view is told.</summary>
    void Lobby();

    /// <summary>One account's own lists moved: that player's lobbies are told.</summary>
    void Account(AccountId account);

    /// <summary>Where a seat's page connects to hear its table move, for a while.</summary>
    Task<Uri> SeatAsync(string table, CancellationToken cancellationToken = default);

    /// <summary>Where a lobby connects to hear the public lists move, and the signed-in player's own.</summary>
    Task<Uri> LobbyAsync(AccountId? account, CancellationToken cancellationToken = default);

    /// <summary>Where a process connects to hear the tables another process moved.</summary>
    Task<Uri> HostsAsync(CancellationToken cancellationToken = default);

    /// <summary>What is still on its way: what a process about to exit waits for.</summary>
    Task SentAsync();
}
