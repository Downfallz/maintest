using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Shared.Primitives;

public sealed class NotSpecification<T> : ISpecification<T>
{
    private readonly ISpecification<T> _inner;

    public NotSpecification(ISpecification<T> inner)
        => _inner = inner;

    public bool IsSatisfiedBy(T candidate)
        => !_inner.IsSatisfiedBy(candidate);
}

