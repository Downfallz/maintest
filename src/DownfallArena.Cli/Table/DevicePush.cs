namespace DownfallArena.Cli.Table;

/// <summary>
/// A notice to one device of an account, as the notices queue holds it (ADR 0203): the account and the endpoint, never
/// the device's keys, which the sender reads with the account's devices when it sends; and the payload and topic as
/// the door made them.
/// </summary>
internal sealed record DevicePush(string Account, string Endpoint, string Payload, string Topic);
