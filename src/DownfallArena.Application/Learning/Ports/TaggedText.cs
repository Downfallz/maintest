namespace DownfallArena.Application.Learning.Ports;

/// <summary>
/// A document as it was read, and the tag of that very content: what a conditional write names to say "only if
/// it is still this" (<see cref="IArtifactWriter.WriteJsonIfAsync{TValue}" />).
/// </summary>
public sealed record TaggedText(string Text, string Tag);
