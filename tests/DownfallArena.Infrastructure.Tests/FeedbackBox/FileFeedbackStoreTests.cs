using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.Infrastructure.FeedbackBox;

namespace DownfallArena.Infrastructure.Tests.FeedbackBox;

public sealed class FileFeedbackStoreTests : FeedbackStoreContract, IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.CreateVersion7():N}");

    protected override Task<IFeedbackStore> StoreAsync() => Task.FromResult<IFeedbackStore>(new FileFeedbackStore(_directory));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
