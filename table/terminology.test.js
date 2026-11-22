// The words a player reads hold to the Player-facing EN column of docs/domain/player-terminology.md: the labels
// terms.js serves say that column, and no legacy word the column replaced is left in the table's pages, its
// scripts' sentences or the battle view's. The tables of that page are read here, so a renamed word is renamed
// in one place.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { retiredWords } from '../scripts/rules/build.mjs';
import { rewrites as rewritesOf, termRows } from '../scripts/terminology.mjs';
import { creatureName, decisionLabel, phaseLabel, stageLabel, stepLabel, tempoLabel } from './terms.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const terminology = readFileSync(join(root, 'docs/domain/player-terminology.md'), 'utf8');

// The page's tables, read by the one reader the rulebook's build shares (scripts/terminology.mjs).
const rows = () => termRows(terminology).map(([internal, legacy, en]) => ({ internal, legacy, en }));
const rewrites = () => rewritesOf(terminology);

const bold = cell => /\*\*(.+?)\*\*/.exec(cell)?.[1];
const row = internal => rows().find(r => r.internal.includes(internal)) ?? assert.fail(`no row for ${internal}`);

test('the page names every word the table translates', () => {
  for (const internal of ['`Speed.Quick`', '`Speed.Standard`', 'Tier `level`', 'Speed (`Speed` choice)', 'named `Main`']) {
    assert.ok(row(internal), internal);
  }
  for (const phase of ['StartOfRound', 'Planning', 'Combat', 'EndOfRound']) assert.ok(row(`\`${phase}\``), phase);
});

test('a speed reads as the tempo the EN column names', () => {
  assert.equal(tempoLabel('Quick'), bold(row('`Speed.Quick`').en));
  assert.equal(tempoLabel('Standard'), bold(row('`Speed.Standard`').en));
  assert.equal(decisionLabel('Speed'), bold(row('Speed (`Speed` choice)').en));
  assert.equal(stepLabel('Speed'), bold(row('Speed (`Speed` choice)').en));
});

test('a round phase reads as the EN column names it', () => {
  for (const phase of ['StartOfRound', 'Planning', 'Combat', 'EndOfRound']) {
    assert.equal(phaseLabel(phase), bold(row(`\`${phase}\``).en), phase);
  }
});

test('a level reads as the stage the EN column shows, and the base creature as a creature', () => {
  const stage = row('Tier `level`').en;
  assert.equal(stageLabel(2), /\((.+)\)/.exec(stage)[1]);
  assert.equal(creatureName('Main'), bold(row('named `Main`').en));
});

// Legacy words an everyday sentence also uses: matched alone only as a capitalised label ("Speed", not "playback
// speed"); the sentences the page rewrote ("Choose speed") are matched in any case. Main, Start and End open
// ordinary sentences and menus, so terms.js alone answers for them.
const EVERYDAY = new Set(['Speed', 'Quick', 'Standard', 'Planning']);
const SKIPPED = new Set(['Main', 'Start', 'End']);

// The legacy words the EN column replaced, each with the pattern that finds it in a player's text.
function retired() {
  const words = new Map();
  for (const { legacy, en } of rows()) {
    const word = legacy.replace(/\s*\(.*\)$/, '').trim();
    if (!word || SKIPPED.has(word) || en.includes(`**${word}**`)) continue;
    const flags = EVERYDAY.has(word) ? 'u' : 'iu';
    words.set(word, new RegExp(`\\b${word.replace(/\s+/g, '\\s+')}s?\\b`, flags));
  }
  for (const { before, after } of rewrites()) {
    if (before.toLowerCase() !== after.toLowerCase()) words.set(before, new RegExp(`\\b${before.replace(/\s+/g, '\\s+')}\\b`, 'iu'));
  }
  return words;
}

test('the retired words are the ones the page replaced', () => {
  assert.deepEqual([...retired().keys()].filter(word => !word.includes(' ') || word === 'Talent atlas').sort(), [
    'Package',
    'Planning',
    'PvP',
    'Quick',
    'Speed',
    'Standard',
    'Talent atlas',
    'Tier',
  ]);
  assert.ok(retired().has('Choose speed'));
  assert.ok(retired().has('Speeds revealed'));
  assert.ok(!retired().has('Your spellbook'), 'a sentence the page kept is not retired');
});

// Every script, page and spec of the repository (not its dependencies, builds or frozen prototypes).
function repositorySources(folder = '') {
  const found = [];
  for (const entry of readdirSync(join(root, folder), { withFileTypes: true })) {
    const path = folder ? `${folder}/${entry.name}` : entry.name;
    if (entry.isDirectory()) {
      if (!/^(\.|node_modules$|battle-build$|legacy$|artifacts$|bin$|obj$|dist$)/.test(entry.name)) found.push(...repositorySources(path));
    } else if (/\.(m?js|ts|html)$/.test(entry.name)) found.push(path);
  }
  return found;
}

// A file whose code, its comments aside, names player-terminology.md loads it, however it then parses it. Only the
// reviewer picker names it as a path a diff touches, with its test; it reads no table.
const PAGE = 'player-terminology';
const NAMES_THE_PATH = new Set(['scripts/review/reviewers.mjs', 'studio/reviewers.test.js']);
const code = text => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '').replace(/(^|[^:\\])\/\/.*$/gm, '$1');
const importsReader = text => /from\s+['"][./]*(scripts\/)?terminology\.mjs['"]/.test(text);

test('one reader loads player-terminology.md, and every scanner goes through it', () => {
  const texts = new Map(repositorySources().map(path => [path, readFileSync(join(root, path), 'utf8')]));
  const ownReaders = [...texts]
    .filter(([path, text]) => code(text).includes(PAGE) && !importsReader(text))
    .map(([path]) => path)
    .filter(path => path !== 'scripts/terminology.mjs' && !NAMES_THE_PATH.has(path));
  assert.deepEqual(ownReaders, [], 'a scanner loads the page itself: import scripts/terminology.mjs');
  const users = [...texts].filter(([, text]) => importsReader(text)).map(([path]) => path).sort();
  assert.deepEqual(users, Object.keys(scanners()).sort(), 'a new scanner joins scanners() below, so the Before test reads it');
});

// The scanners of the player's words, each with its patterns, by the file that reads the page.
const scanners = () => ({
  'table/terminology.test.js': [...retired().values()],
  'scripts/rules/build.mjs': [...retiredWords(terminology).values()].map(word => word.pattern),
});

// The trap the reviews found twice (W8, then R1): a scanner that exempts an everyday word lets the page's own
// rewritten sentences through ("Choose speed"). Every scanner refuses each one, whatever the case of its letters.
test('every scanner of the player\'s words refuses each sentence of the Before column, whatever its case', () => {
  const replaced = rewrites().filter(({ before, after }) => before.toLowerCase() !== after.toLowerCase());
  assert.ok(replaced.length > 0, 'the Before column was read');
  for (const [scanner, patterns] of Object.entries(scanners())) {
    for (const { before } of replaced) {
      for (const sentence of [before, before.toLowerCase(), before.toUpperCase()]) {
        assert.ok(patterns.some(pattern => pattern.test(sentence)), `${scanner} lets "${sentence}" through`);
      }
    }
  }
});

// What a person reads of a piece of HTML: its text, and the attributes a screen reader, a tooltip or a button says.
function htmlText(html) {
  const out = [];
  const body = html
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<(script|style)\b[\s\S]*?<\/\1>/gi, ' ');
  for (const [, , value, single] of body.matchAll(/\b(aria-label|title|alt|placeholder|content|value)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi)) {
    out.push(value ?? single);
  }
  for (const text of body.replace(/<[^>]*>/g, '\n').split('\n')) if (text.trim()) out.push(text.trim());
  return out;
}

// The properties whose value a person reads as it is: a string assigned to one is a label, even of one word.
const SHOWN = new Set(['textContent', 'innerText', 'title', 'ariaLabel', 'placeholder', 'alt']);

// Every string literal of a script as { text, shown }, its interpolations blanked; comments and regular
// expressions skipped. `shown` says the literal is assigned to a property a person reads (`x.title = 'Go'`,
// `x.textContent = step === 'a' ? 'Yes' : ('No')`): a value the expression can take, not an argument, a key, a
// property of an object or a side of a comparison.
function literals(source) {
  const out = [];
  const n = source.length;
  let i = 0;
  let previous = '';
  let word = '';
  // The brackets open here, true for one that only groups; the assignment to a shown property being read, as
  // the number of brackets open where it started; and the literal just read, which a comparison disowns.
  let open = [];
  let assigned = null;
  let literal = null;
  const shown = () =>
    assigned !== null && open.slice(assigned).every(Boolean) && /^[=?:|&(+]$/.test(previous);
  const push = (label, text) => {
    literal = { text, shown: label };
    out.push(literal);
    previous = 'value';
  };
  const quoted = quote => {
    let text = '';
    for (i++; i < n && source[i] !== quote; i++) text += source[i] === '\\' ? source[++i] : source[i];
    i++;
    return text;
  };
  const template = () => {
    let text = '';
    for (i++; i < n && source[i] !== '`'; ) {
      if (source[i] === '\\') {
        text += source[i + 1];
        i += 2;
      } else if (source[i] === '$' && source[i + 1] === '{') {
        i += 2;
        const outer = [open, assigned];
        [open, assigned] = [[], null];
        scan(true);
        [open, assigned] = outer;
        text += ' ';
      } else text += source[i++];
    }
    i++;
    return text;
  };
  const regex = () => {
    let inClass = false;
    for (i++; i < n && (source[i] !== '/' || inClass); i++) {
      if (source[i] === '\\') i++;
      else if (source[i] === '[') inClass = true;
      else if (source[i] === ']') inClass = false;
    }
    for (i++; /[a-z]/.test(source[i] ?? ''); ) i++;
  };
  function scan(untilBrace) {
    let depth = 0;
    while (i < n) {
      const c = source[i];
      if (c === '/' && source[i + 1] === '/') {
        while (i < n && source[i] !== '\n') i++;
      } else if (c === '/' && source[i + 1] === '*') {
        const end = source.indexOf('*/', i + 2);
        i = end < 0 ? n : end + 2;
      } else if (c === '/' && (previous === '' || previous === 'return' || /^([(,=:[!&|?{};+\-*%<>~^]|compared)$/.test(previous))) {
        regex();
        previous = 'value';
        literal = null;
      } else if (c === "'" || c === '"') {
        push(shown(), quoted(c));
      } else if (c === '`') {
        const label = shown();
        push(label, template());
      } else if (/[\w$]/.test(c)) {
        word = previous === '.' ? '.' : '';
        while (i < n && /[\w$]/.test(source[i])) word += source[i++];
        previous = word === 'return' || word === 'typeof' ? 'return' : 'word';
        literal = null;
      } else {
        const arrow = (c === '=' && source[i + 1] === '>') || (c === '>' && source[i - 1] === '=');
        const compared =
          !arrow && (c === '<' || c === '>' || (c === '=' && (source[i + 1] === '=' || /[=!<>]/.test(source[i - 1]))));
        if (compared) {
          // The literal on either side of a comparison is a value the script tests, not one it shows.
          if (literal) literal.shown = false;
        } else if (c === '=' && !arrow) {
          // An assignment to a shown property opens a label, `+=` included; any other assignment closes it.
          const target = previous === 'word' || (previous === '+' && source[i - 1] === '+');
          assigned = target && word[0] === '.' && SHOWN.has(word.slice(1)) ? open.length : null;
        } else if (c === '(' || c === '[' || c === '{') {
          // A parenthesis after a name or a value is a call: its arguments are not what the property shows.
          open.push(c === '(' && !/^(word|value|[)\]])$/.test(previous));
        } else if (c === ')' || c === ']' || c === '}') {
          open.pop();
          if (assigned !== null && open.length < assigned) assigned = null;
        } else if ((c === ';' || c === ',') && assigned === open.length) assigned = null;
        if (untilBrace && c === '{') depth++;
        if (untilBrace && c === '}' && depth-- === 0) {
          i++;
          return;
        }
        if (!/\s/.test(c)) {
          previous = compared ? 'compared' : c;
          literal = null;
        }
        i++;
      }
    }
  }
  scan(false);
  return out;
}

// The sentences of a script a person reads: string literals of more than one word, markup read as HTML, and
// class lists left out. A one-word literal is read only where it is assigned to a property a person reads
// (`textContent`, `title`, `ariaLabel`...): every other one that says a retired word is an engine value
// ('Speed', 'Quick', 'Planning'), and a one-word label belongs in terms.js or in a page's HTML, which is.
function scriptText(source) {
  return literals(source).flatMap(({ text, shown }) => {
    if (text.includes('<')) return htmlText(text);
    if (!/\S\s+\S/.test(text)) return shown && text.trim() ? [text] : [];
    const tokens = text.trim().split(/\s+/);
    if (tokens.every(token => /^[a-z0-9_-]+$/.test(token)) && tokens.some(token => /[-_]/.test(token))) return [];
    return [text];
  });
}

// Every file under a folder, subfolders included, as a path from the root; the battle bundle is built, not written.
const files = folder =>
  readdirSync(join(root, folder), { recursive: true })
    .map(file => `${folder}/${String(file).replaceAll('\\', '/')}`)
    .filter(path => !path.startsWith('table/battle-build/') && !path.includes('/node_modules/'));

const read = path => readFileSync(join(root, path), 'utf8');

const sources = () => [
  ...files('table')
    .filter(path => path.endsWith('.html'))
    .map(path => ({ path, texts: htmlText(read(path)) })),
  ...files('table')
    .filter(path => path.endsWith('.js') && !path.endsWith('.test.js') && path !== 'table/terms.js')
    .map(path => ({ path, texts: scriptText(read(path)) })),
  ...files('battle/src')
    .filter(path => /\.[jt]s$/.test(path))
    .map(path => ({ path, texts: scriptText(read(path)) })),
];

function violations(files) {
  const found = [];
  for (const { path, texts } of files) {
    for (const text of texts) {
      for (const [word, pattern] of retired()) {
        if (pattern.test(text)) found.push(`${path}: "${text.trim().slice(0, 100)}" says ${word}`);
      }
    }
  }
  return found;
}

test('no page, sentence or battle label says a word the EN column retired', () => {
  assert.deepEqual(violations(sources()), []);
});

test('a retired word is found in a page, a sentence, a label and a tooltip', () => {
  const page = htmlText(
    "<button title=\"Go on\" aria-label='Buy a package'>Go</button><h2>Talent atlas</h2><p>Tier 2</p><input value=\"Quick\"><script>\"Tier\"</script>",
  );
  const script = scriptText(
    'const a = `Choose speed for ${x}`; const b = "Speed revealed"; el.innerHTML = "<b>Quick</b>"; m = \'choose your package first\';',
  );
  const labels = scriptText(
    "chip.textContent = 'Quick'; b.title = fast ? 'Speed' : `Tier`; c.ariaLabel = 'Go'; d.title += 'Package';",
  );
  const chosen = scriptText(
    "b.title = step === 'Speed' ? 'Quick' : isFast(step, 'Tier') ? ('Standard') : names['Package'] || 'Planning';",
  );
  assert.deepEqual(violations([{ path: 'page', texts: page }, { path: 'script', texts: script }]), [
    'page: "Buy a package" says Package',
    'page: "Buy a package" says Buy a package',
    'page: "Quick" says Quick',
    'page: "Talent atlas" says Talent atlas',
    'page: "Tier 2" says Tier',
    'page: "Tier 2" says Tier 2',
    'script: "Choose speed for" says Choose speed',
    'script: "Speed revealed" says Speed',
    'script: "Quick" says Quick',
    'script: "choose your package first" says Package',
  ]);
  assert.deepEqual(violations([{ path: 'labels', texts: labels }]), [
    'labels: "Quick" says Quick',
    'labels: "Speed" says Speed',
    'labels: "Tier" says Tier',
    'labels: "Package" says Package',
  ]);
  assert.deepEqual(violations([{ path: 'chosen', texts: chosen }]), [
    'chosen: "Quick" says Quick',
    'chosen: "Standard" says Standard',
    'chosen: "Planning" says Planning',
  ]);
});

test('identifiers, engine values, comments, regular expressions and everyday words are not a player\'s text', () => {
  const script = [
    "// Choose a package here",
    "chip.className = 'package-tag enemy-package';",
    "const speeds = ['Quick', 'Standard'];",
    "const r = /Talent atlas here/g;",
    "label.textContent = 'Playback speed';",
    "if (x / 2 > 1) y = 'Main menu';",
    "chip.textContent = tempoLabel('Quick');",
    "b.title = step === 'Speed' ? stepLabel(step) : '';",
    "c.dataset.step = 'Planning'; const title = 'Tier'; d.textContent = names['Quick'];",
    "e.textContent = `${speed === 'Quick' ? 1 : 2}`;",
    "f.title = 'Speed' === step ? go({ title: 'Tier' }) : ''; g.title = '', h = 'Quick'; i.alt = ''\nconst j = ['Standard'];",
  ].join('\n');
  assert.deepEqual(violations([{ path: 'script', texts: scriptText(script) }]), []);
});
