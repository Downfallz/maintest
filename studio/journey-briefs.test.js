// A journey's brief (docs/art/journeys/, UX3 of docs/art/ux-craft-plan.md) is what a session builds a page from,
// and what the `ux-designer` reads a journey's sheet and measures against. Its shape is held, so a screen cannot
// lose its states, its form under less motion or its cost, and so is what it leans on: every screen it names is
// one the journey's walk films, every pattern an entry of the pages' craft book, every token a declared one,
// every painting a file. Whether a brief is a good one cannot be asked from a test: the owner reads the sheets.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { headingAnchors } from '../scripts/markdown-anchors.mjs';
import { JOURNEYS } from '../scripts/ux/journeys.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const FOLDER = 'docs/art/journeys';
const BOOK = 'docs/art/ux-craft.md';
// The pages that are in the folder and are not a brief: the format, and the walk before any pass.
const NOT_A_BRIEF = new Set(['README.md', 'baseline.md', 'acceptance.md']);
// Where a token may be declared, as for the craft book (studio/ux-craft.test.js).
const TOKEN_SHEETS = ['docs/art/branding.css', 'table/table.css', 'table/identity.css'];
// What a brief may name before it exists: the walk's sheets and measures, written and never committed.
const NOT_YET_THERE = ['artifacts/'];
const read = path => readFileSync(join(root, path), 'utf8');

// The nine parts of a screen's brief, in order, each a `###` heading (docs/art/journeys/README.md).
export const PARTS = ['Job', 'Words', 'Pictures', 'States', 'Motion', 'Less motion', 'Accessibility', 'Cost', 'Left'];
// The lines a part must hold, each written `- **Label**: ...`, in order.
export const LINES = {
  Job: ['Filled action'],
  Words: ['Order', 'Budget'],
  States: ['First paint', 'Empty', 'Loaded', 'Waiting', 'Error', 'Offline'],
  Accessibility: ['Focus', 'Names', 'Announced', 'Targets'],
  Cost: ['Bytes', 'Largest paint', 'Shift'],
};
// The columns of a part's table. Pictures, Motion and Less motion hold one, or open on "None" and say why.
export const COLUMNS = {
  Pictures: ['Picture', 'Drawn at', 'Answers', 'From'],
  Motion: ['Beat', 'When', 'Moves', 'Pattern', 'Token'],
  'Less motion': ['Beat', 'Form'],
};
// The only properties a beat may move (the craft book, "Transform and opacity only").
const MOVES = new Set(['transform', 'opacity']);
// A picture the library lacks, and a token the kit has not declared yet, are said in these words.
const ASKED = 'asked of the owner';
// The marker names the kit, not a stage: the pass that needs the value adds the token (ADR 0197, #957).
const NO_TOKEN_YET = /^\*Kit\*:\s*\S/;

/** A brief cut at its headings: { opening, screens: [{ title, parts: [{ title, body }] }] }. */
export function brief(markdown) {
  const cut = { opening: '', screens: [] };
  let fenced = false;
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) fenced = !fenced;
    const screen = fenced ? null : /^## (.+)$/.exec(line);
    const part = fenced ? null : /^### (.+)$/.exec(line);
    const last = cut.screens.at(-1);
    if (screen) cut.screens.push({ title: screen[1].trim(), parts: [] });
    else if (part && last) last.parts.push({ title: part[1].trim(), body: '' });
    else if (last?.parts.length > 0) last.parts.at(-1).body += `${line}\n`;
    else if (!last) cut.opening += `${line}\n`;
  }
  return cut;
}

/** A part's labelled lines: `- **Label**: ...` with what continues it, as { label, text }. */
export function labelled(body) {
  const lines = [];
  for (const line of body.split('\n')) {
    const label = /^- \*\*([^*]+)\*\*: (.*)$/.exec(line);
    if (label) lines.push({ label: label[1], text: label[2] });
    else if (lines.length > 0 && /^ {2}\S/.test(line)) lines.at(-1).text += ` ${line.trim()}`;
    else if (line.trim() === '' || !/^ /.test(line)) lines.push(null);
  }
  return lines.filter(Boolean);
}

/** The rows of a part's one table, keyed by its header's cells. */
export function tableRows(body) {
  const lines = body.split('\n').filter(line => line.trim().startsWith('|'));
  const cells = line => line.trim().replace(/^\||\|$/g, '').split('|').map(cell => cell.trim());
  if (lines.length < 2) return { header: [], rows: [] };
  const header = cells(lines[0]);
  return { header, rows: lines.slice(2).map(line => Object.fromEntries(cells(line).map((cell, at) => [header[at], cell]))) };
}

/** The screens a heading names, in backticks: `sign-in-panel`. */
export const screensNamed = title => [...title.matchAll(/`([a-z0-9]+(?:-[a-z0-9]+)*)`/g)].map(([, id]) => id);

const none = body => /^None\b/.test(body.trim());
const blocks = body => Math.floor(body.split('\n').filter(line => line.startsWith('```')).length / 2);

/**
 * What is wrong with one screen's brief, as sentences; none when it holds its format. `known` is what the brief
 * leans on: { screens: the journey's, anchors: the craft book's, tokens: the declared ones, exists: path => bool }.
 */
export function screenProblems(screen, known) {
  const problems = [];
  const ids = screensNamed(screen.title);
  if (ids.length === 0 && !/\(new\)$/.test(screen.title)) problems.push('the heading names its screens in backticks, or ends with (new)');
  for (const id of ids) if (!known.screens.includes(id)) problems.push(`the journey's walk films no screen ${id}`);

  const titles = screen.parts.map(part => part.title);
  if (JSON.stringify(titles) !== JSON.stringify(PARTS)) return [...problems, `its parts are ${PARTS.join(', ')}, in this order: it has ${titles.join(', ') || 'none'}`];
  const part = Object.fromEntries(screen.parts.map(({ title, body }) => [title, body]));
  for (const title of PARTS) if (part[title].trim() === '') problems.push(`${title} says nothing`);

  for (const [title, labels] of Object.entries(LINES)) {
    const lines = labelled(part[title]);
    const found = lines.map(line => line.label).filter(label => labels.includes(label));
    if (JSON.stringify(found) !== JSON.stringify(labels)) problems.push(`${title} holds the lines ${labels.join(', ')}, in this order`);
    for (const line of lines) if (line.text.trim() === '') problems.push(`${title}: ${line.label} is empty`);
  }
  const budget = labelled(part.Words).find(line => line.label === 'Budget');
  if (budget && !/^\d+ words\b/.test(budget.text)) problems.push('Words: the budget opens on a whole number of words, as in "40 words"');

  const tables = {};
  for (const [title, columns] of Object.entries(COLUMNS)) {
    tables[title] = tableRows(part[title]);
    if (none(part[title])) {
      if (tables[title].rows.length > 0) problems.push(`${title} opens on None and holds a table`);
      continue;
    }
    if (JSON.stringify(tables[title].header) !== JSON.stringify(columns)) problems.push(`${title} holds one table of ${columns.join(', ')}, or opens on None and says why`);
    else if (tables[title].rows.length === 0) problems.push(`${title}: the table has no row`);
    else for (const row of tables[title].rows) for (const column of columns) if (!row[column]) problems.push(`${title}: a row has no ${column}`);
  }

  let asked = 0;
  for (const row of tables.Pictures.rows) {
    const name = row.Picture ?? 'a picture';
    if (!/^\d+ by \d+ px\b/.test(row['Drawn at'] ?? '')) problems.push(`Pictures, ${name}: drawn at a width by a height, as in "160 by 107 px"`);
    const from = row.From ?? '';
    const path = /`([^`\s]+)`/.exec(from)?.[1];
    if (from.startsWith(ASKED)) asked += 1;
    else if (!path) problems.push(`Pictures, ${name}: from a file in backticks, or "${ASKED}"`);
    else if (!known.exists(path)) problems.push(`Pictures, ${name}: ${path} is not in the repository`);
  }
  if (asked > blocks(part.Pictures)) problems.push(`Pictures: ${asked} asked of the owner, each with its prompt to paste in a fenced block`);

  for (const row of tables.Motion.rows) {
    const name = row.Beat ?? 'a beat';
    const moves = (row.Moves ?? '').split(',').map(move => move.trim().replaceAll('`', ''));
    if (moves.some(move => !MOVES.has(move))) problems.push(`Motion, ${name}: a beat moves transform, opacity or both`);
    const links = [...(row.Pattern ?? '').matchAll(/\]\(([^)#]*)(?:#([^)]*))?\)/g)];
    if (links.length === 0) problems.push(`Motion, ${name}: the pattern is a link to its entry in the craft book`);
    if (links.length > 2) problems.push(`Motion, ${name}: a beat names two patterns at most`);
    for (const [, file, anchor] of links) {
      if (file !== '../ux-craft.md') problems.push(`Motion, ${name}: the pattern links ../ux-craft.md, not ${file}`);
      else if (!anchor) problems.push(`Motion, ${name}: the pattern links an entry of the craft book, by its anchor`);
      else if (!known.anchors.has(anchor)) problems.push(`Motion, ${name}: the craft book has no entry #${anchor}`);
    }
    const tokens = [...(row.Token ?? '').matchAll(/`(--[a-z][a-z0-9-]*)`/g)].map(([, token]) => token);
    if (tokens.length === 0 && !NO_TOKEN_YET.test(row.Token ?? '')) problems.push(`Motion, ${name}: a token in backticks, or *Kit*: and what the kit must declare`);
    for (const token of tokens) if (!known.tokens.has(token)) problems.push(`Motion, ${name}: ${token} is declared in no sheet`);
  }
  const beats = rows => rows.map(row => row.Beat).sort();
  if (JSON.stringify(beats(tables.Motion.rows)) !== JSON.stringify(beats(tables['Less motion'].rows))) {
    problems.push('Less motion gives a form to each beat of Motion, by the same name, and to nothing else');
  }
  return problems;
}

/** The repository paths a text names in backticks: a top-level entry, with a slash or a file extension. */
function pathsNamed(text, topLevel) {
  const paths = [];
  for (const [, span] of text.matchAll(/`([^`\s]+)`/g)) {
    if (/[<>*{}$?]/.test(span)) continue;
    const path = span.replace(/[.,:;)]+$/, '').replace(/#.*$/, '');
    if (!topLevel.has(path.split('/')[0])) continue;
    if (!path.includes('/') && !/\.\w+$/.test(path)) continue;
    paths.push(path);
  }
  return paths;
}

const tokensDeclared = () => new Set(TOKEN_SHEETS.flatMap(sheet => [...read(sheet).matchAll(/(?:^|[\s{;])(--[a-z][a-z0-9-]*)\s*:/g)].map(([, name]) => name)));
const files = () => (existsSync(join(root, FOLDER)) ? readdirSync(join(root, FOLDER)) : []).filter(name => name.endsWith('.md') && !NOT_A_BRIEF.has(name)).sort();
const journeyOf = name => JOURNEYS.find(journey => `${journey.id}.md` === name);

// One screen that holds the format, for the tests of the format itself.
const SAMPLE = [
  '# The lobby',
  'Opening. Not briefed: `home-menu`.',
  '## The home (`home-new`)',
  '### Job', 'To start a battle.', '', '- **Filled action**: New battle.',
  '### Words', '- **Order**: the title, the action.', '- **Budget**: 40 words, down from 191.',
  '### Pictures', '', '| Picture | Drawn at | Answers | From |', '| --- | --- | --- | --- |',
  '| A creature | 160 by 107 px | Who do I play? | `table/a.webp` |',
  '| A duel | 320 by 180 px | What is a battle? | asked of the owner |', '', '```text', 'Two creatures face each other.', '```',
  '### States', '- **First paint**: the frame.', '- **Empty**: a sentence', '  and a link.', '- **Loaded**: the lists.',
  '- **Waiting**: a mark.', '- **Error**: a line.', '- **Offline**: the offline page.',
  '### Motion', '', '| Beat | When | Moves | Pattern | Token |', '| --- | --- | --- | --- | --- |',
  '| The press | On a press | `transform` | [Answer](../ux-craft.md#answer) | `--motion-fast` |',
  '| The window | It opens | `transform`, `opacity` | [A window](../ux-craft.md#a-window) | *Kit*: a duration of 240 ms |',
  '### Less motion', '', '| Beat | Form |', '| --- | --- |', '| The window | A fade. |', '| The press | The same. |',
  '### Accessibility', '- **Focus**: the action first.', '- **Names**: the action says New battle.', '- **Announced**: nothing.', '- **Targets**: 44 px.',
  '### Cost', '- **Bytes**: one painting.', '- **Largest paint**: the title.', '- **Shift**: none.',
  '### Left', 'Nothing.',
].join('\n');
const KNOWN = { screens: ['home-new', 'home-menu'], anchors: new Set(['answer', 'a-window']), tokens: new Set(['--motion-fast']), exists: path => path === 'table/a.webp' };
const changed = (from, to) => {
  assert.ok(SAMPLE.includes(from), `the sample holds "${from}"`);
  return screenProblems(brief(SAMPLE.replace(from, to)).screens[0], KNOWN);
};

test('a brief is cut at its headings, and a heading inside a prompt to paste is left in its part', () => {
  const cut = brief(SAMPLE.replace('Two creatures face each other.', '## Not a screen'));
  assert.match(cut.opening, /Not briefed: `home-menu`/);
  assert.deepEqual(cut.screens.map(screen => screen.title), ['The home (`home-new`)']);
  assert.deepEqual(cut.screens[0].parts.map(part => part.title), PARTS);
  assert.deepEqual(screensNamed('The panel (`sign-in-panel`, `invitation-sign-in`)'), ['sign-in-panel', 'invitation-sign-in']);
  assert.deepEqual(labelled('- **Empty**: a sentence\n  and a link.\n\naside\n- **Loaded**: the lists.'), [{ label: 'Empty', text: 'a sentence and a link.' }, { label: 'Loaded', text: 'the lists.' }]);
});

test('a screen that holds the format is accepted', () => {
  assert.deepEqual(screenProblems(brief(SAMPLE).screens[0], KNOWN), []);
  assert.deepEqual(changed('## The home (`home-new`)', '## The first battle (new)'), []);
  const still = SAMPLE.replace(/### Motion[\s\S]*?### Accessibility/, '### Motion\nNone: the screen is read, nothing answers.\n### Less motion\nNone.\n### Accessibility');
  assert.deepEqual(screenProblems(brief(still).screens[0], KNOWN), []);
});

test('a screen is refused when it names a screen the walk does not film, or loses a part, a line or its order', () => {
  assert.match(changed('(`home-new`)', '(`home-old`)')[0], /films no screen home-old/);
  assert.match(changed('## The home (`home-new`)', '## The home')[0], /in backticks, or ends with \(new\)/);
  assert.match(changed('### Left', '### Later')[0], /its parts are Job, Words/);
  assert.match(changed('### Left\nNothing.', '### Left\n')[0], /Left says nothing/);
  assert.match(changed('- **Filled action**: New battle.', 'The action is New battle.')[0], /Job holds the lines Filled action/);
  assert.match(changed('- **Waiting**: a mark.\n', '')[0], /States holds the lines First paint, Empty, Loaded, Waiting, Error, Offline/);
  assert.match(changed('- **Shift**: none.', '- **Shift**: ')[0], /Cost: Shift is empty/);
  assert.match(changed('- **Budget**: 40 words', '- **Budget**: fewer words')[0], /a whole number of words/);
});

test('a picture is refused without its size, a file that exists, or the prompt for one asked of the owner', () => {
  assert.match(changed('160 by 107 px', 'small')[0], /drawn at a width by a height/);
  assert.match(changed('`table/a.webp`', '`table/b.webp`')[0], /table\/b\.webp is not in the repository/);
  assert.match(changed('`table/a.webp`', 'the library')[0], /from a file in backticks, or "asked of the owner"/);
  assert.match(changed('```text\nTwo creatures face each other.\n```\n', '')[0], /each with its prompt to paste/);
  assert.match(changed('| Picture | Drawn at | Answers | From |', '| Picture | Size | Answers | From |')[0], /one table of Picture, Drawn at, Answers, From/);
});

test('a beat is refused when it moves a layout, names no pattern of the book, no declared token, or has no form under less motion', () => {
  assert.match(changed('| `transform` |', '| `height` |')[0], /moves transform, opacity or both/);
  assert.match(changed('[Answer](../ux-craft.md#answer)', 'Answer')[0], /a link to its entry/);
  assert.match(changed('../ux-craft.md#answer', '../ux-craft.md#gone')[0], /no entry #gone/);
  assert.match(changed('../ux-craft.md#answer', '../battle-craft.md#answer')[0], /not \.\.\/battle-craft\.md/);
  assert.match(changed('| `--motion-fast` |', '| `--motion-quick` |')[0], /--motion-quick is declared in no sheet/);
  assert.match(changed('*Kit*: a duration of 240 ms', '240 ms')[0], /a token in backticks, or \*Kit\*/);
  assert.match(changed('*Kit*: a duration of 240 ms', '*UX4*: a duration of 240 ms')[0], /a token in backticks, or \*Kit\*/);
  assert.match(changed('*Kit*: a duration of 240 ms', '*Kit*')[0], /a token in backticks, or \*Kit\*/);
  assert.match(changed('[Answer](../ux-craft.md#answer)', '[Answer](../ux-craft.md#answer), [Other](../battle-craft.md)')[0], /not \.\.\/battle-craft\.md/);
  assert.match(changed('[Answer](../ux-craft.md#answer)', '[Answer](../ux-craft.md)')[0], /by its anchor/);
  assert.match(changed('| The window | A fade. |\n', '')[0], /gives a form to each beat of Motion/);
  assert.match(changed('| The window | A fade. |', '| The sheet | A fade. |')[0], /gives a form to each beat of Motion/);
  assert.match(changed('| The press | The same. |', '| The press | |')[0], /a row has no Form/);
});

test('the two doors have their brief, the worked example', () => {
  assert.ok(files().includes('doors.md'), `${FOLDER}/doors.md is not written`);
});

test('a brief is named after its journey, opens on its title, and names every screen the walk films', () => {
  for (const name of files()) {
    const journey = journeyOf(name);
    assert.ok(journey, `${name}: a brief is named after one of ${JOURNEYS.map(each => `${each.id}.md`).join(', ')}`);
    const markdown = read(`${FOLDER}/${name}`);
    assert.ok(markdown.startsWith(`# ${journey.title}\n`), `${name} opens on "# ${journey.title}"`);
    const cut = brief(markdown);
    assert.ok(cut.screens.length > 0, `${name} briefs a screen`);
    const briefed = cut.screens.flatMap(screen => screensNamed(screen.title));
    assert.deepEqual(briefed.filter((id, at) => briefed.indexOf(id) !== at), [], `${name}: a screen has one brief`);
    // A screen the brief leaves out is named in its opening, with why: none is left out by forgetting it.
    const missing = journey.screens.filter(id => !briefed.includes(id) && !cut.opening.includes(`\`${id}\``));
    assert.deepEqual(missing, [], `${name}: filmed by the walk, and neither briefed nor named in the opening`);
  }
});

test('every screen of every brief holds the nine parts and what each must say', () => {
  const anchors = headingAnchors(read(BOOK));
  const tokens = tokensDeclared();
  for (const name of files()) {
    const known = { screens: journeyOf(name)?.screens ?? [], anchors, tokens, exists: path => existsSync(join(root, path)) };
    for (const screen of brief(read(`${FOLDER}/${name}`)).screens) {
      assert.deepEqual(screenProblems(screen, known), [], `${name}, ${screen.title}`);
    }
  }
});

test('every path a brief or the format names exists, and every link to the craft book lands on a heading', () => {
  const topLevel = new Set(readdirSync(root));
  const anchors = headingAnchors(read(BOOK));
  for (const name of [...files(), 'README.md']) {
    const markdown = read(`${FOLDER}/${name}`);
    const missing = [...new Set(pathsNamed(markdown, topLevel))]
      .filter(path => !NOT_YET_THERE.some(prefix => path.startsWith(prefix)) && !existsSync(join(root, path)));
    assert.deepEqual(missing, [], `named in ${name}, and not in the repository`);
    for (const [, anchor] of markdown.matchAll(/\]\(\.\.\/ux-craft\.md#([^)]+)\)/g)) {
      assert.ok(anchors.has(anchor), `${name}: the craft book has no heading #${anchor}`);
    }
  }
});

test('the format names every part, line and column the test holds, and the briefs are listed where a session looks', () => {
  const index = read(`${FOLDER}/README.md`);
  for (const name of files()) assert.ok(index.includes(`(${name})`), `${FOLDER}/README.md lists ${name}`);
  for (const part of PARTS) assert.ok(index.includes(`**${part}**`), `the format names the part ${part}`);
  for (const label of Object.values(LINES).flat()) assert.ok(index.includes(`**${label}**`), `the format names the line ${label}`);
  for (const columns of Object.values(COLUMNS)) assert.ok(index.includes(`| ${columns.join(' | ')} |`), `the format shows the table of ${columns.join(', ')}`);
  assert.match(read('docs/README.md'), /^\| \[art\/journeys\/README\.md\]\(art\/journeys\/README\.md\) \| .+ \|$/m);
});

test('the designer and the pass exist, and each names the format, the book and the walk', () => {
  for (const prompt of ['.claude/agents/ux-designer.md', '.claude/skills/ux-pass/SKILL.md']) {
    assert.ok(existsSync(join(root, prompt)), `${prompt} is not written`);
    for (const path of [`${FOLDER}/README.md`, BOOK, 'scripts/ux/journey.mjs']) assert.ok(read(prompt).includes(`\`${path}\``), `${prompt} names ${path}`);
  }
});
