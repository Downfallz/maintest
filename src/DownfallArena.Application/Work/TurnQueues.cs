using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;

namespace DownfallArena.Application.Work;

/// <summary>
/// The bots' turn queues as the operator sees and handles them (admin plan, A3b): how many items each holds, the
/// dead letters, and putting a letter's turn back on the queue or dropping it. Requeueing is safe whatever the table
/// became: the worker finds the table before it takes a turn, and one that is over, closed or gone owes nothing.
/// </summary>
/// <remarks>
/// A queue hands out no item by its id, so handling some letters is a walk: letters are received a batch at a time
/// and kept hidden, the ones asked for are handled, and every other one is let back at once when the walk ends. A
/// letter is deleted only after its turn was sent, so a failure in between leaves it where it was.
/// </remarks>
public sealed class TurnQueues
{
    /// <summary>How many letters the tab shows: the first a storage queue lets anybody see without taking them.</summary>
    public const int MostShown = 32;

    /// <summary>The most letters one requeue or drop walks through, so a long queue never holds a request for long.</summary>
    public const int MostWalked = 1000;

    /// <summary>How long the letters a walk holds stay hidden if it stops before letting them back.</summary>
    public static readonly TimeSpan HeldFor = TimeSpan.FromMinutes(2);

    private readonly IWorkChannel _turns;
    private readonly IWorkChannel _poison;
    private readonly IWorkChannel _deadLetters;

    public TurnQueues(IWorkChannel turns, IWorkChannel poison, IWorkChannel deadLetters)
    {
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(poison);
        ArgumentNullException.ThrowIfNull(deadLetters);
        _turns = turns;
        _poison = poison;
        _deadLetters = deadLetters;
    }

    /// <summary>The three queues: the turns owed, the items given up on not yet set aside, and the dead letters.</summary>
    public async Task<IReadOnlyList<QueueState>> StatesAsync(CancellationToken cancellationToken = default) =>
    [
        await StateAsync(_turns, cancellationToken),
        await StateAsync(_poison, cancellationToken),
        await StateAsync(_deadLetters, cancellationToken),
    ];

    /// <summary>The dead letters the queue lets anybody see, first set aside first, at most <see cref="MostShown" />.</summary>
    public async Task<IReadOnlyList<ListedLetter>> LettersAsync(CancellationToken cancellationToken = default) =>
        [.. (await _deadLetters.PeekAsync(MostShown, cancellationToken)).Select(item => Listed(item.Id, item.Body))];

    /// <summary>
    /// Puts the turns of the letters asked for back on the turn queue, each as a new item delivered from zero, and
    /// deletes each letter once its turn is sent. Every letter when <paramref name="ids" /> is null.
    /// </summary>
    public Task<LettersHandled> RequeueAsync(IReadOnlyCollection<string>? ids, CancellationToken cancellationToken = default) =>
        WalkAsync(ids, async (work, letter) =>
        {
            if (letter is null)
            {
                return false;
            }

            await _turns.SendAsync(letter.Body, cancellationToken: cancellationToken);
            await _deadLetters.DeleteAsync(work, cancellationToken);
            return true;
        }, cancellationToken);

    /// <summary>Deletes the letters asked for; nothing goes back on the turn queue.</summary>
    public Task<LettersHandled> DropAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return WalkAsync(ids, async (work, _) =>
        {
            await _deadLetters.DeleteAsync(work, cancellationToken);
            return true;
        }, cancellationToken);
    }

    private static async Task<QueueState> StateAsync(IWorkChannel channel, CancellationToken cancellationToken)
    {
        var count = await channel.CountAsync(cancellationToken);
        var first = count == 0 ? [] : await channel.PeekAsync(1, cancellationToken);
        return new QueueState(channel.Name, count, first.Count > 0 ? first[0].QueuedAt : null);
    }

    private static ListedLetter Listed(string id, string body)
    {
        var letter = DeadLetter.Parse(body);
        return new ListedLetter(id, letter, letter is null ? null : OwedTurn.Parse(letter.Body)?.Run, body);
    }

    private async Task<LettersHandled> WalkAsync(IReadOnlyCollection<string>? ids, Func<QueuedWork, DeadLetter?, Task<bool>> handle, CancellationToken cancellationToken)
    {
        var wanted = ids is null ? null : new HashSet<string>(ids, StringComparer.Ordinal);
        List<string> handled = [];
        List<QueuedWork> held = [];
        try
        {
            var walked = 0;
            while (walked < MostWalked && (wanted is null || wanted.Count > 0))
            {
                var batch = await _deadLetters.ReceiveAsync(MostShown, HeldFor, cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                walked += batch.Count;
                foreach (var work in batch)
                {
                    if ((wanted is null || wanted.Remove(work.Id)) && await handle(work, DeadLetter.Parse(work.Body)))
                    {
                        handled.Add(work.Id);
                    }
                    else
                    {
                        held.Add(work);
                    }
                }
            }
        }
        finally
        {
            // Let back at once what the walk held and did not handle, the way it found them.
            foreach (var work in held)
            {
                await _deadLetters.RetryAsync(work, TimeSpan.Zero, CancellationToken.None);
            }
        }

        return new LettersHandled(handled, wanted is null ? [] : [.. wanted]);
    }
}
