// The journey film's own rules (docs/art/ux-craft-plan.md, UX2), the ones that need no browser: what the script
// accepts, what the journeys name, how the measures are written and compared, and that the docs name them. The
// walk itself is played on the real engine by studio/browser/table.journey.js.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { JOURNEYS } from '../scripts/ux/journeys.mjs';
import { VIEWPORTS, blockedLines, listing, parseArguments, walked } from '../scripts/ux/journey.mjs';
import { differences, measuresLines, parseMeasures } from '../scripts/ux/measures.mjs';
import { readPaintInPage, spread, summary } from '../scripts/ux/paint.mjs';

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const script = new URL('../scripts/ux/journey.mjs', import.meta.url).pathname;

test('the five journeys of the plan and the match\'s windows are walked, each with screens named once', () => {
  assert.deepEqual(JOURNEYS.map(journey => journey.id), ['doors', 'lessons', 'lobby', 'pages', 'match', 'windows']);
  for (const journey of JOURNEYS) {
    assert.ok(journey.screens.length >= 5, `${journey.id} names its screens`);
    assert.equal(new Set(journey.screens).size, journey.screens.length, `${journey.id} names a screen twice`);
    for (const name of journey.screens) assert.match(name, /^[a-z0-9]+(-[a-z0-9]+)*$/, `${journey.id}: ${name}`);
  }
});

test('a strip is filmed at a screen its journey names', () => {
  for (const journey of JOURNEYS) {
    assert.ok(Object.keys(journey.strips).length >= 1, `${journey.id} has a strip`);
    for (const [name, strip] of Object.entries(journey.strips)) {
      assert.ok(journey.screens.includes(strip.at), `${journey.id}: ${name} is filmed at ${strip.at}`);
      assert.equal(typeof strip.act, 'function');
    }
  }
});

// A control the walk could not press at 320 px is pressed by script and named as blocked (play.mjs, choose): a strip
// that presses Settings bare timed out there instead of being filmed (#1021).
test('the match\'s settings-window strip presses Settings as the walk does, so a covered button is named, not a timeout', async () => {
  const pressed = [], blocked = [];
  const covered = { first: () => covered, click: async () => { throw new Error('locator.click: Timeout 5000ms exceeded.'); }, dispatchEvent: async kind => { pressed.push(kind); } };
  const page = { locator: selector => { assert.equal(selector, '#settings'); return covered; } };
  const match = JOURNEYS.find(journey => journey.id === 'match');
  await match.strips['settings-window'].act({ page, blocked: async selector => { blocked.push(selector); } });
  assert.deepEqual(blocked, ['#settings']);
  assert.deepEqual(pressed, ['click']);
});

// A strip, like a walk, says what it had to press by script: frames of a press no player could make are not taken
// for a player's (#1021).
test('what a walk or a strip pressed by script is said with its journey and its width', () => {
  const frames = walked([{ ms: 0 }], ['after second-round: #settings could not be pressed']);
  assert.deepEqual(frames.map(frame => frame.ms), [0]);
  assert.deepEqual(blockedLines('match', { width: 320 }, frames), ['match at 320 px, after second-round: #settings could not be pressed']);
  assert.deepEqual(blockedLines('match', { width: 390 }, walked([], [])), []);
});

test('the walk is at 320 by 568 and 390 by 844', () => {
  assert.deepEqual(VIEWPORTS, [{ width: 320, height: 568 }, { width: 390, height: 844 }]);
});

test('the arguments name journeys, a width, a strip of one journey, or the paints', () => {
  assert.deepEqual(parseArguments(['doors', 'lobby']).journeys, ['doors', 'lobby']);
  assert.equal(parseArguments(['--all']).all, true);
  assert.deepEqual(parseArguments(['lobby', '--width', '320']).viewports, [VIEWPORTS[0]]);
  assert.equal(parseArguments(['lobby', '--strip', 'menu']).strip, 'menu');
  assert.equal(parseArguments(['--paint', '--runs', '3']).runs, 3);
  assert.throws(() => parseArguments([]), /Name a journey/);
  assert.throws(() => parseArguments(['nowhere']), /No journey is named nowhere/);
  assert.throws(() => parseArguments(['doors', '--all']), /not both/);
  assert.throws(() => parseArguments(['doors', '--width', '1440']), /--width is 320 or 390/);
  assert.throws(() => parseArguments(['doors', '--strip', 'nothing']), /no strip named nothing/);
  assert.throws(() => parseArguments(['--all', '--strip', 'menu']), /one interaction of one journey/);
  assert.throws(() => parseArguments(['doors', '--frames', '0']), /positive whole number/);
  assert.throws(() => parseArguments(['doors', '--out']), /needs a value/);
  assert.throws(() => parseArguments(['doors', '--fast']), /Unknown option/);
});

test('the script exits 1 and says why when nothing is named, and lists the journeys when asked', () => {
  const nothing = spawnSync(process.execPath, [script], { encoding: 'utf8' });
  assert.equal(nothing.status, 1);
  assert.match(nothing.stderr, /Name a journey/);
  assert.equal(nothing.stdout, '');
  const listed = spawnSync(process.execPath, [script, '--list'], { encoding: 'utf8' });
  assert.equal(listed.status, 0);
  assert.equal(listed.stdout.trim(), listing());
  for (const journey of JOURNEYS) assert.match(listed.stdout, new RegExp(`^${journey.id}: `, 'm'));
});

const screen = (name, words) => ({ name, staged: false, measures: { window: null, words, text: { smallest: 10, under: 2 }, axe: {} } });

test('the measures are a header and a line a screen, and read back as written', () => {
  const text = measuresLines({ journey: 'doors', viewport: VIEWPORTS[1], axe: true, screens: [screen('one', 12), { ...screen('two', 3), staged: true }] });
  assert.equal(text.trim().split('\n').length, 3);
  const { header, screens } = parseMeasures(text);
  assert.deepEqual(header, { journey: 'doors', viewport: '390x844', axe: true, blocked: [] });
  assert.deepEqual(screens.map(one => one.screen), ['one', 'two']);
  assert.equal(screens[0].staged, undefined);
  assert.equal(screens[1].staged, true);
  assert.equal(screens[0].text.smallest, 10);
});

test('two walks differ by the screens they reach and by any measure, however deep', () => {
  assert.deepEqual(differences([screen('one', 12)], [screen('one', 12)]), []);
  assert.deepEqual(differences([screen('one', 12)], [screen('one', 13)]), ['one: words 12 | 13']);
  const deeper = screen('one', 12); deeper.measures.text.smallest = 7; deeper.measures.axe = { 'color-contrast': 2 };
  assert.deepEqual(differences([screen('one', 12)], [deeper]), ['one: text.smallest 10 | 7', 'one: axe.color-contrast undefined | 2']);
  assert.match(differences([screen('one', 12)], [screen('other', 12)])[0], /^screens: one \| other$/);
  // The shift depends on when a page's parts arrive: it is written, not compared.
  const shifted = screen('one', 12); shifted.measures.shift = 0.03;
  assert.deepEqual(differences([screen('one', 12)], [shifted]), []);
  // The lines of a measures file compare as the walk's own screens do.
  const written = parseMeasures(measuresLines({ journey: 'doors', viewport: VIEWPORTS[1], axe: true, screens: [screen('one', 12)] })).screens;
  assert.deepEqual(differences(written, written), []);
});

test('a page read before its first paint reports none, and a paint in whole milliseconds', () => {
  const kept = globalThis.__journeyPaint, timing = Object.getOwnPropertyDescriptor(globalThis, 'performance');
  const read = (entries, largest) => {
    globalThis.__journeyPaint = { largest, element: 'text', shift: 0.123 };
    Object.defineProperty(globalThis, 'performance', { configurable: true, value: {
      getEntriesByName: name => (name === 'first-contentful-paint' ? entries : []),
      getEntriesByType: () => [{ name: '/a.webp', encodedBodySize: 300 }, { name: '/a.js', encodedBodySize: 900 }],
    } });
    return readPaintInPage();
  };
  try {
    assert.deepEqual(read([], null), { first: null, largest: null, element: 'text', shift: 0.12, pictureBytes: 300 });
    assert.deepEqual(read([{ startTime: 812.6 }], 1204.4), { first: 813, largest: 1204, element: 'text', shift: 0.12, pictureBytes: 300 });
  } finally {
    globalThis.__journeyPaint = kept;
    Object.defineProperty(globalThis, 'performance', timing);
  }
});

test('a paint is reported as its median with the fastest and the slowest run', () => {
  assert.deepEqual(spread([4400, 4100, 4700]), { median: 4400, fastest: 4100, slowest: 4700 });
  assert.deepEqual(spread([10, 20, 30, 40]), { median: 25, fastest: 10, slowest: 40 });
  assert.equal(spread([null]), null);
  // A shift is a fraction: the middle of two runs is not rounded to a whole number.
  assert.deepEqual(spread([0.2, 0.3], 2), { median: 0.25, fastest: 0.2, slowest: 0.3 });
  const told = summary([{ first: 1200, largest: 4400, element: 'picture', shift: 0.26, pictureBytes: 700 }, { first: 1100, largest: 4000, element: 'picture', shift: 0.2, pictureBytes: 700 },
    { first: 1300, largest: 4600, element: 'picture', shift: 0.3, pictureBytes: 700 }]);
  assert.equal(told.runs, 3);
  assert.equal(told.largestPaintMs.median, 4400);
  assert.equal(told.largestIs, 'picture');
  assert.deepEqual(told.shift, { median: 0.26, fastest: 0.2, slowest: 0.3 });
  assert.equal(told.pictureBytes, 700);
});

test('the command reference and the scripts page name the journey film', () => {
  const commands = read('docs/reference/commands.md');
  assert.match(commands, /^### ux\/journey\.mjs$/m);
  for (const flag of ['--all', '--width', '--strip', '--paint', '--runs', '--list', '--out', '--showcase', '--frames', '--interval']) assert.ok(commands.includes(`\`${flag}`), `commands.md names ${flag}`);
  for (const journey of JOURNEYS) assert.ok(commands.includes(`\`${journey.id}\``), `commands.md names the ${journey.id} journey`);
  assert.match(read('scripts/README.md'), /`ux\/`/);
});

test('the baseline keeps every screen of every journey at both widths', () => {
  const baseline = read('docs/art/journeys/baseline.md');
  for (const journey of JOURNEYS) {
    assert.match(baseline, new RegExp(`^## ${journey.title}$`, 'm'), `the baseline has ${journey.title}`);
    // Read in the journey's own section: two journeys may name a screen alike (the match's and the windows' mutation-tree).
    const section = baseline.split(/^## /m).find(part => part.startsWith(`${journey.title}\n`)) ?? '';
    for (const name of journey.screens) {
      const rows = section.split('\n').filter(line => line.startsWith(`| ${name} |`) || line.startsWith(`| ${name} (staged) |`));
      assert.equal(rows.length, 2, `the baseline has ${journey.id}'s ${name} at both widths`);
    }
  }
  assert.match(read('docs/README.md'), /art\/journeys\/baseline\.md/);
});
