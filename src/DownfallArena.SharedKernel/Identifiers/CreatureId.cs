using System.Globalization;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a creature inside a match. Assigned by the match when teams are formed.
/// </summary>
public readonly record struct CreatureId
{
    private CreatureId(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static CreatureId From(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        return new CreatureId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
