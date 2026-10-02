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
/// The driver asks the two seats in a fixed order and the engine is deterministic from the seed, so the order
/// of the lines is the order of the questions; one queue is enough, and a line out of place is a divergence.
/// </para>
/// </remarks>
internal sealed class DecisionJournal
{
    public const string File = "decisions.jsonl";

    private readonly IArtifactWriter _writer;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly Queue<JournalEntry> _recorded;
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
        _recorded = new Queue<JournalEntry>(recorded ?? []);
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
            ApplySwaps();
            if (_recorded.Count == 0)
            {
                return null;
            }

            var next = _recorded.Dequeue();
            if (!next.Answers(slot, kind, creature))
            {
                _recorded.Clear();
                _exhausted.TrySetException(new InvalidOperationException($"The record has diverged: line {next.Seq} is a {next.Kind} by {next.Slot}, and the match asks {TableSeat.NameOf(slot)} for a {kind}."));
                throw new InvalidOperationException($"The record of this table does not match the match being rebuilt at line {next.Seq}.");
            }

            // Swaps asked after the last decision are applied with it, or they would wait for a question that
            // is answered live and apply late.
            ApplySwaps();
            if (_recorded.Count == 0)
            {
                _exhausted.TrySetResult();
            }

            return next;
        }
    }

    /// <summary>
    /// Under the lock: applies the swap lines at the head of the record. A swap that cannot be applied -- the
    /// agent it names is not on this host any more, or the seat refuses the round -- is a divergence like a
    /// decision that does not fit: the record says a seat changed hands, and a table rebuilt without it would
    /// let the wrong occupant decide, which no checkpoint of the boards can notice.
    /// </summary>
    private void ApplySwaps()
    {
        while (_recorded.TryPeek(out var head) && head.IsSwap)
        {
            _recorded.Dequeue();
            if (_swapping is { } swapping && head.To is { } to && head.AtRound is { } round && !swapping(head.PlayerSlot, to, round))
            {
                _recorded.Clear();
                _exhausted.TrySetException(new InvalidOperationException($"The record has diverged: line {head.Seq} seats '{to}' in {head.Slot} from round {round}, and nobody of that name can sit there now."));
                throw new InvalidOperationException($"The record of this table cannot be replayed at line {head.Seq}: the swap it names cannot be applied.");
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
