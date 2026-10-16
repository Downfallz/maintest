using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// What casting one spell at a slot was worth to the agent that read it: the outcome the round played out to
/// (one for a win, minus one for a loss, zero when the match goes on) and the round's score, in the agent's
/// own weights, on the best target set the spell has there.
/// </summary>
public sealed record SpellValue(SpellId Spell, double Outcome, double Score);
