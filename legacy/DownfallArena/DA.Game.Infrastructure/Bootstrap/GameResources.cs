using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;

namespace DA.Game.Infrastructure.Bootstrap;

/// <summary>
/// Représente un ensemble immuable des ressources de jeu (spells et characters)
/// chargées en mémoire lors du runtime. Sert de référence pour instancier les matchs.
/// </summary>
public sealed class GameResources : IGameResources
{
    private readonly IReadOnlyDictionary<SpellId, Spell> _spells;
    private readonly IReadOnlyDictionary<CreatureDefId, CreatureDefinitionRef> _characters;
    private readonly IReadOnlyDictionary<TalentTreeId, TalentTree> _talentTrees;

    public IReadOnlyList<Spell> Spells => _spells.Values.ToList();
    public IReadOnlyList<CreatureDefinitionRef> Creatures => _characters.Values.ToList();
    public IReadOnlyList<TalentTree> TalentTrees => _talentTrees.Values.ToList();
    public string Version { get; }

    private GameResources(
        IReadOnlyDictionary<SpellId, Spell> spells,
        IReadOnlyDictionary<CreatureDefId, CreatureDefinitionRef> characters,
        IReadOnlyDictionary<TalentTreeId, TalentTree> talentTrees,
        string version)
    {
        _spells = spells;
        _characters = characters;
        _talentTrees = talentTrees;
        Version = version;
    }

    /// <summary>
    /// Crée une instance immuable à partir de collections sources.
    /// </summary>
    public static GameResources Create(
        IEnumerable<Spell> spells,
        IEnumerable<CreatureDefinitionRef> characters,
        IEnumerable<TalentTree> talentTrees,
        string version)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(talentTrees);
        ArgumentException.ThrowIfNullOrEmpty(version);

        var dictSpells = spells.ToDictionary(s => s.Id);
        var dictChars = characters.ToDictionary(c => c.Id);
        var dictTalentTrees = talentTrees.ToDictionary(t => t.Id);

        return new GameResources(dictSpells, dictChars, dictTalentTrees, version);
    }

    public Spell GetSpell(SpellId id)
        => _spells.TryGetValue(id, out var spell)
            ? spell
            : throw new KeyNotFoundException($"Spell '{id}' introuvable (version {Version}).");

    public CreatureDefinitionRef GetCreature(CreatureDefId id)
        => _characters.TryGetValue(id, out var def)
            ? def
            : throw new KeyNotFoundException($"Character '{id}' introuvable (version {Version}).");

    public bool TryGetSpell(SpellId id, out Spell? spell)
        => _spells.TryGetValue(id, out spell);

    public bool TryGetCreature(CreatureDefId id, out CreatureDefinitionRef? def)
        => _characters.TryGetValue(id, out def);

    public bool TryGetTalentTree(TalentTreeId id, out TalentTree? talentTree)
        => _talentTrees.TryGetValue(id, out talentTree);
}
