namespace DownfallArena.Cli.Table;

/// <summary>What a test notice came to (<see cref="PushNotices.TestAsync" />): sent, or not, and said how.</summary>
internal sealed record PushTest(bool Sent, string Message)
{
    /// <summary>
    /// What the subscribe routes and the push tests say of a push service this host does not send to, in a player's words
    /// (#1233): Settings and the seat page show it as it is, and the battle view says the same sentence.
    /// </summary>
    public const string UnknownServiceMessage = "This browser's notification service is not one this table can send to. Try another browser.";

    /// <summary>
    /// A test on a subscription on no push service this host sends to (#1209): said as the subscribe routes refuse one,
    /// which its route answers with their status and code, since a new subscription on that service would be refused too.
    /// </summary>
    public static readonly PushTest OnUnknownService = new(false, UnknownServiceMessage) { UnknownService = true };

    /// <summary>Whether the subscription is on no push service this host sends to; its route answers 400, not 502.</summary>
    public bool UnknownService { get; init; }

    /// <summary>
    /// A test the push service refused, said with its status when it answered one: its own words can carry the
    /// service's response text, so they go to the console only (#786).
    /// </summary>
    public static string Refused(Exception failure) =>
        failure switch
        {
            HttpRequestException { StatusCode: { } status } => $"The push service refused it ({(int)status}).",
            System.Security.Cryptography.CryptographicException => "This device's subscription could not be used. Turn notifications off and on again.",
            _ => "The push service could not be reached. Try again in a moment.",
        };
}
