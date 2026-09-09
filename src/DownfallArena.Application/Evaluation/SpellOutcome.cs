namespace DownfallArena.Application.Evaluation;

/// <summary>
/// How one spell correlates with winning: over every side of every match that declared it at least once, how
/// many of those sides won. A spell both teams reach for lands near one half and says nothing; one that shows
/// up mostly on the winning side lands above it.
/// <para>
/// <see cref="Sides"/> is the sample this rests on, and it is the number to read first: a spell declared by
/// three sides can score 1.00 on noise alone.
/// </para>
/// </summary>
public sealed record SpellOutcome
{
    public required string Spell { get; init; }

    /// <summary>Intents declared, counting every cast.</summary>
    public required int Intents { get; init; }

    /// <summary>Sides — one match seen from one player — that declared the spell at least once.</summary>
    public required int Sides { get; init; }

    public required int Wins { get; init; }

    public required int Losses { get; init; }

    public required int Draws { get; init; }

    /// <summary>The share of those sides that won, a draw counting one half. One half is no signal.</summary>
    public double Score => Sides == 0 ? 0 : (Wins + (Draws / 2.0)) / Sides;

    /// <summary>Casts per side that used it, which separates a spell used once from one leaned on.</summary>
    public double IntentsPerSide => Sides == 0 ? 0 : (double)Intents / Sides;
}
