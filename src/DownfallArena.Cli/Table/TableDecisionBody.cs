using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a tap posts. It is the decision as the wire carries it: ids as the page received them, nothing
/// resolved, nothing decided.
/// </summary>
internal sealed record TableDecisionBody
{
    /// <summary>
    /// Which asking this answers, as the seat payload's <c>waitingAsked</c> gave it. A decision belongs to one
    /// question and to no other: two clients on one token can both validate against the options of the first
    /// of two same-shaped questions, and the slower of them would otherwise answer the second -- spending a
    /// pick nobody meant to spend, or handing the driver a choice that is no longer legal.
    /// </summary>
    public long? Asked { get; init; }

    public string? Kind { get; init; }

    public int? Creature { get; init; }

    public string? Spell { get; init; }

    /// <summary>The package a purchase names. Separate from <see cref="Spell"/>: buying and casting are not the same act (ADR 0056).</summary>
    public string? Tier { get; init; }

    public string? Speed { get; init; }

    public IReadOnlyList<int>? Targets { get; init; }

    /// <summary>A tie order: the seat's tied creatures, first to act first (ADR 0063).</summary>
    public IReadOnlyList<int>? Order { get; init; }

    public bool Pass { get; init; }

    /// <summary>
    /// The decision this body names, or the reason it names none. A body that does not parse is refused here
    /// rather than reaching the check: what the check answers is "the options do not offer this", which is a
    /// true thing to tell a player, and "this is not a decision at all" is not.
    /// </summary>
    public PlayerDecision? ToDecision(out string problem)
    {
        problem = string.Empty;
        switch (Kind)
        {
            case "Evolution" when Pass:
                return PlayerDecision.Pass;
            case "Evolution" when Creature is { } creature && Tier is { } tier:
                return PlayerDecision.Buy(CreatureId.From(creature), TierId.Parse(tier));
            case "Speed" when Creature is { } creature && Enum.TryParse<Speed>(Speed, out var speed) && Enum.IsDefined(speed):
                return PlayerDecision.ChooseSpeed(CreatureId.From(creature), speed);
            case "TieOrder" when Order is { } order && order.All(creature => creature > 0):
                return PlayerDecision.OrderTies([.. order.Select(CreatureId.From)]);
            case "Intent" when Creature is { } creature && Spell is { } spell:
                return PlayerDecision.DeclareIntent(CreatureId.From(creature), SpellId.Parse(spell));
            case "Target":
                return PlayerDecision.BindTargets([.. (Targets ?? []).Select(CreatureId.From)]);
            default:
                problem = $"'{Kind}' is not a decision this seat can make, or it is missing what its kind needs.";
                return null;
        }
    }

    /// <summary>
    /// The kinds a body may name, so a refusal can say what was expected. Listed rather than read off
    /// <see cref="PlayerOptionsKind" />, which also carries the states nobody decides — <c>Waiting</c>,
    /// <c>Resolution</c> and <c>Ended</c> are what a seat is in, not what it is asked.
    /// </summary>
    public static string Kinds => string.Join(", ", Decidable);

    private static readonly PlayerOptionsKind[] Decidable =
        [PlayerOptionsKind.Evolution, PlayerOptionsKind.Speed, PlayerOptionsKind.TieOrder, PlayerOptionsKind.Intent, PlayerOptionsKind.Target];
}
