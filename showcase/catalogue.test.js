import assert from 'node:assert/strict';
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test, { after } from 'node:test';
import { loadCatalogue } from './catalogue.mjs';
import { english } from './copy.mjs';
import { guidePage } from './guide.mjs';
import { specimenPath } from './lore.mjs';
import { observe } from './observation.mjs';

// A copy of the content the build reads, where a test may add a version without touching data/.
const source = fileURLToPath(new URL('../data/', import.meta.url));
const copies = mkdtempSync(join(tmpdir(), 'showcase-content-'));
after(() => rmSync(copies, { recursive: true, force: true }));
let made = 0;
function content() {
  const folder = join(copies, String(made++)) + '/';
  for (const part of ['Tiers', 'Spells', 'Creatures', 'presentation', 'aliases.json']) cpSync(join(source, part), join(folder, part), { recursive: true });
  return folder;
}
const readJson = path => JSON.parse(readFileSync(path, 'utf8'));
const writeJson = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2));

test('a mutation shows the version of a spell it names, not the last one on disk', async () => {
  const folder = content();
  const first = readJson(join(folder, 'Spells/base/heavy_strike.v1.json'));
  writeJson(join(folder, 'Spells/base/heavy_strike.v2.json'), { ...first, id: 'spell:heavy_strike:v2', energyCost: first.energyCost + 7 });
  const lich = readJson(join(folder, 'Tiers/lich.v1.json'));
  const named = readJson(join(folder, `Spells/sorcerer/necromancer/${lich.spells[0].split(':')[1]}.v1.json`));
  writeJson(join(folder, 'Spells/zz-later.v2.json'), { ...named, id: named.id.replace(':v1', ':v2'), energyCost: named.energyCost + 5 });

  const catalogue = await loadCatalogue(folder);

  const spell = catalogue.nodes.find(t => t.id === 'tier:lich:v1').spells[0];
  assert.equal(spell.id, lich.spells[0]);
  assert.equal(spell.cost, named.energyCost);
  // The creature names its starting spells by alias, which aliases.json pins to v1.
  assert.equal(catalogue.baseSpells.find(s => s.id.startsWith('spell:heavy_strike:')).cost, first.energyCost);
});

test('a mutation naming a disabled version of a spell fails the build by name', async () => {
  const folder = content();
  const lich = readJson(join(folder, 'Tiers/lich.v1.json'));
  writeJson(join(folder, 'Tiers/lich.v1.json'), { ...lich, spells: [lich.spells[0].replace(':v1', ':v9')] });

  await assert.rejects(loadCatalogue(folder), /Missing playable spell spell:[a-z_]+:v9/);
});

test('the observation follows a mutation that moved to v2', async () => {
  const folder = content();
  const occultist = readJson(join(folder, 'Tiers/occultist.v1.json'));
  rmSync(join(folder, 'Tiers/occultist.v1.json'));
  writeJson(join(folder, 'Tiers/occultist.v2.json'), { ...occultist, id: 'tier:occultist:v2' });
  const necromancer = readJson(join(folder, 'Tiers/necromancer.v1.json'));
  writeJson(join(folder, 'Tiers/necromancer.v1.json'), { ...necromancer, prerequisites: ['tier:occultist:v2'] });
  const art = readJson(join(folder, 'presentation/mutation-art.json'));
  art['tier:occultist:v2'] = art['tier:occultist:v1'];
  delete art['tier:occultist:v1'];
  writeJson(join(folder, 'presentation/mutation-art.json'), art);

  const { owned, specimen } = observe(await loadCatalogue(folder), specimenPath);

  assert.equal(owned[0], 'tier:occultist:v2');
  assert.equal(specimen.length, specimenPath.length + 1);
});

test('an observation step no enabled mutation answers fails the build by name', async () => {
  const catalogue = await loadCatalogue();
  assert.throws(() => observe(catalogue, ['occultist', 'nobody']), /Unreachable observation step nobody/);
  assert.throws(() => observe(catalogue, ['lich']), /Unreachable observation step lich/);
});

for (const [level, word] of [[1, 'root'], [4, 'capstone']]) {
  test(`a family without its ${word} fails the guide by name`, async () => {
    const catalogue = await loadCatalogue();
    const nodes = catalogue.nodes.filter(t => !(t.visual.family === 'warped' && t.level === level));

    assert.throws(() => guidePage(english, { ...catalogue, nodes }, 'https://play.example/'), new RegExp(`No ${word} for family warped`));
  });
}

test('the guide writes every level in Roman numerals', async () => {
  const html = guidePage(english, await loadCatalogue(), 'https://play.example/');
  const numerals = [...html.matchAll(/<span class="roman">([^<]*)<\/span>/g)].map(m => m[1]);
  assert.deepEqual([...new Set(numerals)].sort(), ['I', 'II', 'III', 'IV']);
});

