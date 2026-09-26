using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>Plays the preparation through ordinary gates, then permanently hands the seat to its person.</summary>
internal sealed class PracticeAgent(PracticeScenario scenario, IPlayerAgent fallback, HumanSeat? person = null) : IPlayerAgent
{
    private bool _handedOver;

    private bool Human(PlayerBoardState board, PlayerOptionsKind kind, CreatureId? creature = null)
    {
        _handedOver |= person is not null && board.RoundNumber >= scenario.Round && kind == scenario.Question
            && (creature is null || creature == board.Allies[0].Id);
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
            return board.RoundNumber == 1 ? "brute" : null;
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
        _ => ["occultist"],
    };

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        if (Human(board, PlayerOptionsKind.Speed))
        {
            return person!.DecideSpeed(board, creature);
        }

        return board.Slot == PlayerSlot.Player1 ? Speed.Quick : Speed.Standard;
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

    private string Spell(PlayerBoardState board, int index)
    {
        if (board.RoundNumber < scenario.Round)
        {
            return "wait";
        }
        if (board.Slot == PlayerSlot.Player2)
        {
            return index == 0 ? "pummel" : "basic_attack";
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

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
        Human(board, PlayerOptionsKind.Target, options.Actor) ? person!.DecideTargets(board, options)
            : options.LegalTargets.Candidates.Take(options.LegalTargets.MaxTargets).ToList();
}
