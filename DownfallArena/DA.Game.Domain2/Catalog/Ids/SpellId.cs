namespace DA.Game.Domain2.Catalog.Ids;

public readonly record struct SpellId(string Name)
{
    public static SpellId New(string name) => new(name);
    public override string ToString() => $"{Name}";
}