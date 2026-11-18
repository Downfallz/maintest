// Which sections of the official rulebook a change reaches (docs/tabletop/rulebook-plan.md, R5), so the /rules
// skill and the rules-keeper start from the sections and not from a reading of the whole book:
//
//   git diff origin/main... | node scripts/rules/reach.mjs
//
// A section is reached by an ADR or a game-rules heading its `sources:` line cites, by a value it draws (a spell, a
// mutation, a field of the hosted rule set, a stat or the starting spells of the reference creature, an engine constant, an alias
// a list it states names: the starting spells, a mutation's spells or what it needs), by a spell or a mutation it names,
// and by a change to its own lines. A path that calls for the keeper (scripts/review/reviewers.mjs) and reaches no
// section is listed for a reading by hand: the domain, the words, the pages' rule text, a rule no section teaches.
// The French edition (rules/fr/) is read beside the English: its sections cite and draw the same, so a change
// reaches both, and the report marks the French ones [fr].

import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { reviewersFor } from '../review/reviewers.mjs';
import { ENGINE } from './build.mjs';

const RULE_SET = 'docs/tabletop/playtest.rules.json';
const CREATURE = 'data/Creatures/main.v1.json';
const ALIASES = 'data/aliases.json';
const GAME_RULES = 'docs/domain/game-rules.md';

const HEADING = /^(#{1,2}) ([^{]*)\{#([a-z0-9-]+)\}\s*$/;
// The anchor the build gives a game-rules heading (scripts/rules/build.mjs).
const anchorOf = heading => heading.trim().toLowerCase().replace(/[^a-z0-9 -]/g, '').replaceAll(' ', '-');
const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);

/**
 * The book's parts and sections, in its order: each with its number, its file and lines (`from`, `to`, counted from
 * 1), its text and its sources. A part's own lines are its opening, before its first section.
 */
export function sectionsOf(parts) {
  const found = [];
  parts.forEach(({ file, text }, index) => {
    const lines = text.split('\n');
    let fenced = false;
    let count = 0;
    let current = null;
    lines.forEach((line, at) => {
      if (line.startsWith('```')) fenced = !fenced;
      const heading = fenced ? null : HEADING.exec(line);
      if (heading) {
        if (current) current.to = at;
        const [, marks, spaced, id] = heading;
        const title = spaced.trim();
        const number = marks.length === 1 ? `${index + 1}` : `${index + 1}.${++count}`;
        current = { id, number, title, file, from: at + 1, to: lines.length, text: '', sources: [] };
        found.push(current);
      }
    });
    if (current) current.to = lines.length;
    for (const section of found.filter(section => section.file === file)) {
      const own = lines.slice(section.from - 1, section.to);
      section.text = own.join('\n');
      Object.defineProperty(section, 'rows', { value: lines }); // the whole file, to place a removal; not reported
      const sources = own.find(line => line.startsWith('sources:'));
      section.sources = sources ? sources.slice('sources:'.length).split(',').map(source => source.trim()).filter(Boolean) : [];
    }
  });
  return found;
}

/**
 * The paths of a unified diff, each with the lines it removes and adds, the lines of the new file it adds
 * (`lines`), and where in the new file it removes some (`removedAt`: the line that now follows them).
 */
export function readDiff(text) {
  const changes = [];
  let current = null;
  let line = 0;
  for (const row of text.split('\n')) {
    const header = /^diff --git a\/(.+) b\/(.+)$/.exec(row);
    const hunk = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(row);
    if (header) {
      current = { path: header[2], added: [], removed: [], lines: new Set(), removedAt: new Set() };
      changes.push(current);
    } else if (!current || row.startsWith('+++ ') || row.startsWith('--- ')) {
      continue;
    } else if (hunk) {
      line = Number(hunk[1]);
    } else if (row.startsWith('+')) {
      current.added.push(row.slice(1));
      current.lines.add(line++);
    } else if (row.startsWith('-')) {
      current.removed.push(row.slice(1));
      current.removedAt.add(line);
    } else if (row.startsWith(' ')) {
      line++;
    }
  }
  return changes;
}

/**
 * The lines of a file a change touched: those it adds, and where it removes some. Lines removed just above a heading,
 * or at the end, were the end of what came before: git writes the removal of a section's last paragraph there.
 */
function touched(change, rows, isHeading) {
  const lines = new Set(change.lines);
  for (const at of change.removedAt) {
    const above = at > 1 && (at > rows.length || isHeading(rows[at - 1]));
    lines.add(above ? at - 1 : at);
  }
  return lines;
}

/** The anchors of the game-rules headings that hold the given lines, every level, and the headings a change rewrote. */
function headingsChanged(gameRules, change) {
  const anchors = new Set(change.removed.concat(change.added).filter(row => /^#{1,6} /.test(row)).map(row => anchorOf(row.replace(/^#+ /, ''))));
  const stack = [];
  const rows = gameRules.split('\n');
  const lines = touched(change, rows, row => /^#{1,6} /.test(row));
  rows.forEach((row, at) => {
    const heading = /^(#{1,6}) (.*)$/.exec(row);
    if (heading) {
      stack.length = Math.min(stack.length, heading[1].length - 1);
      stack[heading[1].length - 1] = anchorOf(heading[2]);
    }
    if (lines.has(at + 1)) for (const anchor of stack.filter(Boolean)) anchors.add(anchor);
  });
  return anchors;
}

/** The keys of a JSON file a change rewrites: `"energyPerRound": 3` gives energyPerRound. */
const keysChanged = change => new Set(change.removed.concat(change.added).flatMap(row => [...row.matchAll(/"(\w+)"\s*:/g)].map(match => match[1])));

/** The names a change removes: `"name": "Heavy Strike"`. */
const namesIn = rows => rows.flatMap(row => [.../"name"\s*:\s*"([^"]+)"/.exec(row) ?? []].slice(1));

/** The creature's stat a key holds, as the book draws it: baseHealth is creature.health. */
// A creature's field as the book draws it: `baseHealth` is `creature.health`, `startingSpellIds` is `creature.knows`.
function statOf(key) {
  if (key === 'startingSpellIds') return 'knows';
  return /^base[A-Z]/.test(key) ? `${key[4].toLowerCase()}${key.slice(5)}` : null;
}

/** The id a content file has, from its text or, once it is deleted, from its name. */
function contentOf(path, content, kind) {
  const read = content(path);
  if (read?.id) return read;
  const name = /^([a-z0-9_]+)\.(v\d+)\.json$/.exec(path.slice(path.lastIndexOf('/') + 1));
  return name ? { id: `${kind}:${name[1]}:${name[2]}` } : null;
}

/** An ADR reaches the sections whose sources cite it. */
function byAdr({ path }, section) {
  const adr = /^docs\/adr\/(\d{4})-/.exec(path);
  return adr && section.sources.includes(`ADR ${adr[1]}`) ? [`cites ADR ${adr[1]}`] : [];
}

/** game-rules reaches the sections whose sources cite a heading it changed. */
function byGameRules(change, section, gameRules) {
  if (change.path !== GAME_RULES) return [];
  return [...headingsChanged(gameRules, change)]
    .filter(anchor => section.sources.includes(`game-rules#${anchor}`))
    .map(anchor => `cites game-rules#${anchor}`);
}

const CONTENT_KINDS = [
  ['data/Spells/', 'spell'],
  ['data/Tiers/', 'tier'],
];

/** A spell or a mutation reaches the sections that draw it or name it, by its new name or its old one. */
function byContent(change, section, content) {
  const kind = CONTENT_KINDS.find(([folder]) => change.path.startsWith(folder))?.[1];
  const definition = kind && change.path.endsWith('.json') ? contentOf(change.path, content, kind) : null;
  if (!definition) return [];
  const reasons = section.text.includes(`${definition.id}.`) ? [`draws ${definition.id}`] : [];
  // A renamed spell or mutation is still named by its old name where the book has not caught up.
  const names = new Set([definition.name, ...namesIn(change.removed)].filter(Boolean));
  for (const name of names) if (new RegExp(String.raw`\b${escape(name)}\b`).test(section.text)) reasons.push(`names ${name}`);
  return reasons;
}

/** The rule set or the reference creature reaches the sections that draw a value the change moved. */
function byValues(change, section) {
  if (change.path !== RULE_SET && change.path !== CREATURE) return [];
  const prefix = change.path === RULE_SET ? 'rules' : 'creature';
  const shown = [...keysChanged(change)];
  // The creature's starting spells are written one id a line: a changed line holding only a spell id is that list.
  if (prefix === 'creature' && [...change.added, ...change.removed].some(line => /^\s*"spell:[^"]+",?\s*$/.test(line))) shown.push('startingSpellIds');
  const keys = prefix === 'creature' ? shown.map(statOf).filter(Boolean) : shown;
  const draws = key => new RegExp(String.raw`\b${prefix}\.${key}\b`).test(section.text);
  // No key in sight (a hunk of brackets alone): every value the section draws from the file may have moved.
  const moved = shown.length > 0 ? keys : [...section.text.matchAll(new RegExp(String.raw`\b${prefix}\.(\w+)`, 'g'))].map(match => match[1]);
  return [...new Set(moved)].filter(draws).map(key => `draws ${prefix}.${key}`);
}

/** The alias keys a change to data/aliases.json rewrites: `"spell:wait": "spell:wait:v1"` gives spell:wait. */
const aliasesChanged = change => new Set(change.removed.concat(change.added).flatMap(row => [.../^\s*"([^"]+)"\s*:/.exec(row) ?? []].slice(1)));

// The lists a section states, each with the file and field data/ writes it in: what an alias may move.
function listsOf(section) {
  return [...new Set(section.text.match(/\bcreature\.knows\b|\btier:[a-z0-9_]+:v\d+\.(?:teaches|needs)\b/g) ?? [])].map(name => {
    if (name === 'creature.knows') return { name, path: CREATURE, field: 'startingSpellIds' };
    const [, file, version, list] = /^tier:([a-z0-9_]+):(v\d+)\.(\w+)$/.exec(name);
    return { name, path: `data/Tiers/${file}.${version}.json`, field: list === 'teaches' ? 'spells' : 'prerequisites' };
  });
}

/**
 * An alias may move a spell the creature starts with or a mutation teaches, or a mutation another one needs: it
 * reaches the sections stating a list that names a changed alias. A list whose file cannot be read, or a hunk that
 * names no alias, reaches every section stating one: too many sections is the safe side.
 */
function byAliases(change, section, content) {
  if (change.path !== ALIASES) return [];
  const keys = aliasesChanged(change);
  return listsOf(section).filter(({ path, field }) => {
    const items = content(path)?.[field];
    return keys.size === 0 || !Array.isArray(items) || items.some(item => keys.has(item));
  }).map(({ name }) => `draws ${name}`);
}

/** A change to an engine constant the book draws (`engine.<name>`) reaches the sections that draw it. */
function byEngine(change, section) {
  return Object.entries(ENGINE)
    .filter(([name, { file, constant }]) => change.path === file
      && [...change.added, ...change.removed].some(line => new RegExp(String.raw`\bconst int ${constant}\b`).test(line))
      && new RegExp(String.raw`\bengine\.${name}\b`).test(section.text))
    .map(([name]) => `draws engine.${name}`);
}

/** A change to the book reaches the sections whose lines it touched. */
function byOwnLines(change, section) {
  if (change.path !== `rules/${section.file}`) return [];
  const lines = touched(change, section.rows, row => HEADING.test(row));
  return [...lines].some(line => line >= section.from && line <= section.to) ? ['changed in the book'] : [];
}

/** What reaches a section, for one changed path: the reasons, or none. */
function reasonsFor(change, section, { gameRules, content }) {
  return [
    ...byAdr(change, section),
    ...byGameRules(change, section, gameRules),
    ...byContent(change, section, content),
    ...byValues(change, section),
    ...byAliases(change, section, content),
    ...byEngine(change, section),
    ...byOwnLines(change, section),
  ];
}

/**
 * The sections the changes reach, in the book's order, each with why; and the paths that call for the keeper but
 * reach no section, for a reading by hand.
 */
export function reach(changes, given) {
  const reached = new Map();
  const reaching = new Set();
  for (const change of changes) {
    for (const section of given.sections) {
      const reasons = reasonsFor(change, section, given);
      if (reasons.length === 0) continue;
      reaching.add(change.path);
      const why = reached.get(section) ?? [];
      reached.set(section, [...new Set([...why, ...reasons])]);
    }
  }
  const sections = given.sections.filter(section => reached.has(section)).map(section => ({ ...section, why: reached.get(section) }));
  const unmapped = changes
    .map(change => change.path)
    .filter(path => !reaching.has(path) && reviewersFor([path]).includes('rules-keeper'));
  return { sections, unmapped };
}

/** The report: one line a section reached, then the paths to read by hand. */
export function report({ sections, unmapped }) {
  const edition = section => (section.file.startsWith('fr/') ? ' [fr]' : '');
  const lines = sections.map(section => `${section.number} ${section.id}${edition(section)} (${section.title}): ${section.why.join(', ')}`);
  if (sections.length === 0) lines.push('No section of the book draws or cites what this change touches.');
  if (unmapped.length > 0) lines.push('', 'Read by hand: no section draws or cites these.', ...unmapped.map(path => `- ${path}`));
  return `${lines.join('\n')}\n`;
}

function main() {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
  // The English parts, then the French ones as `fr/<file>`, numbered as the English.
  const partsIn = folder => (existsSync(join(root, folder)) ? readdirSync(join(root, folder)) : []).filter(name => /^\d{2}-[a-z0-9-]+\.md$/.test(name)).sort((a, b) => a.localeCompare(b))
    .map(file => ({ file, text: readFileSync(join(root, folder, file), 'utf8') }));
  const french = partsIn('rules/fr').map(part => ({ ...part, file: `fr/${part.file}` }));
  const content = path => {
    const file = join(root, path);
    if (!existsSync(file)) return null;
    try {
      return JSON.parse(readFileSync(file, 'utf8'));
    } catch {
      return null;
    }
  };
  const given = { sections: [...sectionsOf(partsIn('rules')), ...sectionsOf(french)], gameRules: readFileSync(join(root, GAME_RULES), 'utf8'), content };
  const input = readFileSync(0, 'utf8');
  const changes = readDiff(input);
  // Empty or bare paths must never read as "nothing reached": /rules would then write "Rulebook: none".
  if (!input.startsWith('diff --git ') || changes.length === 0) {
    console.error('reach.mjs: the input is not a unified diff. Pipe `git diff <range>` into it, not --name-only; fetch main first.');
    process.exit(1);
  }
  process.stdout.write(report(reach(changes, given)));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
