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
}
