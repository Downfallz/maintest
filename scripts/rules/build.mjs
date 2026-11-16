// The official rulebook's build (ADR 0188, docs/tabletop/rulebook-plan.md): reads `rules/`, draws its values from
// the hosted rule set, the reference creature and the spells under data/, refuses what a machine can find wrong,
// and writes `table/rules.json`, the parts, sections, blocks and cards as data, never HTML. The file is committed
// (ADR 0184); table/rules.test.js fails when it is out of date.
//
//   node scripts/rules/build.mjs            # write table/rules.json, or list what it refuses and exit 1
//   node scripts/rules/build.mjs --check    # exit 1 when table/rules.json is not the build of rules/
//
// No Markdown package: the book uses headings of two levels with a stable id, paragraphs, lists, tables, bold,
// italics and links, and the build refuses anything else. `rules/README.md` is the format.

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, extname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const RULE_SET = 'docs/tabletop/playtest.rules.json';
const CREATURE = 'data/Creatures/main.v1.json';
const OUTPUT = 'table/rules.json';

// Where a link to the rules may be written: the pages, the battle view and the tutorials.
const LINKED_FROM = ['table', 'battle/src', 'scenarios/tutorials'];
const LINKING_FILES = new Set(['.js', '.mjs', '.ts', '.html', '.json', '.css', '.md']);
const SKIPPED_FOLDERS = new Set(['battle-build', 'node_modules']);

const ID = /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/;

// --- reading the repository ---------------------------------------------------------------------------------

function filesUnder(root, folder) {
  const start = join(root, folder);
  if (!existsSync(start)) return [];
  const found = [];
  for (const entry of readdirSync(start, { withFileTypes: true })) {
    const path = `${folder}/${entry.name}`;
    if (entry.isDirectory()) {
      if (!SKIPPED_FOLDERS.has(entry.name)) found.push(...filesUnder(root, path));
    } else found.push(path);
  }
  return found;
}

const readJson = path => JSON.parse(readFileSync(path, 'utf8'));

/** The ids a text links to as `/rules#<id>`, `/rules/#<id>` or the same on a host. */
export function linksIn(text) {
  return [...text.matchAll(/(?<![\w.\/-])(?:https?:\/\/[\w.:-]+)?\/rules\/?#([A-Za-z0-9_-]+)/g)].map(match => match[1]);
}

/** Whether a path may hold a link to the rules: a page's source, never a test or the built book. */
export function linksFrom(path) {
  return LINKING_FILES.has(extname(path)) && !/\.(test|spec)\.[a-z]+$/.test(path) && path !== OUTPUT;
}

/**
 * The ids a built book holds, sections, parts and redirects, or the error that keeps it from being read: a file
 * with a merge conflict is settled by taking main's and rebuilding, never by hand.
 */
export function idsOf(text, source) {
  try {
    const book = JSON.parse(text);
    return { ids: [...(book.parts ?? []).flatMap(part => [part.id, ...(part.sections ?? []).map(section => section.id)]), ...Object.keys(book.moved ?? {})] };
  } catch {
    return { ids: [], error: `${source} is not JSON: settle a conflict by taking main's file and rebuilding` };
  }
}

// The ids an earlier book held: main's and the working file's. Main's is read from git, so a book rebuilt on a
// branch cannot forget an id main still links to; a checkout without origin/main has the working file alone.
function previousIds(root) {
  const found = [];
  const errors = [];
  let main = null;
  try {
    main = execFileSync('git', ['show', `origin/main:${OUTPUT}`], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
  } catch {
    main = null;
  }
  const working = existsSync(join(root, OUTPUT)) ? readFileSync(join(root, OUTPUT), 'utf8') : null;
  for (const [text, source] of [[main, `origin/main:${OUTPUT}`], [working, OUTPUT]]) {
    if (text === null) continue;
    const { ids, error } = idsOf(text, source);
    found.push(...ids);
    if (error) errors.push(error);
  }
  return { ids: [...new Set(found)], errors };
}

/** A heading's anchor as GitHub writes it: lowercased, punctuation dropped, spaces as hyphens. */
const anchorOf = heading => heading.trim().toLowerCase().replace(/[^a-z0-9 -]/g, '').replace(/ /g, '-');

/** Everything the build reads, from the repository at `root`. */
export function readInputs(root) {
  const rules = join(root, 'rules');
  const parts = readdirSync(rules).filter(name => /^\d{2}-[a-z0-9-]+\.md$/.test(name)).sort()
    .map(file => ({ file, text: readFileSync(join(rules, file), 'utf8') }));
  const moved = existsSync(join(rules, 'moved.json')) ? readJson(join(rules, 'moved.json')) : {};
  const changes = existsSync(join(rules, 'changes.md')) ? readFileSync(join(rules, 'changes.md'), 'utf8') : null;
  const spells = new Map(filesUnder(root, 'data/Spells').filter(path => path.endsWith('.json'))
    .map(path => readJson(join(root, path))).map(spell => [spell.id, spell]));
  const gameRules = new Set(readFileSync(join(root, 'docs/domain/game-rules.md'), 'utf8').split('\n')
    .filter(line => /^#{1,6} /.test(line)).map(line => anchorOf(line.replace(/^#+ /, ''))));
  const conceptCards = readFileSync(join(root, 'src/DownfallArena.Application/Matches/Scenarios/ConceptCards.cs'), 'utf8');
  const conceptIds = (/Ids \{ get; \} =\s*\[([^\]]+)\]/.exec(conceptCards)?.[1].match(/"([a-z-]+)"/g) ?? []).map(id => id.slice(1, -1));
  const links = LINKED_FROM.flatMap(folder => filesUnder(root, folder)).filter(linksFrom)
    .flatMap(file => linksIn(readFileSync(join(root, file), 'utf8')).map(id => ({ file, id })));
  return {
    parts,
    moved,
    changes,
    ruleSet: { path: RULE_SET, values: readJson(join(root, RULE_SET)) },
    creature: readJson(join(root, CREATURE)),
    spells,
    terminology: readFileSync(join(root, 'docs/domain/player-terminology.md'), 'utf8'),
    adrs: new Set(readdirSync(join(root, 'docs/adr')).map(name => /^(\d{4})-/.exec(name)?.[1]).filter(Boolean)),
    gameRules,
    conceptIds,
    links,
    previous: previousIds(root),
  };
}

// --- the values -------------------------------------------------------------------------------------------------

const FUNCTIONS = { max: Math.max, min: Math.min, floor: Math.floor };

/**
 * A value of the book: a name (`rules.energyPerRound`, `creature.health`, `spell:pummel:v1.damage`), a number,
 * `+ - * /`, parentheses, and `max`, `min`, `floor`. `look` answers a name, or undefined. Throws on anything else.
 */
export function evaluate(expression, look) {
  const tokens = expression.match(/\s*(\d+(?:\.\d+)?|[A-Za-z][A-Za-z0-9_]*(?::[A-Za-z0-9_]+)*(?:\.[A-Za-z][A-Za-z0-9_]*)*|[-+*/(),]|\S)/g)?.map(token => token.trim()) ?? [];
  let at = 0;
  const fail = () => { throw new Error(`"${expression}" is not a value`); };
  const peek = () => tokens[at];
  const take = expected => (tokens[at] === expected ? (at += 1, true) : false);
  const number = value => (typeof value === 'number' && Number.isFinite(value) ? value : fail());

  function factor() {
    const token = tokens[at++];
    if (token === undefined) fail();
    if (/^\d/.test(token)) return Number(token);
    if (token === '-') return -number(factor());
    if (token === '(') {
      const value = sum();
      if (!take(')')) fail();
      return value;
    }
    if (!/^[A-Za-z]/.test(token)) fail();
    if (take('(')) {
      const call = Object.hasOwn(FUNCTIONS, token) ? FUNCTIONS[token] : fail();
      const args = [number(sum())];
      while (take(',')) args.push(number(sum()));
      if (!take(')')) fail();
      return call(...args);
    }
    const value = look(token);
    if (value === undefined) throw new Error(`"${token}" does not resolve`);
    return value;
  }
  function product() {
    let value = factor();
    while (peek() === '*' || peek() === '/') value = tokens[at++] === '*' ? number(value) * number(factor()) : number(value) / number(factor());
    return value;
  }
  function sum() {
    let value = product();
    while (peek() === '+' || peek() === '-') value = tokens[at++] === '+' ? number(value) + number(product()) : number(value) - number(product());
    return value;
  }

  const value = sum();
  if (at !== tokens.length) fail();
  return value;
}

const capital = word => word.charAt(0).toUpperCase() + word.slice(1);
const scalar = value => (typeof value === 'number' || typeof value === 'string' ? value : undefined);

/** Answers a name from the rule set, the reference creature and the spells, and notes each spell it read. */
function lookup({ ruleSet, creature, spells }, used) {
  return name => {
    if (name.startsWith('rules.')) return scalar(ruleSet.values[name.slice(6)]);
    if (name.startsWith('creature.')) return scalar(creature[`base${capital(name.slice(9))}`]);
    const spell = /^(spell:[a-z0-9_]+:v\d+)\.(.+)$/.exec(name);
    if (!spell) return undefined;
    const [, id, path] = spell;
    const definition = spells.get(id);
    if (!definition) throw new Error(`spell "${id}" is not in data/`);
    used.add(id);
    const [first, field, ...rest] = path.split('.');
    if (rest.length > 0) return undefined;
    if (field === undefined && scalar(definition[first]) !== undefined) return definition[first];
    const effects = (definition.effects ?? []).filter(effect => effect.kind === capital(first));
    if (effects.length > 1) throw new Error(`"${name}" names one of ${effects.length === 2 ? 'two' : effects.length} ${capital(first)} effects`);
    const [effect] = effects;
    if (!effect) return undefined;
    return scalar(effect[field ?? ('amount' in effect ? 'amount' : 'amountPerRound')]);
  };
}

// --- the player's words -----------------------------------------------------------------------------------------

// Legacy words an everyday sentence also uses are refused only as a capitalised word ("Speed", not "at speed");
// Main, Start and End open ordinary sentences, so the pages' terminology test alone answers for them.
const EVERYDAY = new Set(['Speed', 'Quick', 'Standard', 'Planning']);
const SKIPPED = new Set(['Main', 'Start', 'End']);

/** The engine and legacy words of docs/domain/player-terminology.md, each with the player's word that replaced it. */
export function retiredWords(terminology) {
  const words = new Map();
  let inTable = false;
  for (const line of terminology.split('\n')) {
    if (!line.startsWith('|')) {
      inTable = false;
      continue;
    }
    const cells = line.split('|').slice(1, -1).map(cell => cell.trim());
    if (cells[0].startsWith('Internal')) {
      inTable = true;
      continue;
    }
    if (!inTable || /^-+$/.test(cells[0])) continue;
    const [internal, legacy, en] = cells;
    const player = [...en.matchAll(/\*\*(.+?)\*\*/g)].map(match => match[1]);
    const candidates = [legacy.replace(/\s*\(.*\)$/, '').trim(),
      ...[...internal.matchAll(/`([^`]+)`/g)].map(match => match[1].split('.').at(-1)).filter(word => /^[A-Z][A-Za-z]+$/.test(word))];
    for (const word of candidates) {
      if (!word || SKIPPED.has(word) || player.includes(word) || words.has(word) || !player[0]) continue;
      const exact = EVERYDAY.has(word) || /[a-z][A-Z]/.test(word);
      words.set(word, { player: player[0], pattern: new RegExp(`\\b${word.replace(/\s+/g, '\\s+')}s?\\b`, exact ? 'u' : 'iu') });
    }
  }
  // The sentences the page rewrote ("Choose speed"), in any case, each with what replaced it.
  let rewrites = false;
  for (const line of terminology.split('\n')) {
    const cells = line.startsWith('|') ? line.split('|').slice(1, -1).map(cell => cell.trim()) : null;
    if (!cells) rewrites = false;
    else if (cells[0] === 'Before') rewrites = true;
    else if (rewrites && !/^-+$/.test(cells[0]) && cells[0].toLowerCase() !== cells[1].toLowerCase() && !words.has(cells[0])) {
      words.set(cells[0], { player: cells[1], pattern: new RegExp(`\\b${cells[0].replace(/\s+/g, '\\s+')}\\b`, 'iu') });
    }
  }
  return words;
}

// --- the Markdown subset ------------------------------------------------------------------------------------------

const HEADING = /^(#+)\s+(.*)$/;
const LIST_ITEM = /^(-|\d+\.)\s+(.*)$/;
// What opens a list in the middle of a paragraph: as in CommonMark, "-" or "1.", so a wrapped "2." stays prose.
const LIST_START = /^(-|1\.)\s+/;
// Markdown the subset does not read, which would otherwise pass for a paragraph: another list marker, a heading
// with no space, a thematic break or a setext underline.
const OUTSIDE = /^([*+]\s|#|-{3,}\s*$|={3,}\s*$|\*{3,}\s*$|_{3,}\s*$)/;
const SEPARATOR = /^\|(\s*:?-+:?\s*\|)+$/;

/** Builds the book from what `readInputs` read; `errors` lists every refusal, and the book is complete only without one. */
export function buildRules(given) {
  const errors = [];
  const used = new Set();
  const look = lookup(given, used);
  const retired = retiredWords(given.terminology);
  const conceptIds = new Set(given.conceptIds);
  const bookLinks = [];

  function words(text, where) {
    for (const [word, { player, pattern }] of retired) {
      if (pattern.test(text)) errors.push(`${where}: "${word}": the player's word is ${player}`);
    }
  }

  // A line's values drawn and its markup checked: the text a player reads, or null when it cannot be read.
  function resolve(raw, where) {
    if (raw.includes('`')) errors.push(`${where}: code is outside the subset`);
    if (/<[A-Za-z/!]/.test(raw)) errors.push(`${where}: HTML is outside the subset`);
    if (raw.includes('![')) errors.push(`${where}: an image is outside the subset`);
    const text = raw.replace(/\{([^{}#][^{}]*)\}/g, (_, expression) => {
      try {
        const value = evaluate(expression.trim(), look);
        if (typeof value === 'number' && !Number.isFinite(value)) throw new Error(`"${expression.trim()}" is not a finite number`);
        return typeof value === 'number' ? String(Number(value.toFixed(6))) : value;
      } catch (error) {
        errors.push(`${where}: ${error.message}`);
        return '';
      }
    });
    if (/[{}]/.test(text)) errors.push(`${where}: an unmatched brace`);
    words(text, where);
    return text;
  }

  // Bold, italics and links, as spans of text.
  function spans(raw, where) {
    const text = resolve(raw, where);
    const out = [];
    let last = 0;
    for (const match of text.matchAll(/\*\*(.+?)\*\*|\*(.+?)\*|\[([^\]]+)\]\(([^)\s]+)\)/g)) {
      if (match.index > last) out.push({ text: text.slice(last, match.index) });
      if (match[1] !== undefined) out.push({ text: match[1], strong: true });
      else if (match[2] !== undefined) out.push({ text: match[2], emphasis: true });
      else {
        const href = match[4];
        if (href.startsWith('#')) bookLinks.push({ id: href.slice(1), where });
        else if (!href.startsWith('https://')) errors.push(`${where}: a link to "${href}" leaves the book; link a section (#id) or a page (https://)`);
        out.push({ text: match[3], href });
      }
      last = match.index + match[0].length;
    }
    if (last < text.length) out.push({ text: text.slice(last) });
    if (out.some(span => !span.href && /\*|\[|\]\(/.test(span.text))) errors.push(`${where}: markup the subset does not read (a lone *, a [ or a titled link)`);
    return out;
  }

  function heading(rest, where) {
    const match = /^(.*?)\s*\{#([^}]*)\}\s*$/.exec(rest);
    if (!match) {
      errors.push(`${where}: heading "${rest}" has no {#id}`);
      return { title: resolve(rest, where), id: null };
    }
    if (!ID.test(match[2])) errors.push(`${where}: id "${match[2]}" is not lowercase words and hyphens`);
    return { title: resolve(match[1], where), id: match[2] };
  }

  function card(lines, where) {
    const found = { id: null, title: null, lines: [], example: null };
    for (const line of lines) {
      if (/\*|\[|\]\(/.test(line)) errors.push(`${where}: a card is plain text, with no markup`);
      const field = /^(id|title):\s*(.*)$/.exec(line);
      if (field) found[field[1]] = field[1] === 'id' ? field[2].trim() : resolve(field[2].trim(), where);
      else if (line.startsWith('> ')) found.example = resolve(line.slice(2).trim(), where);
      else if (line.trim()) found.lines.push(resolve(line.trim(), where));
    }
    const name = found.id ?? '?';
    if (!found.id) errors.push(`${where}: a card has no id`);
    else if (!conceptIds.has(found.id)) errors.push(`${where}: card "${found.id}" is not in ConceptCards.Ids`);
    if (!found.title) errors.push(`${where}: card "${name}" has no title`);
    if (found.lines.length < 1 || found.lines.length > 3) errors.push(`${where}: card "${name}" has ${found.lines.length} lines; one to three`);
    if (!found.example) errors.push(`${where}: card "${name}" has no example`);
    return found;
  }

  function sources(text, where) {
    const named = text.split(',').map(source => source.trim()).filter(Boolean);
    for (const source of named) {
      const adr = /^ADR (\d{4})$/.exec(source);
      const rule = /^game-rules#([a-z0-9-]+)$/.exec(source);
      if (adr) {
        if (!given.adrs.has(adr[1])) errors.push(`${where}: ADR ${adr[1]} does not exist`);
      } else if (rule) {
        if (!given.gameRules.has(rule[1])) errors.push(`${where}: game-rules#${rule[1]} is not a heading of docs/domain/game-rules.md`);
      } else errors.push(`${where}: "${source}" is not an ADR or a game-rules heading`);
    }
    return named;
  }

  function parsePart({ file, text }, index) {
    const lines = text.split('\n');
    const part = { id: null, number: index + 1, title: null, blocks: [], sections: [] };
    let section = null;
    const blocks = () => (section ?? part).blocks;
    const add = (block, where) => {
      if (section?.sources.length > 0 || section?.closed) {
        errors.push(`${where}: section "${section.id}": nothing follows its sources line but its card`);
      }
      blocks().push(block);
    };
    let at = 0;
    while (at < lines.length) {
      const line = lines[at];
      const where = `rules/${file}:${at + 1}`;
      if (!line.trim()) {
        at += 1;
        continue;
      }
      const head = HEADING.exec(line);
      if (head) {
        const level = head[1].length;
        const { title, id } = heading(head[2], where);
        if (level === 1 && part.title === null && section === null && part.blocks.length === 0) Object.assign(part, { id, title });
        else if (level === 1) errors.push(`${where}: a part file has one heading of level 1, its first line`);
        else if (level === 2) {
          if (part.title === null) errors.push(`${where}: the part's heading of level 1 comes first`);
          section = { id, number: `${part.number}.${part.sections.length + 1}`, title, blocks: [], sources: [], card: null };
          part.sections.push(section);
        } else errors.push(`${where}: a heading of level ${level} is outside the subset; a part has sections, a section has none`);
        at += 1;
        continue;
      }
      if (line.startsWith('```')) {
        const end = lines.indexOf('```', at + 1);
        const body = lines.slice(at + 1, end < 0 ? lines.length : end);
        if (line.trim() !== '```card') errors.push(`${where}: a code block is outside the subset; only a card is fenced`);
        else if (end < 0) errors.push(`${where}: a card is not closed`);
        else if (!section) errors.push(`${where}: a card belongs to a section`);
        else if (section.card) errors.push(`${where}: section "${section.id}" has two cards`);
        else {
          if (section.sources.length === 0) errors.push(`${where}: section "${section.id}": its card follows its sources line`);
          section.card = card(body, where);
          section.closed = true;
        }
        at = end < 0 ? lines.length : end + 1;
        continue;
      }
      if (/^sources:/.test(line)) {
        if (!section) errors.push(`${where}: a sources line belongs to a section`);
        else {
          if (section.sources.length > 0) errors.push(`${where}: section "${section.id}" has two sources lines`);
          section.sources = sources(line.slice('sources:'.length), where);
          section.closed = true;
        }
        at += 1;
        continue;
      }
      if (line.startsWith('>')) {
        errors.push(`${where}: a quote is outside the subset`);
        at += 1;
        continue;
      }
      if (OUTSIDE.test(line)) {
        errors.push(`${where}: "${line.trim()}" is outside the subset; a list item starts with "- ", a heading with "# " and its {#id}`);
        at += 1;
        continue;
      }
      if (line.startsWith('|')) {
        const rows = [];
        while (at < lines.length && lines[at].startsWith('|')) rows.push(lines[at++]);
        const cells = row => row.trim().replace(/^\||\|$/g, '').split('|').map(cell => spans(cell.trim(), where));
        if (rows.length < 2 || !SEPARATOR.test(rows[1].trim())) errors.push(`${where}: a table needs its header and a separator line`);
        add({ type: 'table', header: cells(rows[0]), rows: rows.slice(2).map(cells) }, where);
        continue;
      }
      const item = LIST_ITEM.exec(line);
      if (item) {
        const ordered = item[1] !== '-';
        const items = [];
        while (at < lines.length) {
          const next = LIST_ITEM.exec(lines[at]);
          if (next) items.push(next[2]);
          else if (/^\s+\S/.test(lines[at]) && items.length > 0) items[items.length - 1] += ` ${lines[at].trim()}`;
          else break;
          at += 1;
        }
        add({ type: 'list', ordered, items: items.map(text => spans(text, where)) }, where);
        continue;
      }
      const paragraph = [];
      while (at < lines.length && lines[at].trim() && !HEADING.test(lines[at]) && !lines[at].startsWith('```')
        && !/^sources:/.test(lines[at]) && !lines[at].startsWith('|') && !LIST_START.test(lines[at]) && !lines[at].startsWith('>')
        && !OUTSIDE.test(lines[at])) {
        paragraph.push(lines[at++].trim());
      }
      add({ type: 'paragraph', spans: spans(paragraph.join(' '), where) }, where);
    }
    if (part.title === null) errors.push(`rules/${file}: a part file opens with its heading of level 1`);
    for (const one of part.sections) {
      if (one.sources.length === 0) errors.push(`rules/${file}: section "${one.id}" has no sources line`);
      delete one.closed;
    }
    return part;
  }

  const parts = given.parts.map(parsePart);

  // The ids: one a part or a section, each once; the cards, each in one section.
  const ids = new Set();
  for (const id of parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)]).filter(Boolean)) {
    if (ids.has(id)) errors.push(`id "${id}" is used twice`);
    ids.add(id);
  }
  const cards = [];
  for (const section of parts.flatMap(part => part.sections)) {
    if (!section.card?.id) continue;
    if (cards.some(one => one.id === section.card.id)) errors.push(`card "${section.card.id}" is in two sections`);
    else cards.push({ id: section.card.id, section: section.id });
  }

  // An id never dies: one main's book or the working one had is still a section, or moved.json says where it went.
  const moved = given.moved ?? {};
  for (const [from, to] of Object.entries(moved)) {
    if (ids.has(from)) errors.push(`rules/moved.json redirects "${from}", which is still a section`);
    if (!ids.has(to)) errors.push(`rules/moved.json sends "${from}" to "${to}", which is not a section`);
  }
  errors.push(...(given.previous?.errors ?? []));
  for (const id of given.previous?.ids ?? []) {
    if (!ids.has(id) && !Object.hasOwn(moved, id)) errors.push(`id "${id}" is gone and rules/moved.json does not redirect it`);
  }

  for (const { id, where } of bookLinks) {
    if (!ids.has(id)) errors.push(`${where}: a link to #${id}, which is not a section`);
  }
  for (const { file, id } of given.links) {
    if (!ids.has(id) && !Object.hasOwn(moved, id)) errors.push(`${file} links to /rules#${id}, which is not a section`);
  }

  const changes = [];
  for (const line of (given.changes ?? '').split('\n')) {
    if (!line.trim() || line.startsWith('#')) continue;
    const change = /^- (\d{4}-\d{2}-\d{2}): (.+)$/.exec(line.trim());
    if (!change) {
      errors.push(`rules/changes.md: "${line.trim()}" is not "- YYYY-MM-DD: what changed"`);
      continue;
    }
    const adrs = [...change[2].matchAll(/ADR (\d{4})/g)].map(match => match[1]);
    for (const adr of adrs.filter(adr => !given.adrs.has(adr))) errors.push(`rules/changes.md: ADR ${adr} does not exist`);
    words(change[2], 'rules/changes.md');
    changes.push({ date: change[1], text: change[2], adrs });
  }

  const book = {
    drawnFrom: { ruleSet: given.ruleSet.path, rules: given.ruleSet.values, creature: given.creature.id, spells: [...used].sort() },
    parts,
    cards,
    moved,
    changes,
  };
  return { book, errors };
}

/** The committed file's text: two-space JSON and a final newline, so a diff reads line by line. */
export function render(book) {
  return `${JSON.stringify(book, null, 2)}\n`;
}

// --- the command --------------------------------------------------------------------------------------------------

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
  const { book, errors } = buildRules(readInputs(root));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exit(1);
  }
  const output = join(root, OUTPUT);
  const built = render(book);
  if (process.argv.includes('--check')) {
    const current = existsSync(output) ? readFileSync(output, 'utf8') : '';
    if (current !== built) {
      console.error(`${OUTPUT} is not the build of rules/: run node scripts/rules/build.mjs`);
      process.exit(1);
    }
    console.log(`${OUTPUT} is up to date.`);
  } else {
    writeFileSync(output, built);
    console.log(`${relative(root, output)}: ${book.parts.length} parts, ${book.parts.reduce((n, part) => n + part.sections.length, 0)} sections, ${book.cards.length} cards.`);
  }
}
