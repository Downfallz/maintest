namespace DownfallArena.Application.Hosting.Ports;

/// <summary>
/// Where the host's settings are kept (ADR 0177): one document for the host, beside the accounts, read and written
/// whole. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IHostSettingsStore
{
    /// <summary>The settings as last saved, or null for a host that never saved any.</summary>
    Task<HostSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the settings with these.</summary>
    Task SaveAsync(HostSettings settings, CancellationToken cancellationToken = default);
}
