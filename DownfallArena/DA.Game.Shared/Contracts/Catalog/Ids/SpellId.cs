namespace DA.Game.Shared.Contracts.Catalog.Ids;

public readonly record struct SpellId(string Name)
{
    public static SpellId New(string name) => new(name);
    public override string ToString() => $"{Name}";
}