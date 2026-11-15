using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a practice host serves behind <c>/api/</c>: the practice route a page starts or restarts on, and the seat's
/// own routes once one is playing. The recipes of ADR 0079 are one; a scenario file is another (ADR 0179).
/// </summary>
internal interface IPracticeTable : IAsyncDisposable
{
    Task<StudioResponse> HandleAsync(string method, string path, string body, string? supplied, string? ifNoneMatch = null, string? query = null);
}
