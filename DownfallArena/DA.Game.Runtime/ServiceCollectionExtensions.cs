using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Players.Ports;
using DA.Game.Application.Shared.Abstractions;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Data;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Infrastructure.Bootstrap;
using DA.Game.Infrastructure.Matches;
using DA.Game.Infrastructure.Shared;
using DA.Game.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DA.Game.Application.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureDownfallArena(this IServiceCollection services)
    {
        // 👉 Logging config explicite
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            }).AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Cached game resources

        // GameResources singleton, built once at startup from JSON
        services.AddSingleton<IGameResources>(sp =>
        {
            //var env = sp.GetRequiredService<IHostEnvironment>();

            // schema file next to the exe: /Data/game-schema.json
            
            var baseDir = AppContext.BaseDirectory;
            var schemaPath = Path.Combine(baseDir, "Data/dst", "game.schema.json");

            // Throws if invalid, so you fail fast at startup
            return GameResourcesFactory.LoadFromFile(schemaPath.ToString());
        });

        services.AddSingleton<IRuleSetProvider, RuleSetProvider>();

        // Shared (Application)
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRandom, SystemRandom>();
        services.AddScoped<IEventBus, MediatorEventBus>();

        // Repos InMemory
        services.AddScoped<IMatchRepository, InMemoryMatchRepository>();
        services.AddScoped<IPlayerRepository, InMemoryPlayerRepository>();
        services.AddScoped<IPlayerUniqueness, InMemoryPlayerUniqueness>();

        // UoW
        services.AddScoped<IAggregateTracker, AggregateTracker>();
        services.AddScoped<IApplicationEventCollector, ApplicationEventCollector>();
        services.AddScoped<IUnitOfWork, DummyUow>();

        // Handlers (Application)
        services.ConfigureAppServices();
        //    services.AddSingleton<IPolicy>(sp =>
        //new MLPolicy(modelPath: "C:\\gamedata\\model-reward.zip", new GameFeatureExtractor()));


        // (Option) deciders registry: humain→null, bot→random
        services.AddSingleton<ITurnDeciderRegistry>(sp =>
        {
            var reg = new TurnDeciderRegistry(new HumanTurnDecider());
            reg.Register(ActorKind.Human, new HumanTurnDecider());
            reg.Register(ActorKind.Bot, new RandomBotDecider(sp.GetRequiredService<IRandom>()));
            return reg;
        });

        return services;
    }
}