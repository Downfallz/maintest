namespace DownfallArena.Application.Accounts;

/// <summary>
/// An account's devices as they were read, and the version they were read at (ADR 0201): the worker forgets a device
/// its push service says is gone while the API registers another, and neither write may lose the other's.
/// </summary>
public sealed record DeviceListRead(IReadOnlyList<PushDevice> Devices, string? Version);
