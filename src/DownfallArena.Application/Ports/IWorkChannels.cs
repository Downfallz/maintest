namespace DownfallArena.Application.Ports;

/// <summary>The work queues of a host, by name, created on first use.</summary>
public interface IWorkChannels
{
    Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default);
}
