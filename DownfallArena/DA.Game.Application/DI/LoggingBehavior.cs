using DA.Game.Shared.Utilities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DA.Game.Application.DI
{
    public sealed class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("➡️ Handling {RequestName} : {@Request}", requestName, request);

#pragma warning disable CA1062 // Validate arguments of public methods
                var response = await next(cancellationToken);
#pragma warning restore CA1062 // Validate arguments of public methods

                stopwatch.Stop();

                _logger.LogInformation("✅ Completed {RequestName} in {Elapsed}ms",
                    requestName, stopwatch.ElapsedMilliseconds);

                // Si c’est un Result<T>, log les erreurs s’il y en a
                if (response is Result r && !r.IsSuccess)
                    _logger.LogWarning("⚠️ {RequestName} failed: {Error}", requestName, r.Error);

                return response;
            }
            catch
            {
                stopwatch.Stop();
                throw;
            }
        }
    }
}
