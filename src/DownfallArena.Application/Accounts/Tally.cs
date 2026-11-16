namespace DownfallArena.Application.Accounts;

/// <summary>How a set of games went for one player: how many, and how many won, lost and drawn.</summary>
public sealed record Tally(int Games, int Won, int Lost, int Drawn)
{
    public static Tally None { get; } = new(0, 0, 0, 0);

    /// <summary>The tally with one more game, ended as given.</summary>
    public Tally With(GameOutcome outcome) => outcome switch
    {
        GameOutcome.Won => this with { Games = Games + 1, Won = Won + 1 },
        GameOutcome.Lost => this with { Games = Games + 1, Lost = Lost + 1 },
        _ => this with { Games = Games + 1, Drawn = Drawn + 1 },
    };
}
