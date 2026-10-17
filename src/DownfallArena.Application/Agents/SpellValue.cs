using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// What casting one spell at a slot was worth to the agent that read it: the outcome the round played out to
/// (one for a win, minus one for a loss, zero when the match goes on), the outcome against the enemy's worst
/// reply where two casts win (zero where the agent did not read it), and the round's score, in the agent's own
/// weights, on the target set the agent would have cast the spell on there. The three are compared in that order,
/// the order the agent chose on.
/// </summary>
public sealed record SpellValue(SpellId Spell, double Outcome, double Robust, double Score);
