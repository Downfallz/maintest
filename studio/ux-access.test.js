import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { findingLines, knownLine, unknownFindings } from '../scripts/ux/access.mjs';
import { JOURNEYS } from '../scripts/ux/journeys.mjs';

// The checks of accessibility of UX13 (scripts/ux/access.mjs), without a browser: how a finding is written, how the
// list of what is known reads it, and the list itself. The checks run in studio/browser/table.access.js.

test('a check is written a line an element, with its screen and its rule', () => {
  const lines = findingLines('390 home-new', {
    axe: { 'label-content-name-mismatch': ['#inbox-open'] },
    contrast: {},
    forced: { focus: ['#username'], icons: [], state: ['#tab-1', '#tab-2'] },
    spacing: { wide: true, clipped: ['p.cut'], overlaps: [] },
    doubled: { wide: false, clipped: [], overlaps: ['#a | #b'] },
  });
  assert.deepEqual(lines, [
    '390 home-new: axe label-content-name-mismatch #inbox-open',
    '390 home-new: forced focus #username',
    '390 home-new: forced state #tab-1',
    '390 home-new: forced state #tab-2',
    '390 home-new: spacing wide',
    '390 home-new: spacing clipped p.cut',
    '390 home-new: doubled overlaps #a | #b',
  ]);
  assert.deepEqual(findingLines('320 x', { axe: {}, forced: { focus: [], icons: [], state: [] } }), []);
});

test('the list reads a finding without its place among siblings, and a known one is not new', () => {
  assert.equal(knownLine('320 a: forced icons #timeline > li:nth-of-type(4) > button:nth-of-type(1)'), '320 a: forced icons #timeline > li > button');
  const found = ['320 a: forced icons #timeline > li:nth-of-type(1) > button:nth-of-type(1)', '320 a: forced icons #timeline > li:nth-of-type(2) > button:nth-of-type(1)', '320 a: forced focus #new'];
  assert.deepEqual(unknownFindings(found, ['320 a: forced icons #timeline > li > button']), ['320 a: forced focus #new']);
  assert.deepEqual(unknownFindings([], ['anything']), []);
});

test('the list of what is known names journeys and screens that exist, at the two widths, and never axe', () => {
  const known = JSON.parse(readFileSync(new URL('../scripts/ux/access-known.json', import.meta.url), 'utf8'));
  for (const [journey, lines] of Object.entries(known)) {
    const walked = JOURNEYS.find(one => one.id === journey);
    assert.ok(walked, `${journey} is a journey`);
    assert.deepEqual(lines, [...new Set(lines)].sort(), `${journey}'s list is sorted, each line once`);
    for (const line of lines) {
      const [, width, screen, check] = /^(\d+) ([a-z0-9-]+): ([a-z]+) /.exec(line) ?? [];
      assert.ok(['320', '390'].includes(width), `${line}: a width the walk takes`);
      assert.ok(walked.screens.includes(screen), `${line}: a screen of ${journey}`);
      assert.ok(['forced', 'contrast', 'spacing', 'doubled'].includes(check), `${line}: a check, and not axe (ADR 0197)`);
      assert.equal(knownLine(line), line, `${line}: read as the list reads it`);
    }
  }
});
