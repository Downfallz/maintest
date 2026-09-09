namespace DownfallArena.Application.Evaluation;

/// <summary>
/// How one spell correlates with winning, and what it actually did on the way there.
/// <para>
/// <see cref="Sides"/> counts the sides that declared it at least once, and <see cref="Score"/> the share of
/// those that won. That pair is a coarse signal: one declaration is enough to be counted, so a spell made far
/// too expensive still appears on every side it did before. <see cref="Resolved"/>, <see cref="ResolveRate"/>
/// and the effect totals are what move when a number is tuned, and <see cref="ResolvedWhenWon"/> weighs the
/// correlation by casts rather than by sides.
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

    /// <summary>Declarations that reached resolution and landed.</summary>
    public int Resolved { get; init; }

    /// <summary>Declarations that reached resolution and fizzled: not known, not affordable, no legal target.</summary>
    public int Fizzled { get; init; }

    public int Criticals { get; init; }

    /// <summary>Damage its casts dealt on the spot. A bleed's damage lands at upkeep and is not counted here.</summary>
    public int Damage { get; init; }

    public int Healing { get; init; }

    public int Stuns { get; init; }

    /// <summary>Bleeds applied, not the damage they go on to deal.</summary>
    public int Bleeds { get; init; }

    /// <summary>Defense buffs and initiative debuffs applied.</summary>
    public int Buffs { get; init; }

    /// <summary>Casts that landed on a side that went on to win.</summary>
    public int ResolvedWhenWon { get; init; }

    /// <summary>The share of those sides that won, a draw counting one half. One half is no signal.</summary>
    public double Score => Sides == 0 ? 0 : (Wins + (Draws / 2.0)) / Sides;

    /// <summary>Casts per side that used it, which separates a spell used once from one leaned on.</summary>
    public double IntentsPerSide => Sides == 0 ? 0 : (double)Intents / Sides;

    /// <summary>
    /// The share of its declarations that landed. This is the number a cost change moves: a spell nobody can
    /// afford is still declared and still counted a side, but it stops resolving.
    /// </summary>
    public double ResolveRate => Resolved + Fizzled == 0 ? 0 : (double)Resolved / (Resolved + Fizzled);

    /// <summary>Damage per landed cast, which compares spells regardless of how often each was reached.</summary>
    public double DamagePerCast => Resolved == 0 ? 0 : (double)Damage / Resolved;

    /// <summary>
    /// The share of its landed casts made by a side that won: <see cref="Score"/> weighed by use rather than by
    /// side, so a spell leaned on by winners reads differently from one they merely held.
    /// <para>
    /// Its neutral point is not one half. A winning side survives longer and therefore acts more, so across
    /// every spell of a run some share above one half of all casts belongs to winners; that share is the
    /// baseline this one is read against, and a reader given the number alone would take a spell sitting
    /// exactly on it for a winner. Whatever shows the column has to show the baseline with it.
    /// </para>
    /// </summary>
    public double CastShareWhenWon => Resolved == 0 ? 0 : (double)ResolvedWhenWon / Resolved;
}
