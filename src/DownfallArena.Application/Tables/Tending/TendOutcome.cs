namespace DownfallArena.Application.Tables.Tending;

/// <summary>How a tending run ended: it did its whole pass, or it stopped on an error.</summary>
public enum TendOutcome
{
    Completed,
    Failed,
}
