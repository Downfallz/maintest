namespace DownfallArena.Domain.Resources;

/// <summary>
/// Which creatures a spell may target, relative to the caster.
/// </summary>
public enum TargetOrigin
{
    Self,
    Ally,
    Enemy,
    Any,
}
