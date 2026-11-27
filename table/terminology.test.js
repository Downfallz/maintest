// The words a player reads hold to the Player-facing EN column of docs/domain/player-terminology.md: the labels
// terms.js serves say that column, and no legacy word the column replaced is left in the table's pages, its
// scripts' sentences or the battle view's. The tables of that page are read here, so a renamed word is renamed
// in one place.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readCalendar } from '../scripts/promo/calendar.mjs';
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
    'Aftermath',
    'Package',
    'Planning',
    'PvP',
    'Quick',
    'Speed',
    'Standard',
    'Talent atlas',
    'Tier',
    'Upkeep',
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
  // The words a promotion's cut lays over the game (promo/README.md#a-cut): its title card and its captions.
  ...files('promo/cuts')
    .filter(path => path.endsWith('.json'))
    .map(path => {
      const cut = JSON.parse(read(path));
      return { path, texts: [cut.titleCard?.en, ...(cut.shots ?? []).map(stretch => stretch.caption?.en)].filter(text => typeof text === 'string') };
    }),
  // The words of a post of the promotion's calendar (promo/README.md#the-calendar), read by its own parser: its title
  // and its English words, wherever its sections are.
  ...readCalendar(join(root, 'promo/calendar')).map(post => ({ path: `promo/calendar/${post.file}`, texts: [post.title, post.words.en] })),
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

// A name of more than one word is a proper name in the EN column (Mutation Tree): a page writes it with the
// column's capitals, or all in capitals as an eyebrow does (MUTATION TREE), never as an ordinary phrase (#1022).
function miscased(files) {
  const names = [...new Set(rows().flatMap(({ en }) => [...en.matchAll(/\*\*(.+?)\*\*/g)].map(m => m[1])).filter(name => /\s/.test(name)))];
  const found = [];
  for (const { path, texts } of files) {
    for (const text of texts) {
      for (const name of names) {
        for (const [written] of text.matchAll(new RegExp(`\\b${name.replace(/\s+/g, '\\s+')}\\b`, 'giu'))) {
          if (written !== name && written !== written.toUpperCase()) found.push(`${path}: "${text.trim().slice(0, 100)}" writes ${written}`);
        }
      }
    }
  }
  return found;
}

// The public site says the names too (showcase/lore.mjs writes the field guide's words).
const showcaseSources = () => readdirSync(join(root, 'showcase'))
  .filter(name => name.endsWith('.mjs') && !name.endsWith('.test.mjs'))
  .map(name => ({ path: `showcase/${name}`, texts: scriptText(read(`showcase/${name}`)) }));

test('a name of several words in the EN column keeps its capitals in the table, the battle and the showcase', () => {
  assert.deepEqual(miscased([...sources(), ...showcaseSources()]), []);
});

test('a name of several words written as a phrase is found, its capitals and an eyebrow are not', () => {
  const texts = scriptText("a.textContent = 'Full mutation tree'; b.title = 'Open the Mutation Tree'; c.textContent = 'MUTATION TREE · YOURS';");
  assert.deepEqual(miscased([{ path: 'script', texts }]), ['script: "Full mutation tree" writes mutation tree']);
});

// The app says a chance as a percent and a tie between the teams as settled at random; the die belongs to the print
// edition (player-terminology.md, ## Chance and ties, ADR 0208). The record keeps the faces: no page prints one.
const DIE_WORDS = /\bd20\b|\bdic?e\b|\broll[\s-]?offs?\b|\b(re)?roll(s|ed)?\b/i;
const dieWords = files => files.flatMap(({ path, texts }) =>
  texts.filter(text => DIE_WORDS.test(text)).map(text => `${path}: "${text.trim().slice(0, 100)}"`));

// Every literal of a script, a single word included: a label handed to a helper (`text('span', 'd20', ...)`) or
// held in a constant is a label all the same, and a die is never an engine value here. A literal that is one
// identifier of the record (`rollOffs`, `criticalRoll`) is a field's name, not a word.
const everyLiteral = source => literals(source)
  .flatMap(({ text }) => text.includes('<') ? htmlText(text) : [text])
  .filter(text => !/^[a-z]+[A-Z][A-Za-z]*$/.test(text.trim()));
// The lines a tutorial's coach says, and its title and goal (scenarios/README.md): a player reads them in the match.
const tutorialLines = value => Array.isArray(value) ? value.flatMap(tutorialLines)
  : value && typeof value === 'object'
    ? Object.entries(value).flatMap(([key, one]) => ['says', 'title', 'goal'].includes(key) ? [one].flat().filter(text => typeof text === 'string') : tutorialLines(one))
    : [];
const dieSources = () => [
  ...files('table').filter(path => path.endsWith('.html')).map(path => ({ path, texts: htmlText(read(path)) })),
  ...files('table').filter(path => path.endsWith('.js') && !path.endsWith('.test.js')).map(path => ({ path, texts: everyLiteral(read(path)) })),
  ...files('battle/src').filter(path => /\.[jt]s$/.test(path)).map(path => ({ path, texts: everyLiteral(read(path)) })),
  ...files('scenarios/tutorials').filter(path => path.endsWith('.json')).map(path => ({ path, texts: tutorialLines(JSON.parse(read(path))) })),
];

test('no page of the table, label of the battle or line of a tutorial speaks of a die, a roll or a roll-off', () => {
  assert.ok(/^## Chance and ties$/m.test(terminology), 'the page says how a chance and a tie are worded');
  assert.ok(dieSources().some(({ path, texts }) => path.startsWith('scenarios/tutorials/') && texts.length > 0), 'the tutorials\' lines were read');
  assert.deepEqual(dieWords(dieSources()), []);
});

test('a die is found in a label, a sentence, a tooltip, a word handed to a helper and a tutorial\'s line, and a scroll or a field\'s name is not one', () => {
  const texts = everyLiteral(
    "a.textContent = `d20 ${face}+`; b.title = 'Ties broken by D20'; c.textContent = 'The dice gave your side its places'; d.textContent = 'A roll-off broke it'; " +
    "e.textContent = 'Recorded rolls'; f.textContent = 'Rerolled once'; g.textContent = 'Scroll to the top'; h.title = 'A role of its own'; i.textContent = 'Enrolled players'; " +
    "side.append(text('span', 'd20', 'timeline-tie')); export const RANDOM_TIE = 'Roll-off'; const rolls = board['rollOffs'], face = action['criticalRoll'];",
  );
  assert.deepEqual(dieWords([{ path: 'script', texts }]), [
    'script: "d20  +"', 'script: "Ties broken by D20"', 'script: "The dice gave your side its places"', 'script: "A roll-off broke it"',
    'script: "Recorded rolls"', 'script: "Rerolled once"', 'script: "d20"', 'script: "Roll-off"',
  ]);
  const lesson = tutorialLines({ title: 'Who acts first', steps: [{ at: { kind: 'Action' }, says: ['Pummel rolls for a critical.', 'It never crits at Instinct.'], concepts: ['critical'] }] });
  assert.deepEqual(dieWords([{ path: 'lesson', texts: lesson }]), ['lesson: "Pummel rolls for a critical."']);
});

// The round's two automatic moments read Round start and Round end (player-terminology.md, ## Round phases). The
// sentences are held by the retired words above; a label of one word handed to a helper or kept in a constant
// (`category: 'Upkeep'`, `title: 'Aftermath'`) is refused here, and so is a tutorial's line. The engine's own names
// stay: the interlude `upkeep`, the field `upkeepEnergy`, an element's id.
const MOMENT_LABEL = /^(Upkeep|Aftermath|UPKEEP|AFTERMATH)$/;
const MOMENT_WORDS = /\b(upkeep|aftermath)s?\b/i;
const momentWords = files => files.flatMap(({ path, texts }) => texts
  .filter(text => path.startsWith('scenarios/') ? MOMENT_WORDS.test(text) : MOMENT_LABEL.test(text.trim()))
  .map(text => `${path}: "${text.trim().slice(0, 100)}"`));

test('no label of one word and no line of a tutorial says Upkeep or Aftermath', () => {
  assert.deepEqual(momentWords(dieSources()), []);
});

test('a moment\'s old word is found as a label of one word and in a tutorial\'s line, and the engine\'s names are not', () => {
  const texts = everyLiteral("const row = { title: 'Aftermath', category: 'Upkeep' }; small.textContent = 'UPKEEP'; step.interlude = 'upkeep'; el('upkeep-dialog'); passive['upkeepEnergy'];");
  assert.deepEqual(momentWords([{ path: 'script', texts }]), ['script: "Aftermath"', 'script: "Upkeep"', 'script: "UPKEEP"']);
  const lesson = tutorialLines({ steps: [{ says: ['Bleed takes Health each Upkeep.', 'It counts down at the end of each round.', "It ends in the round's aftermath."] }] });
  assert.deepEqual(momentWords([{ path: 'scenarios/tutorials/lesson.json', texts: lesson }]), [
    'scenarios/tutorials/lesson.json: "Bleed takes Health each Upkeep."', 'scenarios/tutorials/lesson.json: "It ends in the round\'s aftermath."',
  ]);
});

// The notification modules speak of "this table" and of a "notification service" (player-terminology.md,
// ## Notifications, #1233, #1266): the host and the push service are the engineers' words.
const NOTIFICATION_MODULES = ['table/push.js', 'table/devices.js', 'table/battle-services.js'];
const ENGINEERS_WORDS = /\bhosts?\b|\bpush[\s-]*services?\b/i;

// The seat page's own test of its notifications, in table.js: that function alone, as the page's other sentences
// about the host are not yet settled (player-terminology.md).
const NOTIFICATION_FUNCTIONS = [{ path: 'table/table.js', name: 'testNotices' }];

/** The source of a top-level function, from its declaration to the closing brace at the start of a line. */
function functionSource(source, name) {
  const start = source.search(new RegExp(`^(?:async )?function ${name}\\(`, 'm'));
  assert.ok(start >= 0, `no function ${name}`);
  return source.slice(start, source.indexOf('\n}\n', start) + 3);
}

const engineersWords = source => literals(source).map(({ text }) => text).filter(text => ENGINEERS_WORDS.test(text));

test('no sentence of the notification modules tells a player "host" or "push service"', () => {
  const found = [
    ...NOTIFICATION_MODULES.flatMap(path => engineersWords(read(path)).map(text => `${path}: "${text}"`)),
    ...NOTIFICATION_FUNCTIONS.flatMap(({ path, name }) => engineersWords(functionSource(read(path), name)).map(text => `${path} ${name}: "${text}"`)),
  ];
  assert.deepEqual(found, []);
});

test('the seat page\'s test is read whole, and a "host" put back in it is found', () => {
  const body = functionSource(read('table/table.js'), 'testNotices');

  assert.match(body, /^async function testNotices\(state\) \{/);
  assert.match(body, /UNREACHABLE/);
  assert.match(body, /\n\}\n$/);
  assert.deepEqual(engineersWords(body.replace('UNREACHABLE;', "'Could not reach the host. Try again.';")), ['Could not reach the host. Try again.']);
});

test('the notification modules\' scanner finds "host" and "push service" in every spelling, and not in "hosted"', () => {
  for (const text of ['Could not reach the host.', 'The HOST answered', 'Two Hosts', 'gone from its push service', 'Push-Service', 'push  services']) {
    assert.ok(ENGINEERS_WORDS.test(text), text);
  }
  assert.ok(!ENGINEERS_WORDS.test('the hosted table, or this computer at localhost, can notify.'));
  assert.deepEqual(literals("// the host\nconst a = 'Could not reach the host. Try again.'; b = /host/;").map(({ text }) => text), ['Could not reach the host. Try again.']);
});
