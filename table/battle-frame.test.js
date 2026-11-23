import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// The match's frame as the page paints it before its scripts run (#984, docs/art/journeys/match.md): the dock names
// nothing, so the game the lobby opened reads as an arrival, not as a correction of "Battle view".
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const page = read('table/battle.html');
const inner = id => page.match(new RegExp(`id="${id}"[^>]*>([^<]*)<`))?.[1];

test('the battle\'s first frame names nothing before its module runs', () => {
  // The heading shows no word, and still has one for a screen reader (Sonar's S6850): an unseen "Match".
  assert.match(page, /<h1 id="actor-name"><span class="unseen">Match<\/span><\/h1>/);
  assert.equal(inner('actor-meta'), '');
  assert.equal(inner('instruction'), '');
  assert.doesNotMatch(read('battle/src/live.ts'), /'Battle view'/, 'the dock does not name the view while it waits');
});

// History's clock and the order row's separator are the set's drawings (#1000, ADR 0198), drawn by the sheet as a mask:
// each mask's path is the set's own, stroked as the set strokes it.
test('the frame\'s History and the order row draw the set\'s history and next', async () => {
  const { ICONS } = await import('./icons.js');
  const sheet = read('table/battle.css');
  // Each rule's body, then both its masks: the prefixed copy and the standard one, which must not drift apart.
  const masks = rule => [...sheet.matchAll(new RegExp(`${rule}[^{]*\\{([^}]*)\\}`, 'g'))]
    .flatMap(body => [...body[1].matchAll(/(?:-webkit-)?mask: url\("data:image\/svg\+xml,([^"]+)"\)/g)].map(match => decodeURIComponent(match[1])));
  for (const [rule, name] of [['#battle #history-open::before', 'history'], ['#battle #timeline li \\+ li::before', 'next']]) {
    const drawn = masks(rule.replace(/[.*?()[\]]/g, '\\$&'));
    assert.ok(drawn.length >= 2 && drawn.length % 2 === 0, `${rule} is drawn by a mask and its prefixed copy`);
    for (const svg of drawn) {
      assert.ok(svg.includes(`d='${ICONS[name]}'`), `${rule} draws ${name}`);
      assert.ok(svg.includes("stroke-linecap='square'") && svg.includes("stroke-linejoin='miter'"), `${rule} is stroked as the set`);
    }
  }
  assert.doesNotMatch(sheet, /content: '›'/, 'no glyph stands for next');
  assert.match(read('table/battle.html'), /id="opponent-friend-tag"[^>]*>Friend</, 'a friend is a word, not the heart of health');
});

// A placed creature's caption hangs from its button by a translation, not by `bottom` (#999): a button whose height
// changes as a sheet docks then moves no layout.
test('a placed caption hangs from its button by a translation of the button\'s height', () => {
  assert.match(read('table/battle-lab.css'), /\[style\*="--hit-height"\] > \.caption \{ top: 0; bottom: auto; translate: 0 calc\(var\(--hit-height\) - 100%\); \}/);
  assert.match(read('battle/src/targets.ts'), /setProperty\('--hit-height', `\$\{height\}px`\)/);
  assert.match(read('table/battle.html'), /id="help-open"[^>]*aria-describedby="history-unread"/, 'Help speaks History\'s unread count');
});

// #1092: the opponent's rank and Friend are one tag, square as the brand's tags are (a pill is a counter): one rule
// draws both, Friend differs only in its colour, and nothing is named after a heart any more.
test('the opponent\'s rank and Friend are one square tag that differs only in colour', () => {
  const page = read('table/battle.html'), css = read('table/battle.css');
  assert.match(page, /<span id="opponent-badge" class="opponent-tag opponent-badge" hidden><\/span>/);
  assert.match(page, /<span id="opponent-friend-tag" class="opponent-tag opponent-friend-tag" hidden>Friend<\/span>/);
  const tag = css.match(/^\.opponent-tag \{([^}]*)\}/m);
  assert.ok(tag, 'one rule draws the tag');
  assert.match(tag[1], /border-radius: var\(--radius-sharp\)/);
  assert.equal(css.match(/^\.opponent-friend-tag \{([^}]*)\}/m)?.[1].trim(), 'color: var(--gold);', 'Friend differs in its colour alone');
  assert.doesNotMatch(css, /^\.opponent-badge \{/m, 'the rank has no rule of its own beside the tag');
  for (const path of ['table/battle.html', 'table/battle.css', 'battle/src/live.ts', 'studio/browser/battle-live.spec.js']) assert.doesNotMatch(read(path), /opponent-heart/, path);
});

// An id names one element of its page: `$()` finds the first, so a second one is never reached (#1092's review).
test('no id appears twice in the battle page', () => {
  const ids = [...read('table/battle.html').matchAll(/\sid="([^"]+)"/g)].map(([, id]) => id);
  assert.deepEqual(ids.filter((id, index) => ids.indexOf(id) !== index), []);
});
