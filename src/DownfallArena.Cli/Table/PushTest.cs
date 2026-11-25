namespace DownfallArena.Cli.Table;

/// <summary>What a test notice came to (<see cref="PushNotices.TestAsync" />): sent, or not, and said how.</summary>
internal sealed record PushTest(bool Sent, string Message)
{
    /// <summary>
    /// What the subscribe routes and the push tests say of a push service this host does not send to, in a player's words
    /// (#1233): Settings and the seat page show it as it is, and the battle view says the same sentence.
    /// </summary>
    public const string UnknownServiceMessage = "This browser's notification service is not one this table can send to. Try another browser.";

    /// <summary>What the subscribe routes and the push tests say on a host given no push keys (#1266).</summary>
    public const string NoNoticesMessage = "This table sends no notifications.";

    /// <summary>
    /// What Settings' subscribe route says of a subscription its notification service said was gone (#1266), naming
    /// Settings' own switch: Settings shows it when the fresh subscription it makes on a 410 is refused too.
    /// </summary>
    public const string DeviceGoneMessage = "This subscription is gone from its notification service. Turn \"Pop-ups on this device\" on again.";

    /// <summary>The same, said by a seat's subscribe route, naming the seat page's and the battle view's button (#1266).</summary>
    public const string SeatGoneMessage = "This subscription is gone from its notification service. Tap \"Notify me when it's my turn\" again.";

    /// <summary>What Settings' test says of a device its notification service let go of (#1266).</summary>
    public const string DeviceTestGoneMessage = "This subscription is gone. Turn \"Pop-ups on this device\" on again.";

    /// <summary>What a seat's test says of a subscription its notification service let go of (#1266).</summary>
    public const string SeatTestGoneMessage = "This subscription is gone. Tap \"Notify me when it's my turn\" again.";

    /// <summary>How Settings asks for a subscription made again: its own switch, off and on (#1266).</summary>
    public const string DeviceAgain = "Turn \"Pop-ups on this device\" off and on again.";

    /// <summary>How a seat's page asks for it: its own button, which reads "Stop notifying me" while it is on (#1266).</summary>
    public const string SeatAgain = "Tap \"Stop notifying me\", then \"Notify me when it's my turn\".";

    /// <summary>
    /// A test on a subscription on no push service this host sends to (#1209): said as the subscribe routes refuse one,
    /// which its route answers with their status and code, since a new subscription on that service would be refused too.
    /// </summary>
    public static readonly PushTest OnUnknownService = new(false, UnknownServiceMessage) { UnknownService = true };

    /// <summary>Whether the subscription is on no push service this host sends to; its route answers 400, not 502.</summary>
    public bool UnknownService { get; init; }

    /// <summary>
    /// A test the push service refused, said with its status when it answered one: its own words can carry the
    /// service's response text, so they go to the console only (#786). <paramref name="again" /> names the asking
    /// page's own control (<see cref="DeviceAgain" />, <see cref="SeatAgain" />).
    /// </summary>
    public static string Refused(Exception failure, string again) =>
        failure switch
        {
            HttpRequestException { StatusCode: { } status } => $"The notification service refused it ({(int)status}).",
            System.Security.Cryptography.CryptographicException => $"This device's subscription could not be used. {again}",
            _ => "The notification service could not be reached. Try again in a moment.",
        };
}
