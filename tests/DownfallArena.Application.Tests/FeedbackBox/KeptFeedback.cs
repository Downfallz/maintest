using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>A feedback store held in a dictionary, so the guards read what they wrote as a real store gives it back.</summary>
internal sealed class KeptFeedback : IFeedbackStore
{
    private readonly Dictionary<string, Feedback> _kept = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Feedback> All => _kept.Values;

    public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        _kept[feedback.Id] = feedback;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _kept.Keys.OrderDescending(StringComparer.Ordinal)]);

    public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_kept.GetValueOrDefault(id));

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _kept.Remove(id);
        return Task.CompletedTask;
    }
}
