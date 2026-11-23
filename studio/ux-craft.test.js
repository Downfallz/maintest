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

import { headingAnchors } from '../scripts/markdown-anchors.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const BOOK = 'docs/art/ux-craft.md';
const NOTES = 'docs/art/ux-craft-research.md';
const read = path => readFileSync(join(root, path), 'utf8');

// The groups the plan asks the book to cover (docs/art/ux-craft-plan.md, "The craft book").
const GROUPS = ['The first visit', 'Teaching', 'The home', "The match's frame", 'Motion', 'Pictures', 'Accessibility'];
// The six lines of an entry, in order.
const LINES = ['What', 'Source', 'Pages', 'Cost', 'Less motion and screen reader', 'Here'];
// Where a token may be declared: the palette both doors read, the sheet every page of the table loads, which
// holds the motion tokens, and the table's identity sheet (ADR 0158).
const TOKEN_SHEETS = ['docs/art/branding.css', 'table/table.css', 'table/identity.css'];

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

/** The custom properties a text names in backticks, wherever in the span: `--gold`, `color: var(--ink)`. */
export function tokensNamed(text) {
  const tokens = [];
  for (const [, span] of text.matchAll(/`([^`\n]+)`/g)) {
    tokens.push(...[...span.matchAll(/(?<![\w-])--[a-z][a-z0-9-]*/g)].map(([name]) => name));
  }
  return tokens;
}

/** The links a text makes to a file beside it or to one of its own headings: { path, anchor }, either empty. */
export function linksNamed(text) {
  const links = [];
  for (const [, target] of text.matchAll(/\]\(([^)\s]+)\)/g)) {
    if (/^[a-z][a-z0-9+.-]*:/i.test(target)) continue;
    const [path, anchor = ''] = target.split('#');
    links.push({ path, anchor });
  }
  return links;
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

test('a token is read wherever its span holds it, and a declaration is read from a sheet', () => {
  assert.deepEqual(tokensNamed('`--gold`, `var(--cut)`, `--Not`, `transform`, `color: var(--ink)`, `a--b`, --bare'), ['--gold', '--cut', '--ink']);
  assert.deepEqual([...tokensDeclared(':root {\n  --gold: #fff; --cut:12px;\n}\na { color: var(--ink); }')], ['--gold', '--cut']);
});

test('a link is read to a file beside the book or to a heading, and a link to the web is left alone', () => {
  assert.deepEqual(linksNamed('[a](plan.md), [b](#a-heading), [c](other.md#part), [d](https://example.org/x#y), [e](mailto:x@y.z)'), [
    { path: 'plan.md', anchor: '' },
    { path: '', anchor: 'a-heading' },
    { path: 'other.md', anchor: 'part' },
  ]);
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
      assert.ok(/\]\(https:\/\/[^)\s]+\)/.test(source) || source.startsWith('*house*'), `${where}: the source has no link and does not start with *house*`);
    }
  }
});

// A brief names a pattern by its anchor, and GitHub numbers the second of two headings that give the same one.
test('no two headings of the book give the same anchor, so a brief can name a pattern by it', () => {
  const anchors = book.split('\n').filter(line => /^#{1,6}\s+\S/.test(line)).map(line => [...headingAnchors(line)][0]);
  const twice = anchors.filter((anchor, at) => anchors.indexOf(anchor) !== at);
  assert.deepEqual(twice, []);
});

test('every link the book makes to a file beside it, or to a heading, leads somewhere', () => {
  const folder = dirname(BOOK);
  const broken = linksNamed(book).filter(({ path, anchor }) => {
    if (path && !existsSync(join(root, folder, path))) return true;
    if (!anchor) return false;
    return !headingAnchors(path ? read(join(folder, path)) : book).has(anchor);
  });
  assert.deepEqual(broken, []);
});

test('every repository path the book names exists', () => {
  const topLevel = new Set(readdirSync(root));
  const missing = [...new Set(pathsNamed(book, topLevel))].filter(path => !existsSync(join(root, path)));
  assert.deepEqual(missing, [], `named in ${BOOK}, and not in the repository`);
});

test('every token the book names is declared in the palette, the table sheet or the identity sheet', () => {
  const declared = new Set(TOKEN_SHEETS.flatMap(sheet => [...tokensDeclared(read(sheet))]));
  const missing = [...new Set(tokensNamed(book))].filter(token => !declared.has(token));
  assert.deepEqual(missing, [], `named in ${BOOK}, and declared in none of ${TOKEN_SHEETS.join(', ')}`);
});

test('the documentation index lists the book, and the notes it replaces are gone from the repository', () => {
  assert.match(read('docs/README.md'), /^\| \[art\/ux-craft\.md\]\(art\/ux-craft\.md\) \| .+ \|$/m);
  assert.ok(!existsSync(join(root, NOTES)), `${NOTES} is still there: the book holds its entries`);
  for (const path of ['docs/README.md', 'docs/art/ux-craft-plan.md']) {
    assert.ok(!read(path).includes('ux-craft-research'), `${path} still links to the notes`);
  }
});

const entry = title => groups.flatMap(section => section.entries).find(found => found.title === title);
const lineOf = (title, label) => labelled(entry(title)?.body ?? '').find(line => line.label === label)?.text ?? '';

test('the book names the axe tag that turns the target-size rule on, and the measures run with it (#929)', async () => {
  const { AXE_TAGS } = await import('../scripts/ux/measures.mjs');
  const here = lineOf('What a test can see', 'Here');
  assert.match(here, /`target-size`[^.]*`wcag22aa`/, 'the entry names the rule and its tag');
  assert.ok(AXE_TAGS.includes('wcag22aa'), 'scripts/ux/measures.mjs runs the tag the book names');
  const unopened = cut.find(section => section.title === 'What could not be opened')?.body ?? '';
  assert.doesNotMatch(unopened, /axe's own list of tags/, 'the tags were read: they are no longer unopened');
});

test('the hint at the moment of need says what a stall is on the pages: the question still asked, a sheet closed (#981)', () => {
  const body = entry('A hint at the moment of need')?.body ?? '';
  assert.match(body, /`waitingAsked`/, 'the entry names what the stream says');
  assert.match(body, /never (?:on|by) a clock/, 'the entry says a clock never starts it');
});

test('the plan\'s walk says the classic sheet held the motion tokens and a rule for less motion (#928)', () => {
  const rows = new Map(read('docs/art/ux-craft-plan.md').split('\n')
    .map(line => /^\| ([^|]+?) \| (.+) \|$/.exec(line)).filter(Boolean).map(([, concern, holds]) => [concern, holds]));
  assert.match(rows.get('Motion on the pages') ?? '', /`--motion-[a-z]+`/, 'the row names the tokens table.css declared');
  assert.match(rows.get('Less motion') ?? '', /`prefers-reduced-motion: reduce`/, 'the row names the blanket rule');
  assert.doesNotMatch(rows.get('Less motion') ?? '', /nothing to reduce/);
});
