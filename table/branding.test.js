import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

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
// table.css paints the rune behind every .brand-mark, so a letter left inside one is drawn over it.
test('no page that marks its brand puts a letter over the rune', () => {
  for (const page of ['table/index.html', 'table/admin.html', 'table/pilot.html', 'table/navigation.js']) {
    assert.doesNotMatch(read(page), /class="brand-mark"[^>]*>[^<]+<\/span>/, page);
  }
});
// The battle page loads no table.css: the mark in its navigation header must carry the rune itself.
test('the battle page draws the rune in its navigation header', () => {
  assert.doesNotMatch(read('table/battle.html'), /href="table\.css"/);
  assert.match(read('table/battle.css'), /\.home-header \.brand-mark \{[^}]*url\('\/icon\.svg'\)/);
});
