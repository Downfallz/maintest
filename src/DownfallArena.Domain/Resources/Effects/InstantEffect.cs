namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// An effect applied once, when the action resolves.
/// </summary>
public abstract record InstantEffect : Effect
{
    protected InstantEffect()
    {
    }
}
