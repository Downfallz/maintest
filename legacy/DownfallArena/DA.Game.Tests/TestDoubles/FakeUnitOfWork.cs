using DA.Game.Application.Shared.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Tests.TestDoubles;
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
