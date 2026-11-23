// The journey film's own rules (docs/art/ux-craft-plan.md, UX2), the ones that need no browser: what the script
// accepts, what the journeys name, how the measures are written and compared, and that the docs name them. The
// walk itself is played on the real engine by studio/browser/table.journey.js.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { JOURNEYS, account } from '../scripts/ux/journeys.mjs';
import { VIEWPORTS, blockedLines, ended, listing, parseArguments, settle, walked } from '../scripts/ux/journey.mjs';
import { differences, measuresLines, parseMeasures } from '../scripts/ux/measures.mjs';
import { readPaintInPage, signIn, spread, summary } from '../scripts/ux/paint.mjs';
import { confirm } from '../scripts/ux/play.mjs';
import { serveShowcase, startHost, stopProcess } from '../scripts/ux/set.mjs';

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const script = new URL('../scripts/ux/journey.mjs', import.meta.url).pathname;
const baselineScript = new URL('../scripts/ux/baseline.mjs', import.meta.url).pathname;

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

// The lessons film their first cast (#978): the coach and its ring, the spell chosen, the coach aside, then the blow,
// from the first step's screen, where the spell choice is open.
test('the lessons film the first cast from the first step', () => {
  const lessons = JOURNEYS.find(journey => journey.id === 'lessons');
  assert.equal(lessons.strips['first-cast']?.at, 'lesson-1-first-step');
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

// An option the chosen mode ignores is refused, not taken in silence (#1093, 7).
test('the strip\'s and the paints\' options are refused without their mode', () => {
  assert.equal(parseArguments(['lobby', '--strip', 'menu', '--frames', '6', '--interval', '40']).frames, 6);
  assert.throws(() => parseArguments(['doors', '--frames', '6']), /--frames films a strip: name --strip/);
  assert.throws(() => parseArguments(['doors', '--interval', '40']), /--interval films a strip: name --strip/);
  assert.throws(() => parseArguments(['doors', '--runs', '3']), /--runs counts the paints: name --paint/);
});

// The baseline's flags each take a value, never the next flag (#1093, 6).
test('the baseline refuses a flag given no value', () => {
  const run = spawnSync(process.execPath, [baselineScript, 'nowhere', '--commit', '--date', '2026-11-23'], { encoding: 'utf8' });
  assert.equal(run.status, 1);
  assert.match(run.stderr, /Usage/);
  assert.equal(run.stdout, '');
});

// A screen that never comes to rest is not measured as if it had (#1093, 4).
test('a screen whose measures never come to rest stops the walk and says so', async () => {
  let reads = 0;
  const page = { evaluate: async (_, argument) => (argument ? { words: reads++ } : undefined), waitForTimeout: async () => {} };
  await assert.rejects(settle(page), /never came to rest/);
  let still = 0;
  const calm = { evaluate: async (_, argument) => (argument ? { words: 3, read: still++ > 1 } : undefined), waitForTimeout: async () => {} };
  assert.deepEqual(await settle(calm), { words: 3, read: true });
});

// A strip whose page throws does not write its sheet as if nothing happened (#1093, 5).
test('a strip whose page threw fails as a walk does', () => {
  const lobby = JOURNEYS.find(journey => journey.id === 'lobby');
  const shots = [{ ms: 0 }];
  assert.throws(() => ended(lobby, { strip: 'menu', shots, screens: [], errors: ['boom'], blocks: [] }), /lobby journey's page threw: boom/);
  assert.deepEqual(ended(lobby, { strip: 'menu', shots, screens: [], errors: [], blocks: [] }).map(shot => shot.ms), [0]);
  assert.throws(() => ended(lobby, { strip: 'menu', shots: null, screens: [], errors: [], blocks: [] }), /lobby journey never reached "home-new", where menu is filmed/);
});

// A setup post the host refuses stops the walk there, not somewhere unrelated later (#1093, 2).
const refusing = refused => {
  const posted = [];
  const request = { post: async url => { posted.push(url); return { ok: () => !refused.test(url), status: () => (refused.test(url) ? 500 : 204) }; } };
  return { posted, request };
};
test('a walk\'s account stops when the host refuses to skip its lessons', async () => {
  const { request } = refusing(/\/skip$/);
  const walk = { set: { game: 'http://game' } };
  await assert.rejects(account(walk, { request }, 'Walker'), /refused to skip the lessons of Walker: 500/);
});
test('the paints\' sign-in stops when the host refuses the account or the skip', async () => {
  for (const [refused, said] of [[/\/dev$/, /refused the account Walker: 500/], [/\/skip$/, /refused to skip the lessons of Walker: 500/]]) {
    const context = refusing(refused);
    await assert.rejects(signIn(context, 'http://game'), said);
  }
  const context = refusing(/nothing/);
  await signIn(context, 'http://game');
  assert.equal(context.posted.length, 2);
});

// The tempo sheet sends one decision per creature: a refusal of any of them is read, not only of the first (#1093, 3).
test('a confirmation reads every decision its sheet sends until the sheet closes', async () => {
  const decision = status => ({ url: () => 'http://game/api/seat/x/decision', request: () => ({ method: () => 'POST' }), status: () => status });
  const fake = statuses => {
    const listeners = new Set();
    let hidden;
    const closed = new Promise(done => { hidden = done; });
    return {
      on: (event, listener) => { if (event === 'response') listeners.add(listener); },
      off: (event, listener) => { listeners.delete(listener); },
      waitForResponse: predicate => new Promise(done => listeners.add(response => { if (predicate(response)) done(response); })),
      locator: selector => ({
        click: async () => {
          for (const status of statuses) { await Promise.resolve(); for (const listener of [...listeners]) listener(decision(status)); }
          // The sheet stays open on a refusal, as the tempo sheet does; it closes once every tempo is in.
          if (statuses.every(status => status === 204)) hidden();
        },
        waitFor: async () => { assert.equal(selector, '#planning-dialog'); await closed; },
      }),
    };
  };
  await assert.rejects(confirm(fake([204, 409]), '#planning-confirm', { until: '#planning-dialog' }), /answered 409/);
  await confirm(fake([204, 204]), '#planning-confirm', { until: '#planning-dialog' });
  await assert.rejects(confirm(fake([409]), '#planning-confirm'), /answered 409/);
});

// A host killed by a signal has no exit code: closing it returns at once, not at the spec's timeout (#1093, 1).
const child = (code = '') => new Promise((done, fail) => {
  const one = spawn(process.execPath, ['-e', `${code}; setInterval(() => {}, 1000); console.log('ready');`]);
  one.once('error', fail);
  one.stdout.once('data', () => done(one));
});
test('closing a process a signal already killed returns at once', { timeout: 5000 }, async () => {
  const one = await child();
  const gone = new Promise(done => one.once('exit', done));
  one.kill('SIGKILL');
  await gone;
  assert.equal(one.exitCode, null);
  await stopProcess(one, { grace: 60000 });
});
test('a process that ignores the interrupt is killed after its grace', { timeout: 5000 }, async () => {
  const one = await child("process.on('SIGINT', () => {})");
  await stopProcess(one, { grace: 200 });
  assert.equal(one.signalCode, 'SIGKILL');
});

// A host that cannot be spawned is said, not an uncaught error that ends the process (#1093, 8).
test('a host whose program is missing fails to start with a message', async () => {
  const folder = await mkdtemp(join(tmpdir(), 'ux-host-'));
  try {
    await assert.rejects(startHost(folder, { program: join(folder, 'no-dotnet') }), /The table host did not start/);
  } finally {
    await rm(folder, { recursive: true, force: true });
  }
});

// A malformed escape in a path is a 400, not a rejection nobody catches (#1093, 9).
test('the showcase answers a malformed path with 400 and keeps serving', async () => {
  const folder = await mkdtemp(join(tmpdir(), 'ux-site-'));
  const site = await serveShowcase(folder);
  try {
    assert.equal((await fetch(`${site.origin}/%E0%A4%A`, { signal: AbortSignal.timeout(5000) })).status, 400);
    assert.equal((await fetch(`${site.origin}/missing`, { signal: AbortSignal.timeout(5000) })).status, 404);
  } finally {
    await site.close();
    await rm(folder, { recursive: true, force: true });
  }
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
