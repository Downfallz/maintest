using System.Globalization;
using System.Text.Json.Serialization;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One line of <c>decisions.jsonl</c>: a decision a seat made, whoever made it, or a swap the pilot asked for,
/// in the order the match took them. With the seed, these lines are the match (ADR 0091): the engine draws
/// every roll from the seed, so the same decisions in the same order rebuild the same board.
/// </summary>
/// <remarks>
/// Ids travel as the page carries them -- a creature by its number, a spell and a package by their ids --
/// which is also how <see cref="TableDecisionBody" /> carries them; a journal line is a decision as the wire
/// would have posted it, plus which seat made it and when.
/// </remarks>
internal sealed record JournalEntry
{
    public const string SwapKind = "Swap";

    public required long Seq { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>The seat, as a URL names it: <c>player1</c> or <c>player2</c>.</summary>
    public required string Slot { get; init; }

    /// <summary>A <see cref="PlayerOptionsKind" /> name, or <see cref="SwapKind" />.</summary>
    public required string Kind { get; init; }

    public int? Creature { get; init; }

    public string? Spell { get; init; }

    public string? Tier { get; init; }

    public string? Speed { get; init; }

    public IReadOnlyList<int>? Targets { get; init; }

    public IReadOnlyList<int>? Order { get; init; }

    public bool Pass { get; init; }

    /// <summary>For a swap: who the pilot named for the seat, as the pilot typed it.</summary>
    public string? To { get; init; }

    /// <summary>For a swap: the round the seat changes hands at the top of.</summary>
    public int? AtRound { get; init; }

    [JsonIgnore]
    public PlayerSlot PlayerSlot => TableSeat.SlotOf(Slot) ?? throw new InvalidOperationException($"'{Slot}' names no seat.");

    [JsonIgnore]
    public bool IsSwap => Kind == SwapKind;

    public static JournalEntry Of(long seq, DateTimeOffset at, PlayerSlot slot, PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new JournalEntry
        {
            Seq = seq,
            At = at,
            Slot = TableSeat.NameOf(slot),
            Kind = decision.Kind.ToString(),
            Creature = decision.Creature?.Value,
            Spell = decision.Spell?.ToString(),
            Tier = decision.Tier?.ToString(),
            Speed = decision.Speed?.ToString(),
            Targets = decision.Kind == PlayerOptionsKind.Target ? [.. decision.Targets.Select(target => target.Value)] : null,
            Order = decision.Kind == PlayerOptionsKind.TieOrder ? [.. decision.Order.Select(creature => creature.Value)] : null,
            Pass = decision.IsPass,
        };
    }

    public static JournalEntry Swap(long seq, DateTimeOffset at, PlayerSlot slot, string to, int atRound) =>
        new() { Seq = seq, At = at, Slot = TableSeat.NameOf(slot), Kind = SwapKind, To = to, AtRound = atRound };

    /// <summary>The decision this line records, read back the way a posted body is.</summary>
    public PlayerDecision ToDecision()
    {
        var body = new TableDecisionBody { Kind = Kind, Creature = Creature, Spell = Spell, Tier = Tier, Speed = Speed, Targets = Targets, Order = Order, Pass = Pass };
        return body.ToDecision(out var problem) ?? throw new InvalidOperationException($"Journal line {Seq.ToString(CultureInfo.InvariantCulture)} is not a decision: {problem}");
    }

    /// <summary>Whether this line answers the question a seat is being asked now: same seat, same kind, same creature.</summary>
    public bool Answers(PlayerSlot slot, PlayerOptionsKind kind, CreatureId? creature) =>
        !IsSwap && PlayerSlot == slot && Kind == kind.ToString() && (creature is null || Creature is null || Creature == creature.Value.Value);

    [JsonIgnore]
    public Speed SpeedValue => Enum.Parse<Speed>(Speed ?? throw new InvalidOperationException("A speed line carries a speed."));
}
