using DownfallArena.Application.Agents;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// Who a recorder should ask for a decision, and what to call them on the step it writes.
/// </summary>
/// <remarks>
/// The two travel together because they have to be one reading. Who is playing a seat can change while a
/// match runs, so naming the decider and then asking the seat again are two readings of something that moves:
/// a swap landing between them writes one occupant's name on another's decision, and nothing in the record
/// would show it. Handing out both at once means whatever the reading found is who answers and who is named,
/// and a swap that arrives after it is simply a swap that arrives after this question -- which is what a seat
/// already promises.
/// </remarks>
/// <param name="Agent">Who to ask. It is the occupant itself, never a seat that would look up again.</param>
/// <param name="Name">What to call them, or null when nothing names deciders at all.</param>
public sealed record Decider(IPlayerAgent Agent, string? Name);
