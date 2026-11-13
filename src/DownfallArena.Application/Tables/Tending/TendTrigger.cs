namespace DownfallArena.Application.Tables.Tending;

/// <summary>What started a tending run: the hourly job, or an operator from the admin panel.</summary>
public enum TendTrigger
{
    Schedule,
    Manual,
}
