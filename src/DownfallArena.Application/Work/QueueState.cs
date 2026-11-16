namespace DownfallArena.Application.Work;

/// <summary>One queue as the Jobs tab shows it (admin plan, A3b): its name, how many items it holds, and when its oldest waiting one was put on it.</summary>
public sealed record QueueState(string Name, int Count, DateTimeOffset? Oldest);
