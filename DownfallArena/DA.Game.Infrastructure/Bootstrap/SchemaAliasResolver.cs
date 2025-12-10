using DA.Game.Shared.Contracts.Resources.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Infrastructure.Bootstrap;

public sealed class SchemaAliasResolver : IIdAliasResolver
{
    private readonly GameSchema _schema;

    public SchemaAliasResolver(GameSchema schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public string Resolve(string rawId)
    {
        ArgumentNullException.ThrowIfNull(rawId);

        // Already versioned? Just return as-is.
        if (rawId.Contains(":v", StringComparison.InvariantCultureIgnoreCase))
            return rawId;

        if (_schema.Aliases is null ||
            !_schema.Aliases.TryGetValue(rawId, out var concrete))
        {
            throw new KeyNotFoundException($"Alias not found: {rawId}");
        }

        return concrete;
    }
}
