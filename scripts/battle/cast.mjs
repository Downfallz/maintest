// Whom the bench plays a spell on, read from the content: the acceptance of a staging (its browser spec, its
// frame time) plays it on as many figures as the spell may reach, since a staging costs by the figure.

import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

function spells(root) {
  return readdirSync(join(root, 'data/Spells'), { withFileTypes: true, recursive: true })
    .filter(entry => entry.isFile() && entry.name.endsWith('.json'))
    .map(entry => JSON.parse(readFileSync(join(entry.parentPath, entry.name), 'utf8')));
}

/** A function from a spell's id to the fixture's creatures it is cast on, by `a1`: itself, one ally, or as many enemies as it may reach. */
export function castTargets(root) {
  const targeting = Object.fromEntries(spells(root).map(spell => [spell.id, spell.targeting ?? {}]));
  return id => {
    const { origin, maxTargets = 1 } = targeting[id] ?? {};
    if (origin === 'Self') return ['a1'];
    if (origin === 'Ally') return ['a2'];
    return ['b1', 'b2', 'b3'].slice(0, maxTargets);
  };
}

/** Each spell's name as its file gives it, by id. */
export function spellNames(root) {
  return Object.fromEntries(spells(root).map(spell => [spell.id, spell.name]));
}
