namespace DownfallArena.Application.Ports;

/// <summary>An item as it is received from a <see cref="IWorkChannel" />.</summary>
/// <param name="Id">The item's own id, the same at every delivery.</param>
/// <param name="Receipt">What proves this delivery, which deleting or retrying it needs.</param>
/// <param name="Delivered">How many times the item has been received, this time included.</param>
/// <param name="QueuedAt">When the item was put on the queue.</param>
public sealed record QueuedWork(string Id, string Receipt, string Body, long Delivered, DateTimeOffset QueuedAt);
