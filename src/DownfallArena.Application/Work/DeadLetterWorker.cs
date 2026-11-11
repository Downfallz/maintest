using DownfallArena.Application.Ports;

namespace DownfallArena.Application.Work;

/// <summary>
/// The second worker (ADR 0159, phase 5): it takes what its poison queue holds and keeps it, for ever, on a
/// dead-letter queue (<c>&lt;queue&gt;-dlq</c>), with the error its first worker gave up on and when and where it was
/// set aside, and says so in one line. An item it cannot keep stays on the poison queue for the next look.
/// </summary>
/// <param name="host">The name of the machine that sets items aside, what a dead letter says.</param>
public sealed class DeadLetterWorker(IWorkChannel poison, IWorkChannel deadLetters, TimeProvider clock, string host)
{
    /// <summary>How long an item is hidden while it is kept: far more than keeping one takes.</summary>
    public static readonly TimeSpan HiddenFor = TimeSpan.FromMinutes(1);

    /// <summary>How often the poison queue is looked at.</summary>
    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(30);

    /// <summary>What a set-aside item is said with: the host's console.</summary>
    public Action<string> Said { get; init; } = _ => { };

    /// <summary>One look at the poison queue: every item it holds kept as a dead letter. How many were.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var kept = 0;
        foreach (var work in await poison.ReceiveAsync(16, HiddenFor, cancellationToken))
        {
            var letter = LetterOf(work);
            await deadLetters.SendAsync(letter.ToJson(), Timeout.InfiniteTimeSpan, cancellationToken: cancellationToken);
            await poison.DeleteAsync(work, cancellationToken);
            Said($"  Dead letter on {deadLetters.Name}: {letter.Body} from {letter.Queue}, given up on after {letter.Delivered} deliveries: {letter.Error}");
            kept++;
        }

        return kept;
    }

    /// <summary>Looks at the poison queue every <see cref="LookEvery" /> until stopped; a look that fails is said, and the next one tries again.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                Said($"  The poison queue {poison.Name} could not be read: {failure.Message}");
            }

            try
            {
                await Task.Delay(LookEvery, clock, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// The dead letter of a poisoned item: what the first worker said of it, or, for an item that is not one it
    /// poisoned, the item itself with what its poison queue knows.
    /// </summary>
    private DeadLetter LetterOf(QueuedWork work)
    {
        var now = clock.GetUtcNow();
        return PoisonedWork.Parse(work.Body) is { } poisoned
            ? new DeadLetter(poisoned.Queue, poisoned.Body, poisoned.Error, poisoned.Delivered, poisoned.QueuedAt, poisoned.PoisonedAt, now, host)
            : new DeadLetter(poison.Name, work.Body, "Put on the poison queue by something other than its worker: no error was kept.", work.Delivered, work.QueuedAt, work.QueuedAt, now, host);
    }
}
