using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What <see cref="TableHost.ComposeAsync(IServiceProvider, CliOptions, Domain.Matches.RuleSet, TableHost.Pushing?, string, CancellationToken)" />
/// composed: the <c>table</c> command's parts, wired as they serve, and what they hold, let go of together.
/// </summary>
internal sealed class ComposedHost(ComposedHost.Kept kept) : IAsyncDisposable
{
    public required TableServer Server { get; init; }

    public required TableRegistry Registry { get; init; }

    public required TableComposer Composer { get; init; }

    public required AdminApi Admin { get; init; }

    /// <summary>The store the sessions are recorded into; none for a host told <c>--no-record</c>.</summary>
    public IArtifactStore? Store { get; init; }

    public StoredSessions? Stored { get; init; }

    /// <summary>The players' sign-in (ADR 0122); none with accounts off.</summary>
    public AccountApi? Accounts { get; init; }

    /// <summary>Where the accounts are kept; none with accounts off.</summary>
    public IAccountStore? AccountStore { get; init; }

    public FriendsApi? Friends { get; init; }

    /// <summary>The one door every notice to an account goes through (ADR 0201); none with accounts off.</summary>
    public AccountNotifier? Notifier { get; init; }

    /// <summary>"Your turn" to a seat's devices (ADR 0118); none without push keys.</summary>
    public PushNotices? Push { get; init; }

    public required OperatorGate Gate { get; init; }

    public IChangeRelay? Relay { get; init; }

    /// <summary>What the lobbies and an account's pages read on (ADR 0164).</summary>
    public required LobbyChanges Changes { get; init; }

    public required string Described { get; init; }

    public TurnsMode Mode { get; init; }

    public IWorkChannels? Channels { get; init; }

    /// <summary>The bots' turns queue (ADR 0159); none when they are taken in this process.</summary>
    public IWorkChannel? Turns { get; init; }

    /// <summary>The notices queue (ADR 0203); none on a host with no queue or no push keys.</summary>
    public IWorkChannel? Notices { get; init; }

    public ValueTask DisposeAsync() => kept.DisposeAsync();

    /// <summary>What the composition holds, let go of in the reverse of the order it was made in.</summary>
    internal sealed class Kept : IAsyncDisposable
    {
        private readonly List<object> _held = [];

        public T Add<T>(T held)
        {
            if (held is IDisposable or IAsyncDisposable)
            {
                _held.Add(held);
            }

            return held;
        }

        public async ValueTask DisposeAsync()
        {
            for (var at = _held.Count - 1; at >= 0; at--)
            {
                switch (_held[at])
                {
                    case IAsyncDisposable later:
                        await later.DisposeAsync();
                        break;
                    case IDisposable now:
                        now.Dispose();
                        break;
                }
            }

            _held.Clear();
        }
    }
}
