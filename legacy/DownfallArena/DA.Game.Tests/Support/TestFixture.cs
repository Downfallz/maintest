using DA.Game.Application;
using DA.Game.Application.DI;
using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Players.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Infrastructure.Matches;
using DA.Game.Infrastructure.Players;
using DA.Game.Infrastructure.Shared;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Utilities;
using DA.Game.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DA.Game.Tests.Support;

public sealed class TestFixture : IDisposable
{

    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public IServiceProvider Provider => _scope.ServiceProvider;
    public TestFixture()
    {
        var services = new ServiceCollection();
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

        // Shared (Application)
        services.AddSingleton<IClock, FakeClock>();
        services.AddSingleton<IRandom, FixedRandom>();
        services.AddScoped<IEventBus, MediatorEventBus>();

        // Repos InMemory
        services.AddScoped<IMatchRepository, InMemoryMatchRepository>();
        services.AddScoped<IPlayerRepository, InMemoryPlayerRepository>();
        services.AddScoped<IPlayerUniqueness, InMemoryPlayerUniqueness>();

        // UoW
        services.AddScoped<IAggregateTracker, AggregateTracker>();
        services.AddScoped<IApplicationEventCollector, ApplicationEventCollector>();
        services.AddScoped<IUnitOfWork, FakeUnitOfWork>();

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

        _root = services.BuildServiceProvider(validateScopes: true);
        _scope = _root.CreateScope();  // ✅ crée un scope
    
    }

    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        _root.Dispose();
    }
}

