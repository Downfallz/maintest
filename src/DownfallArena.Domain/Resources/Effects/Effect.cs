namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// One consequence of a spell on a target. The set of effects is closed: every kind is a sealed record in this
/// folder, and the combat rules handle each one explicitly (ADR 0012).
/// </summary>
public abstract record Effect
{
    protected Effect()
    {
    }

    protected static int Positive(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, parameterName);
        return value;
    }
}
