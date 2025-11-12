using DA.Game.Application.Matches.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Infrastructure.Bootstrap
{
    public interface IGameBootstrapper { Task StartAsync(CancellationToken ct); }

    public sealed class GameBootstrapper(
        IGameResourcesProvider provider,
        IGameCatalogCache cache
    ) : IGameBootstrapper
    {
        public async Task StartAsync(CancellationToken ct)
        {
            var dto = await provider.LoadAsync(ct);
            var snapshot = MapToSnapshot(dto); // mapping -> Domain defs immuables
            cache.Set(snapshot);
        }
    }

}
