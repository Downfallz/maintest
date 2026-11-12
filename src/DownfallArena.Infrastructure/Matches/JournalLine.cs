using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// One line of <c>decisions.jsonl</c>, the file the table has written since ADR 0091: a decision as the wire
/// would have posted it -- a creature by its number, a spell and a package by their ids -- with the seat that
/// made it and when; or a seat changing hands; or, since ADR 0159, a seat conceding; or, since ADR 0169, the seats
/// that let their time to answer run out.
/// </summary>
internal sealed record JournalLine
{
    public required long Seq { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>The seat, as a URL names it: <c>player1</c> or <c>player2</c>.</summary>
    public required string Slot { get; init; }

    /// <summary>A <see cref="PlayerOptionsKind" /> name for a decision, <see cref="SwapKind" /> or <see cref="ConcessionKind" />.</summary>
    public required string Kind { get; init; }

    public int? Creature { get; init; }

    public string? Spell { get; init; }

    public string? Tier { get; init; }

    public string? Speed { get; init; }

    public IReadOnlyList<int>? Targets { get; init; }

    public IReadOnlyList<int>? Order { get; init; }

    public bool Pass { get; init; }

    /// <summary>For a seat changing hands: who was named for it.</summary>
    public string? To { get; init; }

    /// <summary>For a seat changing hands: the round it changes at the top of.</summary>
    public int? AtRound { get; init; }

    /// <summary>For a seat changing hands: the last round the seat had decided anything in when it was asked; none in older lines.</summary>
    public int? Reached { get; init; }

    /// <summary>For a timeout: every seat that let its time run out, <see cref="Slot" /> the first of them.</summary>
    public IReadOnlyList<string>? Late { get; init; }

    /// <summary>The kind of the line a seat changing hands is written as, since ADR 0091.</summary>
    public const string SwapKind = "Swap";

    /// <summary>The kind of the line a concession is written as (ADR 0159).</summary>
    public const string ConcessionKind = "Concession";

    /// <summary>The kind of the line a timeout is written as (ADR 0169).</summary>
    public const string TimeoutKind = "Timeout";

    public static JournalLine Of(long seq, DateTimeOffset at, IJournalEntry entry) => entry switch
    {
        DecisionEntry decided => Of(seq, at, decided.Decision),
        ConcessionEntry conceded => new JournalLine { Seq = seq, At = at, Slot = NameOf(conceded.Slot), Kind = ConcessionKind },
        SeatChangeEntry changed => new JournalLine { Seq = seq, At = at, Slot = NameOf(changed.Slot), Kind = SwapKind, To = changed.To, AtRound = changed.FromRound, Reached = changed.Reached },
        TimeoutEntry timedOut when timedOut.Late.Count > 0 => new JournalLine { Seq = seq, At = at, Slot = NameOf(timedOut.Late[0]), Kind = TimeoutKind, Late = [.. timedOut.Late.Select(NameOf)] },
        _ => throw new ArgumentException($"A journal does not hold a {entry?.GetType().Name}.", nameof(entry)),
    };

    /// <summary>
    /// What this line records, or nothing for a kind of line this reader does not know. A line that names a
    /// kind it knows and lacks what the kind needs is a broken file, not another kind of line.
    /// </summary>
    public IJournalEntry? ToEntry()
    {
        if (Kind == ConcessionKind)
        {
            return new ConcessionEntry(SlotOf());
        }

        if (Kind == TimeoutKind)
        {
            return new TimeoutEntry([.. (Late ?? [Slot]).Select(SlotNamed)]);
        }

        if (Kind == SwapKind)
        {
            return To is { } to && AtRound is { } round
                ? new SeatChangeEntry(SlotOf(), to, round, Reached)
                : throw Broken("it is a seat changing hands without who takes it or from which round");
        }

        return ToDecision() is { } decision ? new DecisionEntry(decision) : null;
    }

    private static JournalLine Of(long seq, DateTimeOffset at, SeatDecision taken)
    {
        var decision = taken.Decision;
        return new JournalLine
        {
            Seq = seq,
            At = at,
            Slot = NameOf(taken.Slot),
            Kind = decision.Kind.ToString(),
            Creature = decision.Creature?.Value,
            Spell = decision.Spell?.ToString(),
            Tier = decision.Tier?.ToString(),
            Speed = decision.Speed?.ToString(),
            Targets = decision.Kind is PlayerOptionsKind.Target or PlayerOptionsKind.Action ? [.. decision.Targets.Select(target => target.Value)] : null,
            Order = decision.Kind == PlayerOptionsKind.TieOrder ? [.. decision.Order.Select(creature => creature.Value)] : null,
            Pass = decision.IsPass,
        };
    }

    /// <summary>The decision this line records, or none for a line that is not a decision.</summary>
    private SeatDecision? ToDecision()
    {
        if (!Enum.TryParse<PlayerOptionsKind>(Kind, out var kind) || kind is PlayerOptionsKind.Waiting or PlayerOptionsKind.Ended)
        {
            return null;
        }

        var decision = kind switch
        {
            PlayerOptionsKind.Evolution when Pass => PlayerDecision.Pass,
            PlayerOptionsKind.Evolution when Creature is { } creature && Tier is { } tier => PlayerDecision.Buy(CreatureId.From(creature), TierId.Parse(tier)),
            PlayerOptionsKind.Speed when Creature is { } creature && Enum.TryParse<Speed>(Speed, out var speed) && Enum.IsDefined(speed) => PlayerDecision.ChooseSpeed(CreatureId.From(creature), speed),
            PlayerOptionsKind.TieOrder when Order is { } order => PlayerDecision.OrderTies([.. order.Select(CreatureId.From)]),
            PlayerOptionsKind.Intent when Creature is { } creature && Spell is { } spell => PlayerDecision.DeclareIntent(CreatureId.From(creature), SpellId.Parse(spell)),
            PlayerOptionsKind.Target => PlayerDecision.BindTargets([.. (Targets ?? []).Select(CreatureId.From)]),
            PlayerOptionsKind.Action when Creature is { } creature && Spell is { } spell => PlayerDecision.Act(CreatureId.From(creature), SpellId.Parse(spell), [.. (Targets ?? []).Select(CreatureId.From)]),
            _ => throw Broken($"it is a {Kind} without what a {Kind} needs"),
        };
        return new SeatDecision(SlotOf(), decision);
    }

    private static string NameOf(PlayerSlot slot) => slot == PlayerSlot.Player1 ? "player1" : "player2";

    private PlayerSlot SlotOf() => SlotNamed(Slot);

    private PlayerSlot SlotNamed(string name) => name switch
    {
        "player1" => PlayerSlot.Player1,
        "player2" => PlayerSlot.Player2,
        _ => throw Broken($"'{name}' names no seat"),
    };

    private InvalidDataException Broken(string why) => new($"Journal line {Seq} is not one this journal reads: {why}.");
}
