using DA.Game.Shared.Utilities;
using FluentValidation;
using MediatR;

namespace DA.Game.Application.DI;

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
#pragma warning disable CA1062 // Validate arguments of public methods
            return await next(cancellationToken); // aucune règle, on continue
#pragma warning restore CA1062 // Validate arguments of public methods

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = results.SelectMany(r => r.Errors)
                              .Where(f => f != null)
                              .ToList();

        if (failures.Count != 0)
        {
            // si ton app utilise Result<T> :
            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var fail = typeof(Result<>)
                    .MakeGenericType(typeof(TResponse).GetGenericArguments()[0])
                    .GetMethod("Fail", new[] { typeof(string) })!
                    .Invoke(null, new object[] { string.Join("; ", failures.Select(f => f.ErrorMessage)) });

                return (TResponse)fail!;
            }

            // sinon, lance une exception standard (souvent mieux pour API)
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
