namespace DownfallArena.Application.Learning;

/// <summary>
/// The numeric form of an action: its kind, the acting creature's board slot, the spell's schema index, the
/// speed, and the target slots as a bitmask. Fields that do not apply are -1 (or 0 for the mask).
/// </summary>
public sealed record ActionCode(ActionKind Kind, int ActingSlot, int SpellIndex, int Speed, int TargetMask)
{
    public static ActionCode Pass { get; } = new(ActionKind.Pass, -1, -1, -1, 0);
}
