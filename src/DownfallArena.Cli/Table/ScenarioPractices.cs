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
/// <remarks>
/// A tutorial that is done is kept done on its account before its practice is let go of, whichever way it goes: a
/// reload or a Leave, another practice opened in its place (Next, Play again), or the idle sweep (#931, #1138). The
/// keeping is a store write, given by the caller as <c>keepDone</c> (the account, the scenario), and made outside the
/// gate every account's practices wait on.
/// </remarks>
internal sealed class ScenarioPractices(TableComposer composer, ScenarioStore store, TimeProvider clock) : IAsyncDisposable
{
    /// <summary>How many practices this process keeps at once: a bound on what memory they take, far above an onboarding's.</summary>
    public int Most { get; init; } = 200;

    /// <summary>How long a practice nobody reads is kept.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromHours(1);

    /// <summary>
    /// How long past <see cref="Idle" /> a tutorial done whose keeping fails is still kept for the next sweep: past it,
    /// it is let go of unkept, so a store that keeps failing for one account does not hold its practice forever.
    /// </summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromHours(1);

    /// <summary>Where its console lines go: the host's console unless a test reads them.</summary>
    public Action<string> Said { get; init; } = Console.WriteLine;

    /// <summary>The journal each practice is played on, kept in memory: a test gives one that cannot be played on (#931).</summary>
    internal Func<IDecisionJournal> Journals { get; init; } = () => new InMemoryDecisionJournal();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly InProcessAgentTurns _turns = new(clock);
    private readonly Lock _keeping = new();
    private Task _kept = Task.CompletedTask;
    private readonly Dictionary<string, Practice> _byAccount = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Practice> _byToken = new(StringComparer.Ordinal);

    /// <summary>A practice being played: whose, of which scenario, its playthrough, and when it was last read.</summary>
    internal sealed class Practice(string account, string scenario, ScenarioPlay play, DateTimeOffset seen)
    {
        public string Account { get; } = account;

        public string Scenario { get; } = scenario;

        public ScenarioPlay Play { get; } = play;

        public DateTimeOffset Seen { get; set; } = seen;

        /// <summary>Whether a sweep is keeping it done now, outside the gate: no other sweep writes it meanwhile. Changed under the gate.</summary>
        public bool Keeping { get; set; }

        /// <summary>When a sweep first failed to keep it done; none while no try failed. Changed under the gate.</summary>
        public DateTimeOffset? KeepFailedSince { get; set; }
    }

    /// <summary>Why a practice was not opened: a stable code, the words a player reads, and the status that says it.</summary>
    internal sealed record Refusal(string Error, string Message, int Status);

    /// <summary>The last practice, a tutorial done, could not be kept done on its account: nothing is opened (#1138).</summary>
    internal static readonly Refusal NotKept = new("Tutorial.Unavailable", "Your progress could not be saved just now. Try again in a moment.", 503);

    public int Count => _byToken.Count;

    /// <summary>The sweeps' keeping, run after the request that swept: a test waits on it, and so does disposing.</summary>
    internal Task Kept
    {
        get
        {
            lock (_keeping)
            {
                return _kept;
            }
        }
    }

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
    /// <param name="keepDone">Keeps a tutorial done on its account before the sweep lets it go (#1138).</param>
    public async Task<Practice?> CurrentAsync(string account, string scenario, Func<string, string, Task>? keepDone = null)
    {
        IReadOnlyList<Practice> held;
        Practice? current;
        await _gate.WaitAsync();
        try
        {
            held = await SweepAsync(keepDone is not null);
            current = _byAccount.TryGetValue(account, out var practice) && practice.Scenario == scenario ? practice : null;
        }
        finally
        {
            _gate.Release();
        }

        KeepSwept(held, keepDone);
        return current;
    }

    /// <summary>
    /// The account's practice of a scenario to go on with, as a reloaded page asks for it: the one it has, except a
    /// tutorial that is done, which is let go of so the next time it opens it starts from its cut. A tutorial whose
    /// table plays on no more without having ended its match (let go of, or it could not go on) is let go of too,
    /// with nothing kept on the account: it is not done, and there is nothing left to play (#912).
    /// </summary>
    /// <param name="keepDone">
    /// Keeps on the account (the first argument) that the tutorial (the second) is done, before a done tutorial is let
    /// go of. Whether it is done is read under this gate, once to decide and again where it is let go of, so a match
    /// that ends meanwhile is never let go of unkept (#931). The keeping itself is a store write, made outside the gate,
    /// which every account's practices wait on: if it fails, the practice is kept for the next try: a reload, a Leave,
    /// another practice opened or a sweep.
    /// </param>
    public async Task<Practice?> ResumeAsync(string account, string scenario, Func<string, string, Task>? keepDone = null)
    {
        var (practice, done, over) = await ReadAsync(account, scenario, sweep: true, keepDone);
        if (practice is null || practice.Play.Api.Practice?.Tutorial is null)
        {
            return practice;
        }

        if (!done && !over)
        {
            return practice;
        }

        await KeepAsync(practice, done, keepDone);
        await LetGoAsync(practice, done, keepDone);
        return null;
    }

    /// <summary>Lets go of the account's practice of a scenario, when it is the one it has: whether there was one.</summary>
    /// <param name="keepDone">As <see cref="ResumeAsync" />'s: run outside the gate before a done tutorial is let go of.</param>
    public async Task<bool> ForgetAsync(string account, string scenario, Func<string, string, Task>? keepDone = null)
    {
        var (practice, done, _) = await ReadAsync(account, scenario, sweep: false, keepDone);
        if (practice is null)
        {
            return false;
        }

        await KeepAsync(practice, done, keepDone);
        await LetGoAsync(practice, done, keepDone);
        return true;
    }

    /// <summary>
    /// The account's practice of the scenario, and, for a tutorial, whether it is done and whether its table plays on
    /// no more, read once under the gate. Over is read first: a table whose match ended has its board's outcome before
    /// it says it is over, so a tutorial read over and not done did not end its match.
    /// </summary>
    private async Task<(Practice? Practice, bool Done, bool Over)> ReadAsync(string account, string scenario, bool sweep, Func<string, string, Task>? keepDone)
    {
        IReadOnlyList<Practice> held = [];
        (Practice?, bool, bool) read;
        await _gate.WaitAsync();
        try
        {
            if (sweep)
            {
                held = await SweepAsync(keepDone is not null);
            }

            if (!_byAccount.TryGetValue(account, out var practice) || practice.Scenario != scenario)
            {
                read = (null, false, false);
            }
            else
            {
                var over = practice.Play.Session.IsOver;
                read = (practice, Done(practice), over);
            }
        }
        finally
        {
            _gate.Release();
        }

        KeepSwept(held, keepDone);
        return read;
    }

    /// <summary>Whether the practice is a tutorial that is done.</summary>
    private static bool Done(Practice practice) => practice.Play.Api.Practice?.Tutorial?.Done == true;

    private static async Task KeepAsync(Practice practice, bool done, Func<string, string, Task>? keepDone)
    {
        if (done && keepDone is not null)
        {
            await keepDone(practice.Account, practice.Scenario);
        }
    }

    /// <summary>
    /// Lets go of a practice read before, under the gate, unless the account opened another meanwhile. Whether it is
    /// done is read again there: a match that ended since the first read is kept done first, outside the gate, and
    /// the practice is let go of on that second read (#931).
    /// </summary>
    private async Task LetGoAsync(Practice practice, bool kept, Func<string, string, Task>? keepDone)
    {
        while (!await LetGoUnlessDoneSinceAsync(practice, kept || keepDone is null))
        {
            await keepDone!(practice.Account, practice.Scenario);
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

            if (!kept && Done(practice))
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
    /// <param name="keepDone">
    /// Keeps the last practice done on the account before it is replaced, when it is a tutorial that is done (Next, Play
    /// again, #1138). Read under the gate, kept outside it, then read again where it is replaced, as a Leave does. A
    /// store that cannot keep it is refused 503 <c>Tutorial.Unavailable</c>: nothing is opened, and the last practice
    /// is kept for the next try.
    /// </param>
    public async Task<(Practice? Opened, Refusal? Refused)> OpenAsync(string scenario, string account, Func<string, string, Task>? keepDone = null, CancellationToken cancellationToken = default)
    {
        Practice? kept = null;
        while (true)
        {
            var (opened, refused, unkept, held) = await OpenUnlessDoneUnkeptAsync(scenario, account, kept, keepDone is not null, cancellationToken);
            KeepSwept(held, keepDone);
            if (unkept is null)
            {
                return (opened, refused);
            }

            try
            {
                await keepDone!(unkept.Account, unkept.Scenario);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                Said($"  A tutorial done could not be kept on its account: {Hosting.HttpHost.OneLine(failure)}");
                return (null, NotKept);
            }

            kept = unkept;
        }
    }

    /// <summary>
    /// Opens the practice under the gate, unless the account's last one is a tutorial done that was not kept done
    /// yet: that one is answered instead, still the account's, for the caller to keep outside the gate.
    /// </summary>
    private async Task<(Practice? Opened, Refusal? Refused, Practice? Unkept, IReadOnlyList<Practice> Held)> OpenUnlessDoneUnkeptAsync(
        string scenario, string account, Practice? kept, bool keeps, CancellationToken cancellationToken)
    {
        IReadOnlyList<Practice> held = [];
        await _gate.WaitAsync(cancellationToken);
        try
        {
            held = await SweepAsync(keeps);
            if (await store.GetAsync(scenario, cancellationToken) is not { } document)
            {
                return (null, new Refusal("Scenario.NotFound", "This scenario does not exist, or was removed.", 404), null, held);
            }

            if (_byAccount.TryGetValue(account, out var previous))
            {
                if (keeps && !ReferenceEquals(previous, kept) && Done(previous))
                {
                    return (null, null, previous, held);
                }

                await StopAsync(previous);
            }
            else if (_byToken.Count >= Most)
            {
                return (null, new Refusal("Scenario.Busy", "Too many practices are being played on this host right now. Try again in a few minutes.", 503), null, held);
            }

            var (content, shared) = await composer.PlayedOnAsync(document.Content, cancellationToken);

            // The opponent answers in this process, whatever the host's tables do: a practice is in no index, so a
            // worker taking the queue's turns (--turns worker) would never find it, and the practice would wait forever.
            var stepped = shared with { Turns = _turns };
            if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))
            {
                return (null, new Refusal("Scenario.ContentGone", "The content this scenario was cut on is no longer kept at this host.", 409), null, held);
            }

            var cut = document.Scenario.Cut;
            var label = new PracticeLabel(scenario, document.Title ?? $"Round {cut.Round}", document.Goal, cut.Round)
            {
                Tutorial = document.Tutorial is { } lesson
                    ? new TutorialPlay(lesson, AssistedSeat.Of(document.Scenario, document.Seat, lesson), document.Seat, store.Series) { Next = NextOf(lesson.Order) }
                    : null,
            };
            ScenarioPlay play;
            try
            {
                play = await ScenarioPlay.StartAsync(content.Services, stepped, document, PracticeRun.NewToken(), label, cancellationToken, Journals());
            }
            catch (InvalidDataException failure)
            {
                Said($"  Scenario {scenario} could not be practised: {failure.Message}");
                return (null, new Refusal("Scenario.Diverged", "This scenario no longer plays on this version of the game.", 409), null, held);
            }

            var practice = new Practice(account, scenario, play, clock.GetUtcNow());
            _byAccount[account] = practice;
            _byToken[play.Token] = practice;
            return (practice, null, null, held);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The lesson that follows the one of this place in the series, as the seat's view names it (#980); none after the last.</summary>
    private TutorialPlay.Lesson? NextOf(int order) =>
        store.Tutorials.FirstOrDefault(one => one.Document.Tutorial!.Order > order) is { } next
            ? new TutorialPlay.Lesson(next.Document.Tutorial!.Title, $"/battle?scenario={next.Id}")
            : null;

    /// <summary>
    /// Lets go of the practices nobody read for <see cref="Idle" />, under the gate. When the caller keeps tutorials
    /// done, a tutorial done is held instead and marked as being kept, for <see cref="KeepSwept" /> to keep done
    /// outside the gate and let go of. One whose keeping has failed for <see cref="KeptFor" /> since its first failed
    /// try is let go of unkept, and said: it is always tried once (#1138).
    /// </summary>
    private async Task<IReadOnlyList<Practice>> SweepAsync(bool keeps)
    {
        var now = clock.GetUtcNow();
        var held = new List<Practice>();
        foreach (var idle in _byAccount.Values.Where(one => now - one.Seen >= Idle).ToList())
        {
            if (keeps && Done(idle))
            {
                if (idle.Keeping)
                {
                    continue;
                }

                if (idle.KeepFailedSince is not { } since || now - since < KeptFor)
                {
                    idle.Keeping = true;
                    held.Add(idle);
                    continue;
                }

                Said($"  A tutorial done was let go of unkept: its account's store could not keep it for {KeptFor.TotalMinutes:0} minutes.");
            }

            await StopAsync(idle);
        }

        return held;
    }

    /// <summary>
    /// Keeps done the tutorials a sweep held, after the request that swept, which waits on no other account's write
    /// (#1138). The keepings run one after the other, never two at once.
    /// </summary>
    private void KeepSwept(IReadOnlyList<Practice> held, Func<string, string, Task>? keepDone)
    {
        if (held.Count == 0 || keepDone is null)
        {
            return;
        }

        lock (_keeping)
        {
            _kept = _kept.ContinueWith(_ => KeepSweptAsync(held, keepDone), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>
    /// Keeps each held tutorial done, and lets it go once kept, unless it was read meanwhile or replaced. Whatever one
    /// account's write throws is said on the console and stops nothing else: the practice stays for the next sweep.
    /// </summary>
    private async Task KeepSweptAsync(IReadOnlyList<Practice> held, Func<string, string, Task> keepDone)
    {
        foreach (var practice in held)
        {
            var kept = true;
            try
            {
                await keepDone(practice.Account, practice.Scenario);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                Said($"  A tutorial done could not be kept on its account before it was let go of; the next sweep tries again: {Hosting.HttpHost.OneLine(failure)}");
                kept = false;
            }

            await SettleAsync(practice, kept);
        }
    }

    /// <summary>
    /// Under the gate, after a sweep's keeping: kept, the practice is let go of while it is still the account's and
    /// still idle; not kept, the first failure is noted, which <see cref="KeptFor" /> counts from.
    /// </summary>
    private async Task SettleAsync(Practice practice, bool kept)
    {
        await _gate.WaitAsync();
        try
        {
            practice.Keeping = false;
            if (!kept)
            {
                practice.KeepFailedSince ??= clock.GetUtcNow();
            }
            else if (_byAccount.TryGetValue(practice.Account, out var current) && ReferenceEquals(current, practice) && clock.GetUtcNow() - practice.Seen >= Idle)
            {
                await StopAsync(practice);
            }
        }
        finally
        {
            _gate.Release();
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
        // What a sweep keeps is said on the console when it fails; it never throws.
        await Kept;
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
