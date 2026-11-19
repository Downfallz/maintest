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

import { declares, extendedBy, indexDeclarations, installedIndex } from '../scripts/battle/phaser-symbols.mjs';

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
 * The engine names a text writes in backticks: `Phaser.Namespace.Class#member`, with or without a call after,
 * as { written, holders }. A span that starts with a dot (`.lifespan`) continues the full name before it: it is
 * a key of that name when the name holds members (a configuration type), or a key beside that one when the
 * name is itself a key. `holders` lists the names the key may belong to, in that order.
 */
function engineNames(text) {
  const names = [];
  let last = null;
  for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
    const name = /^Phaser(?:[.#][A-Za-z_$][\w$]*)+/.exec(span);
    const key = /^\.([A-Za-z_$][\w$]*)$/.exec(span);
    if (name) {
      last = name[0];
      names.push({ written: last });
    } else if (key && last) {
      names.push({ written: `${last}.${key[1]}`, key: key[1], holders: [last, last.replace(/[.#][^.#]+$/, '')] });
    }
  }
  return names;
}

/** A full name is declared; a continued key is declared on a name that holds members, never beside a namespace's types. */
function found(index, name) {
  if (!name.holders) return declares(index, name.written);
  return name.holders.some(holder => index.holders.has(holder) && declares(index, `${holder}.${name.key}`));
}

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

// The pages' tests need no install (ADR 0024), so without the battle's packages the engine checks are skipped,
// and say so. Where the change is judged they are never skipped: CI's full build installs the battle before
// this runs and fails here if the types are missing, and the local gate (scripts/verify.sh) installs them when
// they are. CI's light build installs nothing, and runs only for Markdown this test does not hold to the
// engine: studio/browser/plan.js keeps the book out of it.
const index = installedIndex();
const judged = process.env.CI === 'true' && process.env.LIGHT_BUILD !== 'true';
const INSTALL = 'The Phaser types are not installed: run `npm --prefix battle ci --ignore-scripts` first.';
const engine = { skip: !index && !judged ? `not checked. ${INSTALL}` : false };

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
  // A `#` names a member of something that holds members: a namespace's function is not one.
  for (const name of ['Phaser.Ghost', 'Phaser.GameObjects.Image#constructor', 'Phaser.GameObjects.Image#explode', 'Phaser.GameObjects.GameObject#setTint',
    'Phaser.Types.Config.inner', 'Phaser.Math.PI2', 'Phaser.Geom.Point', 'Phaser.Math#Clamp', 'Phaser.GameObjects#Image', 'Phaser.GameObjects.Image#setTint#x']) {
    assert.ok(!declares(sampled, name), name);
  }
});

test('what a class extends is read up to its implements or its brace, each parent without its type arguments', () => {
  assert.deepEqual(extendedBy('class Sprite extends GameObject implements Components.Alpha {'), ['GameObject']);
  assert.deepEqual(extendedBy('class Sprite extends GameObject\timplements Components.Alpha {'), ['GameObject']);
  assert.deepEqual(extendedBy('interface Both extends Map<string, number>, Phaser.Events.EventEmitter {'), ['Map', 'Phaser.Events.EventEmitter']);
  assert.deepEqual(extendedBy('class Last extends Base'), ['Base']);
  assert.equal(extendedBy('class Alone {'), null);
  assert.equal(extendedBy('class Empty extends {'), null);
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

test('the command exits 2, and names the install, when the declarations are not there', () => {
  const script = join(root, 'scripts/battle/phaser-symbols.mjs');
  const absent = spawnSync(process.execPath, [script, 'Phaser.Math.Vector2'], {
    encoding: 'utf8',
    env: { ...process.env, PHASER_TYPES_PATH: join(root, 'studio/no-such-phaser.d.ts') },
  });
  assert.equal(absent.status, 2);
  assert.equal(absent.stdout, '');
  assert.match(absent.stderr, /npm --prefix battle ci --ignore-scripts/);
});

// The engine checks read a span from its first character. A name written inside a call (`new Phaser.Geom.Point()`)
// or behind an instance (`this.cameras.main.shake`) would pass unread, so the book writes neither.
test('an engine name starts its span, and nothing reaches the engine through an instance', () => {
  for (const section of cut) {
    const text = [section.body, ...section.entries.map(entry => entry.body)].join('\n');
    for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
      assert.ok(!/.Phaser\./.test(span), `${section.title}: \`${span}\` hides an engine name the test cannot read`);
      assert.ok(!/^(this|scene|camera|cam|sprite|image|emitter|tween|gameObject)\./.test(span), `${section.title}: \`${span}\` names the engine through an instance; write the class`);
    }
  }
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
  const distinct = new Set(names.map(name => name.written));
  assert.ok(distinct.size >= 40, `the book names only ${distinct.size} engine symbols: is the convention still \`Phaser.…\` in backticks?`);
  const missing = [...new Set(names.filter(name => !found(index, name)).map(name => name.written))];
  assert.deepEqual(missing, [], `not in battle/node_modules/phaser/types: ${missing.join(', ')}`);
});

test('every Phaser 3 name the book lists as gone is gone from the installed Phaser', engine, () => {
  assert.ok(index, INSTALL);
  assert.ok(gone, `the book has no "${GONE}" section`);
  const names = engineNames(gone.body.split('\n').map(row => row.split('|')[1] ?? '').join('\n'));
  assert.ok(names.length >= 5, 'the gone table lists fewer than five names');
  const back = names.filter(name => found(index, name)).map(name => name.written);
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
