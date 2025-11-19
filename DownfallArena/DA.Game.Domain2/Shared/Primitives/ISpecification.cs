namespace DA.Game.Domain2.Shared.Primitives;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T candidate);

    ISpecification<T> And(ISpecification<T> other)
        => new AndSpecification<T>(this, other);

    ISpecification<T> Or(ISpecification<T> other)
        => new OrSpecification<T>(this, other);

    ISpecification<T> Not()
        => new NotSpecification<T>(this);
}

