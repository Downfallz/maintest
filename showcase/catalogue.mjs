import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fieldNotes, spellNamesFr } from './field-notes.mjs';

const content = fileURLToPath(new URL('../data/', import.meta.url));
const shortId = id => id.split(':')[1];

// The guide anchors each entry by its short id (form-lich, spell-drain), and the field notes are keyed by it. Two
// versions of one mutation, or of one spell the guide shows, would share that anchor, and links and search would
// reach only the first. The catalogue refuses them, as the observation refuses an ambiguous step.
function refuseTwoVersions(kind, ids) {
  const byShort = new Map();
  for (const id of new Set(ids)) byShort.set(shortId(id), [...byShort.get(shortId(id)) ?? [], id]);
  for (const [short, versions] of byShort) {
    if (versions.length > 1) throw new Error(`Two enabled versions of ${kind} ${short}: ${versions.sort().join(', ')}`);
  }
}

// The content folder is data/ unless a test names a copy. A reference is a versioned id or, like a creature's
// starting spells, an alias that aliases.json pins to one (ADR 0009). As in the content builder's AliasResolver, a
// versioned id is taken as written; the guide shows the version a reference names.
export async function loadCatalogue(data = content) {
  const read = async file => JSON.parse(await readFile(join(data, file), 'utf8'));
  const entries = async directory => {
    const files = await readdir(join(data, directory), { recursive: true });
    return Promise.all(files.filter(file => file.endsWith('.json')).sort().map(file => read(join(directory, file))));
  };
  const [tiers, spells, artwork, spellArt, creature, aliases] = await Promise.all([
    entries('Tiers'), entries('Spells'), read('presentation/mutation-art.json'), read('presentation/spell-art.json'),
    read('Creatures/main.v1.json'), read('aliases.json'),
  ]);
  const available = new Map(spells.filter(s => s.enabled !== false).map(s => [s.id, s]));
  const spell = reference => {
    const id = /:v\d+$/.test(reference) ? reference : aliases[reference] ?? reference, s = available.get(id);
    if (!s) throw new Error(`Missing playable spell ${id}`);
    // The portrait the battle draws beside the spell's name (docs/art/spell-vfx.md#portraits), when it has one.
    return { id: s.id, name: s.name, fr: spellNamesFr[shortId(s.id)] ?? s.name, portrait: spellArt[s.id]?.portrait ?? null,
      cost: s.energyCost, critical: s.criticalChance, target: s.targeting.origin, count: s.targeting.maxTargets, effects: s.effects, casterEffects: s.casterEffects ?? [] };
  };
  const enabled = tiers.filter(t => t.enabled !== false);
  // Before the field entries, so a second version without its artwork is told the real problem.
  refuseTwoVersions('tier', enabled.map(t => t.id));
  const nodes = enabled.map(t => {
    const notes = fieldNotes[shortId(t.id)];
    if (!notes || !artwork[t.id]) throw new Error(`Missing field entry for ${t.id}`);
    return { id: t.id, name: t.name, fr: notes[0], level: t.level, initiative: t.initiativeBonus, prerequisites: t.prerequisites,
      anyOf: t.anyOf ?? [], visual: artwork[t.id], spells: t.spells.map(spell), notes: { en: notes[1], fr: notes[2] },
      passive: t.passive, shared: t.sharedWithTeam === true };
  });
  const baseSpells = creature.startingSpellIds.map(spell);
  refuseTwoVersions('spell', [...baseSpells, ...nodes.flatMap(t => t.spells)].map(s => s.id));
  return { nodes, baseSpells };
}
