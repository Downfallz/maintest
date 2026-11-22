import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// The match's frame as the page paints it before its scripts run (#984, docs/art/journeys/match.md): the dock names
// nothing, so the game the lobby opened reads as an arrival, not as a correction of "Battle view".
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const page = read('table/battle.html');
const inner = id => page.match(new RegExp(`id="${id}"[^>]*>([^<]*)<`))?.[1];

test('the battle\'s first frame names nothing before its module runs', () => {
  assert.equal(inner('actor-name'), '');
  assert.equal(inner('actor-meta'), '');
  assert.equal(inner('instruction'), '');
  assert.doesNotMatch(read('battle/src/live.ts'), /'Battle view'/, 'the dock does not name the view while it waits');
});

// History's clock and the order row's separator are the set's drawings (#1000, ADR 0198), drawn by the sheet as a mask:
// each mask's path is the set's own, stroked as the set strokes it.
test('the frame\'s History and the order row draw the set\'s history and next', async () => {
  const { ICONS } = await import('./icons.js');
  const sheet = read('table/battle.css');
  const masks = rule => [...sheet.matchAll(new RegExp(`${rule}[^{]*\\{[^}]*mask: url\\("data:image/svg\\+xml,([^"]+)"\\)`, 'g'))].map(match => decodeURIComponent(match[1]));
  for (const [rule, name] of [['#battle #history-open::before', 'history'], ['#battle #timeline li \\+ li::before', 'next']]) {
    const drawn = masks(rule.replaceAll('#', '#').replace(/[.*?()[\]]/g, '\\$&'));
    assert.ok(drawn.length, `${rule} is drawn by a mask`);
    for (const svg of drawn) {
      assert.ok(svg.includes(`d='${ICONS[name]}'`), `${rule} draws ${name}`);
      assert.ok(svg.includes("stroke-linecap='square'") && svg.includes("stroke-linejoin='miter'"), `${rule} is stroked as the set`);
    }
  }
  assert.doesNotMatch(sheet, /content: '›'/, 'no glyph stands for next');
  assert.match(read('table/battle.html'), /id="opponent-heart"[^>]*>Friend</, 'a friend is a word, not the heart of health');
});
