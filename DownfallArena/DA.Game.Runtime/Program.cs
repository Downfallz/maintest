using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.Features.CreateMatch;
using DA.Game.Application.Matches.Features.JoinMatch;
using DA.Game.Application.Matches.Features.SubmitCombatActionChoice;
using DA.Game.Application.Matches.Features.SubmitEvolutionChoice;
using DA.Game.Application.Matches.Features.SubmitSpeedChoice;
using DA.Game.Application.Players.Features.Create;
using DA.Game.Shared;
using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Resources;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace DA.Game.Runtime
{
    internal class Program
    {
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

            var re1 = await mediator.Send(new SubmitEvolutionChoiceCommand(matchId,
                PlayerSlot.Player1, 
                new SpellUnlockChoiceDto(CharacterId.New(1), basicAttack)));
            var re2 = await mediator.Send(new SubmitEvolutionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpellUnlockChoiceDto(CharacterId.New(2), basicAttack)));
            var re3 = await mediator.Send(new SubmitEvolutionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpellUnlockChoiceDto(CharacterId.New(4), basicAttack)));
            var re4 = await mediator.Send(new SubmitEvolutionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpellUnlockChoiceDto(CharacterId.New(5), basicAttack)));

            var speed1 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(1), Speed.Quick)));

            var speed2 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(2), Speed.Quick)));

            var speed3 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(3), Speed.Quick)));

            var speed4 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(4), Speed.Quick)));

            var speed5 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(5), Speed.Quick)));

            var speed6 = await mediator.Send(new SubmitSpeedChoiceCommand(matchId,
                PlayerSlot.Player1,
                new SpeedChoiceDto(CharacterId.New(6), Speed.Quick)));


            var action1 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(1), basicAttack, new List<CharacterId>() { new CharacterId(4) })));
            var action2 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(2), basicAttack, new List<CharacterId>() { new CharacterId(4) })));
            var action3 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(3), basicAttack, new List<CharacterId>() { new CharacterId(4) })));
            var action4 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(4), basicAttack, new List<CharacterId>() { new CharacterId(1) })));
            var action5 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(5), basicAttack, new List<CharacterId>() { new CharacterId(1) })));
            var action6 = await mediator.Send(new SubmitCombatActionChoiceCommand(matchId,
                PlayerSlot.Player1,
                new CombatActionChoiceDto(CharacterId.New(6), basicAttack, new List<CharacterId>() { new CharacterId(1) })));


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
