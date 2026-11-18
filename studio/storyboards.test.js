// A storyboard (docs/art/storyboards/, BC3 of docs/art/battle-craft-plan.md) is what a session builds an effect
// from, and what the `battle-designer` reads a film strip against. It is held to what it leans on: every pattern
// it names is an entry of the craft book, every spell it stages is an entry of the spell art, and every beat
// fits inside the window it is placed in (the craft book's rule 2), on the timings the view plays.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { headingAnchors } from '../scripts/markdown-anchors.mjs';

import { WINDUP_MS, IMPACT_MS, SETTLE_MS } from '../table/battle-timing.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const FOLDER = 'docs/art/storyboards';
const BOOK = 'docs/art/battle-craft.md';
const read = path => readFileSync(join(root, path), 'utf8');

// The parts of a storyboard, in order, each a `###` heading (docs/art/storyboards/README.md).
const PARTS = ['Intent', 'Today', 'Beats', 'Reduced motion', 'Cost', 'On the film strip', 'Left'];
// The columns of the table of beats.
const COLUMNS = ['At', 'Lasts', 'Window', 'Layer', 'Beat', 'Pattern'];

/** The windows of one action at 1×, in milliseconds from its start: { name: [from, to] }. */
export function windows() {
  return {
    windup: [0, WINDUP_MS],
    impact: [WINDUP_MS, WINDUP_MS + IMPACT_MS],
    settle: [WINDUP_MS + IMPACT_MS, WINDUP_MS + IMPACT_MS + SETTLE_MS],
  };
}

/** A storyboard file cut at its `##` headings: [{ title, parts: [{ title, body }] }]. */
export function storyboards(markdown) {
  const found = [];
  for (const line of markdown.split('\n')) {
    const board = /^## (.+)$/.exec(line);
    const part = /^### (.+)$/.exec(line);
    if (board) found.push({ title: board[1].trim(), parts: [] });
    else if (part && found.length > 0) found.at(-1).parts.push({ title: part[1].trim(), body: '' });
    else if (found.at(-1)?.parts.length > 0) found.at(-1).parts.at(-1).body += `${line}\n`;
  }
  return found;
}

/** The table rows of a part, as objects keyed by the header's cells. A part holds one table. */
export function tableRows(body) {
  const lines = body.split('\n').filter(line => line.trim().startsWith('|'));
  const cells = line => line.trim().replace(/^\||\|$/g, '').split('|').map(cell => cell.trim());
  if (lines.length < 2) return { header: [], rows: [] };
  const header = cells(lines[0]);
  const rows = lines.slice(2).map(line => Object.fromEntries(cells(line).map((cell, at) => [header[at], cell])));
  return { header, rows };
}

/** What is wrong with one beat, as sentences; none when it fits its window and names a pattern of the book. */
export function beatProblems(row, bookAnchors, spans = windows()) {
  const problems = [];
  const whole = cell => /^\d+$/.test(cell ?? '');
  const at = Number(row.At), lasts = Number(row.Lasts);
  const span = Object.hasOwn(spans, row.Window ?? '') ? spans[row.Window] : null;
  if (!whole(row.At) || !whole(row.Lasts) || lasts <= 0) problems.push('At and Lasts are whole milliseconds');
  if (!span) problems.push(`the window is one of ${Object.keys(spans).join(', ')}`);
  else if (at < span[0] || at + lasts > span[1]) problems.push(`${at} to ${at + lasts} ms leaves the ${row.Window} (${span[0]} to ${span[1]} ms)`);
  const links = [...(row.Pattern ?? '').matchAll(/\]\(([^)#]*)#([^)]+)\)/g)];
  if (links.length === 0) problems.push('the pattern is a link to its entry in the craft book');
  if (links.length > 2) problems.push('a beat names two patterns at most: split it into rows');
  for (const [, file, anchor] of links) {
    if (file !== '../battle-craft.md') problems.push(`the pattern links ../battle-craft.md, not ${file}`);
    else if (!bookAnchors.has(anchor)) problems.push(`the craft book has no entry #${anchor}`);
  }
  if (!row.Layer || !row.Beat) problems.push('a beat names its layer and what it shows');
  return problems;
}

/** The name a player reads for each spell, from the authored files: { id: name }. */
function spellNames(folder = join(root, 'data/Spells')) {
  const names = {};
  for (const entry of readdirSync(folder, { withFileTypes: true })) {
    if (entry.isDirectory()) Object.assign(names, spellNames(join(folder, entry.name)));
    else if (entry.name.endsWith('.json')) {
      const spell = JSON.parse(readFileSync(join(folder, entry.name), 'utf8'));
      names[spell.id] = spell.name;
    }
  }
  return names;
}

const files = () => readdirSync(join(root, FOLDER)).filter(name => name.endsWith('.md') && name !== 'README.md').sort();

test('a beat is refused when it leaves its window, names no pattern, or names one the book does not hold', () => {
  const anchors = new Set(['hit-stop']);
  const spans = { windup: [0, 840], impact: [840, 2540] };
  const beat = { At: '840', Lasts: '60', Window: 'impact', Layer: 'figures', Beat: 'Both freeze.', Pattern: '[Hit stop](../battle-craft.md#hit-stop)' };
  assert.deepEqual(beatProblems(beat, anchors, spans), []);
  assert.match(beatProblems({ ...beat, At: '800' }, anchors, spans)[0], /leaves the impact/);
  assert.match(beatProblems({ ...beat, Lasts: '1701' }, anchors, spans)[0], /leaves the impact/);
  assert.match(beatProblems({ ...beat, Window: 'later' }, anchors, spans)[0], /the window is one of/);
  assert.match(beatProblems({ ...beat, Window: 'constructor' }, anchors, spans)[0], /the window is one of/);
  for (const cell of ['a while', '', '0x3c', '60.5', '0']) {
    assert.match(beatProblems({ ...beat, Lasts: cell }, anchors, spans)[0], /whole milliseconds/, `Lasts "${cell}"`);
  }
  assert.match(beatProblems({ ...beat, At: '' }, anchors, spans)[0], /whole milliseconds/);
  assert.match(beatProblems({ ...beat, Pattern: 'Hit stop' }, anchors, spans)[0], /a link to its entry/);
  const three = Array(3).fill(beat.Pattern).join(', ');
  assert.match(beatProblems({ ...beat, Pattern: three }, anchors, spans)[0], /two patterns at most/);
  assert.match(beatProblems({ ...beat, Pattern: '[Glow](../battle-craft.md#glow)' }, anchors, spans)[0], /no entry #glow/);
  assert.match(beatProblems({ ...beat, Pattern: '[Hit stop](other.md#hit-stop)' }, anchors, spans)[0], /not other\.md/);
  assert.match(beatProblems({ ...beat, Layer: '' }, anchors, spans)[0], /names its layer/);
});

test('a storyboard file is cut at its headings and its table read by its header', () => {
  const markdown = '# Family\nintro\n## Strike (`spell:a:v1`)\n### Beats\n\n| At | Lasts |\n| --- | --- |\n| 0 | 240 |\n| 240 | 600 |\n### Cost\nNone.\n## Other\n';
  const [first, second] = storyboards(markdown);
  assert.equal(first.title, 'Strike (`spell:a:v1`)');
  assert.deepEqual(first.parts.map(part => part.title), ['Beats', 'Cost']);
  assert.deepEqual(tableRows(first.parts[0].body), { header: ['At', 'Lasts'], rows: [{ At: '0', Lasts: '240' }, { At: '240', Lasts: '600' }] });
  assert.deepEqual(second.parts, []);
});

test('the base attack has its storyboard, the worked example', () => {
  assert.ok(files().length > 0, `${FOLDER} holds a storyboard`);
  const titles = files().flatMap(name => storyboards(read(`${FOLDER}/${name}`)).map(board => board.title));
  assert.ok(titles.some(title => title.includes('`spell:heavy_strike:v1`')), 'spell:heavy_strike:v1 is staged');
});

test('every storyboard names what it stages and has its seven parts, in order', () => {
  const art = JSON.parse(read('data/presentation/spell-art.json'));
  const names = spellNames();
  const staged = new Set();
  for (const name of files()) {
    const boards = storyboards(read(`${FOLDER}/${name}`));
    assert.ok(boards.length > 0, `${name} holds a storyboard`);
    for (const board of boards) {
      const where = `${name}, ${board.title}`;
      for (const [, id] of board.title.matchAll(/`(spell:[^`]+)`/g)) {
        assert.ok(Object.hasOwn(art, id), `${where}: ${id} is an entry of the spell art`);
        // The id is an old slug ("heavy_strike" is Strike): the title is the name the content gives the spell.
        assert.ok(board.title.startsWith(`${names[id]} (`), `${where}: ${id} is titled ${names[id]}, the name of its file`);
        assert.ok(!staged.has(id), `${where}: ${id} has one storyboard`);
        staged.add(id);
      }
      assert.deepEqual(board.parts.map(part => part.title), PARTS, where);
      for (const part of board.parts) assert.ok(part.body.trim().length > 0, `${where}: ${part.title} says something`);
    }
  }
});

test('every beat fits its window on the timings the view plays, and names a pattern of the craft book', () => {
  const anchors = headingAnchors(read(BOOK));
  for (const name of files()) {
    for (const board of storyboards(read(`${FOLDER}/${name}`))) {
      const { header, rows } = tableRows(board.parts.find(part => part.title === 'Beats')?.body ?? '');
      assert.deepEqual(header, COLUMNS, `${name}, ${board.title}`);
      assert.ok(rows.length > 0, `${name}, ${board.title}: at least one beat`);
      for (const row of rows) assert.deepEqual(beatProblems(row, anchors), [], `${name}, ${board.title}, the beat at ${row.At} ms`);
    }
  }
});

test('every link from a storyboard to the craft book lands on a heading', () => {
  const anchors = headingAnchors(read(BOOK));
  for (const name of [...files(), 'README.md']) {
    for (const [, anchor] of read(`${FOLDER}/${name}`).matchAll(/\]\(\.\.\/battle-craft\.md#([^)]+)\)/g)) {
      assert.ok(anchors.has(anchor), `${name}: the craft book has no heading #${anchor}`);
    }
  }
});

test('the storyboards are listed where a session looks for them', () => {
  const index = read(`${FOLDER}/README.md`);
  for (const name of files()) assert.ok(index.includes(`(${name})`), `${FOLDER}/README.md lists ${name}`);
  for (const part of PARTS) assert.ok(index.includes(`**${part}**`), `the format names the part ${part}`);
  // The format states the windows in figures: a change to the timings must reach that sentence.
  const prose = index.replace(/\s+/g, ' ');
  for (const [name, [from, to]] of Object.entries(windows())) {
    assert.ok(prose.includes(`the ${name} runs from ${from} to ${to} ms`), `the format gives the ${name} as ${from} to ${to} ms`);
  }
  assert.ok(read('docs/README.md').includes('(art/storyboards/README.md)'), 'docs/README.md lists the storyboards');
});
