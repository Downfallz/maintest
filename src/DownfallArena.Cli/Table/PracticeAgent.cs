using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Plays the preparation through ordinary gates, then permanently hands the seat to its person. A recipe at the
/// slot (ADR 0110) is played through <see cref="DecideAction" />: its speeds put the creature that sets the board
/// up before the one the person plays, and its spells give that creature the energy the recipe is about.
/// </summary>
internal sealed class PracticeAgent(PracticeScenario scenario, IPlayerAgent fallback, HumanSeat? person = null) : IPlayerAgent
{
    private bool _handedOver;

    private bool Human(PlayerBoardState board, PlayerOptionsKind kind, CreatureId? creature = null)
    {
        _handedOver |= person is not null && board.RoundNumber >= scenario.Round && kind == scenario.Question
            && (creature is null || scenario.AllyIndex is not { } index || creature == board.Allies[index].Id);
        return _handedOver;
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        if (Human(board, PlayerOptionsKind.Evolution))
        {
            return person!.DecideEvolution(board, options);
        }
        if (board.RoundNumber > scenario.Round)
        {
            return fallback.DecideEvolution(board, options);
        }

        foreach (var offered in options.Creatures)
        {
            var index = board.Allies.ToList().FindIndex(creature => creature.Id == offered.Creature);
            var name = Package(board, index);
            if (name is null)
            {
                continue;
            }
            var id = TierId.Parse($"tier:{name}:v1");
            if (!offered.AvailableTiers.Contains(id))
            {
                throw new InvalidDataException($"Practice '{scenario.Id}' needs {id} for creature {offered.Creature} in round {board.RoundNumber}. Rebuild the standard catalogue.");
            }

            return EvolutionDecision.Unlock(new EvolutionChoice(offered.Creature, id));
        }

        return EvolutionDecision.Pass;
    }

    private string? Package(PlayerBoardState board, int index)
    {
        if (index > 1)
        {
            return null;
        }
        if (board.Slot == PlayerSlot.Player2)
        {
            return (board.RoundNumber, index, scenario.Id) switch
            {
                (1, _, _) => "brute",
                (3, 0, "slot-armor") => "ironbound",
                (3, 0, "slot-drain" or "slot-stun") => "marauder",
                _ => null,
            };
        }
        var path = PackagePath(index);
        var stage = board.RoundNumber switch { 1 => 0, 3 => 1, 5 => 2, _ => -1 };
        return stage >= 0 && stage < path.Count ? path[stage] : null;
    }

    private IReadOnlyList<string> PackagePath(int index) => (scenario.Id, index) switch
    {
        ("multiclass", 0) => ["brute"],
        ("stun", 0) => ["prowler", "plague_doctor", "blightweaver"],
        ("resolution", 0) => ["occultist", "shaman", "spiritcaller"],
        ("resolution", 1) => ["brute", "berserker", "ravager"],
        ("slot-armor", 0) => ["occultist", "elementalist"],
        ("slot-overdrive" or "slot-drain", 0) => ["brute", "berserker"],
        ("slot-overdrive", 1) => ["prowler", "plague_doctor"],
        ("slot-chain", 0) => ["prowler", "plague_doctor", "blightweaver"],
        ("slot-chain", 1) => ["brute", "berserker", "ravager"],
        _ => ["occultist"],
    };

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        if (Human(board, PlayerOptionsKind.Speed))
        {
            return person!.DecideSpeed(board, creature);
        }

        var first = board.Allies[0].Id == creature;
        var mine = board.Slot == PlayerSlot.Player1;
        var quick = scenario.Id switch
        {
            // The enemy that sets the board up goes first, and every creature of the person after it.
            "slot-armor" or "slot-drain" or "slot-stun" => !mine && first,
            // The ally casting Overdrive goes before the one it is cast for.
            "slot-overdrive" => mine && board.Allies[1].Id == creature,
            _ => mine,
        };
        return quick ? Speed.Quick : Speed.Standard;
    }

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
        Human(board, PlayerOptionsKind.TieOrder) ? person!.DecideTieOrder(board, options) : options.AsRolled;

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        if (Human(board, PlayerOptionsKind.Intent))
        {
            return person!.DecideIntent(board, intentOption);
        }
        if (board.RoundNumber > scenario.Round)
        {
            return fallback.DecideIntent(board, intentOption);
        }
        var index = board.Allies.ToList().FindIndex(creature => creature.Id == intentOption.Creature);
        var name = Spell(board, index);
        var id = SpellId.Parse($"spell:{name}:v1");
        if (!intentOption.CastableSpells.Contains(id))
        {
            throw new InvalidDataException($"Practice '{scenario.Id}' needs castable {id} in round {board.RoundNumber}. Rebuild the standard catalogue.");
        }

        return id;
    }

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);
        if (Human(board, PlayerOptionsKind.Action, options.Actor))
        {
            return person!.DecideAction(board, options);
        }
        if (board.RoundNumber > scenario.Round)
        {
            return fallback.DecideAction(board, options);
        }
        var index = board.Allies.ToList().FindIndex(creature => creature.Id == options.Actor);
        var id = SpellId.Parse($"spell:{Spell(board, index)}:v1");
        var offered = options.For(id)
            ?? throw new InvalidDataException($"Practice '{scenario.Id}' needs castable {id} in round {board.RoundNumber}. Rebuild the standard catalogue.");

        return new ChosenAction(id, Targets(board, offered));
    }

    /// <summary>
    /// The first legal targets, except that the enemy setting a recipe up aims a single-target spell at the
    /// person's first creature: Claim and Crash are about that creature, not whichever the candidates list first.
    /// </summary>
    private IReadOnlyList<CreatureId> Targets(PlayerBoardState board, TargetOptions options)
    {
        var legal = options.LegalTargets;
        var setsUp = board.Slot == PlayerSlot.Player2 && board.RoundNumber == scenario.Round
            && board.Allies[0].Id == options.Actor && legal.MinTargets <= 1 && board.Enemies.Count > 0;
        return setsUp && legal.Candidates.Contains(board.Enemies[0].Id)
            ? [board.Enemies[0].Id]
            : [.. legal.Candidates.Take(legal.MaxTargets)];
    }

    private string Spell(PlayerBoardState board, int index)
    {
        if (board.RoundNumber < scenario.Round)
        {
            return Preparation(board, index);
        }
        if (board.Slot == PlayerSlot.Player2)
        {
            return (scenario.Id, index) switch
            {
                ("slot-armor", 0) => "full_plate",
                ("slot-drain", 0) => "extort",
                ("slot-stun", 0) => "protective_slam",
                (_, 0) => "pummel",
                _ => "heavy_strike",
            };
        }
        if (scenario.Id == "slot-overdrive" && index == 1)
        {
            return "adrenaline_tonic";
        }
        if (index > 1)
        {
            return "wait";
        }
        return scenario.Id switch
        {
            "stun" => index == 0 ? "tranquilizer_dart" : "lightning_bolt",
            "resolution" => index == 0 ? "toxic_waves" : "psycho_rush",
            _ => "lightning_bolt",
        };
    }

    /// <summary>
    /// Focus, which gathers energy, except for the creature a recipe at the slot is about: it spends energy so
    /// that it comes to its slot with exactly what the recipe needs. Two to begin each round, nothing at the
    /// start: Overdrive finds two and makes four, so Fury (three) becomes payable; Claim finds three and leaves
    /// two, so Fury is out of reach.
    /// </summary>
    private string Preparation(PlayerBoardState board, int index) =>
        (scenario.Id, board.Slot, index, board.RoundNumber) switch
        {
            ("slot-overdrive", PlayerSlot.Player1, 0, _) => "heavy_strike",
            ("slot-drain", PlayerSlot.Player1, 0, 1) => "pummel",
            ("slot-drain", PlayerSlot.Player1, 0, _) => "heavy_strike",
            _ => "wait",
        };

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
        Human(board, PlayerOptionsKind.Target, options.Actor) ? person!.DecideTargets(board, options)
            : [.. options.LegalTargets.Candidates.Take(options.LegalTargets.MaxTargets)];
}
