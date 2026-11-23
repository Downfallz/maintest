using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The match scenarios being practised at this host (docs/tabletop/scenarios-plan.md, S4): one a signed-in account,
/// kept in this process's memory over the services already built for the scenario's content, never recorded, never
/// indexed, never in the lobby or the ladder. Opening one replaces the account's last; one nobody reads for
/// <see cref="Idle" /> is let go of; past <see cref="Most" /> a new one is refused. A practice does not survive the
/// process: a restart or a deploy ends it, and the page offers to start the round again.
/// </summary>
internal sealed class ScenarioPractices(TableComposer composer, ScenarioStore store, TimeProvider clock) : IAsyncDisposable
{
    /// <summary>How many practices this process keeps at once: a bound on what memory they take, far above an onboarding's.</summary>
    public int Most { get; init; } = 200;

    /// <summary>How long a practice nobody reads is kept.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromHours(1);

    /// <summary>The journal each practice is played on, kept in memory: a test gives one that cannot be played on (#931).</summary>
    internal Func<IDecisionJournal> Journals { get; init; } = () => new InMemoryDecisionJournal();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly InProcessAgentTurns _turns = new(clock);
    private readonly Dictionary<string, Practice> _byAccount = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Practice> _byToken = new(StringComparer.Ordinal);

    /// <summary>A practice being played: whose, of which scenario, its playthrough, and when it was last read.</summary>
    internal sealed class Practice(string account, string scenario, ScenarioPlay play, DateTimeOffset seen)
    {
        public string Account { get; } = account;

        public string Scenario { get; } = scenario;

        public ScenarioPlay Play { get; } = play;

        public DateTimeOffset Seen { get; set; } = seen;
    }

    /// <summary>Why a practice was not opened: a stable code, the words a player reads, and the status that says it.</summary>
    internal sealed record Refusal(string Error, string Message, int Status);

    public int Count => _byToken.Count;

    /// <summary>The practice a seat token belongs to, read now; none for any other token.</summary>
    public Practice? ByToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_byToken.TryGetValue(token, out var practice))
        {
            return null;
        }

        practice.Seen = clock.GetUtcNow();
        return practice;
    }

    /// <summary>The account's practice of a scenario, when it is the one it has; none otherwise.</summary>
    public async Task<Practice?> CurrentAsync(string account, string scenario)
    {
        await _gate.WaitAsync();
        try
        {
            await SweepAsync();
            return _byAccount.TryGetValue(account, out var practice) && practice.Scenario == scenario ? practice : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The account's practice of a scenario to go on with, as a reloaded page asks for it: the one it has, except a
    /// tutorial that is done, which is let go of so the next time it opens it starts from its cut. A tutorial whose
    /// table plays on no more without having ended its match (let go of, or it could not go on) is let go of too,
    /// with nothing kept on the account: it is not done, and there is nothing left to play (#912).
    /// </summary>
    /// <param name="keepDone">
    /// Keeps on the account that the tutorial is done, before a done tutorial is let go of. Whether it is done is read
    /// under this gate, once to decide and again where it is let go of, so a match that ends meanwhile is never let go
    /// of unkept (#931). The keeping itself is a store write, made outside the gate, which every account's practices
    /// wait on: if it fails, the practice is kept for the next reload or Leave.
    /// </param>
    public async Task<Practice?> ResumeAsync(string account, string scenario, Func<Task>? keepDone = null)
    {
        var (practice, done, over) = await ReadAsync(account, scenario, sweep: true);
        if (practice is null || practice.Play.Api.Practice?.Tutorial is null)
        {
            return practice;
        }

        if (!done && !over)
        {
            return practice;
        }

        await KeepAsync(done, keepDone);
        await LetGoAsync(practice, done, keepDone);
        return null;
    }

    /// <summary>Lets go of the account's practice of a scenario, when it is the one it has: whether there was one.</summary>
    /// <param name="keepDone">As <see cref="ResumeAsync" />'s: run outside the gate before a done tutorial is let go of.</param>
    public async Task<bool> ForgetAsync(string account, string scenario, Func<Task>? keepDone = null)
    {
        var (practice, done, _) = await ReadAsync(account, scenario, sweep: false);
        if (practice is null)
        {
            return false;
        }

        await KeepAsync(done, keepDone);
        await LetGoAsync(practice, done, keepDone);
        return true;
    }

    /// <summary>
    /// The account's practice of the scenario, and, for a tutorial, whether it is done and whether its table plays on
    /// no more, read once under the gate. Over is read first: a table whose match ended has its board's outcome before
    /// it says it is over, so a tutorial read over and not done did not end its match.
    /// </summary>
    private async Task<(Practice? Practice, bool Done, bool Over)> ReadAsync(string account, string scenario, bool sweep)
    {
        await _gate.WaitAsync();
        try
        {
            if (sweep)
            {
                await SweepAsync();
            }

            if (!_byAccount.TryGetValue(account, out var practice) || practice.Scenario != scenario)
            {
                return (null, false, false);
            }

            var over = practice.Play.Session.IsOver;
            return (practice, practice.Play.Api.Practice?.Tutorial?.Done == true, over);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task KeepAsync(bool done, Func<Task>? keepDone)
    {
        if (done && keepDone is not null)
        {
            await keepDone();
        }
    }

    /// <summary>
    /// Lets go of a practice read before, under the gate, unless the account opened another meanwhile. Whether it is
    /// done is read again there: a match that ended since the first read is kept done first, outside the gate, and
    /// the practice is let go of on that second read (#931).
    /// </summary>
    private async Task LetGoAsync(Practice practice, bool kept, Func<Task>? keepDone)
    {
        while (!await LetGoUnlessDoneSinceAsync(practice, kept || keepDone is null))
        {
            await keepDone!();
            kept = true;
        }
    }

    /// <summary>Lets go of the practice under the gate, and says so; false, keeping it, when it became done unkept.</summary>
    private async Task<bool> LetGoUnlessDoneSinceAsync(Practice practice, bool kept)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_byAccount.TryGetValue(practice.Account, out var current) || !ReferenceEquals(current, practice))
            {
                return true;
            }

            if (!kept && practice.Play.Api.Practice?.Tutorial?.Done == true)
            {
                return false;
            }

            await StopAsync(practice);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// A new practice of the scenario for the account, from its cut, in place of whatever practice the account had:
    /// the practice, or why there is none.
    /// </summary>
    public async Task<(Practice? Opened, Refusal? Refused)> OpenAsync(string scenario, string account, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await SweepAsync();
            if (await store.GetAsync(scenario, cancellationToken) is not { } document)
            {
                return (null, new Refusal("Scenario.NotFound", "This scenario does not exist, or was removed.", 404));
            }

            if (_byAccount.TryGetValue(account, out var previous))
            {
                await StopAsync(previous);
            }
            else if (_byToken.Count >= Most)
            {
                return (null, new Refusal("Scenario.Busy", "Too many practices are being played on this host right now. Try again in a few minutes.", 503));
            }

            var (content, shared) = await composer.PlayedOnAsync(document.Content, cancellationToken);

            // The opponent answers in this process, whatever the host's tables do: a practice is in no index, so a
            // worker taking the queue's turns (--turns worker) would never find it, and the practice would wait forever.
            var stepped = shared with { Turns = _turns };
            if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))
            {
                return (null, new Refusal("Scenario.ContentGone", "The content this scenario was cut on is no longer kept at this host.", 409));
            }

            var cut = document.Scenario.Cut;
            var label = new PracticeLabel(scenario, document.Title ?? $"Round {cut.Round}", document.Goal, cut.Round)
            {
                Tutorial = document.Tutorial is { } lesson
                    ? new TutorialPlay(lesson, AssistedSeat.Of(document.Scenario, document.Seat, lesson), document.Seat, store.Series)
                    : null,
            };
            ScenarioPlay play;
            try
            {
                play = await ScenarioPlay.StartAsync(content.Services, stepped, document, PracticeRun.NewToken(), label, cancellationToken, Journals());
            }
            catch (InvalidDataException failure)
            {
                Console.WriteLine($"  Scenario {scenario} could not be practised: {failure.Message}");
                return (null, new Refusal("Scenario.Diverged", "This scenario no longer plays on this version of the game.", 409));
            }

            var practice = new Practice(account, scenario, play, clock.GetUtcNow());
            _byAccount[account] = practice;
            _byToken[play.Token] = practice;
            return (practice, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lets go of the practices nobody read for <see cref="Idle" />.</summary>
    private async Task SweepAsync()
    {
        var now = clock.GetUtcNow();
        foreach (var idle in _byAccount.Values.Where(one => now - one.Seen >= Idle).ToList())
        {
            await StopAsync(idle);
        }
    }

    private async Task StopAsync(Practice practice)
    {
        _byAccount.Remove(practice.Account);
        _byToken.TryRemove(practice.Play.Token, out _);
        await practice.Play.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var practice in _byAccount.Values.ToList())
            {
                await StopAsync(practice);
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
