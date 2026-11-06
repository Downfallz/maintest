using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The settings an account brings to every new game (ADR 0140): how confirmed results are read, at what speed,
/// whether first-use help shows, and whether motion is reduced. A game can override them for its own duration
/// without changing these; a new game starts from them again.
/// </summary>
public sealed record GamePreferences(ReadingPace Pace, decimal Speed, bool Help, bool ReducedMotion)
{
    /// <summary>The speeds the match settings offer.</summary>
    public static IReadOnlyList<decimal> Speeds { get; } = [0.5m, 1m, 1.5m, 2m];

    /// <summary>What an account that never chose reads with: step by step, at normal speed, with help.</summary>
    public static GamePreferences Default { get; } = new(ReadingPace.Step, 1m, Help: true, ReducedMotion: false);

    /// <summary>The preferences a page sent, or why they are not ones the settings offer.</summary>
    public static Result<GamePreferences> Create(string? pace, decimal speed, bool help, bool reducedMotion)
    {
        if (!Enum.TryParse<ReadingPace>(pace, ignoreCase: true, out var reading) || !Enum.IsDefined(reading) || !Speeds.Contains(speed))
        {
            return Result.Failure<GamePreferences>(AccountErrors.PreferencesInvalid);
        }

        return Result.Success(new GamePreferences(reading, speed, help, reducedMotion));
    }
}
