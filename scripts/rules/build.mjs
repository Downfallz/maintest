// The official rulebook's build (ADR 0188, docs/tabletop/rulebook-plan.md): reads `rules/`, draws its values from
// the hosted rule set, the reference creature, and the spells and mutations under data/, refuses what a machine can
// find wrong, and writes `table/rules.json`, the parts, sections, blocks and cards as data, never HTML. The file is
// committed (ADR 0184); table/rules.test.js fails when it is out of date.
//
//   node scripts/rules/build.mjs            # write table/rules.json and rules.fr.json, or list what it refuses
//   node scripts/rules/build.mjs --check    # exit 1 when either file is not the build of rules/
//
// The French edition (rules/fr/, rulebook-plan.md R6) is the same book in French: the same files, ids, sources and
// cards, its values drawn the same way and written the French way, and the French column of player-terminology.md
// as its words. It is built into table/rules.fr.json, beside the English.
//
// No Markdown package: the book uses headings of two levels with a stable id, paragraphs, lists, tables, bold,
// italics and links, and the build refuses anything else. `rules/README.md` is the format.

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, extname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const RULE_SET = 'docs/tabletop/playtest.rules.json';
const CREATURE = 'data/Creatures/main.v1.json';
const WORDS = 'rules/words.json';
// The engine's values the hosted rule set does not hold, each read from the constant the domain declares.
export const ENGINE = { defenseBuffCeiling: { file: 'src/DownfallArena.Domain/Matches/Creatures/Creature.cs', constant: 'DefenseBuffCeiling' } };
const OUTPUT = 'table/rules.json';
const OUTPUT_FR = 'table/rules.fr.json';

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
  return [...text.matchAll(/(?<![\w./-])(?:https?:\/\/[\w.:-]+)?\/rules\/?#([\w-]+)/g)].map(match => match[1]);
}

/** Whether a path may hold a link to the rules: a page's source, never a test or the built book. */
export function linksFrom(path) {
  return LINKING_FILES.has(extname(path)) && !/\.(test|spec)\.[a-z]+$/.test(path) && path !== OUTPUT && path !== OUTPUT_FR;
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
    // NOSONAR below: javascript:S4036, git found on PATH. The command and its arguments are fixed, and nothing the
    // book holds reaches them; scripts/digest/digest.mjs reads git the same way.
    main = execFileSync('git', ['show', `origin/main:${OUTPUT}`], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }); // NOSONAR
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
const anchorOf = heading => heading.trim().toLowerCase().replace(/[^a-z0-9 -]/g, '').replaceAll(' ', '-');

// An edition's part files, in their order, and its changes, or null when it has none.
function editionIn(folder) {
  const parts = readdirSync(folder).filter(name => /^\d{2}-[a-z0-9-]+\.md$/.test(name)).sort((a, b) => a.localeCompare(b))
    .map(file => ({ file, text: readFileSync(join(folder, file), 'utf8') }));
  const changes = existsSync(join(folder, 'changes.md')) ? readFileSync(join(folder, 'changes.md'), 'utf8') : null;
  return { parts, changes };
}

// The engine's values, each from its `const int` in the domain's source, and a refusal for each the source no
// longer declares (a file moved, a constant renamed), so the book says where its value went missing.
function engineValues(root) {
  const values = {};
  const missing = [];
  for (const [name, { file, constant }] of Object.entries(ENGINE)) {
    const source = existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '';
    const found = new RegExp(String.raw`\bconst int ${constant} = (\d+);`).exec(source);
    if (found) values[name] = Number(found[1]);
    else missing.push(`${file} declares no const int ${constant}: engine.${name} has no source`);
  }
  return { values, missing };
}

/** Everything the build reads, from the repository at `root`. */
export function readInputs(root) {
  const rules = join(root, 'rules');
  const { parts, changes } = editionIn(rules);
  const moved = existsSync(join(rules, 'moved.json')) ? readJson(join(rules, 'moved.json')) : {};
  const french = existsSync(join(rules, 'fr')) ? editionIn(join(rules, 'fr')) : null;
  const byId = folder => new Map(filesUnder(root, folder).filter(path => path.endsWith('.json'))
    .map(path => readJson(join(root, path))).map(item => [item.id, item]));
  const spells = byId('data/Spells');
  const mutations = byId('data/Tiers');
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
    french,
    ruleSet: { path: RULE_SET, values: readJson(join(root, RULE_SET)) },
    creature: readJson(join(root, CREATURE)),
    spells,
    mutations,
    terminology: readFileSync(join(root, 'docs/domain/player-terminology.md'), 'utf8'),
    adrs: new Set(readdirSync(join(root, 'docs/adr')).map(name => /^(\d{4})-/.exec(name)?.[1]).filter(Boolean)),
    gameRules,
    conceptIds,
    words: existsSync(join(root, WORDS)) ? readJson(join(root, WORDS)) : { en: [], fr: [] },
    ...(({ values, missing }) => ({ engine: values, engineMissing: missing }))(engineValues(root)),
    links,
    previous: previousIds(root),
  };
}

// --- the values -------------------------------------------------------------------------------------------------

const FUNCTIONS = { max: Math.max, min: Math.min, floor: Math.floor };
const COMPARISONS = {
  '=': (a, b) => a === b, '!=': (a, b) => a !== b, '<': (a, b) => a < b, '>': (a, b) => a > b, '<=': (a, b) => a <= b, '>=': (a, b) => a >= b,
};

/**
 * A value of the book: a name (`rules.energyPerRound`, `creature.health`, `spell:pummel:v1.damage`), a number,
 * `+ - * /`, parentheses, and `max`, `min`, `floor`; or a fact, one comparison (`= != < > <= >=`) of two of them,
 * where a 'quoted' word is text. `look` answers a name, or undefined. Throws on anything else.
 */
export function evaluate(expression, look) {
  const tokens = expression.match(/\d+(?:\.\d+)?|'[^']*'|[A-Za-z][\w:.]*|[<>!]=|[-+*/(),=<>]|\S/g) ?? [];
  let at = 0;
  const fail = () => { throw new Error(`"${expression}" is not a value`); };
  const peek = () => tokens[at];
  const take = expected => (tokens[at] === expected ? (at += 1, true) : false);
  const number = value => (typeof value === 'number' && Number.isFinite(value) ? value : fail());

  function call(name) {
    const run = Object.hasOwn(FUNCTIONS, name) ? FUNCTIONS[name] : fail();
    const args = [number(sum())];
    while (take(',')) args.push(number(sum()));
    if (!take(')')) fail();
    return run(...args);
  }
  function factor() {
    const token = tokens[at++];
    if (token === undefined) fail();
    if (/^\d/.test(token)) return Number(token);
    if (token.startsWith("'")) return token.slice(1, -1);
    if (token === '-') return -number(factor());
    if (token === '(') {
      const value = sum();
      if (!take(')')) fail();
      return value;
    }
    if (!/^[A-Za-z]/.test(token)) fail();
    if (take('(')) return call(token);
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

  // A comparison, at most one, answers true or false: a fact the book states (`{?...}`).
  function comparison() {
    const left = sum();
    const operator = peek();
    if (!Object.hasOwn(COMPARISONS, operator)) return left;
    at += 1;
    const right = sum();
    if (typeof left === 'string' || typeof right === 'string') {
      if (operator !== '=' && operator !== '!=') fail();
      return COMPARISONS[operator](left, right);
    }
    return COMPARISONS[operator](number(left), number(right));
  }

  const value = comparison();
  if (at !== tokens.length) fail();
  return value;
}

const capital = word => word.charAt(0).toUpperCase() + word.slice(1);
const scalar = value => (typeof value === 'number' || typeof value === 'string' || typeof value === 'boolean' ? value : undefined);
const SPELL_FIELDS = { targets: definition => definition.targeting?.maxTargets, origin: definition => definition.targeting?.origin };

// An effect of a line by its kind (`damage`), or one of several by its order (`defenseBuff2`), and its field: its
// amount when none is named, `count` for how many of that kind the line holds, and `true` for an effect with no
// amount (a Stun), which states that the spell has it.
function effectValue(line, name, kindName, field) {
  const [, word, order] = /^([a-zA-Z]+?)(\d*)$/.exec(kindName) ?? [];
  if (!word) return undefined;
  const kind = capital(word);
  const effects = line.filter(effect => effect.kind === kind);
  if (field === 'count' && !order) return effects.length;
  if (!order && effects.length > 1) throw new Error(`"${name}" names one of ${effects.length === 2 ? 'two' : effects.length} ${kind} effects; name it by its order (${word}1)`);
  const effect = effects[order ? Number(order) - 1 : 0];
  if (!effect) return undefined;
  if (field) return scalar(effect[field]);
  return scalar(effect.amount ?? effect.amountPerRound) ?? true;
}

// A spell's field (`energyCost`, `name`, `targets`, `origin`), one of its effects by kind (`damage`,
// `bleed.durationRounds`), or one of its caster's own line (`caster.damage`).
function spellValue(definition, name, path) {
  const caster = path.startsWith('caster.');
  const parts = (caster ? path.slice('caster.'.length) : path).split('.');
  if (parts.length > 2) return undefined;
  const [first, field] = parts;
  if (!caster && parts.length === 1) {
    if (Object.hasOwn(SPELL_FIELDS, first)) return scalar(SPELL_FIELDS[first](definition));
    if (scalar(definition[first]) !== undefined) return definition[first];
  }
  return effectValue((caster ? definition.casterEffects : definition.effects) ?? [], name, first, field);
}

// A mutation's field (`name`, `level`, `initiativeBonus`) or its passive's (`sunder`, `damageBonus`, `upkeepEnergy`).
function mutationValue(definition, field) {
  return scalar(Object.hasOwn(definition, field) ? definition[field] : definition.passive?.[field]);
}

/** Answers a name from the rule set, the engine, the reference creature, the spells and the mutations, and notes each it read. */
function lookup({ ruleSet, creature, spells, mutations = new Map(), engine = {} }, used, usedMutations) {
  return name => {
    if (name.startsWith('rules.')) return scalar(ruleSet.values[name.slice(6)]);
    if (name.startsWith('engine.')) return Object.hasOwn(engine, name.slice(7)) ? scalar(engine[name.slice(7)]) : undefined;
    if (name.startsWith('creature.')) return scalar(creature[`base${capital(name.slice(9))}`]);
    const mutation = /^(tier:[a-z0-9_]+:v\d+)\.(\w+)$/.exec(name);
    if (mutation) {
      const definition = mutations.get(mutation[1]);
      if (!definition) throw new Error(`mutation "${mutation[1]}" is not in data/`);
      usedMutations.add(mutation[1]);
      return mutationValue(definition, mutation[2]);
    }
    const spell = /^(spell:[a-z0-9_]+:v\d+)\.([\w.]+)$/.exec(name);
    if (!spell) return undefined;
    const [, id, path] = spell;
    const definition = spells.get(id);
    if (!definition) throw new Error(`spell "${id}" is not in data/`);
    used.add(id);
    return spellValue(definition, name, path);
  };
}

// --- the player's words -----------------------------------------------------------------------------------------

// Legacy words an everyday sentence also uses are refused only as a capitalised word ("Speed", not "at speed");
// Main, Start and End open ordinary sentences, so the pages' terminology test alone answers for them.
const EVERYDAY = new Set(['Speed', 'Quick', 'Standard', 'Planning']);
const SKIPPED = new Set(['Main', 'Start', 'End']);

/** The rows of the page's tables whose header's first cell passes `isHeader`, as their trimmed cells. */
function tableRows(text, isHeader) {
  const rows = [];
  let inTable = false;
  for (const line of text.split('\n')) {
    const cells = line.startsWith('|') ? line.split('|').slice(1, -1).map(cell => cell.trim()) : null;
    if (!cells) inTable = false;
    else if (isHeader(cells[0])) inTable = true;
    else if (inTable && !/^-+$/.test(cells[0])) rows.push(cells);
  }
  return rows;
}

const SPACES = String.raw`\s+`;
const wordPattern = (phrase, suffix, flags) => new RegExp(String.raw`\b${phrase.split(/\s+/).join(SPACES)}${suffix}\b`, flags);

// The words a row of the "Internal" tables retires: its legacy word, and the engine's names in its code spans.
function rowCandidates(internal, legacy) {
  const open = legacy.indexOf(' (');
  const codes = [...internal.matchAll(/`([^`]+)`/g)].map(match => match[1].split('.').at(-1));
  return [(open < 0 ? legacy : legacy.slice(0, open)).trim(), ...codes.filter(word => /^[A-Z][A-Za-z]+$/.test(word))];
}

const bold = cell => [...(cell ?? '').matchAll(/\*\*([^*]+)\*\*/g)].map(match => match[1]);

/**
 * The engine and legacy words of docs/domain/player-terminology.md, each with the player's word that replaced it.
 * In French (`fr`), the player's word is the French column's, and an English player's word the French one
 * replaced (Upkeep, Spellbook, Stage) is refused too.
 */
export function retiredWords(terminology, language = 'en') {
  const words = new Map();
  const rows = tableRows(terminology, first => first.startsWith('Internal'));
  for (const [internal, legacy, en, fr] of rows) retireRow(words, rowCandidates(internal, legacy), bold(language === 'fr' ? fr : en), language);
  if (language === 'fr') {
    for (const [, , en, fr] of rows) retireEnglish(words, bold(en)[0], bold(fr)[0]);
    return words;
  }
  // The sentences the page rewrote ("Choose speed"), in any case, each with what replaced it.
  for (const [before, after] of tableRows(terminology, first => first === 'Before')) {
    if (before.toLowerCase() !== after.toLowerCase() && !words.has(before)) words.set(before, { player: after, pattern: wordPattern(before, '', 'iu') });
  }
  return words;
}

// A row's engine and legacy words, each refused for the row's player's word.
function retireRow(words, candidates, player, language) {
  if (!player[0]) return;
  for (const word of candidates) {
    if (!word || SKIPPED.has(word) || player.includes(word) || words.has(word)) continue;
    // In French every word is refused as written: "tiers" and "stage" are French words too.
    const exact = language === 'fr' || EVERYDAY.has(word) || /[a-z][A-Z]/.test(word);
    words.set(word, { player: player[0], pattern: wordPattern(word, 's?', exact ? 'u' : 'iu') });
  }
}

// In French, an English player's word the French column replaced (Upkeep for Entretien).
function retireEnglish(words, english, french) {
  if (english && french && english !== french && !words.has(english)) words.set(english, { player: french, pattern: wordPattern(english, 's?', 'u') });
}

// --- the names a player reads ---------------------------------------------------------------------------------

// A count's nouns, by language: a value written right before one of them takes both its forms ({n|round|rounds}),
// so a value of 1 never reads "1 rounds".
// The words that may stand between a count and its noun ("{n} more rounds").
// A closed list: a wildcard would read "{n} per round" as a count of rounds.
const BETWEEN = {
  en: ['more', 'next', 'extra', 'further', 'other', 'whole', 'additional', 'consecutive', 'later', 'full', 'remaining', 'last', 'first'],
  fr: ['autres', 'prochains', 'prochaines', 'derniers', 'dernières', 'premiers', 'premières', 'nouveaux', 'nouvelles'],
};
const COUNTED = {
  en: [['round', 'rounds'], ['turn', 'turns'], ['enemy', 'enemies'], ['ally', 'allies'], ['target', 'targets'], ['time', 'times'], ['Upkeep', 'Upkeeps']],
  fr: [['tour', 'tours'], ['round', 'rounds'], ['ennemi', 'ennemis'], ['allié', 'alliés'], ['cible', 'cibles'], ['Entretien', 'Entretiens']],
};

const escaped = text => text.replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);
// Where a sentence, a cell, a line or a step label (*Command · Tie order.*) starts: its first word is capitalised
// for that alone. Not after a colon, a semicolon, a dash or a quote: both languages go on in lowercase there, and
// a name after "Brute:" must still be read.
const SENTENCE_START = /(?:^|[.!?·])\s*$/u;
const ROMAN = /^[IVX]+$/;

// The words the book may capitalise inside a sentence besides the names: rules/words.json, in the book's language.
// Not the terminology's bold words: they hold spell names (Strike, Focus), and a name is allowed only while data/
// holds it.
const capitalWords = (given, language) => new Set(given.words?.[language] ?? []);

// The words an edition writes in lowercase: a sentence's capitalised first word is an ordinary word when it is one.
const vocabularyOf = parts => new Set(parts.flatMap(({ text }) => text.replace(/\{[^{}]*\}/g, ' ').match(/(?<![\p{L}\d])\p{Ll}[\p{L}-]*/gu) ?? []));

// --- the Markdown subset ------------------------------------------------------------------------------------------

const HEADING = /^(#+) (.*)$/;
const LIST_ITEM = /^(-|\d+\.) (.*)$/;
// What opens a list in the middle of a paragraph: as in CommonMark, "-" or "1.", so a wrapped "2." stays prose.
const LIST_START = /^(-|1\.) /;
const SEPARATOR = /^\|( *:?-+:? *\|)+$/;
const MARKUP_LEFT = /\*|\[|\]\(/;

// Markdown the subset does not read, which would otherwise pass for a paragraph: another list marker, a heading
// with no space, a thematic break or a setext underline.
const isOutside = line => /^[*+] /.test(line) || line.startsWith('#') || /^(-{3,}|={3,}|\*{3,}|_{3,}) *$/.test(line);

// A line that opens a block of its own, and so ends a paragraph.
const opensBlock = line => !line.trim() || HEADING.test(line) || line.startsWith('```') || line.startsWith('sources:')
  || line.startsWith('|') || LIST_START.test(line) || line.startsWith('>') || isOutside(line);

/** What one build of the book collects: its refusals, the spells it read and the links inside the book. */
class Build {
  constructor(given, { language = 'en', folder = 'rules', errors = [] } = {}) {
    this.given = given;
    this.language = language;
    this.folder = folder;
    this.errors = errors;
    this.used = new Set();
    this.usedMutations = new Set();
    this.look = lookup(given, this.used, this.usedMutations);
    this.retired = retiredWords(given.terminology, language);
    this.conceptIds = new Set(given.conceptIds);
    this.bookLinks = [];
    const names = [...new Set([...given.spells.values(), ...(given.mutations ?? new Map()).values()].map(item => item.name).filter(Boolean))];
    this.names = names.sort((a, b) => b.length - a.length).map(name => new RegExp(String.raw`(?<![\p{L}\d])${escaped(name)}(?![\p{L}\d])`, 'gu'));
    this.capitals = capitalWords(given, language);
    this.vocabulary = vocabularyOf(language === 'fr' ? given.french?.parts ?? [] : given.parts);
    this.firstWords = new Set(given.words?.firstWords?.[language] ?? []);
  }

  // A capitalised word inside a sentence is a spell's or a mutation's name, or a word the book may capitalise, so a
  // name renamed in data/ fails the book until the book follows. A drawn value is not read: it is drawn.
  capitalised(raw, where) {
    let text = raw.replace(/\{\?[^{}]*\}/g, '').replace(/\{[^{}]*\}/g, '0').replace(/\]\([^)]*\)/g, ']').replace(/[*[\]]/g, '');
    for (const name of this.names) text = text.replace(name, '0');
    for (const match of text.matchAll(/\p{Lu}[\p{L}-]*/gu)) {
      const [word] = match;
      const before = text.slice(0, match.index);
      if (word.length === 1 || ROMAN.test(word) || /[\p{L}\d]$/u.test(before)) continue;
      if (this.capitals.has(word) || (word.endsWith('s') && this.capitals.has(word.slice(0, -1)))) continue;
      // A sentence's first word is capitalised for that alone when the book writes it in lowercase elsewhere.
      if (SENTENCE_START.test(before) && (this.vocabulary.has(word.toLowerCase()) || this.firstWords.has(word))) continue;
      this.refuse(`${where}: "${word}" is not the name of a spell or a mutation in data/, nor a word of rules/words.json`);
    }
  }

  // A value written before a count's noun takes both its forms: the noun right after it, or after one more word
  // ("{n} more rounds"), whatever the bold, the italics or a fact between them.
  counted(raw, where) {
    const text = raw.replace(/\{\?[^{}]*\}/g, '').replaceAll('*', '');
    const between = (BETWEEN[this.language] ?? []).join('|');
    for (const [singular, plural] of COUNTED[this.language] ?? []) {
      const pattern = new RegExp(String.raw`\{([^{}#?|][^{}|]*)\}\s+(?:(?:${between})\s+)?(?:${singular}|${plural})(?![\p{L}\d])`, 'gu');
      for (const [whole, expression] of text.matchAll(pattern)) this.refuse(`${where}: "${whole}": a count takes its noun: {${expression.trim()}|${singular}|${plural}}`);
    }
  }

  refuse(message) {
    this.errors.push(message);
  }

  words(text, where) {
    for (const [word, { player, pattern }] of this.retired) {
      if (pattern.test(text)) this.refuse(`${where}: "${word}": the player's word is ${player}`);
    }
  }

  // `{expr}` draws a value; `{expr|round|rounds}` a count with its noun; `{?fact}` checks a fact and draws nothing.
  value(expression, where) {
    try {
      if (expression.startsWith('?')) return this.fact(expression.slice(1).trim());
      const [drawn, ...nouns] = expression.split('|').map(part => part.trim());
      if (nouns.length > 0 && (nouns.length !== 2 || nouns.some(noun => !noun))) throw new Error(`"${expression}": a count names its singular and its plural`);
      const value = evaluate(drawn, this.look);
      if (typeof value === 'boolean') throw new Error(`"${drawn}" is a fact, not a value: write {?${drawn}}`);
      if (typeof value !== 'number') return nouns.length > 0 ? `${value} ${nouns[1]}` : value;
      if (!Number.isFinite(value)) throw new Error(`"${drawn}" is not a finite number`);
      const fixed = String(Number(value.toFixed(6)));
      const written = this.language === 'fr' ? fixed.replace('.', ',') : fixed;
      if (nouns.length === 0) return written;
      const singular = this.language === 'fr' ? Math.abs(value) < 2 : value === 1;
      return `${written} ${singular ? nouns[0] : nouns[1]}`;
    } catch (error) {
      this.refuse(`${where}: ${error.message}`);
      return '';
    }
  }

  fact(expression) {
    const holds = evaluate(expression, this.look);
    if (typeof holds !== 'boolean') throw new Error(`"${expression}" is not a fact: a fact is a comparison, or an effect the spell has`);
    if (!holds) throw new Error(`the fact "${expression}" is false`);
    return '';
  }

  // A line's values drawn and its markup checked: the text a player reads.
  resolve(raw, where) {
    if (raw.includes('`')) this.refuse(`${where}: code is outside the subset`);
    if (/<[A-Za-z/!]/.test(raw.replace(/\{[^{}]*\}/g, ''))) this.refuse(`${where}: HTML is outside the subset`);
    if (raw.includes('![')) this.refuse(`${where}: an image is outside the subset`);
    const text = raw.replace(/\{([^{}#][^{}]*)\}/g, (_, expression) => this.value(expression.trim(), where));
    if (/[{}]/.test(text)) this.refuse(`${where}: an unmatched brace`);
    this.words(text, where);
    this.counted(raw, where);
    this.capitalised(raw, where);
    return text;
  }

  link(label, href, where) {
    if (href.startsWith('#')) this.bookLinks.push({ id: href.slice(1), where });
    else if (!href.startsWith('https://')) this.refuse(`${where}: a link to "${href}" leaves the book; link a section (#id) or a page (https://)`);
    return { text: label, href };
  }

  // The markup that opens at `at`, as its span and where it ends, or null: **bold**, *italics*, [a link](href).
  markup(text, at, where) {
    if (text.startsWith('**', at)) {
      const end = text.indexOf('**', at + 2);
      return end > at + 2 ? { span: { text: text.slice(at + 2, end), strong: true }, next: end + 2 } : null;
    }
    if (text[at] === '*') {
      const end = text.indexOf('*', at + 1);
      return end > at + 1 ? { span: { text: text.slice(at + 1, end), emphasis: true }, next: end + 1 } : null;
    }
    const middle = text[at] === '[' ? text.indexOf('](', at + 1) : -1;
    const end = middle > at + 1 ? text.indexOf(')', middle + 2) : -1;
    const href = end > middle + 2 ? text.slice(middle + 2, end) : '';
    if (!href || /\s/.test(href) || text.slice(at + 1, middle).includes(']')) return null;
    return { span: this.link(text.slice(at + 1, middle), href, where), next: end + 1 };
  }

  // Bold, italics and links, as spans of text.
  spans(raw, where) {
    const text = this.resolve(raw, where);
    const out = [];
    let plain = '';
    let at = 0;
    while (at < text.length) {
      const found = text[at] === '*' || text[at] === '[' ? this.markup(text, at, where) : null;
      if (!found) {
        plain += text[at++];
        continue;
      }
      if (plain) out.push({ text: plain });
      out.push(found.span);
      plain = '';
      at = found.next;
    }
    if (plain) out.push({ text: plain });
    if (out.some(span => !span.href && MARKUP_LEFT.test(span.text))) this.refuse(`${where}: markup the subset does not read (a lone *, a [ or a titled link)`);
    return out;
  }

  heading(rest, where) {
    const trimmed = rest.trim();
    const open = trimmed.lastIndexOf('{#');
    if (open < 0 || !trimmed.endsWith('}')) {
      this.refuse(`${where}: heading "${trimmed}" has no {#id}`);
      return { title: this.resolve(trimmed, where), id: null };
    }
    const id = trimmed.slice(open + 2, -1);
    if (!ID.test(id)) this.refuse(`${where}: id "${id}" is not lowercase words and hyphens`);
    return { title: this.resolve(trimmed.slice(0, open).trim(), where), id };
  }

  card(lines, where) {
    const found = { id: null, title: null, lines: [], example: null };
    for (const line of lines) {
      // A value's own * is arithmetic, not markup: the line is read without its values.
      if (MARKUP_LEFT.test(line.replace(/\{[^{}]*\}/g, ''))) this.refuse(`${where}: a card is plain text, with no markup`);
      const field = /^(id|title):(.*)$/.exec(line);
      if (field?.[1] === 'id') found.id = field[2].trim();
      else if (field) found.title = this.resolve(field[2].trim(), where);
      else if (line.startsWith('> ')) found.example = this.resolve(line.slice(2).trim(), where);
      else if (line.trim()) found.lines.push(this.resolve(line.trim(), where));
    }
    this.checkCard(found, where);
    return found;
  }

  checkCard(found, where) {
    const name = found.id ?? '?';
    if (!found.id) this.refuse(`${where}: a card has no id`);
    else if (!this.conceptIds.has(found.id)) this.refuse(`${where}: card "${found.id}" is not in ConceptCards.Ids`);
    if (!found.title) this.refuse(`${where}: card "${name}" has no title`);
    if (found.lines.length < 1 || found.lines.length > 3) this.refuse(`${where}: card "${name}" has ${found.lines.length} lines; one to three`);
    if (!found.example) this.refuse(`${where}: card "${name}" has no example`);
  }

  source(source, where) {
    const adr = /^ADR (\d{4})$/.exec(source);
    const rule = /^game-rules#([a-z0-9-]+)$/.exec(source);
    if (adr && !this.given.adrs.has(adr[1])) this.refuse(`${where}: ADR ${adr[1]} does not exist`);
    else if (rule && !this.given.gameRules.has(rule[1])) this.refuse(`${where}: game-rules#${rule[1]} is not a heading of docs/domain/game-rules.md`);
    else if (!adr && !rule) this.refuse(`${where}: "${source}" is not an ADR or a game-rules heading`);
  }

  sources(text, where) {
    const named = text.split(',').map(source => source.trim()).filter(Boolean);
    for (const source of named) this.source(source, where);
    return named;
  }
}

// A table row's cells: a bar inside a value ({n|round|rounds}) is the value's, not a cell's edge.
function cellsOf(row) {
  const cells = [''];
  let depth = 0;
  for (const character of row) {
    if (character === '{') depth += 1;
    else if (character === '}') depth = Math.max(0, depth - 1);
    if (character === '|' && depth === 0) cells.push('');
    else cells[cells.length - 1] += character;
  }
  return cells;
}

/** One part file read line by line: each block reader takes the lines it owns and leaves the next one. */
class PartReader {
  constructor(build, file, number) {
    this.build = build;
    this.file = file;
    this.part = { id: null, number, title: null, blocks: [], sections: [] };
    this.section = null;
    this.closed = false;
  }

  refuse(message) {
    this.build.refuse(message);
  }

  add(block, where) {
    if (this.closed) this.refuse(`${where}: section "${this.section.id}": nothing follows its sources line but its card`);
    (this.section ?? this.part).blocks.push(block);
  }

  read(text) {
    const lines = text.split('\n');
    let at = 0;
    while (at < lines.length) at = this.block(lines, at, `${this.build.folder}/${this.file}:${at + 1}`);
    // Each section's own lines, as written, for the French edition's check; not part of the built book.
    const raw = new Map();
    let current = null;
    for (const line of lines) {
      const heading = /^## .*\{#([^}]+)\}\s*$/.exec(line);
      if (heading) current = heading[1];
      if (current) raw.set(current, `${raw.get(current) ?? ''}\n${line}`);
    }
    Object.defineProperty(this.part, 'raw', { value: raw });
    this.finish();
    return this.part;
  }

  block(lines, at, where) {
    const line = lines[at];
    if (!line.trim()) return at + 1;
    if (HEADING.test(line)) return this.heading(line, at, where);
    if (line.startsWith('```')) return this.fence(lines, at, where);
    if (line.startsWith('sources:')) return this.sources(line, at, where);
    if (line.startsWith('>')) return this.outside('a quote is outside the subset', at, where);
    if (isOutside(line)) return this.outside(`"${line.trim()}" is outside the subset; a list item starts with "- ", a heading with "# " and its {#id}`, at, where);
    if (line.startsWith('|')) return this.table(lines, at, where);
    if (LIST_ITEM.test(line)) return this.list(lines, at, where);
    return this.paragraph(lines, at, where);
  }

  outside(message, at, where) {
    this.refuse(`${where}: ${message}`);
    return at + 1;
  }

  heading(line, at, where) {
    const [, marks, rest] = HEADING.exec(line);
    const { title, id } = this.build.heading(rest, where);
    if (marks.length === 1) this.partHeading(title, id, where);
    else if (marks.length === 2) this.sectionHeading(title, id, where);
    else this.refuse(`${where}: a heading of level ${marks.length} is outside the subset; a part has sections, a section has none`);
    return at + 1;
  }

  partHeading(title, id, where) {
    if (this.part.title === null && this.section === null && this.part.blocks.length === 0) Object.assign(this.part, { id, title });
    else this.refuse(`${where}: a part file has one heading of level 1, its first line`);
  }

  sectionHeading(title, id, where) {
    if (this.part.title === null) this.refuse(`${where}: the part's heading of level 1 comes first`);
    this.section = { id, number: `${this.part.number}.${this.part.sections.length + 1}`, title, blocks: [], sources: [], card: null };
    this.closed = false;
    this.part.sections.push(this.section);
  }

  fence(lines, at, where) {
    const end = lines.indexOf('```', at + 1);
    const next = end < 0 ? lines.length : end + 1;
    if (lines[at].trim() !== '```card') this.refuse(`${where}: a code block is outside the subset; only a card is fenced`);
    else if (end < 0) this.refuse(`${where}: a card is not closed`);
    else this.placeCard(lines.slice(at + 1, end), where);
    return next;
  }

  placeCard(body, where) {
    const { section } = this;
    if (!section) return this.refuse(`${where}: a card belongs to a section`);
    if (section.card) return this.refuse(`${where}: section "${section.id}" has two cards`);
    if (section.sources.length === 0) this.refuse(`${where}: section "${section.id}": its card follows its sources line`);
    section.card = this.build.card(body, where);
    this.closed = true;
    return undefined;
  }

  sources(line, at, where) {
    const { section } = this;
    if (!section) this.refuse(`${where}: a sources line belongs to a section`);
    else {
      if (section.sources.length > 0) this.refuse(`${where}: section "${section.id}" has two sources lines`);
      section.sources = this.build.sources(line.slice('sources:'.length), where);
      this.closed = true;
    }
    return at + 1;
  }

  table(lines, at, where) {
    const rows = [];
    let next = at;
    while (next < lines.length && lines[next].startsWith('|')) rows.push(lines[next++]);
    const cells = row => cellsOf(row.trim().slice(1, row.trim().endsWith('|') ? -1 : undefined)).map(cell => this.build.spans(cell.trim(), where));
    if (rows.length < 2 || !SEPARATOR.test(rows[1].trim())) this.refuse(`${where}: a table needs its header and a separator line`);
    this.add({ type: 'table', header: cells(rows[0]), rows: rows.slice(2).map(cells) }, where);
    return next;
  }

  list(lines, at, where) {
    const ordered = !lines[at].startsWith('-');
    const items = [];
    let next = at;
    for (; next < lines.length; next += 1) {
      const item = LIST_ITEM.exec(lines[next]);
      if (item) items.push(item[2]);
      else if (/^ +\S/.test(lines[next])) items[items.length - 1] += ` ${lines[next].trim()}`;
      else break;
    }
    this.add({ type: 'list', ordered, items: items.map(text => this.build.spans(text, where)) }, where);
    return next;
  }

  paragraph(lines, at, where) {
    const text = [lines[at].trim()];
    let next = at + 1;
    while (next < lines.length && !opensBlock(lines[next])) text.push(lines[next++].trim());
    this.add({ type: 'paragraph', spans: this.build.spans(text.join(' '), where) }, where);
    return next;
  }

  finish() {
    if (this.part.title === null) this.refuse(`${this.build.folder}/${this.file}: a part file opens with its heading of level 1`);
    for (const section of this.part.sections) {
      if (section.sources.length === 0) this.refuse(`${this.build.folder}/${this.file}: section "${section.id}" has no sources line`);
    }
  }
}

// The ids: one a part or a section, each once.
function sectionIds(parts, build) {
  const ids = new Set();
  for (const id of parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)]).filter(Boolean)) {
    if (ids.has(id)) build.refuse(`id "${id}" is used twice`);
    ids.add(id);
  }
  return ids;
}

// The cards, each in one section.
function cardsOf(parts, build) {
  const cards = [];
  for (const section of parts.flatMap(part => part.sections).filter(one => one.card?.id)) {
    if (cards.some(one => one.id === section.card.id)) build.refuse(`card "${section.card.id}" is in two sections`);
    else cards.push({ id: section.card.id, section: section.id });
  }
  return cards;
}

// An id never dies: one main's book or the working one had is still a section, or moved.json says where it went;
// and every link, in the book or from the pages, lands on a section.
function checkLinks(given, ids, moved, build) {
  for (const [from, to] of Object.entries(moved)) {
    if (ids.has(from)) build.refuse(`rules/moved.json redirects "${from}", which is still a section`);
    if (!ids.has(to)) build.refuse(`rules/moved.json sends "${from}" to "${to}", which is not a section`);
  }
  for (const error of given.previous?.errors ?? []) build.refuse(error);
  const lands = id => ids.has(id) || Object.hasOwn(moved, id);
  for (const id of (given.previous?.ids ?? []).filter(id => !lands(id))) build.refuse(`id "${id}" is gone and rules/moved.json does not redirect it`);
  for (const { id, where } of build.bookLinks.filter(link => !ids.has(link.id))) build.refuse(`${where}: a link to #${id}, which is not a section`);
  for (const { file, id } of given.links.filter(link => !lands(link.id))) build.refuse(`${file} links to /rules#${id}, which is not a section`);
}

// rules/changes.md: one dated line per change a player would notice.
function readChanges(text, build) {
  const file = `${build.folder}/changes.md`;
  const changes = [];
  for (const line of (text ?? '').split('\n').map(one => one.trim()).filter(one => one && !one.startsWith('#'))) {
    const change = /^- (\d{4}-\d{2}-\d{2}): (.+)$/.exec(line);
    if (!change) {
      build.refuse(`${file}: "${line}" is not "- YYYY-MM-DD: what changed"`);
      continue;
    }
    const adrs = [...change[2].matchAll(/ADR (\d{4})/g)].map(match => match[1]);
    for (const adr of adrs.filter(one => !build.given.adrs.has(one))) build.refuse(`${file}: ADR ${adr} does not exist`);
    build.words(change[2], file);
    changes.push({ date: change[1], text: change[2], adrs });
  }
  return changes;
}

const listed = ids => (ids.length > 0 ? ids.join(', ') : 'none');
// The values a section's lines draw, sorted, so a translation may move them but never type one or drop one.
// A count's nouns are the language's own, so a value is compared without them.
const valuesOf = text => [...(text ?? '').matchAll(/\{([^{}#][^{}]*)\}/g)].map(match => match[1].split('|')[0].replace(/\s+/g, '')).sort((a, b) => a.localeCompare(b));
const cardOf = section => section.card?.id ?? 'none';

// The French part holds the English part's sections, in its order, each citing the same sources and carrying the
// same card: a translation, never another book.
function checkPart(english, french, where, build) {
  if (french.id !== english.id) build.refuse(`${where}: the part's id is "${french.id}"; the English part's is "${english.id}"`);
  const englishIds = english.sections.map(section => section.id);
  const frenchIds = french.sections.map(section => section.id);
  for (const id of englishIds.filter(id => !frenchIds.includes(id))) build.refuse(`${where}: section "${id}" is missing; the French edition has every section`);
  for (const id of frenchIds.filter(id => !englishIds.includes(id))) build.refuse(`${where}: section "${id}" is not in the English edition`);
  const shared = englishIds.filter(id => frenchIds.includes(id));
  if (shared.join() !== frenchIds.filter(id => englishIds.includes(id)).join()) build.refuse(`${where}: the sections are not in the English order (${shared.join(', ')})`);
  for (const section of french.sections) {
    const original = english.sections.find(one => one.id === section.id);
    if (!original) continue;
    if (section.sources.join() !== original.sources.join()) build.refuse(`${where}: section "${section.id}" cites ${listed(section.sources)}; the English cites ${listed(original.sources)}`);
    if (cardOf(section) !== cardOf(original)) build.refuse(`${where}: section "${section.id}" carries card ${cardOf(section)}; the English carries ${cardOf(original)}`);
    const frenchValues = valuesOf(french.raw?.get(section.id));
    const englishValues = valuesOf(english.raw?.get(section.id));
    if (frenchValues.join('\n') !== englishValues.join('\n')) build.refuse(`${where}: section "${section.id}" draws ${listed(frenchValues)}; the English draws ${listed(englishValues)}`);
  }
}

// rules/fr/changes.md: the English lines translated, one for one, with the same dates and ADRs.
function checkChanges(english, french, build) {
  if (french.length !== english.length) build.refuse(`rules/fr/changes.md has ${french.length} lines; rules/changes.md has ${english.length}`);
  french.forEach((change, at) => {
    const original = english[at];
    if (original && (change.date !== original.date || change.adrs.join() !== original.adrs.join())) {
      build.refuse(`rules/fr/changes.md, line ${at + 1}: ${change.date} (${listed(change.adrs)}); the English line is ${original.date} (${listed(original.adrs)})`);
    }
  });
}

function bookOf(given, build, parts, cards, moved, changes) {
  return {
    language: build.language,
    drawnFrom: { ruleSet: given.ruleSet.path, rules: given.ruleSet.values, engine: given.engine ?? {}, creature: given.creature.id, spells: [...build.used].sort((a, b) => a.localeCompare(b)), mutations: [...build.usedMutations].sort((a, b) => a.localeCompare(b)) },
    parts,
    cards,
    moved,
    changes,
  };
}

// The French edition, read like the English and held to it; its refusals join the English's.
function buildFrench(given, english, ids, errors) {
  const build = new Build(given, { language: 'fr', folder: 'rules/fr', errors });
  const files = given.parts.map(part => part.file);
  for (const file of files.filter(file => !given.french.parts.some(part => part.file === file))) build.refuse(`rules/fr/${file} is missing; the French edition has every part`);
  for (const { file } of given.french.parts.filter(part => !files.includes(part.file))) build.refuse(`rules/fr/${file} has no English part`);
  const parts = given.french.parts.map(({ file, text }) => new PartReader(build, file, files.indexOf(file) + 1).read(text)).filter(part => part.number > 0);
  for (const part of parts) checkPart(english.parts[part.number - 1], part, `rules/fr/${files[part.number - 1]}`, build);
  for (const { id, where } of build.bookLinks.filter(link => !ids.has(link.id))) build.refuse(`${where}: a link to #${id}, which is not a section`);
  const changes = readChanges(given.french.changes, build);
  checkChanges(english.changes, changes, build);
  const cards = parts.flatMap(part => part.sections).filter(section => section.card?.id).map(section => ({ id: section.card.id, section: section.id }));
  return bookOf(given, build, parts, cards, english.moved, changes);
}

/**
 * Builds the book from what `readInputs` read, and its French edition when there is one (`french`, else null);
 * `errors` lists every refusal of both, and the books are complete only without one.
 */
export function buildRules(given) {
  const build = new Build(given);
  for (const missing of given.engineMissing ?? []) build.refuse(missing);
  const parts = given.parts.map(({ file, text }, index) => new PartReader(build, file, index + 1).read(text));
  const ids = sectionIds(parts, build);
  const cards = cardsOf(parts, build);
  const moved = given.moved ?? {};
  checkLinks(given, ids, moved, build);
  const changes = readChanges(given.changes, build);
  const book = bookOf(given, build, parts, cards, moved, changes);
  const french = given.french ? buildFrench(given, book, ids, build.errors) : null;
  return { book, french, errors: build.errors };
}

/** The committed file's text: two-space JSON and a final newline, so a diff reads line by line. */
export function render(book) {
  return `${JSON.stringify(book, null, 2)}\n`;
}

// --- the command --------------------------------------------------------------------------------------------------

function main() {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
  const { book, french, errors } = buildRules(readInputs(root));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exit(1);
  }
  const outputs = [{ file: OUTPUT, built: book }, ...(french ? [{ file: OUTPUT_FR, built: french }] : [])];
  if (process.argv.includes('--check')) {
    const stale = outputs.filter(({ file, built }) => (existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '') !== render(built));
    for (const { file } of stale) console.error(`${file} is not the build of rules/: run node scripts/rules/build.mjs`);
    if (stale.length > 0) process.exit(1);
    console.log(`${outputs.map(({ file }) => file).join(' and ')}: up to date.`);
    return;
  }
  for (const { file, built } of outputs) {
    writeFileSync(join(root, file), render(built));
    const sections = built.parts.reduce((count, part) => count + part.sections.length, 0);
    console.log(`${relative(root, join(root, file))}: ${built.parts.length} parts, ${sections} sections, ${built.cards.length} cards.`);
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
