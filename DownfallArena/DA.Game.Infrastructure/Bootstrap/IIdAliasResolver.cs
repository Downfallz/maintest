namespace DA.Game.Infrastructure.Bootstrap;

// Simple abstraction for id alias resolution (spell:foo -> spell:foo:v1)
public interface IIdAliasResolver
{
    string Resolve(string rawId);
}
