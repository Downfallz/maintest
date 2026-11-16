using System.Text;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The lobby's stream (ADR 0164): <c>ready</c>, then a <c>changed</c> for the public lists and one for the signed-in
/// player's own lists, and nothing of another player's.
/// </summary>
public sealed class LobbyChangesTests
{
    private static readonly AccountId Mark = AccountId.Parse("acct_markmarkmarkmark");
    private static readonly AccountId Bastien = AccountId.Parse("acct_bastienbastienb2");

    [Fact]
    public async Task The_stream_says_ready_then_each_change_of_the_public_lists_and_of_the_player_s_own()
    {
        var changes = new LobbyChanges();

        var written = await StreamedAsync(changes, Mark, "account 1", () =>
        {
            changes.Everyone();
            changes.Account(Mark);
        });

        written.ShouldStartWith("event: ready\n");
        written.ShouldContain("event: changed\ndata: lobby 1\n\n");
        written.ShouldContain("event: changed\ndata: account 1\n\n");
    }

    [Fact]
    public async Task A_player_s_stream_says_nothing_of_another_player_s_lists()
    {
        var changes = new LobbyChanges();

        var written = await StreamedAsync(changes, Mark, "lobby 1", () =>
        {
            changes.Account(Bastien);
            changes.Everyone();
        });

        written.ShouldNotContain("account");
    }

    [Fact]
    public async Task A_stream_nobody_signed_in_to_says_the_public_lists_only()
    {
        var changes = new LobbyChanges();

        var written = await StreamedAsync(changes, null, "lobby 1", () =>
        {
            changes.Account(Mark);
            changes.Everyone();
        });

        written.ShouldNotContain("account");
        written.ShouldContain("data: lobby 1");
    }

    [Fact]
    public async Task A_quiet_stream_says_it_is_still_there()
    {
        var changes = new LobbyChanges { KeepAlive = TimeSpan.FromMilliseconds(20) };

        var written = await StreamedAsync(changes, Mark, ": keep-alive", () => { });

        written.ShouldContain(": keep-alive\n\n");
    }

    // Runs the stream until what it wrote holds `until`, after `act` once it said ready; then the browser goes.
    private static async Task<string> StreamedAsync(LobbyChanges changes, AccountId? account, string until, Action act)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stream = new Written();
        var streaming = changes.StreamAsync(stream, account, stopping.Token);
        await stream.HoldsAsync("event: ready", TestContext.Current.CancellationToken);
        act();
        await stream.HoldsAsync(until, TestContext.Current.CancellationToken);
        await stopping.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => streaming);
        return stream.Text;
    }

    /// <summary>A stream that keeps what is written to it, read safely while it is still being written.</summary>
    private sealed class Written : MemoryStream
    {
        private readonly Lock _gate = new();

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return Encoding.UTF8.GetString(ToArray());
                }
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                Write(buffer.Span);
            }

            return ValueTask.CompletedTask;
        }

        public async Task HoldsAsync(string text, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!Text.Contains(text, StringComparison.Ordinal))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"The stream never said '{text}': {Text}");
                }

                await Task.Delay(5, cancellationToken);
            }
        }
    }
}
