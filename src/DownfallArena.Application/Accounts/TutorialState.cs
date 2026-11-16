namespace DownfallArena.Application.Accounts;

/// <summary>Where an account stands with one Tutorial (ADR 0187).</summary>
public enum TutorialState
{
    /// <summary>Neither finished nor skipped: the first of the series opens by itself.</summary>
    New,

    /// <summary>Its last step was shown and its round ended, or its match ended.</summary>
    Done,

    /// <summary>Skipped, or left before its end: it never opens by itself again.</summary>
    Skipped,
}
