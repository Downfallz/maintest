namespace DownfallArena.Application.Work;

/// <summary>What a requeue or a drop did: the letters it handled, and the ids it was asked for that it did not find.</summary>
public sealed record LettersHandled(IReadOnlyList<string> Handled, IReadOnlyList<string> Missing);
