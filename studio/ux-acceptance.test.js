// The pages' acceptance (docs/art/ux-craft-plan.md, UX14): the walk under less motion, the journeys read against
// their baseline, and the record. No browser: the walk itself is played by browser/table.motion.js.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { JOURNEYS } from '../scripts/ux/journeys.mjs';
import { parseArguments } from '../scripts/ux/journey.mjs';
import { measuresLines, parseMeasures } from '../scripts/ux/measures.mjs';
import { acceptanceMarkdown, missingWalks, parseBaseline, parseBaselinePaints, tally } from '../scripts/ux/acceptance.mjs';
import { baselineMarkdown } from '../scripts/ux/baseline.mjs';
import { VIEWPORTS } from '../scripts/ux/set.mjs';
import { TRAVEL, lessMotionFaults } from '../scripts/ux/motion.mjs';

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const script = new URL('../scripts/ux/acceptance.mjs', import.meta.url).pathname;

const measured = (name, { words = 10, filled = 1, under = 0, axe = {}, animating = 0, travel } = {}) => ({
  screen: name, window: null, height: 844, words, filled,
  pictures: { img: 0, background: 0, canvas: 0, oversized: 0, undersized: 0 },
  text: { smallest: 11, under }, targets: { all: 4, under: 0, tiny: 0 }, overlaps: 0, animating, shift: 0, axe,
  ...(travel ? { travel } : {}),
});
const walk = (journey, screens, { lessMotion = false, width = 390 } = {}) => ({
  header: { journey, viewport: `${width}x${width === 390 ? 844 : 568}`, axe: true, ...(lessMotion ? { lessMotion: true } : {}), blocked: [] }, screens,
});
const BASELINE = `# The journeys' baseline

## The two doors

At 390 by 844:

| Screen | Words | Filled actions | Smallest text | Words under 11 px | Targets under 44 px | Overlaps | Animating | axe |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| sign-in-panel | 35 | 0 | 10 px | 10 | 4 of 7 | 0 | 0 | nothing |
| name-window (staged) | 41 | 1 | 10 px | 4 | 0 of 3 | 0 | 0 | nothing |

## The first paints, on a slow phone

| Page | First paint | Largest paint | The largest is | Layout shift | Pictures loaded |
| --- | --- | --- | --- | --- | --- |
| sign-in-panel | 0.7 s (0.7 to 0.7) | 4.7 s (4.6 to 4.7) | picture | 0.04 (0.04 to 0.04) | 790 KB |
`;

test('the committed baseline is read for every screen of every journey at both widths', () => {
  const tables = parseBaseline(read('docs/art/journeys/baseline.md'));
  for (const journey of JOURNEYS) {
    for (const width of [320, 390]) {
      for (const name of journey.screens) assert.ok(tables[journey.id]?.[width]?.has(name), `${journey.id}'s ${name} at ${width}`);
    }
  }
  // A staged screen is read by its own name, and a table of paints is no journey's.
  assert.equal(tables.doors[390].get('name-window')['Filled actions'], '1');
  assert.equal(parseBaselinePaints(read('docs/art/journeys/baseline.md')).get('sign-in-panel')['Pictures loaded'], '790 KB');
});

test('a baseline with no table of a journey is refused, not read as all new', () => {
  assert.throws(() => parseBaseline('# Nothing here\n'), /no table of a journey/);
});

test('a screen is written then → now where it moved, once where it did not, new or gone against the baseline', () => {
  const page = acceptanceMarkdown({
    walks: [walk('doors', [measured('sign-in-panel', { words: 35, filled: 1, under: 0 }), measured('name-window', { words: 41, under: 4 }), measured('door-new')])],
    paint: null, baseline: parseBaseline(BASELINE), commit: 'abc1234', date: '2026-11-24',
  });
  const row = name => page.split('\n').find(line => line.startsWith(`| ${name}`));
  assert.match(row('sign-in-panel'), /^\| sign-in-panel \| 35 \| 0 → 1 \| 10 px → 11 px \| 10 → 0 \| 4 of 7 → 0 of 4 \| 0 \| 0 \| nothing \|$/);
  assert.match(row('name-window'), /^\| name-window \| 41 \| 1 \| 10 px → 11 px \| 4 \|/);
  assert.match(row('door-new'), /^\| door-new \*new\* \| 10 \| 1 \|/);
  assert.match(page, /`main` at\s+`abc1234`, 2026-11-24/);
  assert.match(page, /3 screens walked\. 0 with more than one filled action\. 4 words under 11 px in all\./);
  const gone = acceptanceMarkdown({ walks: [walk('doors', [measured('sign-in-panel')])], paint: null, baseline: parseBaseline(BASELINE), commit: 'a', date: 'b' });
  assert.match(gone, /In the baseline and not walked now: name-window\./);
});

test('the paints are read against the baseline\'s', () => {
  // The page is named as the budget file names it; the baseline named it sign-in-panel.
  const paint = { profile: { viewport: '390x844' }, pages: { 'table/sign-in': { runs: 5, largestPaintMs: { median: 2000 }, largestIs: 'text', shift: { median: 0 }, pictureBytes: 34 * 1024 } } };
  const page = acceptanceMarkdown({ walks: [walk('doors', [measured('sign-in-panel')])], paint, baseline: parseBaseline(BASELINE), baselinePaints: parseBaselinePaints(BASELINE), commit: 'a', date: 'b' });
  assert.match(page, /^\| table\/sign-in \| 4\.7 s → 2\.0 s \| text \| 0\.04 → 0\.00 \| 790 KB → 34 KB \|$/m);
});

test('a walk under less motion says what travelled and what animated at rest, or that nothing did', () => {
  const full = walk('doors', [measured('sign-in-panel')]);
  const still = walk('doors', [measured('sign-in-panel', { travel: [] })], { lessMotion: true });
  const moving = walk('doors', [measured('sign-in-panel', { travel: ['#door transition translate'], animating: 1 })], { lessMotion: true, width: 320 });
  const quiet = acceptanceMarkdown({ walks: [full, still], paint: null, baseline: parseBaseline(BASELINE), commit: 'a', date: 'b' });
  assert.match(quiet, /## Less motion[\s\S]*1 screens\.[\s\S]*Nothing travelled, and nothing animated at rest\./);
  // The walks under less motion are not read as a second walk against the baseline.
  assert.equal(quiet.split('\n').filter(line => line.startsWith('| sign-in-panel')).length, 1);
  const loud = acceptanceMarkdown({ walks: [full, moving], paint: null, baseline: parseBaseline(BASELINE), commit: 'a', date: 'b' });
  assert.match(loud, /^- doors at 320 px, sign-in-panel: #door transition translate$/m);
  assert.match(loud, /^- doors at 320 px, sign-in-panel: 1 still animating at rest$/m);
  assert.throws(() => acceptanceMarkdown({ walks: [still], paint: null, baseline: {}, commit: 'a', date: 'b' }), /no measures under full motion/);
});

test('the faults of less motion refuse a walk that has no screens or was not walked under less motion', () => {
  assert.throws(() => lessMotionFaults([]), /No screens/);
  assert.throws(() => lessMotionFaults([{ name: 'one', measures: measured('one') }]), /not walked under less motion/);
  assert.deepEqual(lessMotionFaults([{ name: 'one', measures: measured('one', { travel: [] }) }]), []);
  assert.deepEqual(TRAVEL, ['transform', 'translate', 'scale', 'rotate']);
  assert.throws(() => lessMotionFaults([{ name: 'one', measures: { ...measured('one'), travel: null } }]), /not walked under less motion/);
});

test('a walk\'s header says it was walked under less motion, and only then', () => {
  const lines = measuresLines({ journey: 'doors', viewport: { width: 390, height: 844 }, axe: true, lessMotion: true, screens: [] });
  assert.equal(parseMeasures(lines).header.lessMotion, true);
  assert.equal(parseMeasures(measuresLines({ journey: 'doors', viewport: { width: 390, height: 844 }, axe: true, screens: [] })).header.lessMotion, undefined);
});

test('the tally counts the screens over one filled action, with a finding of axe, and the words under the floor', () => {
  const tallied = tally([walk('doors', [measured('a', { filled: 2, axe: { 'color-contrast': 1 } }), measured('b')])]);
  assert.deepEqual(tallied, { screens: 2, overFilled: 1, wordsUnder: 0, overlaps: 0, axe: 1, axeRun: true, animating: 0 });
});

test('the paints are refused when paint.json holds no page', () => {
  assert.throws(() => acceptanceMarkdown({ walks: [walk('doors', [measured('a')])], paint: { profile: { viewport: '390x844' }, pages: {} }, baseline: parseBaseline(BASELINE), commit: 'a', date: 'b' }), /paint\.json holds no page/);
});

test('a folder lacks every journey at every width not walked, and all the walks under less motion once one is there', () => {
  const every = JOURNEYS.flatMap(journey => VIEWPORTS.map(({ width }) => walk(journey.id, [measured('a')], { width })));
  assert.deepEqual(missingWalks(every), []);
  assert.deepEqual(missingWalks(every.slice(1)), [`${JOURNEYS[0].id} at 320 px`]);
  const oneLess = [...every, walk('doors', [measured('a', { travel: [] })], { lessMotion: true, width: 390 })];
  assert.equal(missingWalks(oneLess).length, JOURNEYS.length * VIEWPORTS.length - 1);
  assert.ok(missingWalks(oneLess).includes('doors at 320 px under less motion'));
});

test('the baseline\'s page leaves out the walks under less motion', () => {
  const page = baselineMarkdown([walk('doors', [measured('only')]), walk('doors', [measured('only', { travel: [] })], { lessMotion: true })], null, { commit: 'a', date: 'b' });
  assert.equal(page.split('\n').filter(line => line.startsWith('| only |')).length, 1);
});

test('--less-motion walks journeys, never a strip, and needs one named', () => {
  assert.equal(parseArguments(['--all', '--less-motion']).lessMotion, true);
  assert.equal(parseArguments(['doors']).lessMotion, false);
  assert.throws(() => parseArguments(['--less-motion']), /--less-motion walks journeys: name one, or --all/);
  assert.throws(() => parseArguments(['lobby', '--strip', 'menu', '--less-motion']), /a strip films with the animations stopped/);
  assert.throws(() => parseArguments(['--all', '--paint', '--less-motion']), /the paints are measured with full motion/);
});

test('the script refuses a missing flag, a folder with no walk and a folder short of a walk, on stderr, with exit 1', () => {
  const usage = spawnSync(process.execPath, [script, 'nowhere', '--commit', '--date', '2026-11-24'], { encoding: 'utf8' });
  assert.equal(usage.status, 1);
  assert.match(usage.stderr, /Usage/);
  assert.equal(usage.stdout, '');
  const folder = mkdtempSync(join(tmpdir(), 'ux-acceptance-'));
  try {
    const empty = spawnSync(process.execPath, [script, folder, '--commit', 'a', '--date', 'b'], { encoding: 'utf8' });
    assert.equal(empty.status, 1);
    assert.match(empty.stderr, /not the whole acceptance\. Missing: doors at 320 px; doors at 390 px;/);
    assert.equal(empty.stdout, '');
    const write = (journey, viewport) => writeFileSync(join(folder, `${journey}-${viewport.width}.measures.jsonl`), measuresLines({ journey, viewport, axe: true, screens: [{ name: 'sign-in-panel', measures: measured('sign-in-panel') }] }));
    write('doors', VIEWPORTS[1]);
    const short = spawnSync(process.execPath, [script, folder, '--commit', 'a', '--date', 'b'], { encoding: 'utf8' });
    assert.equal(short.status, 1);
    assert.match(short.stderr, /not the whole acceptance\. Missing: doors at 320 px;/);
    assert.equal(short.stdout, '');
    for (const journey of JOURNEYS) for (const viewport of VIEWPORTS) write(journey.id, viewport);
    const whole = spawnSync(process.execPath, [script, folder, '--commit', 'a', '--date', 'b'], { encoding: 'utf8' });
    assert.equal(whole.status, 0, whole.stderr);
    assert.match(whole.stdout, /^# The journeys at acceptance$/m);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('the record and its page hold every journey, the paints, less motion and the owner\'s two gates', () => {
  const page = read('docs/art/journeys/acceptance.md');
  for (const journey of JOURNEYS) assert.match(page, new RegExp(`^## ${journey.title}$`, 'm'), `the page has ${journey.title}`);
  assert.match(page, /^## The first paints, on a slow phone$/m);
  assert.match(page, /^## Less motion$/m);
  const record = read('docs/art/ux-acceptance.md');
  for (const heading of ['The answer in short', 'The measures held', 'Less motion', 'The sheets', 'The gates only the owner can close']) {
    assert.match(record, new RegExp(`^## ${heading}$`, 'm'), `the record has ${heading}`);
  }
  assert.match(read('docs/README.md'), /\(art\/ux-acceptance\.md\)/);
  const commands = read('docs/reference/commands.md');
  assert.ok(commands.includes('`--less-motion`'), 'commands.md names --less-motion');
  assert.match(commands, /^### ux\/acceptance\.mjs$/m);
});
