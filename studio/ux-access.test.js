import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { findingLines, goneFindings, knownLine, unknownFindings } from '../scripts/ux/access.mjs';
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

test('a line the list knows and the walk no longer finds is gone, whatever its place among siblings', () => {
  const found = ['320 a: forced icons #timeline > li:nth-of-type(3) > button:nth-of-type(1)'];
  assert.deepEqual(goneFindings(found, ['320 a: forced icons #timeline > li > button', '390 a: spacing wide', '320 a: doubled clipped #x']),
    ['320 a: doubled clipped #x', '390 a: spacing wide']);
  assert.deepEqual(goneFindings(found, ['320 a: forced icons #timeline > li > button']), []);
  assert.deepEqual(goneFindings([], []), []);
});

test('an unsteady line, marked ~, is neither new when a walk finds it nor gone when it does not', () => {
  const known = ['~ 320 a: spacing overlaps #coach | #field', '320 a: spacing wide'];
  assert.deepEqual(unknownFindings(['320 a: spacing overlaps #coach | #field', '320 a: spacing wide'], known), []);
  assert.deepEqual(goneFindings(['320 a: spacing wide'], known), []);
  assert.deepEqual(goneFindings([], known), ['320 a: spacing wide']);
});

test('the pages that load no sheet of the table keep the focus\'s ring under forced colours as identity.css does', () => {
  const ring = /@media \(forced-colors: active\) \{\s*:focus-visible \{ ([^}]*) \}/;
  const [table, showcase, offline] = ['../table/identity.css', '../showcase/showcase.css', '../table/offline.html']
    .map(path => ring.exec(readFileSync(new URL(path, import.meta.url), 'utf8'))?.[1]);
  assert.ok(table, 'identity.css keeps a ring under forced colours');
  assert.equal(showcase, table, 'the showcase writes the same ring');
  assert.equal(offline, table, 'the offline page writes the same ring');
});

test('the list of what is known names journeys and screens that exist, at the two widths, and never axe', () => {
  const known = JSON.parse(readFileSync(new URL('../scripts/ux/access-known.json', import.meta.url), 'utf8'));
  for (const [journey, lines] of Object.entries(known)) {
    const walked = JOURNEYS.find(one => one.id === journey);
    assert.ok(walked, `${journey} is a journey`);
    assert.deepEqual(lines, [...new Set(lines)].sort(), `${journey}'s list is sorted, each line once`);
    for (const line of lines) {
      const [, width, screen, check] = /^(?:~ )?(\d+) ([a-z0-9-]+): ([a-z]+) /.exec(line) ?? [];
      assert.ok(['320', '390'].includes(width), `${line}: a width the walk takes`);
      assert.ok(walked.screens.includes(screen), `${line}: a screen of ${journey}`);
      assert.ok(['forced', 'contrast', 'spacing', 'doubled'].includes(check), `${line}: a check, and not axe (ADR 0197)`);
      assert.equal(knownLine(line), line, `${line}: read as the list reads it`);
      if (line.startsWith('~ ')) assert.ok(!lines.includes(line.slice(2)), `${line}: steady or unsteady, not both`);
    }
  }
});

test('a pull request walks the narrowest phone alone, main and the nightly both, and an unknown width is refused (#1269)', async () => {
  const { walkWidths } = await import('../scripts/ux/set.mjs');
  assert.deepEqual(walkWidths({}).map(one => one.width), [320, 390]);
  assert.deepEqual(walkWidths({ UX_WALK_WIDTHS: '320 390' }).map(one => one.width), [320, 390]);
  assert.deepEqual(walkWidths({ UX_WALK_WIDTHS: '320' }), [{ width: 320, height: 568 }]);
  assert.throws(() => walkWidths({ UX_WALK_WIDTHS: '360' }), /360 is not a width the walk takes/);
  assert.throws(() => walkWidths({ UX_WALK_WIDTHS: ' ' }), /names no width/);
});

test('the list of what is known is read at the widths walked, so a width left out is not called fixed (#1269)', async () => {
  const { knownAt } = await import('../scripts/ux/access.mjs');
  const known = ['320 a: spacing wide', '~ 390 a: spacing overlaps #x | #y', '390 a: doubled clipped #z'];
  assert.deepEqual(knownAt(known, [{ width: 320 }]), ['320 a: spacing wide']);
  assert.deepEqual(knownAt(known, [{ width: 320 }, { width: 390 }]), known);
  assert.deepEqual(goneFindings([], knownAt(known, [{ width: 320 }])), ['320 a: spacing wide']);
});

test('the real-engine job walks one width on a pull request and both elsewhere', () => {
  const workflow = readFileSync(new URL('../.github/workflows/studio-browser.yml', import.meta.url), 'utf8');
  assert.match(workflow, /UX_WALK_WIDTHS: \$\{\{ github\.event_name == 'pull_request' && '320' \|\| '320 390' \}\}/);
  for (const spec of ['browser/table.access.js', 'browser/table.motion.js']) {
    const source = readFileSync(new URL(spec, import.meta.url), 'utf8');
    assert.match(source, /walkWidths\(\)/, `${spec} takes the widths the job names`);
  }
});
