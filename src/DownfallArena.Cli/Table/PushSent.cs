namespace DownfallArena.Cli.Table;

/// <summary>What came of one send through <see cref="IPushSender" />, when nothing was thrown.</summary>
internal enum PushSent
{
    /// <summary>The push service took the notice.</summary>
    Taken,

    /// <summary>The push service said the subscription is gone (404 or 410): the browser makes a new one.</summary>
    Gone,

    /// <summary>
    /// Not sent: the endpoint is on no push service this host sends to. It is a subscription kept from before the
    /// subscribe routes checked it (#1132). It is forgotten like a gone one, but a new subscription on that service
    /// would be refused (#1209).
    /// </summary>
    UnknownService,
}
