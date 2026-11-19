import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { inflateSync } from 'node:zlib';

const root = new URL('../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');
test('every independently shipped shell carries the current brand palette', () => {
  execFileSync(process.execPath, [new URL('scripts/sync-branding.mjs', root).pathname, '--check']);
});
const tokens = Object.fromEntries([...read('docs/art/branding.css').matchAll(/(--[\w-]+): (#[\da-f]{6});/g)].map(match => [match[1], match[2]]));
const luminance = hex => hex.slice(1).match(/../g).map(value => parseInt(value, 16) / 255)
  .map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4)
  .reduce((sum, value, index) => sum + value * [.2126, .7152, .0722][index], 0);
const contrast = (a, b) => (Math.max(luminance(a), luminance(b)) + .05) / (Math.min(luminance(a), luminance(b)) + .05);
test('brand text, links and action labels remain readable on their intended surfaces', () => {
  for (const background of ['--brand-slate', '--brand-panel', '--brand-raised', '--brand-wash']) {
    for (const foreground of ['--brand-ivory', '--brand-muted', '--brand-gold', '--brand-ember']) {
      assert.ok(contrast(tokens[foreground], tokens[background]) >= 4.5, `${foreground} on ${background}`);
    }
  }
  // Bone titles, and text at the sooty foot of a page (ADR 0158).
  for (const background of ['--brand-slate', '--brand-panel', '--brand-raised', '--brand-soot']) {
    assert.ok(contrast(tokens['--brand-bone'], tokens[background]) >= 7, `--brand-bone on ${background}`);
  }
  for (const foreground of ['--brand-ivory', '--brand-muted', '--brand-gold', '--brand-ember']) {
    assert.ok(contrast(tokens[foreground], tokens['--brand-soot']) >= 4.5, `${foreground} on --brand-soot`);
  }
  for (const background of ['--brand-gold', '--brand-gold-bright', '--brand-ember']) {
    assert.ok(contrast(tokens['--brand-on-accent'], tokens[background]) >= 7, background);
  }
});
test('installed app and browser chrome use the same slate as the page', () => {
  const manifest = JSON.parse(read('table/manifest.webmanifest'));
  assert.equal(manifest.name, 'Downfall Instinct');
  assert.equal(manifest.short_name, 'Downfall Instinct');
  assert.match(read('viewer/index.html'), /<title>Downfall Instinct viewer<\/title>/);
  assert.equal(manifest.background_color, tokens['--brand-slate']);
  assert.equal(manifest.theme_color, tokens['--brand-slate']);
  // A launcher crops a maskable icon to its own shape, and the splash screen draws the icon on the background colour:
  // each picture says what it is for, so the rune is never cropped and no square of another shade shows.
  const pictures = manifest.icons.filter(icon => icon.type === 'image/png');
  assert.ok(pictures.every(icon => icon.purpose === 'any' || icon.purpose === 'maskable'), 'each picture says what it is for');
  assert.deepEqual(pictures.filter(icon => icon.purpose === 'maskable').map(icon => icon.sizes), ['192x192', '512x512']);
  for (const page of ['index', 'lobby', 'leaderboard', 'admin', 'pilot', 'battle', 'battle-lab']) {
    assert.ok(read(`table/${page}.html`).includes(`name="theme-color" content="${tokens['--brand-slate']}"`));
  }
  assert.ok(read('studio/index.html').includes(`name="theme-color" content="${tokens['--brand-slate']}"`));
});
test('the app and Android notifications use the rune instead of the placeholder letter', () => {
  assert.match(read('table/icon.svg'), /<path d=/);
  assert.doesNotMatch(read('table/icon.svg'), /<text/);
  assert.match(read('table/sw.js'), /badge: BADGE/);
  assert.match(read('table/sw.js'), /notification-badge\.png/);
  assert.doesNotMatch(read('table/navigation.js'), /brand-mark[^<]*>D<\/span>/);
});
// The first pixel of a PNG, which whatever filter its first row uses stores as it is: enough to read the ground.
function cornerOf(path) {
  const bytes = readFileSync(new URL(path, root));
  const chunks = [];
  for (let at = 8; at < bytes.length;) {
    const length = bytes.readUInt32BE(at), type = bytes.toString('latin1', at + 4, at + 8);
    if (type === 'IDAT') chunks.push(bytes.subarray(at + 8, at + 8 + length));
    at += 12 + length;
  }
  const pixels = inflateSync(Buffer.concat(chunks));
  return `#${[...pixels.subarray(1, 4)].map(value => value.toString(16).padStart(2, '0')).join('')}`;
}
test('the app icons stand on the same slate as the splash screen and the page', () => {
  for (const icon of ['icon-192', 'icon-512', 'icon-maskable-192', 'icon-maskable-512']) {
    assert.equal(cornerOf(`table/${icon}.png`), tokens['--brand-slate'].toLowerCase(), icon);
  }
});
// The app icon keeps its slate square, which a launcher needs; the pages draw the rune alone, so no darker square
// sits on their lighter header.
test('the pages mark their brand with the rune alone, not the app icon and its square', () => {
  assert.match(read('table/brand-mark.svg'), /<path d=/);
  assert.doesNotMatch(read('table/brand-mark.svg'), /<rect/);
  for (const sheet of ['table/table.css', 'table/battle.css', 'table/battle-lab.css']) {
    assert.match(read(sheet), /url\('\/brand-mark\.svg'\)/, sheet);
    assert.doesNotMatch(read(sheet), /url\('\/icon\.svg'\)/, sheet);
  }
});
// table.css paints the rune behind every .brand-mark, so a letter left inside one is drawn over it.
test('no page that marks its brand puts a letter over the rune', () => {
  for (const page of ['table/index.html', 'table/admin.html', 'table/pilot.html', 'table/navigation.js']) {
    assert.doesNotMatch(read(page), /class="brand-mark"[^>]*>[^<]+<\/span>/, page);
  }
});
// The battle page loads no table.css: the mark in its navigation header must carry the rune itself.
test('the battle page draws the rune in its navigation header', () => {
  assert.doesNotMatch(read('table/battle.html'), /href="table\.css"/);
  assert.match(read('table/battle.css'), /\.home-header \.brand-mark \{[^}]*url\('\/brand-mark\.svg'\)/);
});
// The battle's concept dialog and the rulebook's card are one card: both carry identity.css's .concept-card, and
// neither page sheet styles its parts again, which is how the two drifted apart (#699).
test('the concept card of the battle and of the rulebook is drawn by one rule of the identity sheet', () => {
  assert.match(read('table/battle.html'), /<dialog id="concept-dialog" class="concept-card"[^>]*>\s*<p class="eyebrow">[^<]*<\/p>\s*<h2 id="concept-title" class="concept-card-title">/);
  assert.match(read('table/rules.js'), /node\(doc, 'aside', 'rules-card concept-card'\)/);
  assert.match(read('table/rules.js'), /node\(doc, 'h4', 'concept-card-title', card\.title\)/);
  const identity = read('table/identity.css');
  for (const part of ['.concept-card .eyebrow', '.concept-card-title', '.concept-card .concept-lines p', '.concept-card .concept-example']) {
    assert.ok(identity.includes(`${part} {`), part);
  }
  for (const sheet of ['table/battle.css', 'table/rules.css']) {
    assert.doesNotMatch(read(sheet), /#concept-dialog (?:\.eyebrow|h2)|\.concept-(?:lines p|example)\s*[{,]|\.rules-card(?:-title| \.)/, sheet);
  }
});
// A tutorial shows the briefing, then the concept cards: one frame in the identity's tokens, and one title step (#809).
test('the practice briefing shares the concept card\'s frame and title step, in the identity\'s tokens', () => {
  assert.match(read('table/battle.html'), /<h2 id="practice-brief-title" class="concept-card-title">/);
  const css = read('table/battle.css');
  const rules = [...css.matchAll(/^([^{}\n]*(?:#practice-brief|\.practice-brief-)[^{}\n]*)\{([^}]*)\}/gm)];
  assert.ok(rules.length >= 4, 'the briefing\'s rules');
  for (const [, selector, body] of rules) assert.doesNotMatch(body, /#[0-9a-f]{3,8}\b/i, `${selector.trim()} names a raw colour`);
  assert.doesNotMatch(css, /#practice-brief h2 \{/);
});

// #869, #895: the coach's window, the opponent's card and the header's chip, beside the briefing, write their colours
// in the same tokens.
test('the coach\'s window, the opponent\'s card and its chip name no raw colour', () => {
  const css = read('table/battle.css');
  // The round's own rules that only ask whether the chip shows are the round's, not the chip's.
  const card = /\.coach\b|#opponent-|\.opponent-/;
  const rules = [...css.matchAll(/^([^{}\n@]*)\{([^}]*)\}/gm)].filter(([, selector]) => card.test(selector) && !/\.round\b/.test(selector));
  assert.ok(rules.filter(([, selector]) => /\.coach\b/.test(selector)).length >= 8, 'the coach\'s rules');
  for (const name of ['#opponent-dialog {', '#opponent-dialog::backdrop {', '.opponent-head .eyebrow {', '.opponent-rank {', '.opponent-note {', '#opponent-problem {', '#opponent-close {',
    '.opponent-chip {', '.opponent-chip:hover {', '.opponent-avatar {', '.opponent-vs {', '.opponent-badge {', '.opponent-heart {', ':is(.opponent-chip, #opponent-dialog)[data-kind="waiting"] .opponent-avatar {']) {
    assert.ok(rules.some(([whole]) => whole.trimStart().startsWith(name)), name);
  }
  // The medallion's gradients are a portrait and keep their colours (the owner, #895); its rings and letter do not.
  const portrait = /background: radial-gradient\([^;]*\);/;
  for (const [, selector, body] of rules) assert.doesNotMatch(body.replace(portrait, ''), /#[0-9a-f]{3,8}\b/i, `${selector.trim()} names a raw colour`);
  assert.equal(rules.filter(([, selector, body]) => /opponent-avatar/.test(selector) && portrait.test(body)).length, 2, 'the two portraits');
  // A refusal reads in the refused colour, as everywhere else.
  assert.match(css, /#opponent-problem \{[^}]*color: var\(--refused\)/);
});

// Every page of the table ends with the shared identity sheet, so one rule restyles a shared part everywhere.
test('every page of the table loads the identity sheet last and the display face is the host\'s own', () => {
  for (const page of ['index', 'lobby', 'leaderboard', 'admin', 'pilot', 'battle']) {
    const sheets = [...read(`table/${page}.html`).matchAll(/rel="stylesheet" href="([^"]+)"/g)].map(match => match[1]);
    assert.equal(sheets.at(-1), 'identity.css', page);
  }
  assert.match(read('docs/art/branding.css'), /url\('\/fonts\/marcellus-latin-400\.woff2'\)/);
  for (const sheet of ['table/table.css', 'table/lobby.css', 'table/battle.css', 'table/battle-lab.css', 'table/identity.css']) {
    assert.doesNotMatch(read(sheet).split('/* END GENERATED BRANDING */').at(-1), /Georgia/, sheet);
  }
});
