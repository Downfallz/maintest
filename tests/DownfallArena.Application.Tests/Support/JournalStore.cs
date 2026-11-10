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
    private readonly List<SeatDecision> _lines = [];

    public JournalStore()
    {
        Journal = Substitute.For<IDecisionJournal>();
        Journal.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new JournalReading([.. _lines], End));
        Journal.AppendAsync(Arg.Any<string>(), Arg.Any<JournalPosition>(), Arg.Any<IReadOnlyList<SeatDecision>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                BeforeAppend?.Invoke();
                BeforeAppend = null;
                if (call.Arg<JournalPosition>() != End)
                {
                    return (JournalPosition?)null;
                }

                _lines.AddRange(call.Arg<IReadOnlyList<SeatDecision>>());
                return End;
            });
    }

    public IDecisionJournal Journal { get; }

    public IReadOnlyList<SeatDecision> Lines => _lines;

    /// <summary>Run once, as the next append arrives and before it is judged: where another writer gets in first.</summary>
    public Action? BeforeAppend { get; set; }

    /// <summary>A line written by somebody else.</summary>
    public void WriteElsewhere(SeatDecision line) => _lines.Add(line);

    private JournalPosition End => new(_lines.Count, _lines.Count);
}
