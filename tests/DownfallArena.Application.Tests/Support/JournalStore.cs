using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using NSubstitute;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// One journal behind the port, conditional as a real one is: an append is refused when the journal no longer
/// ends where its writer read it to. Several processes can be given the same one, and a line can be written
/// behind a writer's back, between its reading and its writing.
/// </summary>
internal sealed class JournalStore
{
    private readonly List<IJournalEntry> _lines = [];
    private bool _exists;

    /// <param name="started">Whether the journal is there, empty, before anything is written: a recorded table's (#874).</param>
    public JournalStore(bool started = false)
    {
        _exists = started;
        Journal = Substitute.For<IDecisionJournal>();
        Journal.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await ReadingAsync();
                return _exists ? new JournalReading([.. _lines], End) : JournalReading.AbsentAt(JournalPosition.Start);
            });
        Journal.ReadAfterAsync(Arg.Any<string>(), Arg.Any<JournalPosition>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await ReadingAsync();
                var after = call.Arg<JournalPosition>();
                return after.Mark > _lines.Count ? null
                    : !_exists ? JournalReading.AbsentAt(after)
                    : new JournalReading([.. _lines.Skip((int)after.Mark)], End);
            });
        Journal.AppendAsync(Arg.Any<string>(), Arg.Any<JournalPosition>(), Arg.Any<IReadOnlyList<IJournalEntry>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                BeforeAppend?.Invoke();
                BeforeAppend = null;
                if (call.Arg<JournalPosition>() != End)
                {
                    return (JournalPosition?)null;
                }

                var entries = call.Arg<IReadOnlyList<IJournalEntry>>();
                _lines.AddRange(entries);
                _exists |= entries.Count > 0;
                return End;
            });
        Journal.EndAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => (long)_lines.Count);
    }

    public IDecisionJournal Journal { get; }

    public IReadOnlyList<IJournalEntry> Lines => _lines;

    /// <summary>The decisions among the lines, in order.</summary>
    public IReadOnlyList<SeatDecision> Decisions => [.. _lines.OfType<DecisionEntry>().Select(entry => entry.Decision)];

    /// <summary>Awaited once, as the next reading arrives and before it is answered: where a step is held in its turn.</summary>
    public Func<Task>? BeforeRead { get; set; }

    /// <summary>Run once, as the next append arrives and before it is judged: where another writer gets in first.</summary>
    public Action? BeforeAppend { get; set; }

    /// <summary>The journal deleted with its run by another process (#856): nothing left of it.</summary>
    public void Delete()
    {
        _lines.Clear();
        _exists = false;
    }

    /// <summary>Whether the journal is there, empty or not: an append of entries at its start creates it, as the stores do.</summary>
    public bool Exists => _exists;

    /// <summary>A line written by somebody else.</summary>
    public void WriteElsewhere(SeatDecision line) => WriteElsewhere(new DecisionEntry(line));

    /// <summary>A line of any kind written by somebody else.</summary>
    public void WriteElsewhere(IJournalEntry line)
    {
        _lines.Add(line);
        _exists = true;
    }

    private async Task ReadingAsync()
    {
        if (BeforeRead is { } wait)
        {
            BeforeRead = null;
            await wait();
        }
    }

    private JournalPosition End => new(_lines.Count, _lines.Count);
}
