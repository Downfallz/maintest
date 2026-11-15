// The words a player reads hold to the Player-facing EN column of docs/domain/player-terminology.md: the labels
// terms.js serves say that column, and no legacy word the column replaced is left in the table's pages, its
// scripts' sentences or the battle view's. The tables of that page are read here, so a renamed word is renamed
// in one place.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { creatureName, decisionLabel, phaseLabel, stageLabel, stepLabel, tempoLabel } from './terms.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const terminology = readFileSync(join(root, 'docs/domain/player-terminology.md'), 'utf8');

// The rows of the page's tables whose header starts with `Internal`: { internal, legacy, en } as written.
function rows() {
  const found = [];
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
    found.push({ internal: cells[0], legacy: cells[1], en: cells[2] });
  }
  return found;
}

// The page's "Before | After" table: the sentences it rewrote, as { before, after }.
function rewrites() {
  const found = [];
  let inTable = false;
  for (const line of terminology.split('\n')) {
    const cells = line.startsWith('|') ? line.split('|').slice(1, -1).map(cell => cell.trim()) : null;
    if (!cells) inTable = false;
    else if (cells[0] === 'Before') inTable = true;
    else if (inTable && !/^-+$/.test(cells[0])) found.push({ before: cells[0], after: cells[1] });
  }
  return found;
}

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

// Every string literal of a script, its interpolations blanked; comments and regular expressions skipped.
function literals(source) {
  const out = [];
  const n = source.length;
  let i = 0;
  let previous = '';
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
        scan(true);
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
      } else if (c === '/' && (previous === '' || previous === 'return' || /^[(,=:[!&|?{};+\-*%<>~^]$/.test(previous))) {
        regex();
        previous = 'value';
      } else if (c === "'" || c === '"') {
        out.push(quoted(c));
        previous = 'value';
      } else if (c === '`') {
        out.push(template());
        previous = 'value';
      } else if (/[\w$]/.test(c)) {
        let word = '';
        while (i < n && /[\w$]/.test(source[i])) word += source[i++];
        previous = word === 'return' || word === 'typeof' ? 'return' : 'value';
      } else {
        if (untilBrace && c === '{') depth++;
        if (untilBrace && c === '}' && depth-- === 0) {
          i++;
          return;
        }
        if (!/\s/.test(c)) previous = c;
        i++;
      }
    }
  }
  scan(false);
  return out;
}

// The sentences of a script a person reads: string literals of more than one word, markup read as HTML, and
// class lists left out. A one-word literal is not read: today every one that says a retired word is an engine
// value ('Speed', 'Quick', 'Planning'), and a one-word label belongs in terms.js or in a page's HTML, which is.
function scriptText(source) {
  return literals(source).flatMap(text => {
    if (text.includes('<')) return htmlText(text);
    if (!/\S\s+\S/.test(text)) return [];
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
});

test('identifiers, engine values, comments, regular expressions and everyday words are not a player\'s text', () => {
  const script = [
    "// Choose a package here",
    "chip.className = 'package-tag enemy-package';",
    "const speeds = ['Quick', 'Standard'];",
    "const r = /Talent atlas here/g;",
    "label.textContent = 'Playback speed';",
    "if (x / 2 > 1) y = 'Main menu';",
  ].join('\n');
  assert.deepEqual(violations([{ path: 'script', texts: scriptText(script) }]), []);
});
