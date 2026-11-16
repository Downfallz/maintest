namespace DownfallArena.Application.Accounts;

/// <summary>How a battle reads confirmed results (ADR 0139): Step waits for Next on every item, Auto on incoming actions only.</summary>
public enum ReadingPace
{
    Auto,
    Step,
}
