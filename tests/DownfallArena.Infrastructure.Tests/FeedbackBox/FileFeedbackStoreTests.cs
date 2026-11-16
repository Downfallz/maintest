using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.Infrastructure.FeedbackBox;

namespace DownfallArena.Infrastructure.Tests.FeedbackBox;

public sealed class FileFeedbackStoreTests : FeedbackStoreContract, IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.CreateVersion7():N}");

    protected override Task<IFeedbackStore> StoreAsync() => Task.FromResult<IFeedbackStore>(new FileFeedbackStore(_directory));

    [Fact]
    public async Task A_record_that_cannot_be_read_is_found_as_none()
    {
        var store = new FileFeedbackStore(_directory);
        var id = Application.FeedbackBox.Feedback.NewId(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"{id}.json"), "{ not json", Cancel);

        (await store.FindAsync(id, Cancel)).ShouldBeNull();
        (await store.IdsAsync(Cancel)).ShouldBe([id]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
