using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.Features.Commands.CreateMatch;
using DA.Game.Application.Matches.Features.Commands.JoinMatch;
using DA.Game.Application.Matches.Features.Commands.ResolveNextCombatAction;
using DA.Game.Application.Matches.Features.Commands.RevealNextActionBindTargets;
using DA.Game.Application.Matches.Features.Commands.SubmitCombatIntent;
using DA.Game.Application.Matches.Features.Commands.SubmitEvolutionChoice;
using DA.Game.Application.Matches.Features.Commands.SubmitSpeedChoice;
using DA.Game.Application.Matches.Features.Queries.get_;
using DA.Game.Application.Matches.Features.Queries.GetPlayerOptions;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Application.Players.Features.Create;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Spells;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace DA.Game.Runtime
{
    internal class Program
    {
        // -------------------------------------------------
        // Helper
        // -------------------------------------------------
        static T PickRandom<T>(IReadOnlyList<T> list)
        {
            if (list is null || list.Count == 0)
                throw new InvalidOperationException("Cannot pick a random element from an empty list.");

            return list[Random.Shared.Next(list.Count)];
        }
        static SkillSpeed PickRandomSpeed()
        {
            return Random.Shared.Next(2) == 0
                ? SkillSpeed.Quick
                : SkillSpeed.Standard;
        }
        static IReadOnlyList<CreatureId> PickDistinctTargets(
    IReadOnlyList<CreatureId> pool,
    int count)
        {
            if (count <= 0)
                return Array.Empty<CreatureId>();

            if (pool.Count < count)
                throw new InvalidOperationException($"Not enough targets. Need {count}, have {pool.Count}.");

            var remaining = pool.ToList();
            var selected = new List<CreatureId>(count);

            for (var i = 0; i < count; i++)
            {
                var idx = Random.Shared.Next(remaining.Count);
                selected.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }

            return selected;
        }
        static async Task Main(string[] args)
        {
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    // You can tune this level depending on how noisy you want it
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            var logger = host.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("DownfallArenaLogger");

            var services = new ServiceCollection();

            services.ConfigureDownfallArena();

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var gamere = serviceProvider.GetRequiredService<IGameResources>();
            var matchid = await mediator.Send(new CreateMatchCommand());
            if (matchid.IsSuccess)
            {
                AnsiConsole.MarkupLine("[green]Match created successfully![/]");
                AnsiConsole.MarkupLine($"Match ID: [yellow]{matchid}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to create match:[/]");
                AnsiConsole.MarkupLine($"[red]{matchid.Error}[/]");
            }



            var player1 = await mediator.Send(new CreatePlayerCommand("PlayerOne", ActorKind.Human));
            var player2 = await mediator.Send(new CreatePlayerCommand("BotOne", ActorKind.Bot));
            if (player1.IsSuccess && player2.IsSuccess)
            {
                AnsiConsole.MarkupLine("[green]Players created successfully![/]");
                AnsiConsole.MarkupLine($"Player 1 ID: [yellow]{player1.Value}[/]");
                AnsiConsole.MarkupLine($"Player 2 ID: [yellow]{player2.Value}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to create players:[/]");
                if (!player1.IsSuccess)
                    AnsiConsole.MarkupLine($"[red]{player1.Error}[/]");
                if (!player2.IsSuccess)
                    AnsiConsole.MarkupLine($"[red]{player2.Error}[/]");
            }


            var pr1 = mapper.Map<PlayerRef>(player1.Value!);
            var pr2 = mapper.Map<PlayerRef>(player2.Value!);
            var matchId = matchid.Value!;
            var basicAttack = gamere.Spells.Single(x => x.Id == SpellId.New("spell:basic_attack:v1"));

            var joinMatchResult = await mediator.Send(new JoinMatchCommand(matchId, pr1));
            var joinMatchResult2 = await mediator.Send(new JoinMatchCommand(matchId, pr2));
            while (true)
            {
                // -------------------------------------------------
                // Fetch options + unlockables
                // -------------------------------------------------

                var optionEvol1 = await mediator.Send(
                    new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1));
                var unlockable1 = await mediator.Send(
                    new GetUnlockableSpellsForPlayerQuery(matchId, PlayerSlot.Player1));

                // -------------------------------------------------
                // PLAYER 1 — Evolution POC
                // -------------------------------------------------
                var evol1 = optionEvol1.Value!.Evolution;

                if (evol1 is not null && evol1.RemainingPicks > 0)
                {
                    for (var i = 0; i < evol1.RemainingPicks; i++)
                    {
                        optionEvol1 = await mediator.Send(
                            new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1));

                        unlockable1 = await mediator.Send(
                            new GetUnlockableSpellsForPlayerQuery(matchId, PlayerSlot.Player1));
                        var actualEvol1 = optionEvol1.Value!.Evolution;

                        // Pick a random legal creature
                        var creatureId = PickRandom(actualEvol1.LegalCreatureIds);

                        // Get its unlockable spells
                        var unlockableSpells = unlockable1.Value!.Creatures
                            .Single(c => c.CreatureId == creatureId)
                            .UnlockableSpellIds;

                        // Pick a random spell
                        var spellId = PickRandom(unlockableSpells);

                        // Optional: resolve Spell from resources / game repo
                        var spell = gamere.Spells.Single(s => s.Id == spellId);

                        // -----------------------------------------
                        // Submit evolution choice (POC)
                        // -----------------------------------------
                        var result = await mediator.Send(
                            new SubmitEvolutionChoiceCommand(
                                matchId,
                                PlayerSlot.Player1,
                                new SpellUnlockChoiceDto(
                                creatureId,
                                spell)));

                        if (!result.IsSuccess)
                            Console.WriteLine($"Failed to submit evolution choice for Player 1: {result.Error}");
                    }
                }


                // -------------------------------------------------
                // PLAYER 2 — Evolution POC
                // -------------------------------------------------
                var optionEvol2 = await mediator.Send(
                    new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2));

                var unlockable2 = await mediator.Send(
                    new GetUnlockableSpellsForPlayerQuery(matchId, PlayerSlot.Player2));
                var evol2 = optionEvol2.Value!.Evolution;

                if (evol2 is not null && evol2.RemainingPicks > 0)
                {
                    for (var i = 0; i < evol2.RemainingPicks; i++)
                    {
                        optionEvol2 = await mediator.Send(
                    new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2));

                        unlockable2 = await mediator.Send(
                            new GetUnlockableSpellsForPlayerQuery(matchId, PlayerSlot.Player2));
                        var actualEvol2 = optionEvol2.Value!.Evolution;

                        var creatureId = PickRandom(evol2.LegalCreatureIds);

                        var unlockableSpells = unlockable2.Value!.Creatures
                            .Single(c => c.CreatureId == creatureId)
                            .UnlockableSpellIds;

                        var spellId = PickRandom(unlockableSpells);
                        var spell = gamere.Spells.Single(s => s.Id == spellId);

                        // -----------------------------------------
                        // Submit evolution choice (POC)
                        // -----------------------------------------
                        var result = await mediator.Send(
                            new SubmitEvolutionChoiceCommand(
                                matchId,
                                PlayerSlot.Player2,
                                new SpellUnlockChoiceDto(
                                creatureId,
                                spell)));
                        if (!result.IsSuccess)
                            Console.WriteLine($"Failed to submit evolution choice for Player 2: {result.Error}");
                    }
                }



                var ttt = await mediator.Send(new GetBoardStateForPlayerQuery(matchId, PlayerSlot.Player1));

                // -------------------------------------------------
                // Fetch options
                // -------------------------------------------------
                var optionsP1 = await mediator.Send(
                    new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1));

                var optionsP2 = await mediator.Send(
                    new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2));

                // -------------------------------------------------
                // PLAYER 1 — Speed POC
                // -------------------------------------------------
                var speedOptionsP1 = optionsP1.Value!.Speed;

                if (speedOptionsP1 is not null && speedOptionsP1.Remaining > 0)
                {
                    foreach (var creatureId in speedOptionsP1.RequiredCreatures)
                    {
                        var speed = PickRandomSpeed();

                        var result = await mediator.Send(new SubmitSpeedChoiceCommand(
                            matchId,
                            PlayerSlot.Player1,
                            new SpeedChoiceDto(creatureId, speed)));
                        if (!result.IsSuccess)
                            Console.WriteLine($"Failed to submit evolution choice for Player 1: {result.Error}");
                    }
                }

                // -------------------------------------------------
                // PLAYER 2 — Speed POC
                // -------------------------------------------------
                var speedOptionsP2 = optionsP2.Value!.Speed;

                if (speedOptionsP2 is not null && speedOptionsP2.Remaining > 0)
                {
                    foreach (var creatureId in speedOptionsP2.RequiredCreatures)
                    {
                        var speed = PickRandomSpeed();

                        var result = await mediator.Send(new SubmitSpeedChoiceCommand(
                            matchId,
                            PlayerSlot.Player2,
                            new SpeedChoiceDto(creatureId, speed)));
                        if (!result.IsSuccess)
                            Console.WriteLine($"Failed to submit evolution choice for Player 2: {result.Error}");
                    }
                }
                // -------------------------------------------------
                // Fetch options + board states
                // -------------------------------------------------
                var combatOptionsP1 = await mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1));
                var combatOptionsP2 = await mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2));

                var boardP1 = await mediator.Send(new GetBoardStateForPlayerQuery(matchId, PlayerSlot.Player1));
                var boardP2 = await mediator.Send(new GetBoardStateForPlayerQuery(matchId, PlayerSlot.Player2));

                // -------------------------------------------------
                // PLAYER 1 — Combat Intent POC (random known spell)
                // -------------------------------------------------
                var planningP1 = combatOptionsP1.Value!.CombatPlanning;

                if (planningP1 is not null && planningP1.MissingCreatureIds.Count > 0)
                {
                    foreach (var creatureId in planningP1.MissingCreatureIds)
                    {
                        var creature = boardP1.Value!.FriendlyCreatures
                            .Single(c => c.Id == creatureId);

                        var spellId = PickRandom(creature.KnownSpellIds);
                        var spell = gamere.Spells.Single(s => s.Id == spellId);

                        await mediator.Send(new SubmitCombatIntentCommand(
                            matchId,
                            PlayerSlot.Player1,
                            new CombatIntentDto(creatureId, spell)));
                    }
                }

                // -------------------------------------------------
                // PLAYER 2 — Combat Intent POC (random known spell)
                // -------------------------------------------------
                var planningP2 = combatOptionsP2.Value!.CombatPlanning;

                if (planningP2 is not null && planningP2.MissingCreatureIds.Count > 0)
                {
                    foreach (var creatureId in planningP2.MissingCreatureIds)
                    {
                        var creature = boardP2.Value!.FriendlyCreatures
                            .Single(c => c.Id == creatureId);

                        var spellId = PickRandom(creature.KnownSpellIds);
                        var spell = gamere.Spells.Single(s => s.Id == spellId);

                        await mediator.Send(new SubmitCombatIntentCommand(
                            matchId,
                            PlayerSlot.Player2,
                            new CombatIntentDto(creatureId, spell)));
                    }
                }





                // ---------------------------------------
                // Reveal loop (controls both players)
                // ---------------------------------------
                while (true)
                {
                    // Since you control both players, just query both and use whichever exposes CombatAction.
                    var opt1 = await mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player1));
                    var opt2 = await mediator.Send(new GetPlayerOptionsQuery(matchId, PlayerSlot.Player2));

                    var action = opt1.Value?.CombatAction ?? opt2.Value?.CombatAction;

                    // Not in reveal subphase or no reveal pending
                    if (action is null || action.NextActorId is null || action.RemainingReveals <= 0)
                        break;

                    var actorId = action.NextActorId.Value;

                    // If no legal targets, you can either break or throw (POC).
                    if (action.LegalTargetIds.Count == 0 && action.MinTargets > 0)
                        throw new InvalidOperationException($"No legal targets for actor {actorId.Value}.");

                    // Pick a random target count between Min and Max.
                    var k = action.MaxTargets <= 0
                        ? 0
                        : Random.Shared.Next(action.MinTargets, action.MaxTargets + 1);

                    var targets = PickDistinctTargets(action.LegalTargetIds, k);

                    await mediator.Send(new RevealNextActionBindTargetsCommand(
                        matchId,
                        actorId,
                        targets.ToList()));
                }
               
                var roundEnded = false;
                CombatStepOutcomeView stepOutcome = null!;
                while (!roundEnded)
                {
                    var z = await mediator.Send(new ResolveNextCombatActionCommand(matchId));
                    stepOutcome = z!.Value!.stepOutcome;
                    roundEnded = z!.Value!.stepOutcome.IsRoundCompleted;
                }

                if (stepOutcome.IsMatchEnded)
                    break;
            }
            
            


            Console.ReadLine();
        }

    }


    //    static async Task Main(string[] args)
    //    {
    //        var services = new ServiceCollection();
    //        // Bus
    //        services.AddSingleton<IEventBus, InMemoryEventBus>();

    //        // Domain event handlers
    //        services.AddScoped<IDomainEventHandler<TurnAdvanced>, TurnAdvancedHandler>();

    //        services.AddSingleton<ITurnDecider, HumanTurnDecider>();             // fallback global
    //        services.AddSingleton<HumanTurnDecider>();
    //        services.AddSingleton<RandomBotDecider>();
    //        // Application ports
    //        services.AddScoped<ICommandBus, DA.Game.Infrastructure.Commands.CommandBus>();

    //        // Handlers (exemples)
    //        services.AddScoped<ICommandHandler<CreatePlayerCommand, Result<PlayerId>>, CreatePlayerHandler>();
    //        services.AddScoped<ICommandHandler<JoinMatchCommand, Result<MatchState>>, JoinMatchHandler>();
    //        services.AddScoped<ICommandHandler<PlayTurnCommand, Result>, PlayTurnHandler>();

    //        services.AddSingleton<ITurnDeciderRegistry>(sp =>
    //        {
    //            var registry = new TurnDeciderRegistry(sp.GetRequiredService<HumanTurnDecider>());

    //            // Fallback par ActorKind
    //            registry.Register(ActorKind.Human, sp.GetRequiredService<HumanTurnDecider>());
    //            registry.Register(ActorKind.Bot, sp.GetRequiredService<RandomBotDecider>());

    //            // (Optionnel) cas particuliers par PlayerId
    //            // registry.Register(someBotId, sp.GetRequiredService<SmarterBotDecider>());

    //            return registry;
    //        });

    //        //var schema = SchemaLoader.LoadFromFile("dist/game.schema.json");
    //        //Console.WriteLine($"Loaded schema v{schema.SchemaVersion} ({schema.BuildHash ?? "no-hash"})");

    //        //var sim = new BattleSimulator(schema);
    //        //sim.Cast("Mage A", "spell:frost_nova");         // via alias
    //        //sim.Cast("Mage B", "spell:lighning_bolt:v3");  // version explicite








    //        //var repo = new InMemoryMatchRepository();
    //        //var uow = new DummyUow();
    //        //var bus = new ConsoleBus();
    //        //var clock = new SystemClock();
    //        //var join = new JoinMatchHandler(repo, uow, bus, clock);

    //        //var matchId = MatchId.New();
    //        //var p1 = PlayerId.New();
    //        //var p2 = PlayerId.New();
    //        //var p3 = PlayerId.New();

    //        //Console.WriteLine($"[BOOT] Match {matchId} waiting for players...");

    //        //// Joueur 1
    //        //await join.HandleAsync(new JoinMatchCommand(matchId, p1));
    //        //// Joueur 2 → Match démarre automatiquement
    //        //await join.HandleAsync(new JoinMatchCommand(matchId, p2));

    //        //// Essai d’un troisième (doit échouer)

    //        //var result = await join.HandleAsync(new JoinMatchCommand(matchId, p3));

    //        //if (!result.IsSuccess)
    //        //    Console.WriteLine(result.Error);

    //        //Console.WriteLine("[DONE] Press any key.");
    //        //Console.ReadKey();


    //        // Boot DI “maison”
    //        var repo = new InMemoryMatchRepository();
    //        var uow = new DummyUow();
    //        var clock = new SystemClock();
    //        var rng = new SystemRandom();
    //        var bus = new InMemoryEventBus();
    //        // Crée un match + 2 joueurs
    //        var matchId = MatchId.New();
    //        var player1Id = PlayerId.New();
    //        var player2Id = PlayerId.New();

    //        // Use cases
    //        var join = new JoinMatchHandler(repo, uow, bus, clock, rng);
    //        var play = new PlayTurnHandler(repo, uow, bus, clock);

    //        // Factory d’acteurs: ici, on instancie un Bot si l’id correspond au slot 2 (exemple)
    //        // Choisis ton mode ici en assignant la factory :
    //        Func<PlayerId, IActor> actorFactory = id => new BotActor(id, rng);                 // Bot vs Bot
    //                                                                                           // Func<PlayerId, IActor> actorFactory = id => id.Equals(botId) ? new BotActor(id, rng) : new HumanActor(id); // Human vs Bot
    //                                                                                           // Func<PlayerId, IActor> actorFactory = id => new HumanActor(id);                  // Human vs Human (pour plus tard)


    //        // 3) Domain event handlers
    //        var turnHandler = new TurnAdvancedHandler(repo, play, actorFactory, clock);
    //        bus.RegisterHandler(turnHandler);

    //        // 4) TurnSignal – s’abonne AVANT le 2e join, et on l’arme AVANT de publier le 1er event
    //        using var turnSignal = new TurnSignal(matchId);
    //        bus.RegisterHandler(turnSignal);
    //        turnSignal.ArmNext();
    //        var test = CharacterLoadout.From(CharacterId.New(), "Player 1", CharacterDefId.New());
    //        if (!test.IsSuccess) throw new Exception("Bad loadout");


    //        // 5) Joins (le 2e déclenchera MatchStarted + TurnAdvanced)
    //        await join.HandleAsync(new JoinMatchCommand(matchId, player1Id, actorFactory(player1Id).Kind));
    //        await join.HandleAsync(new JoinMatchCommand(matchId, player2Id, actorFactory(player2Id).Kind));

    //        // 6) Synchronise-toi sur le premier tour (sinon tu le rates)
    //        await turnSignal.WaitNextAsync();
    //        // Joue N tours “visibles” (utile en console/demo)
    //        // La boucle n’essaie pas de “faire jouer” le bot elle-même : elle attend l’event
    //        for (int seen = 0; seen < 8; seen++)
    //        {
    //            var match = await repo.GetAsync(matchId) ?? throw new Exception("Match?");
    //            if (match.State != MatchState.Started || match.CurrentTurn is null) break;

    //            var currentId = match.CurrentTurn == PlayerSlot.Player1 ? match.Player1Id : match.Player2Id;
    //            if (currentId is null) break;

    //            var actor = actorFactory(currentId.Value);

    //            if (actor.Kind == ActorKind.Human)
    //            {
    //                Console.WriteLine($"(HUMAN {currentId}) press Enter to play turn #{match.TurnNumber}...");
    //                Console.ReadLine();

    //                var action = new PlayerAction("noop", "human-turn");
    //                var res = await play.HandleAsync(new PlayTurnCommand(matchId, currentId.Value, action));
    //                if (!res.IsSuccess) { Console.WriteLine($"PlayTurn failed: {res.Error}"); break; }

    //                // Le PlayTurn publie un TurnAdvanced ⇒ on va attendre ce prochain tour
    //                turnSignal.ArmNext();
    //                await turnSignal.WaitNextAsync(); // Attendre que le prochain tour arrive
    //            }
    //            else
    //            {
    //                Console.WriteLine($"(BOT {currentId}) auto-playing turn #{match.TurnNumber}...");
    //                // NE FAIS RIEN : c’est le TurnAdvancedHandler qui enverra PlayTurn pour le bot
    //                // On attend juste le prochain TurnAdvanced
    //                turnSignal.ArmNext();
    //                await turnSignal.WaitNextAsync();
    //            }
    //        }



    //    }

    //}
    ////// ------------- helpers --------------
    ////static class HandlerExt
    ////{
    ////    public static Task<Result<MatchState>> Handle(this JoinMatchHandler h, JoinMatchCommand c, CancellationToken ct = default)
    ////        => h.Handle(c, ct);
    ////    public static Task<Result> Handle(this PlayTurnHandler h, PlayTurnCommand c, CancellationToken ct = default)
    ////        => h.Handle(c, ct);

    ////    public static void RegisterHandler<TEvent>(this InMemoryEventBus bus, IDomainEventHandler<TEvent> handler)
    ////        where TEvent : IDomainEvent => bus.RegisterHandler(handler);
    ////}
}
