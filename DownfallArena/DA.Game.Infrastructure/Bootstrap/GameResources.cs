using DA.Game.Domain2.Catalog.Ids;

namespace DA.Game.Domain2.Matches.Resources;

/// <summary>
/// Représente un ensemble immuable des ressources de jeu (spells et characters)
/// chargées en mémoire lors du runtime. Sert de référence pour instancier les matchs.
/// </summary>
public sealed class GameResources : IGameResources
{
    private readonly IReadOnlyDictionary<SpellId, SpellRef> _spells;
    private readonly IReadOnlyDictionary<CharacterDefId, CharacterDefinitionRef> _characters;

    public IReadOnlyList<SpellRef> Spells => _spells.Values.ToList();
    public IReadOnlyList<CharacterDefinitionRef> Characters => _characters.Values.ToList();
    public string Version { get; }

    private GameResources(
        IReadOnlyDictionary<SpellId, SpellRef> spells,
        IReadOnlyDictionary<CharacterDefId, CharacterDefinitionRef> characters,
        string version)
    {
        _spells = spells;
        _characters = characters;
        Version = version;
    }

    /// <summary>
    /// Crée une instance immuable à partir de collections sources.
    /// </summary>
    public static GameResources Create(
        IEnumerable<SpellRef> spells,
        IEnumerable<CharacterDefinitionRef> characters,
        string version)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentException.ThrowIfNullOrEmpty(version);

        var dictSpells = spells.ToDictionary(s => s.Id);
        var dictChars = characters.ToDictionary(c => c.Id);

        return new GameResources(dictSpells, dictChars, version);
    }

    public SpellRef GetSpell(SpellId id)
        => _spells.TryGetValue(id, out var spell)
            ? spell
            : throw new KeyNotFoundException($"Spell '{id}' introuvable (version {Version}).");

    public CharacterDefinitionRef GetCharacter(CharacterDefId id)
        => _characters.TryGetValue(id, out var def)
            ? def
            : throw new KeyNotFoundException($"Character '{id}' introuvable (version {Version}).");

    public bool TryGetSpell(SpellId id, out SpellRef? spell)
        => _spells.TryGetValue(id, out spell);

    public bool TryGetCharacter(CharacterDefId id, out CharacterDefinitionRef? def)
        => _characters.TryGetValue(id, out def);
}
