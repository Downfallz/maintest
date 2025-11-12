using DA.Game.Data;

public sealed class BattleSimulator
{
    private readonly GameSchema _schema;
    private readonly IReadOnlyDictionary<string, SpellDef> _spellIndex;

    public BattleSimulator(GameSchema schema)
    {
        _schema = schema;
        _spellIndex = SchemaLoader.BuildSpellIndex(schema);
    }

    public void Cast(string caster, string spellRequestedIdOrBase)
    {
        var spellId = SchemaLoader.ResolveAlias(_schema, spellRequestedIdOrBase);
        var spell = _spellIndex[spellId];

        // Exemple ultra simple : applique le premier effet Damage s’il existe
        var dmg = spell.Effects.FirstOrDefault(e => e.Type == "Damage")?.Value ?? 0;
        Console.WriteLine($"{caster} casts {spell.Name} for {dmg} damage (Init {spell.Initiative}, Cost {spell.EnergyCost})");
    }
}