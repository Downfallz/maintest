// The pages' craft book (docs/art/ux-craft.md, UX1 of docs/art/ux-craft-plan.md) is what a session that restyles
// a page, and the `ux-designer` that briefs one, read first. Its shape is held, so an entry cannot lose its
// source, its cost or its form under less motion, and so is what it leans on in the repository: every path it
// names exists and every token it names is declared. Whether a link still says what its entry says cannot be
// asked from a test: the book's own head says when each was last read.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const BOOK = 'docs/art/ux-craft.md';
const NOTES = 'docs/art/ux-craft-research.md';
const read = path => readFileSync(join(root, path), 'utf8');

// The groups the plan asks the book to cover (docs/art/ux-craft-plan.md, "The craft book").
const GROUPS = ['The first visit', 'Teaching', 'The home', "The match's frame", 'Motion', 'Pictures', 'Accessibility'];
// The six lines of an entry, in order.
const LINES = ['What', 'Source', 'Pages', 'Cost', 'Less motion and screen reader', 'Here'];
// Where a token may be declared: the palette both doors read, and the table's identity sheet (ADR 0158).
const TOKEN_SHEETS = ['docs/art/branding.css', 'table/identity.css'];
// Paths the book may name before they exist: what a later stage of the plan builds, and build output.
const NOT_YET_THERE = [
  'scripts/ux/', // the journey film, UX2
  'docs/art/journeys/', // the briefs and the baseline, UX2 and UX3
  '.claude/agents/ux-designer.md', // UX3
  '.claude/skills/ux-pass/', // UX3
  'showcase/dist/', // the showcase's build
  'table/battle-build/', // the battle bundle
];

/** The book cut at its `##` headings: { title, body, entries: [{ title, body }] }. */
export function sections(markdown) {
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

/** An entry's labelled lines: `- **What**: ...` up to the next label, as { label, text }. */
export function labelled(body) {
  const lines = [];
  for (const line of body.split('\n')) {
    const label = /^- \*\*([^*]+)\*\*: (.*)$/.exec(line);
    if (label) lines.push({ label: label[1], text: label[2] });
    else if (lines.length > 0 && /^ {2}\S/.test(line)) lines.at(-1).text += ` ${line.trim()}`;
  }
  return lines;
}

/** The repository paths a text names in backticks: a top-level entry, with a slash or a file extension. */
export function pathsNamed(text, topLevel) {
  const paths = [];
  for (const [, span] of text.matchAll(/`([^`\s]+)`/g)) {
    // A placeholder or a glob (`/j/<code>`, `table/*.css`) names a shape, not a path.
    if (/[<>*{}$?]/.test(span)) continue;
    const path = span.replace(/[.,:;)]+$/, '').replace(/#.*$/, '');
    if (!topLevel.has(path.split('/')[0])) continue;
    if (!path.includes('/') && !/\.\w+$/.test(path)) continue;
    paths.push(path);
  }
  return paths;
}

/** The custom properties a text names in backticks, alone or inside `var(...)`: `--gold`, `var(--cut)`. */
export function tokensNamed(text) {
  const tokens = [];
  for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
    const token = /^(?:var\()?(--[a-z][a-z0-9-]*)\)?$/.exec(span);
    if (token) tokens.push(token[1]);
  }
  return tokens;
}

/** The custom properties a sheet declares. */
export function tokensDeclared(css) {
  return new Set([...css.matchAll(/(?:^|[\s{;])(--[a-z][a-z0-9-]*)\s*:/g)].map(([, name]) => name));
}

const present = existsSync(join(root, BOOK));
const book = present ? read(BOOK) : '';
const cut = sections(book);
const groups = cut.filter(section => GROUPS.includes(section.title));

test('the book is cut at its headings, and an entry keeps its labelled lines with what continues them', () => {
  const sample = ['# Book', 'intro', '## A group', 'about', '### An entry', '', '- **What**: one', '  two', '- **Source**: *house*', '', 'aside', '## Other'].join('\n');
  const [group, other] = sections(sample);
  assert.equal(group.title, 'A group');
  assert.equal(group.body, 'about\n');
  assert.deepEqual(group.entries.map(entry => entry.title), ['An entry']);
  assert.deepEqual(labelled(group.entries[0].body), [{ label: 'What', text: 'one two' }, { label: 'Source', text: '*house*' }]);
  assert.deepEqual(other.entries, []);
});

test('a span is read as a path only when it starts at the root and is not a placeholder or a glob', () => {
  const top = new Set(['table', 'docs', 'AGENTS.md']);
  const text = 'See `table/lobby.html`, `docs/art/branding.md#palette`, `AGENTS.md`. Not `table/*.css`, `/j/<code>`, `lobby.css`, `transform`, `table`.';
  assert.deepEqual(pathsNamed(text, top), ['table/lobby.html', 'docs/art/branding.md', 'AGENTS.md']);
});

test('a token is read alone or inside var(), and a declaration is read from a sheet', () => {
  assert.deepEqual(tokensNamed('`--gold`, `var(--cut)`, `--Not`, `transform`, `color: var(--ink)`'), ['--gold', '--cut']);
  assert.deepEqual([...tokensDeclared(':root {\n  --gold: #fff; --cut:12px;\n}\na { color: var(--ink); }')], ['--gold', '--cut']);
});

test('the book covers the seven groups of the plan, in its order, each with entries', () => {
  assert.ok(present, `${BOOK} is not written`);
  assert.deepEqual(groups.map(group => group.title), GROUPS);
  for (const group of groups) assert.ok(group.entries.length > 0, `${group.title} has no entry`);
});

test('every entry has the same six lines, and a source that is a link or marked house', () => {
  assert.ok(groups.length > 0, `${BOOK} has no group`);
  for (const group of groups) {
    for (const entry of group.entries) {
      const lines = labelled(entry.body);
      const where = `${group.title} / ${entry.title}`;
      assert.deepEqual(lines.map(line => line.label), LINES, where);
      for (const line of lines) assert.ok(line.text.trim().length > 0, `${where}: ${line.label} is empty`);
      const source = lines[1].text;
      assert.ok(/\]\(https:\/\/[^)\s]+\)/.test(source) || source.includes('*house*'), `${where}: the source is neither a link nor marked *house*`);
    }
  }
});

test('no entry is named twice, so a brief can name a pattern by its heading', () => {
  const titles = groups.flatMap(group => group.entries.map(entry => entry.title));
  const twice = titles.filter((title, at) => titles.indexOf(title) !== at);
  assert.deepEqual(twice, []);
});

test('every repository path the book names exists', () => {
  const topLevel = new Set(readdirSync(root));
  const missing = [...new Set(pathsNamed(book, topLevel))]
    .filter(path => !NOT_YET_THERE.some(prefix => path.startsWith(prefix)))
    .filter(path => !existsSync(join(root, path)));
  assert.deepEqual(missing, [], `named in ${BOOK}, and not in the repository`);
});

test('every token the book names is declared in the palette or in the identity sheet', () => {
  const declared = new Set(TOKEN_SHEETS.flatMap(sheet => [...tokensDeclared(read(sheet))]));
  const missing = [...new Set(tokensNamed(book))].filter(token => !declared.has(token));
  assert.deepEqual(missing, [], `named in ${BOOK}, and declared in neither ${TOKEN_SHEETS.join(' nor ')}`);
});

test('the documentation index lists the book, and the notes it replaces are gone from the repository', () => {
  assert.match(read('docs/README.md'), /^\| \[art\/ux-craft\.md\]\(art\/ux-craft\.md\) \| .+ \|$/m);
  assert.ok(!existsSync(join(root, NOTES)), `${NOTES} is still there: the book holds its entries`);
  for (const path of ['docs/README.md', 'docs/art/ux-craft-plan.md']) {
    assert.ok(!read(path).includes('ux-craft-research'), `${path} still links to the notes`);
  }
});
