using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Every decision a table takes, written as it is taken, and read back to rebuild the table after the host
/// that played it is gone (ADR 0091). A match lives in memory; this is what makes that survivable.
/// </summary>
/// <remarks>
/// <para>
/// Two modes, one object. Opened fresh, it writes: every decision whoever is seated makes, and every swap the
/// pilot asks for, as one line each in the order the match took them. Opened over what an earlier host wrote,
/// it replays: while lines remain, a seat asked a question is answered from the next line instead of by
/// whoever is seated, a swap line is applied as it is reached, and a line that does not answer the question
/// being asked is a replay that has diverged -- the content or the engine changed under the record -- and is
/// refused rather than guessed at. Once the lines run out it writes again, and the people and bots at the
/// table carry on from where the earlier host left them.
/// </para>
/// <para>
/// A line is matched to the seat it belongs to rather than to its place in the file: the two seats pick their
/// packages at once (ADR 0092), so which of them answered first is not a fact the record can promise to
/// repeat. Within a seat the order is the order of its questions, and a line that does not answer the
/// question its seat is asked is a divergence. A swap line is applied when the next decision after it is
/// answered, whichever seat that is: a swap names a round the match has not reached, so where exactly it is
/// applied before that round makes no difference to where it lands.
/// </para>
/// </remarks>
internal sealed class DecisionJournal
{
    public const string File = "decisions.jsonl";

    private readonly IArtifactWriter _writer;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _recorded;
    private readonly TaskCompletionSource _exhausted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<PlayerSlot, string, int, bool>? _swapping;
    private long _seq;
    private Task _writing = Task.CompletedTask;

    /// <param name="recorded">What an earlier host wrote, to replay before anybody is asked anything; empty for a table opening now.</param>
    public DecisionJournal(IArtifactWriter writer, TimeProvider clock, IReadOnlyList<JournalEntry>? recorded = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(clock);
        _writer = writer;
        _clock = clock;
        _recorded = [.. recorded ?? []];
        _seq = recorded?.Count > 0 ? recorded[^1].Seq : 0;
        if (_recorded.Count == 0)
        {
            _exhausted.TrySetResult();
        }
    }

    /// <summary>Whether lines remain to replay: the table is still catching up with its own record.</summary>
    public bool Replaying
    {
        get
        {
            lock (_gate)
            {
                return _recorded.Count > 0;
            }
        }
    }

    /// <summary>Completes once the last recorded line has been handed to the match, which is when the table is live again.</summary>
    public Task Exhausted => _exhausted.Task;

    /// <summary>How a swap line is applied when replay reaches it: the pilot's own seating, handed in by whoever composes the table.</summary>
    public void SwapsThrough(Func<PlayerSlot, string, int, bool> swapping) => _swapping = swapping;

    /// <summary>Writes down a swap the pilot asked for, so a rebuilt table asks for it at the same point.</summary>
    public void SwapAsked(PlayerSlot slot, string to, int atRound)
    {
        lock (_gate)
        {
            if (_recorded.Count > 0)
            {
                // Replaying: the swap being applied is the one on the line, already written.
                return;
            }

            Append(JournalEntry.Swap(++_seq, _clock.GetUtcNow(), slot, to, atRound));
        }
    }

    /// <summary>Whatever is still on its way into the file, so closing a session can wait for it.</summary>
    public Task Written
    {
        get
        {
            lock (_gate)
            {
                return _writing;
            }
        }
    }

    /// <summary>
    /// The occupant of a seat, as the driver should play it: answered from the record while there is one,
    /// written down once there is not.
    /// </summary>
    public IPlayerAgent Around(IPlayerAgent inner, PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new Journaled(this, inner, slot);
    }

    /// <summary>
    /// The recorded answer to the question being asked, or <c>null</c> when the record is spent and whoever is
    /// seated answers. Swap lines ahead of the answer are applied first, where they were asked.
    /// </summary>
    private JournalEntry? Recorded(PlayerSlot slot, PlayerOptionsKind kind, CreatureId? creature)
    {
        lock (_gate)
        {
            if (_recorded.Count == 0)
            {
                return null;
            }

            var index = _recorded.FindIndex(entry => !entry.IsSwap && entry.PlayerSlot == slot);
            if (index < 0)
            {
                // Nothing left for this seat while lines remain for the other: the other seat is asked next,
                // or the record no longer fits and the rebuild times out waiting for it to be spent.
                return null;
            }

            var next = _recorded[index];
            if (!next.Answers(slot, kind, creature))
            {
                _recorded.Clear();
                _exhausted.TrySetException(new InvalidOperationException($"The record has diverged: line {next.Seq} is a {next.Kind} by {next.Slot}, and the match asks {TableSeat.NameOf(slot)} for a {kind}."));
                throw new InvalidOperationException($"The record of this table does not match the match being rebuilt at line {next.Seq}.");
            }

            // Every swap asked before this decision is applied with it; so is every swap asked right after it,
            // or a swap asked after the last decision would wait for a question that is answered live.
            ApplySwaps(index);
            _recorded.Remove(next);
            var following = _recorded.FindIndex(entry => !entry.IsSwap);
            ApplySwaps(following >= 0 ? following : _recorded.Count);
            if (_recorded.Count == 0)
            {
                _exhausted.TrySetResult();
            }

            return next;
        }
    }

    /// <summary>Under the lock: applies and removes the swap lines among the first <paramref name="before" /> entries, in their order.</summary>
    private void ApplySwaps(int before)
    {
        var swaps = _recorded.Take(Math.Min(before, _recorded.Count)).Where(entry => entry.IsSwap).ToList();
        foreach (var entry in swaps)
        {
            _recorded.Remove(entry);
            if (_swapping is { } swapping && entry.To is { } to && entry.AtRound is { } round)
            {
                swapping(entry.PlayerSlot, to, round);
            }
        }
    }

    /// <summary>
    /// Writes a decision down and waits for the line to land before the decision goes to the engine. On the
    /// driver's own thread, which a person blocks for minutes at a time: a few milliseconds to a file, or one
    /// round trip to a blob, is what makes "the decision reached the store" a fact rather than a hope -- a host
    /// that died in between asks the question again instead of rebuilding a match a move ahead of its record.
    /// A write that fails is a line on the console; the match goes on without it.
    /// </summary>
    private void Record(PlayerSlot slot, PlayerDecision decision)
    {
        Task written;
        lock (_gate)
        {
            Append(JournalEntry.Of(++_seq, _clock.GetUtcNow(), slot, decision));
            written = _writing;
        }

        try
        {
            written.GetAwaiter().GetResult();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException or Azure.RequestFailedException)
        {
            // Already said on the console by the continuation below; the decision stands.
        }
    }

    /// <summary>Under the lock: one line onto the end of the file, after whatever is already on its way there.</summary>
    private void Append(JournalEntry entry)
    {
        _writing = _writing.ContinueWith(
            _ => _writer.AppendJsonLinesAsync(File, [entry]),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).Unwrap();
        _writing.ContinueWith(
            failed => Console.Error.WriteLine($"  A decision could not be written to {File}: {failed.Exception?.GetBaseException().Message}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class Journaled(DecisionJournal journal, IPlayerAgent inner, PlayerSlot slot) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
        {
            if (journal.Recorded(slot, PlayerOptionsKind.Evolution, creature: null) is { } line)
            {
                var recorded = line.ToDecision();
                return recorded.IsPass ? EvolutionDecision.Pass : EvolutionDecision.Unlock(new EvolutionChoice(recorded.Creature!.Value, recorded.Tier!));
            }

            var decision = inner.DecideEvolution(board, options);
            journal.Record(slot, decision.Choice is { } choice ? PlayerDecision.Buy(choice.Creature, choice.Tier) : PlayerDecision.Pass);
            return decision;
        }

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            if (journal.Recorded(slot, PlayerOptionsKind.Speed, creature) is { } line)
            {
                return line.SpeedValue;
            }

            var speed = inner.DecideSpeed(board, creature);
            journal.Record(slot, PlayerDecision.ChooseSpeed(creature, speed));
            return speed;
        }

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options)
        {
            if (journal.Recorded(slot, PlayerOptionsKind.TieOrder, creature: null) is { } line)
            {
                return line.ToDecision().Order;
            }

            var order = inner.DecideTieOrder(board, options);
            journal.Record(slot, PlayerDecision.OrderTies(order));
            return order;
        }

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            ArgumentNullException.ThrowIfNull(intentOption);
            if (journal.Recorded(slot, PlayerOptionsKind.Intent, intentOption.Creature) is { } line)
            {
                return line.ToDecision().Spell!;
            }

            var spell = inner.DecideIntent(board, intentOption);
            journal.Record(slot, PlayerDecision.DeclareIntent(intentOption.Creature, spell));
            return spell;
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (journal.Recorded(slot, PlayerOptionsKind.Target, options.Actor) is { } line)
            {
                return line.ToDecision().Targets;
            }

            var targets = inner.DecideTargets(board, options);
            journal.Record(slot, PlayerDecision.BindTargets(targets));
            return targets;
        }
    }
}
