namespace DownfallArena.Cli.Table;

/// <summary>What became of one queued notice to one device (ADR 0203).</summary>
internal enum DeviceSend
{
    /// <summary>The push service took it.</summary>
    Sent,

    /// <summary>The push service said the subscription is gone: the device is forgotten, its endpoint remembered as gone.</summary>
    Gone,

    /// <summary>The endpoint is no device of the account any more: turned off, signed out, or replaced since it was queued.</summary>
    NotADevice,
}
