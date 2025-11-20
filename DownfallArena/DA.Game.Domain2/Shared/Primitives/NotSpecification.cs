namespace DA.Game.Domain2.Shared.Primitives;

public sealed class NotSpecification<T> : ISpecification<T>
{
    private readonly ISpecification<T> _inner;

    public NotSpecification(ISpecification<T> inner)
        => _inner = inner;

    public bool IsSatisfiedBy(T candidate)
        => !_inner.IsSatisfiedBy(candidate);
}

