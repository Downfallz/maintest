namespace DownfallArena.Application.Ports;

/// <summary>An item as a <see cref="IWorkChannel" /> shows it without handing it out: nothing proves a delivery, so nothing can delete it.</summary>
/// <param name="Id">The item's own id, the same at every delivery.</param>
/// <param name="Delivered">How many times the item has been received so far.</param>
/// <param name="QueuedAt">When the item was put on the queue.</param>
public sealed record PeekedWork(string Id, string Body, long Delivered, DateTimeOffset QueuedAt);
