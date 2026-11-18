import { readFile, readdir } from 'node:fs/promises';
import { join } from 'node:path';

// What the effects bench reads of the content for each spell, when the bundle is built (`build.mjs`): the name a
// player reads (`data/Spells/**`), the mutations that teach it (`data/Tiers`), and whether a creature starts with it
// (`data/Creatures`). The spell art holds ids only, and an id's slug is not always the name (`spell:heavy_strike:v1`
// is Strike). The laboratory's bundle alone carries this; the live page names no spell.
async function documents(folder) {
  const found = [];
  for (const entry of await readdir(folder, { withFileTypes: true, recursive: true })) {
    if (entry.isFile() && entry.name.endsWith('.json')) found.push(JSON.parse(await readFile(join(entry.parentPath, entry.name), 'utf8')));
  }
  return found;
}

export async function benchSpells(data = '../data') {
  const aliases = JSON.parse(await readFile(join(data, 'aliases.json'), 'utf8'));
  const resolve = id => aliases[id] ?? id;
  const spells = {};
  for (const { id, name } of await documents(join(data, 'Spells'))) {
    if (typeof id === 'string' && typeof name === 'string') spells[id] = { name, mutations: [], starting: false };
  }
  for (const tier of await documents(join(data, 'Tiers'))) {
    for (const id of tier.spells ?? []) spells[resolve(id)]?.mutations.push({ name: tier.name, level: tier.level });
  }
  for (const creature of await documents(join(data, 'Creatures'))) {
    for (const id of creature.startingSpellIds ?? []) if (spells[resolve(id)]) spells[resolve(id)].starting = true;
  }
  for (const spell of Object.values(spells)) spell.mutations.sort((one, other) => one.level - other.level || one.name.localeCompare(other.name, 'en'));
  return spells;
}
