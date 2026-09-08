namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// What happens when a lasting effect is applied to a creature that already carries it.
/// </summary>
public enum StackingPolicy
{
    /// <summary>A new instance is added next to the existing ones.</summary>
    Stack,

    /// <summary>The existing instance's duration restarts; the amount is unchanged.</summary>
    Refresh,

    /// <summary>The new application is ignored while an instance is active.</summary>
    Ignore,
}
