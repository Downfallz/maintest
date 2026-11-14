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

    private readonly SemaphoreSlim _gate = new(1, 1);
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

            var (content, stepped) = await composer.PlayedOnAsync(document.Content, cancellationToken);
            if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))
            {
                return (null, new Refusal("Scenario.ContentGone", "The content this scenario was cut on is no longer kept at this host.", 409));
            }

            var cut = document.Scenario.Cut;
            var label = new PracticeLabel(scenario, document.Title ?? $"Round {cut.Round}", document.Goal, cut.Round);
            ScenarioPlay play;
            try
            {
                play = await ScenarioPlay.StartAsync(content.Services, stepped, document, PracticeRun.NewToken(), label, cancellationToken);
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
