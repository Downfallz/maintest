namespace DownfallArena.Cli.Table;

/// <summary>What came of one send through <see cref="IPushSender" />, when nothing was thrown.</summary>
internal enum PushSent
{
    /// <summary>The push service took the notice.</summary>
    Taken,

    /// <summary>The push service said the subscription is gone (404 or 410): the browser makes a new one.</summary>
    Gone,

    /// <summary>
    /// Not sent: the endpoint is on no push service this host sends to, a subscription kept before the subscribe routes
    /// checked it (#1132). It is forgotten as a gone one is, but a new one on the same service would be refused (#1209).
    /// </summary>
    UnknownService,
}
