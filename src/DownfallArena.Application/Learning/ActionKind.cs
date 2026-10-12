namespace DownfallArena.Application.Learning;

/// <summary>
/// The kinds of decision an action encodes, in the order of the sub-phases that ask for them.
/// </summary>
public enum ActionKind
{
    Pass,
    Evolve,
    Speed,
    Intent,
    Targets,

    /// <summary>A spell and its targets chosen together at the slot, under a rule set that declares no intent (ADR 0110).</summary>
    Act,
}
