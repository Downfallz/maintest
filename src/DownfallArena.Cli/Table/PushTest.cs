namespace DownfallArena.Cli.Table;

/// <summary>What a test notice came to (<see cref="PushNotices.TestAsync" />): sent, or not, and said how.</summary>
internal sealed record PushTest(bool Sent, string Message);
