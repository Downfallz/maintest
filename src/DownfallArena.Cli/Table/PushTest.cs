namespace DownfallArena.Cli.Table;

/// <summary>What a test notice came to (<see cref="PushNotices.TestAsync" />): sent, or not, and said how.</summary>
internal sealed record PushTest(bool Sent, string Message)
{
    /// <summary>
    /// A test the push service refused, said with its status when it answered one: its own words can carry the
    /// service's response text, so they go to the console only (#786).
    /// </summary>
    public static string Refused(Exception failure) =>
        failure is HttpRequestException { StatusCode: { } status }
            ? $"The push service refused it ({(int)status})."
            : "The push service could not be reached. Try again in a moment.";
}
