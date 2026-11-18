// The craft book (docs/art/battle-craft.md) is what every session that stages an effect reads first. Most
// Phaser examples on the web are for Phaser 3, and Phaser 4 renamed or removed much of it, so the book is held
// to the engine the battle installs: every engine name it writes exists in those types, and every name it
// lists as gone is gone. The book's own shape is held too, so an entry cannot lose its source or its
// reduced-motion form.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { declares, indexDeclarations, installedIndex } from '../scripts/battle/phaser-symbols.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const BOOK = 'docs/art/battle-craft.md';
const read = path => readFileSync(join(root, path), 'utf8');

// The groups the plan asks the book to cover (docs/art/battle-craft-plan.md, "The craft book").
const GROUPS = ['Timing', 'The body', 'The camera', 'Particles', 'Light and surface', 'Projectiles', 'The ground', 'Reading'];
// The six lines of an entry, in order.
const LINES = ['What', 'Source', 'Phaser', 'Cost', 'Reduced motion', 'Here'];
const GONE = 'Phaser 3 names that are gone';
// Phaser 3 idioms a search result carries, which no qualified name would catch: an instance property or a
// method written bare.
const PHASER_3_IDIOMS = ['preFX', 'postFX', 'setPipeline', 'createEmitter', 'setTintFill', 'setMask'];

/** The book cut at its `##` headings: { title, body, entries: [{ title, body }] }. */
function sections(markdown) {
  const found = [];
  for (const line of markdown.split('\n')) {
    const section = /^## (.+)$/.exec(line);
    const entry = /^### (.+)$/.exec(line);
    if (section) found.push({ title: section[1].trim(), body: '', entries: [] });
    else if (entry && found.length > 0) found.at(-1).entries.push({ title: entry[1].trim(), body: '' });
    else if (found.length > 0) {
      const target = found.at(-1).entries.at(-1) ?? found.at(-1);
      target.body += `${line}\n`;
    }
  }
  return found;
}

/**
 * The engine names a text writes in backticks: `Phaser.Namespace.Class#member`, with or without a call after.
 * A span that starts with a dot (`.lifespan`) continues the full name before it, as a key of that type or as a
 * key beside that one: it is returned as the candidates it may stand for, and one of them must exist.
 */
function engineNames(text) {
  const names = [];
  let last = null;
  for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
    const name = /^Phaser(?:[.#][A-Za-z_$][\w$]*)+/.exec(span);
    const key = /^\.([A-Za-z_$][\w$]*)$/.exec(span);
    if (name) {
      last = name[0];
      names.push([last]);
    } else if (key && last) {
      names.push([`${last}.${key[1]}`, `${last.replace(/[.#][^.#]+$/, '')}.${key[1]}`]);
    }
  }
  return names;
}

const written = candidates => candidates[0];
const found = (index, candidates) => candidates.some(name => declares(index, name));

/** An entry's labelled lines: `- **What**: ...` up to the next label, as { label, text }. */
function labelled(body) {
  const lines = [];
  for (const line of body.split('\n')) {
    const label = /^- \*\*([^*]+)\*\*: (.*)$/.exec(line);
    if (label) lines.push({ label: label[1], text: label[2] });
    else if (lines.length > 0 && /^ {2}\S/.test(line)) lines.at(-1).text += ` ${line.trim()}`;
  }
  return lines;
}

const book = read(BOOK);
const cut = sections(book);
const groups = cut.filter(section => GROUPS.includes(section.title));
const gone = cut.find(section => section.title === GONE);
const living = cut.filter(section => section !== gone).map(section => [section.body, ...section.entries.map(entry => entry.body)].join('\n')).join('\n');

// The light build installs no battle package, and runs only for Markdown this test does not read
// (studio/browser/plan.js keeps the book out of it): nothing to check there. Anywhere else, missing types are a
// failure, not a pass.
const index = installedIndex();
const light = process.env.LIGHT_BUILD === 'true';
const engine = { skip: !index && light ? 'the light build installs no Phaser, and the book is unchanged in it' : false };
const INSTALL = 'The Phaser types are not installed: run `npm --prefix battle ci --ignore-scripts` first.';

test('the reader of the declarations finds classes, members, inherited members, enums and configuration keys', () => {
  const sample = [
    'declare namespace Phaser {',
    '    /**',
    '     * class Ghost { is only a comment }',
    '     */',
    '    enum BlendModes {',
    '        NORMAL,',
    '        ADD,',
    '    }',
    '    namespace GameObjects {',
    '        class GameObject extends Phaser.Events.EventEmitter {',
    '            constructor(scene: Phaser.Scene);',
    '            destroy(fromScene?: boolean): void;',
    '        }',
    '        class Image extends Phaser.GameObjects.GameObject implements Phaser.GameObjects.Components.Tint {',
    '            readonly tintMode: number;',
    '            static RENDER_MASK: number;',
    '            setTint(color?: number): this;',
    '        }',
    '    }',
    '    namespace Types {',
    '        type Config = {',
    '            lifespan?: number;',
    '            bounds?: {',
    '                inner: number;',
    '            };',
    '        };',
    '        type Callback = (value: number)=>void;',
    '    }',
    '    namespace Math {',
    '        function Clamp(value: number): number;',
    '        const TAU: number;',
    '    }',
    '}',
  ].join('\n');
  const sampled = indexDeclarations(sample);
  for (const name of ['Phaser.BlendModes.ADD', 'Phaser.GameObjects.Image', 'Phaser.GameObjects.Image#setTint', 'Phaser.GameObjects.Image#tintMode',
    'Phaser.GameObjects.Image.RENDER_MASK', 'Phaser.GameObjects.Image#destroy', 'Phaser.Types.Config.lifespan', 'Phaser.Types.Callback', 'Phaser.Math.Clamp', 'Phaser.Math.TAU']) {
    assert.ok(declares(sampled, name), name);
  }
  for (const name of ['Phaser.Ghost', 'Phaser.GameObjects.Image#constructor', 'Phaser.GameObjects.Image#explode', 'Phaser.GameObjects.GameObject#setTint',
    'Phaser.Types.Config.inner', 'Phaser.Math.PI2', 'Phaser.Geom.Point']) {
    assert.ok(!declares(sampled, name), name);
  }
});

test('the command says yes or no for each name, and exits 1 when one is missing and 2 with no name', engine, () => {
  assert.ok(index, INSTALL);
  const run = (...names) => spawnSync(process.execPath, [join(root, 'scripts/battle/phaser-symbols.mjs'), ...names], { encoding: 'utf8' });
  const present = run('Phaser.Cameras.Scene2D.Camera#shake', 'Phaser.Math.Vector2');
  assert.equal(present.status, 0, present.stderr);
  assert.equal(present.stdout, 'yes  Phaser.Cameras.Scene2D.Camera#shake\nyes  Phaser.Math.Vector2\n');
  const mixed = run('Phaser.Math.Vector2', 'Phaser.Geom.Point');
  assert.equal(mixed.status, 1);
  assert.equal(mixed.stdout, 'yes  Phaser.Math.Vector2\nno   Phaser.Geom.Point\n');
  const none = run();
  assert.equal(none.status, 2);
  assert.equal(none.stdout, '');
  assert.match(none.stderr, /^usage: /);
});

test('the book covers the eight groups of the plan, each with entries', () => {
  assert.deepEqual(groups.map(group => group.title), GROUPS);
  for (const group of groups) assert.ok(group.entries.length > 0, `${group.title} has no entry`);
});

test('every entry has the same six lines, a linked source and an engine name', () => {
  for (const group of groups) {
    for (const entry of group.entries) {
      const lines = labelled(entry.body);
      const where = `${group.title} / ${entry.title}`;
      assert.deepEqual(lines.map(line => line.label), LINES, where);
      for (const line of lines) assert.ok(line.text.trim().length > 0, `${where}: ${line.label} is empty`);
      const [, source, phaser] = lines;
      assert.match(source.text, /\]\(https:\/\/[^)\s]+\)/, `${where}: the source has no link`);
      assert.ok(engineNames(phaser.text).length > 0, `${where}: the Phaser line names no engine symbol`);
    }
  }
});

test('every engine name the book writes exists in the installed Phaser', engine, () => {
  assert.ok(index, INSTALL);
  const names = engineNames(living);
  const distinct = new Set(names.map(written));
  assert.ok(distinct.size >= 40, `the book names only ${distinct.size} engine symbols: is the convention still \`Phaser.…\` in backticks?`);
  const missing = [...new Set(names.filter(candidates => !found(index, candidates)).map(written))];
  assert.deepEqual(missing, [], `not in battle/node_modules/phaser/types: ${missing.join(', ')}`);
});

test('every Phaser 3 name the book lists as gone is gone from the installed Phaser', engine, () => {
  assert.ok(index, INSTALL);
  assert.ok(gone, `the book has no "${GONE}" section`);
  const names = engineNames(gone.body.split('\n').map(row => row.split('|')[1] ?? '').join('\n'));
  assert.ok(names.length >= 5, 'the gone table lists fewer than five names');
  const back = names.filter(candidates => found(index, candidates)).map(written);
  assert.deepEqual(back, [], `listed as gone, yet declared: ${back.join(', ')}`);
});

test('the book writes no Phaser 3 idiom outside the sections that warn about them', () => {
  const warned = new Set([GONE, 'Declared, and dead']);
  for (const section of cut.filter(candidate => !warned.has(candidate.title))) {
    const text = [section.body, ...section.entries.map(entry => entry.body)].join('\n');
    for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
      const idiom = PHASER_3_IDIOMS.find(word => new RegExp(String.raw`(^|[.#\s])${word}\b`).test(span));
      assert.equal(idiom, undefined, `${section.title}: \`${span}\` is Phaser 3`);
    }
  }
});

test('the documentation index lists the book', () => {
  assert.match(read('docs/README.md'), /^\| \[art\/battle-craft\.md\]\(art\/battle-craft\.md\) \| .+ \|$/m);
});
