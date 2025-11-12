namespace DA.Game.Domain2.Catalog.Ids;

public readonly record struct SpellId(string Name, string Version)
{
    public static SpellId New(string name, string version) => new(name, version);
    public override string ToString() => $"{Name}:{Version}".ToString();
}