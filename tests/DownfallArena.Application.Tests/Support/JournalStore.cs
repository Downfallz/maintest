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

    public JournalStore()
    {
        Journal = Substitute.For<IDecisionJournal>();
        Journal.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (BeforeRead is { } wait)
                {
                    BeforeRead = null;
                    await wait();
                }

                return new JournalReading([.. _lines], End);
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

                _lines.AddRange(call.Arg<IReadOnlyList<IJournalEntry>>());
                return End;
            });
    }

    public IDecisionJournal Journal { get; }

    public IReadOnlyList<IJournalEntry> Lines => _lines;

    /// <summary>The decisions among the lines, in order.</summary>
    public IReadOnlyList<SeatDecision> Decisions => [.. _lines.OfType<DecisionEntry>().Select(entry => entry.Decision)];

    /// <summary>Awaited once, as the next reading arrives and before it is answered: where a step is held in its turn.</summary>
    public Func<Task>? BeforeRead { get; set; }

    /// <summary>Run once, as the next append arrives and before it is judged: where another writer gets in first.</summary>
    public Action? BeforeAppend { get; set; }

    /// <summary>A line written by somebody else.</summary>
    public void WriteElsewhere(SeatDecision line) => _lines.Add(new DecisionEntry(line));

    /// <summary>A line of any kind written by somebody else.</summary>
    public void WriteElsewhere(IJournalEntry line) => _lines.Add(line);

    private JournalPosition End => new(_lines.Count, _lines.Count);
}
