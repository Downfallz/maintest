using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// A feedback store held in a dictionary, so the guards read what they wrote as a real store gives it back; and its index
/// (#866), whose version moves with each write, as a blob's tag does.
/// </summary>
internal sealed class KeptFeedback : IFeedbackStore, IFeedbackIndexStore
{
    private readonly Dictionary<string, Feedback> _kept = new(StringComparer.Ordinal);
    private FeedbackIndexContent? _index;
    private int _version;

    public IReadOnlyCollection<Feedback> All => _kept.Values;

    /// <summary>How many records were opened, one by one.</summary>
    public int Opened { get; private set; }

    /// <summary>The index as last written; none before the first write.</summary>
    public FeedbackIndexContent? Index => _index;

    /// <summary>How many index writes go through before the next one fails as a storage hiccup would; none never fails.</summary>
    public int? IndexWritesBeforeFailure { get; set; }

    /// <summary>Run once before the next index write is checked: another process's write landing first.</summary>
    public Func<Task>? BeforeIndexWrite { get; set; }

    public Task<FeedbackIndexRead> ReadIndexAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new FeedbackIndexRead(_index, _index is null ? null : _version.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public async Task<bool> WriteIndexAsync(FeedbackIndexContent content, string? version, CancellationToken cancellationToken = default)
    {
        if (IndexWritesBeforeFailure is { } left)
        {
            IndexWritesBeforeFailure = left == 0 ? null : left - 1;
            if (left == 0)
            {
                throw new IOException("The storage did not answer.");
            }
        }

        if (BeforeIndexWrite is { } first)
        {
            BeforeIndexWrite = null;
            await first();
        }

        if (version != (_index is null ? null : _version.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        {
            return false;
        }

        _index = content;
        _version++;
        return true;
    }

    public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        _kept[feedback.Id] = feedback;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _kept.Keys.OrderDescending(StringComparer.Ordinal)]);

    public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _kept.Keys.Where(id => Feedback.SentAt(id) is { } at && at > after && at <= until).OrderDescending(StringComparer.Ordinal)]);

    public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        Opened++;
        return Task.FromResult(_kept.GetValueOrDefault(id));
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _kept.Remove(id);
        return Task.CompletedTask;
    }
}
