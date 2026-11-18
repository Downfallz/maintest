using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>An index of feedback held in memory (#866), for a desk built over a store that keeps none.</summary>
internal sealed class KeptFeedbackIndex : IFeedbackIndexStore
{
    private FeedbackIndexContent? _index;
    private int _version;

    public Task<FeedbackIndexRead> ReadIndexAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new FeedbackIndexRead(_index, _index is null ? null : $"{_version}"));

    public Task<bool> WriteIndexAsync(FeedbackIndexContent content, string? version, CancellationToken cancellationToken = default)
    {
        if (version != (_index is null ? null : $"{_version}"))
        {
            return Task.FromResult(false);
        }

        (_index, _version) = (content, _version + 1);
        return Task.FromResult(true);
    }
}
