import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fieldNotes, spellNamesFr } from './field-notes.mjs';

const data = fileURLToPath(new URL('../data/', import.meta.url));
const read = async file => JSON.parse(await readFile(join(data, file), 'utf8'));
const shortId = id => id.split(':')[1];
const versionless = id => id.split(':').slice(0, 2).join(':');

async function entries(directory) {
  const files = await readdir(join(data, directory), { recursive: true });
  return Promise.all(files.filter(file => file.endsWith('.json')).sort().map(file => read(join(directory, file))));
}

export async function loadCatalogue() {
  const [tiers, spells, artwork, creature] = await Promise.all([
    entries('Tiers'), entries('Spells'), read('presentation/mutation-art.json'), read('Creatures/main.v1.json'),
  ]);
  const available = new Map(spells.filter(s => s.enabled !== false).map(s => [versionless(s.id), s]));
  const spell = id => {
    const s = available.get(versionless(id));
    if (!s) throw new Error(`Missing playable spell ${id}`);
    return { id: s.id, name: s.name, fr: spellNamesFr[shortId(s.id)] ?? s.name,
      cost: s.energyCost, target: s.targeting.origin, count: s.targeting.maxTargets, effects: s.effects, casterEffects: s.casterEffects ?? [] };
  };
  const nodes = tiers.filter(t => t.enabled !== false).map(t => {
    const notes = fieldNotes[shortId(t.id)];
    if (!notes || !artwork[t.id]) throw new Error(`Missing field entry for ${t.id}`);
    return { id: t.id, name: t.name, fr: notes[0], level: t.level, prerequisites: t.prerequisites,
      anyOf: t.anyOf ?? [], visual: artwork[t.id], spells: t.spells.map(spell), notes: { en: notes[1], fr: notes[2] },
      passive: t.passive, shared: t.sharedWithTeam === true };
  });
  return { nodes, baseSpells: creature.startingSpellIds.map(spell) };
}
