using DA.Game.Domain2.Matches.Services.Planning;
using DA.Game.Domain2.Matches.Services.Queries;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using System.Reflection;

namespace DA.Game.Application.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IGameSettings>(new GameSettings { SimulationMode = true });

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(Assembly.GetExecutingAssembly());
        });

        // Validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Behaviors (pipeline)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        // Notifications global logging
        //services.AddTransient(typeof(INotificationHandler<>), typeof(NotificationLoggingBehavior<>));
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ServiceCollectionExtensions).Assembly));

        services.Scan(selector => selector
            .FromAssemblies(Assembly.GetExecutingAssembly()) // Application assembly
            .AddClasses(filter => filter.InNamespaces("DA.Game.Application")) // only Application namespace
            .UsingRegistrationStrategy(RegistrationStrategy.Skip) // avoid duplicates
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<ITalentQueryService, TalentQueryService>();
        services.AddScoped<ITalentUnlockService, TalentUnlockService>();
        return services;
    }
}