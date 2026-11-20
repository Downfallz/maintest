import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { openSpells } from './battle-spells-fixture.js';
import { fitsViewport } from './fit.js';
import { matchResult } from './battle-help-fixture.js';
import { decided } from './decision.js';

// A tutorial in the battle view (ADR 0187, onboarding-plan.md O3b): the band says Tutorial n of the series, the
// coach card shows the lesson step of the question asked, a step opens its concept cards before the spell choice,
// an assisted question is not drawn, and the phase tips stay silent. The lesson is tutorial 1's own, read from its
// file; the board and the host's answers are this fixture's.
test.describe.configure({ mode: 'default' });
const file = JSON.parse(readFileSync(new URL('../../scenarios/tutorials/your-first-combat.json', import.meta.url), 'utf8'));
const lesson = file.tutorial;
// The cards are read from the official rulebook the table serves (rulebook-plan.md R4), as the page reads it.
const book = JSON.parse(readFileSync(new URL('../../table/rules.json', import.meta.url), 'utf8'));
const cardOf = id => {
  const section = book.parts.flatMap(part => part.sections).find(one => one.card?.id === id);
  return { ...section.card, section };
};
const id = 'sc-repo-tutorial-your-first-combat';
const legal = candidates => ({ candidates, minTargets: 1, maxTargets: 1 });
const creature = (number, health = 30) => ({ id: number, name: `Creature ${number}`, health, maxHealth: 30, energy: 4, totalDefense: number === 5 ? 2 : 0,
  currentInitiative: 5, isAlive: true, isStunned: false, isStunImmune: false, knownSpells: ['shock'], conditions: [], acquiredTiers: [] });

async function mount(page, { assistedFirst = false, book = null, upkeepAtEnd = false, endsMatch = false, order: place = lesson.order, series = null } = {}) {
  const state = { errors: [], writes: [], said: [], left: 0, opened: 0, asked: 20, answered: 0, handedBack: false };
  page.on('pageerror', error => state.errors.push(error.message));
  const order = lesson.steps.map(step => step.at.creature);
  // The lesson's casts, each one Shock on creature 4, as a match played to its end records them: the live seat reads
  // each in the poll after its answer, and Replay the match reads them all back from the start (#904).
  const enemy = health => [creature(4, health), creature(5), creature(6)], allies = [1, 2, 3].map(number => creature(number));
  const casts = order.map((actor, step) => ({ sequence: step, round: 2, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor, spell: 'shock', targets: [4] } }, appliedOutcomes: [{ kind: 'DamageOutcome', amount: 3, target: 4 }],
    frame: { before: [...allies, ...enemy(9 - 3 * step)], after: [...allies, ...enemy(Math.max(0, 6 - 3 * step))], timeline: [], rollOffs: [] } } }));
  const view = (since = 0) => {
    const done = state.answered >= order.length, assisted = assistedFirst && state.answered === 0 && !state.released;
    // A lesson that plays to the match's end (tutorial 4): its last answer ends the match, and the host says it is done.
    const over = done && endsMatch;
    const board = { slot: 'Player1', roundNumber: done ? 3 : 2, phase: done || assisted ? 'Planning' : 'Combat', subPhase: done || assisted ? 'Speed' : 'Activation', activationCursor: 0,
      allies: [1, 2, 3].map(number => creature(number)), enemies: over ? [4, 5, 6].map(number => ({ ...creature(number, 0), isAlive: false })) : [creature(4, Math.max(1, 9 - 3 * state.answered)), creature(5), creature(6)],
      timeline: done || assisted ? [] : [{ creature: order[state.answered], owner: 'Player1', speed: 'Standard' }], speedChoices: [] };
    const tutorial = { order: place, of: 4, title: lesson.title, teaches: lesson.teaches,
      assisted: state.handedBack ? [] : ['Evolution', 'Speed', 'TieOrder', 'Intent', 'Target'], steps: lesson.steps, shown: Math.min(order.length, state.answered + (assisted ? 0 : 1)), done: done && !state.notYetDone };
    const question = assisted || (done && state.handedBack) ? { waitingFor: 'Speed', waitingAsked: state.asked, waitingCreature: 1, options: { speed: { missing: [1, 2, 3] } } }
      : done ? { waitingFor: null }
        : { waitingFor: 'Action', waitingAsked: state.asked, waitingCreature: order[state.answered], options: { action: { actor: order[state.answered], spells: [{ spell: 'shock', legalTargets: legal([4, 5, 6]) }] } } };
    // The round the lesson ends on opens with its upkeep, as the host's view says it.
    const feed = done && upkeepAtEnd ? [
      { sequence: 1, event: { kind: 'RoundEnergyGranted', roundId: 3, grants: [1, 2, 3, 4, 5, 6].map(one => ({ creature: one, gained: 2, before: 2, after: 4, baseRequested: 2, passiveRequested: 0 })) } },
      { sequence: 2, event: { kind: 'OngoingEffectsApplied', roundId: 3, energyRegenerationTicks: [], regenerationTicks: [], bleedTicks: [] } },
    ] : endsMatch ? casts.slice(0, state.answered) : [];
    // The feed from `since` on, counted as the host counts it (TableApi): a replay reads it whole, the live seat only
    // what it has not seen.
    const read = endsMatch ? feed.filter(entry => entry.sequence >= since) : feed;
    return { over, outcome: over ? { winner: 'player1', reason: 'Elimination' } : null, recording: false, feed: read, feedNext: feed.length, practice: { scenario: id, title: file.title, goal: file.goal, round: 2, tutorial }, board, ...question };
  };
  await page.addInitScript(() => {
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      // Every stream the page opens is told (the header's own too): the seat's is whichever reads the seat again.
      const before = window.notifyBattle;
      window.notifyBattle = () => { before?.(); try { controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n')); } catch { /* A stream the page closed. */ } };
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === `/api/scenarios/${id}/practice`) {
      if (request.method() === 'GET') return route.fulfill({ status: 404, json: { error: 'Scenario.NoPractice', message: 'None.' } });
      // Leaving lets go of the practice, so the next time it opens it starts from its cut.
      if (request.method() === 'DELETE') { state.left++; return route.fulfill({ status: 204 }); }
      // Each practice opened is a new table, as the host gives it (ScenariosApi): the result opens once per table.
      state.opened++;
      return route.fulfill({ json: { seat: 'player1', token: 'lesson', table: { id: `practice-lesson-${state.opened}`, format: '3v3', seats: [{ seat: 'player1', name: 'Learner', kind: 'person' }, { seat: 'player2', name: 'Practice bot', kind: 'bot' }] }, practice: view().practice } });
    }
    // What the page says of the account's progress (O4): skipped, or done once the host's view says so.
    if (url.pathname.startsWith(`/api/tutorials/${id}/`) && request.method() === 'POST') { const what = url.pathname.split('/').at(-1); state.said.push(what); return route.fulfill({ json: { id, state: what === 'done' ? 'done' : 'skipped' } }); }
    // The series, as the home reads it: the end card names the next lesson from it (UX8).
    if (url.pathname === '/api/tutorials') return series ? route.fulfill({ json: series }) : route.fulfill({ status: 404, json: {} });
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_learnerlearnerlea', username: 'Learner' } } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { rules: { roundCap: 12, combatDeclaration: 'AtActivation' }, cards: [{ id: 'shock', name: 'Shock', cost: 2, targeting: 'One enemy', effects: ['Damage 7'] }], packages: [] } });
    // Held, the last answer is written but the view that says the lesson is done waits for the test to let it through.
    if (url.pathname.endsWith('/decision') && state.holdEnd && state.answered + 1 === order.length) state.held = view();
    if (url.pathname.endsWith('/decision')) { state.writes.push(decided(request)); state.answered++; state.asked++; return route.fulfill({ status: 204 }); }
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: state.held ?? view(Number(url.searchParams.get('since') ?? 0)) });
    return route.fulfill({ status: 404, json: {} });
  });
  // The rulebook the cards are read from, held until the test lets it through.
  if (book) await page.route('**/table/rules.json', async route => { await book; await route.continue(); });
  await page.goto(`/table/battle.html?scenario=${id}`);
  await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true', { timeout: 20000 });
  return state;
}

// One lesson step as a person plays it: the concept cards it opens, read one after the other, then the coach card
// beside the spell choice, then the cast.
async function playStep(page, state, index, capture) {
  const step = lesson.steps[index], dialog = page.locator('#concept-dialog'), sent = state.writes.length;
  await expect(dialog).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#combat-dialog')).toBeHidden();
  for (let card = 0; card < step.concepts.length; card++) {
    const written = cardOf(step.concepts[card]);
    await expect(page.locator('#concept-title')).toHaveText(written.title);
    await expect(page.locator('#concept-example')).toHaveText(written.example);
    await expect(page.locator('#concept-rulebook')).toHaveText(`Rules, ${written.section.number} ${written.section.title}`);
    await expect(page.locator('#concept-rulebook')).toHaveAttribute('href', `/rules#${written.section.id}`);
    await fitsViewport(page, '#concept-next');
    if (index === 0 && card === 0 && capture) await capture('concept-card');
    await page.locator('#concept-next').click();
  }
  await expect(dialog).toBeHidden();
  const coach = page.locator('#coach');
  await expect(coach).toBeVisible();
  await expect(page.locator('#coach-step')).toHaveText(`STEP ${index + 1} OF ${lesson.steps.length}`);
  for (const line of step.says) await expect(page.locator('#coach-lines')).toContainText(line);
  await expect(page.locator('#coach-cards button')).toHaveCount(step.concepts.length);
  await expect(page.locator(`[data-creature="${step.pointsAt.creature}"]`)).toHaveAttribute('data-coach', 'true');
  await expect(page.locator('#practice-detail')).toHaveText(`Step ${index + 1} of ${lesson.steps.length}`);
  // The phase tips stay silent, and the spell choice opens by itself once the cards are read.
  await expect(page.locator('#help-dialog')).toBeHidden();
  await openSpells(page);
  // Where the spell choice docks under the field, the card stands in the sheet and leaves the field clear (#650).
  if (await page.evaluate(() => !matchMedia('(min-width: 760px), (orientation: landscape) and (max-height: 520px)').matches)) {
    await expect(coach).toHaveClass(/in-sheet/);
    expect(await page.evaluate(() => {
      const card = document.getElementById('coach').getBoundingClientRect(), field = document.getElementById('battle-zone').getBoundingClientRect();
      return card.top >= field.bottom - 1;
    })).toBe(true);
    await fitsViewport(page, '#coach-cards button');
    // Its concept cards stand on the step's row, above its lines, so the sheet keeps its choices in view (#670).
    expect(await page.evaluate(() => document.getElementById('coach-cards').getBoundingClientRect().bottom <= document.getElementById('coach-lines').getBoundingClientRect().top + 1)).toBe(true);
    // Laid out in the sheet, a hidden card is still hidden.
    expect(await page.evaluate(() => { const card = document.getElementById('coach'); card.hidden = true; const shown = getComputedStyle(card).display; card.hidden = false; return shown; })).toBe('none');
  }
  if (index === 0 && capture) await capture('coach-card');
  await page.locator('[data-spell="shock"]').click();
  // Targets are chosen on the field: the coach card steps aside, and the creature the step points at stays ringed.
  await expect(page.locator('#battle')).toHaveAttribute('data-combat', 'targets');
  await expect(coach).toBeHidden();
  await expect(page.locator(`[data-creature="${step.pointsAt.creature}"]`)).toHaveAttribute('data-coach', 'true');
  if (index === 0 && capture) await capture('coach-targets');
  await page.locator('[data-creature="4"]').click();
  await page.locator('#combat-confirm').click();
  await expect.poll(() => state.writes.length).toBe(sent + 1);
}

test('tutorial 1 is played to its end: each step shows its coach card and opens its concept cards', async ({ page }, info) => {
  // Three steps, each with its cards, its spell and its target: a software-rendered desktop canvas on a CI runner
  // took more than the default thirty seconds to play them all.
  test.setTimeout(120000);
  const state = await mount(page);
  const capture = name => page.screenshot({ path: info.outputPath(`${name}.png`) });
  const band = page.locator('#practice-band');
  await expect(band).toBeVisible({ timeout: 10000 });
  await expect(page.locator('#practice-eyebrow')).toHaveText('TUTORIAL 1 OF 4');
  await expect(page.locator('#practice-title')).toHaveText('Your first combat');
  await expect(page.locator('#practice-leave')).toHaveAttribute('href', '/');
  // The band stands in the header, in place of what a tutorial has no use for, not over the field.
  await expect(page.locator('#battle .hud-top > #practice-band')).toHaveCount(1);
  for (const id of ['#opponent-open', '#history-open', '#help-open']) await expect(page.locator(id)).toBeHidden();
  // It opens on its briefing, which says it is a tutorial too; nothing of the lesson shows behind it.
  await expect(page.locator('#practice-brief')).toBeVisible();
  await expect(page.locator('#practice-brief-eyebrow')).toHaveText('TUTORIAL 1 OF 4');
  await expect(page.locator('#concept-dialog')).toBeHidden();
  // The first card says what this is, and offers Start and Skip the tutorial (O4).
  await expect(page.locator('#practice-brief-begin')).toHaveText('Start');
  await expect(page.locator('#practice-brief-skip')).toHaveText('Skip the tutorial');
  await expect(page.locator('#practice-brief-note')).toContainText('Learn to play');
  await fitsViewport(page, '#practice-brief-begin, #practice-brief-skip');
  await capture('first-card');
  await page.locator('#practice-brief-begin').click();

  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index, capture);

  expect(state.writes.map(one => [one.kind, one.creature, one.targets])).toEqual(lesson.steps.map(step => ['Action', step.at.creature, [4]]));
  // Its last step shown and its round ended: the host says it is done, and the coach says so.
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#practice-detail')).toHaveText('Tutorial complete');
  await expect(page.locator('#coach')).toHaveAttribute('data-done', 'true');
  await expect(page.locator('#practice-leave')).toBeVisible();
  // The host said it is done: the page has the account keep it, once.
  await expect.poll(() => state.said).toEqual(['done']);
  // The lesson is not played on: the next round's question is not drawn, whoever was to answer it.
  await expect(page.locator('#planning-dialog')).toBeHidden(); await expect(page.locator('#planning-open')).toBeHidden();
  await expect(page.locator('#combat-open')).toBeHidden();
  await expect(page.locator('#practice-restart')).toHaveText('Play again');
  // On the smallest phone the card's Restart the round goes once the lesson is done: its end card offers Play again (#728).
  await expect(page.locator('#coach-restart')).toBeHidden();
  await capture('tutorial-complete');
  await fitsViewport(page, '#coach-again, #coach-leave, #practice-restart, #practice-leave');
  await expect(page.locator('#coach-again')).toHaveText('Play again');
  await expect(page.locator('#coach-leave')).toHaveAttribute('href', '/');
  // The end says how the board stands and leads on to the next lesson, its one filled action, focused (UX8). The
  // series not read here, it names the next lesson's place and Learn to play, which lists it.
  await expect(page.locator('#coach-lines')).toContainText('Your team: 3 of 3 standing.');
  await expect(page.locator('#coach-next')).toHaveText('Next: tutorial 2 of 4');
  await expect(page.locator('#coach-next')).toHaveAttribute('href', '/#learn-to-play');
  await expect(page.locator('#coach-next')).toHaveClass(/primary/);
  await expect(page.locator('#coach-again')).not.toHaveClass(/primary/);
  await expect(page.locator('#coach-next')).toBeFocused();
  await expect(page.locator('#instruction')).toHaveText('Tutorial complete.');
  // No new round is announced for a round the lesson will not play.
  await expect(page.locator('#round-toast')).toBeHidden();
  // On a phone the end card stands on the dock, which keeps its size, so the field it ends on stays in view.
  if (await page.evaluate(() => !matchMedia('(min-width: 760px), (orientation: landscape) and (max-height: 520px)').matches)) {
    await expect(page.locator('#battle .command-dock > #coach')).toHaveCount(1);
    expect(await page.evaluate(() => {
      const card = document.getElementById('coach').getBoundingClientRect(), dock = document.querySelector('#battle .command-dock').getBoundingClientRect();
      return Math.abs(card.bottom - dock.bottom) <= 1;
    })).toBe(true);
  } else await expect(page.locator('#battle .command-dock > #coach')).toHaveCount(0);
  // Leaving a finished tutorial is not skipping it, and lets go of its practice: it opens from its cut the next time.
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#coach-leave').click();
  await expect(page).toHaveTitle('Home');
  expect(state.said).toEqual(['done']);
  expect(state.left).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(state.errors).toEqual([]);
});

// The smallest phone reads the band as a tutorial from the first second: its place in the series stays in the header,
// in Restart's place, and the coach card offers Restart the round until the lesson is done (#728).
test('on a 320 px phone the header still says Tutorial n of 4, and the coach card offers Restart the round', async ({ page }, info) => {
  test.skip(info.project.name !== 'small-phone', 'The smallest phone is the one that had no room for it.');
  const state = await mount(page);
  await expect(page.locator('#practice-brief')).toBeVisible({ timeout: 10000 });
  await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('#practice-eyebrow')).toHaveText('TUTORIAL 1 OF 4');
  const boxes = await page.evaluate(() => Object.fromEntries(['#recap-open', '#practice-eyebrow', '#practice-leave', '#settings'].map(one => {
    const node = document.querySelector(one), box = node.getBoundingClientRect();
    return [one, { left: box.left, right: box.right, width: box.width, clipped: node.scrollWidth > node.clientWidth + 1 }];
  })));
  // The eyebrow is read whole, and nothing in the header covers it.
  expect(boxes['#practice-eyebrow'].clipped).toBe(false);
  expect(boxes['#practice-eyebrow'].left).toBeGreaterThanOrEqual(boxes['#recap-open'].right - 1);
  expect(boxes['#practice-eyebrow'].right).toBeLessThanOrEqual(boxes['#practice-leave'].left + 1);
  expect(boxes['#practice-leave'].right).toBeLessThanOrEqual(boxes['#settings'].left + 1);
  await fitsViewport(page, '#practice-leave, #settings, #coach-restart');
  await expect(page.locator('#coach-restart')).toBeVisible();
  await page.screenshot({ path: info.outputPath('tutorial-header-320.png') });
  // Restart the round, from the card, restarts it as the band's button does.
  const opened = state.opened;
  await page.locator('#coach-restart').click();
  await expect.poll(() => state.opened).toBeGreaterThan(opened);
});

test('a tutorial done opens nothing over its end card: the next round\'s upkeep waits, and Leave answers', async ({ page }) => {
  // The view that says the lesson is done brings the next round's upkeep. It arrives under Settings, so its results
  // settle unplayed and the upkeep is left to its own notice, as on a busy machine (#771): closing Settings must
  // not open it over the end card, where it held Leave until the test timed out.
  test.setTimeout(120000);
  const state = await mount(page, { upkeepAtEnd: true });
  state.holdEnd = true;
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await page.locator('#settings').click();
  await expect(page.locator('#settings-dialog')).toBeVisible();
  state.held = null;
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#practice-detail')).toHaveText('Tutorial complete');
  await page.keyboard.press('Escape');
  await expect(page.locator('#settings-dialog')).toBeHidden();
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#coach-leave').click({ timeout: 5000 });
  await expect(page).toHaveTitle('Home');
  expect(state.said).toEqual(['done']);
  expect(state.errors).toEqual([]);
});

test('an upkeep already open when the tutorial is done is put away: the end card and its Leave are in front', async ({ page }) => {
  // The host notes the lesson done apart from the seat's view (TutorialPlay), so one view can bring the next round's
  // upkeep before another says the lesson is done (#771). Read under Settings, that upkeep opens on its own once
  // Settings closes; the view that says done must not leave it over the end card.
  test.setTimeout(120000);
  const state = await mount(page, { upkeepAtEnd: true });
  state.holdEnd = true;
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await page.locator('#settings').click();
  await expect(page.locator('#settings-dialog')).toBeVisible();
  state.held = null; state.notYetDone = true;
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#round-number')).toContainText('3');
  await page.keyboard.press('Escape');
  await expect(page.locator('#upkeep-dialog')).toBeVisible();
  state.notYetDone = false;
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#upkeep-dialog')).toBeHidden();
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#coach-leave').click({ timeout: 5000 });
  await expect(page).toHaveTitle('Home');
  expect(state.errors).toEqual([]);
});

test('a concept card opens again from the coach card, and closes with Escape', async ({ page }) => {
  const state = await mount(page);
  await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('#concept-dialog')).toBeHidden();
  await openSpells(page);
  await fitsViewport(page, '#coach-cards button');
  await page.locator('#coach-cards [data-concept="health"]').click();
  await expect(page.locator('#concept-dialog')).toBeVisible();
  await expect(page.locator('#concept-title')).toHaveText(cardOf('health').title);
  await expect(page.locator('#concept-next')).toHaveText('Back to the battle');
  await page.locator('#concept-next').click();
  await expect(page.locator('#concept-dialog')).toBeHidden();
  expect(state.writes).toEqual([]); expect(state.errors).toEqual([]);
});

// The table serves the rulebook at its root (/rules, ADR 0191); this server serves the table under /table/, so the
// tab a card opens is given the real page and what it reads from table/.
async function serveTheRules(context) {
  await context.route(url => /^\/(rules|[\w-]+\.(js|css|json|svg|png|webmanifest))$/.test(url.pathname), route => {
    const path = new URL(route.request().url()).pathname;
    return route.fulfill({ path: new URL(`../../table${path === '/rules' ? '/rules.html' : path}`, import.meta.url).pathname });
  });
  await context.route('**/api/me', route => route.fulfill({ json: { accounts: 'google', account: null, googleClientId: 'test-client', operator: false, notice: null } }));
}

test('a concept card links to its section of the rulebook, which opens in a new tab and leaves the battle on its question', async ({ page, context }) => {
  const state = await mount(page);
  await serveTheRules(context);
  await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  const first = cardOf(lesson.steps[0].concepts[0]);
  const link = page.locator('#concept-rulebook');
  await expect(link).toHaveAttribute('target', '_blank');
  await expect(link).toHaveAccessibleName(`Rules, ${first.section.number} ${first.section.title}, opens in a new tab`);
  await fitsViewport(page, '#concept-rulebook');
  const [tab] = await Promise.all([context.waitForEvent('page'), link.click()]);
  await tab.waitForLoadState();
  expect(new URL(tab.url()).hash).toBe(`#${first.section.id}`);
  await expect(tab.locator('body')).toHaveAttribute('data-rules', 'ready');
  await expect(tab.locator(`#${first.section.id} h3`)).toBeInViewport();
  await expect(tab.locator(`#${first.section.id}`)).toContainText(first.title);
  // The battle is where it was: the same card, the same question, nothing sent.
  await page.bringToFront();
  expect(new URL(page.url()).pathname).toBe('/table/battle.html');
  await expect(page.locator('#concept-dialog')).toBeVisible();
  await expect(page.locator('#concept-title')).toHaveText(first.title);
  for (let card = 0; card < lesson.steps[0].concepts.length; card++) await page.locator('#concept-next').click();
  await expect(page.locator('#concept-dialog')).toBeHidden();
  await openSpells(page);
  await expect(page.locator('#coach-step')).toHaveText(`STEP 1 OF ${lesson.steps.length}`);
  expect(state.writes).toEqual([]); expect(state.errors).toEqual([]);
});

// The cards are read from the book once a tutorial shows (rulebook-plan.md R4): on a slow connection the question's
// own sheet waits for the book, so the step's cards still open first.
test('a step whose rulebook arrives late still opens its cards before the spell choice', async ({ page }) => {
  let release;
  const state = await mount(page, { book: new Promise(resolve => { release = resolve; }) });
  await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#coach')).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#coach-cards')).toBeHidden();
  // Long enough for the sheet to open by itself, were it not waiting.
  await page.waitForTimeout(1500);
  await expect(page.locator('#combat-dialog')).toBeHidden();
  release();
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#concept-title')).toHaveText(cardOf(lesson.steps[0].concepts[0]).title);
  await expect(page.locator('#combat-dialog')).toBeHidden();
  for (let card = 0; card < lesson.steps[0].concepts.length; card++) await page.locator('#concept-next').click();
  await openSpells(page);
  expect(state.writes).toEqual([]); expect(state.errors).toEqual([]);
});

test('an assisted question is not drawn, and is the person\'s once the host hands every question back', async ({ page }) => {
  const state = await mount(page, { assistedFirst: true });
  await page.locator('#practice-brief-begin').click();
  await expect(page.locator('#connection')).toHaveText(/^LIVE/);
  // The host is asking the tempos, which it answers itself: no sheet, no command, no coach card, no tip.
  await expect(page.locator('#planning-dialog')).toBeHidden(); await expect(page.locator('#planning-open')).toBeHidden();
  await expect(page.locator('#coach')).toBeHidden(); await expect(page.locator('#help-dialog')).toBeHidden();
  // The recorded answers no longer fit: every question is the person's, and the sheet opens.
  state.handedBack = true;
  await page.evaluate(() => window.notifyBattle());
  await expect(page.locator('#planning-dialog')).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed');
  expect(state.errors).toEqual([]);
});

test('Skip the tutorial on the first card says it is skipped and leaves for the home page', async ({ page }) => {
  const state = await mount(page);
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await expect(page.locator('#practice-brief')).toBeVisible();

  await page.locator('#practice-brief-skip').click();

  await expect(page).toHaveTitle('Home');
  expect(state.said).toEqual(['skip']);
  expect(state.left).toBe(1);
  expect(state.writes).toEqual([]); expect(state.errors).toEqual([]);
});

test('Leave before the end counts as skipping: the tutorial never opens by itself again', async ({ page }) => {
  const state = await mount(page);
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#practice-brief-begin').click();
  // The step's concept card opens once the board is drawn: it is put away once it is there, not before, or it
  // opens over Leave a moment later (a slow desktop canvas on a CI runner).
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('#concept-dialog')).toBeHidden();
  await fitsViewport(page, '#practice-leave');

  await page.locator('#practice-leave').click();

  await expect(page).toHaveTitle('Home');
  expect(state.said).toEqual(['skip']);
  // Its practice is let go of too: the next time it opens, it starts from its cut.
  expect(state.left).toBe(1);
  expect(state.errors).toEqual([]);
});

test('Play again on the end card starts the tutorial over from its cut', async ({ page }) => {
  test.setTimeout(120000);
  const state = await mount(page);
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  const opened = state.opened;

  state.answered = 0;
  await page.locator('#coach-again').click();

  await expect.poll(() => state.opened).toBe(opened + 1);
  await expect(page.locator('#practice-brief')).toBeVisible();
  await expect(page.locator('#practice-restart')).toHaveText('Restart the round');
  expect(state.errors).toEqual([]);
});

// A tutorial that plays to the match's end (tutorial 4) ends on the match result, which is the tutorial's end
// (tutorial-follow-ups-plan.md T1, #726): it says so, and offers Play again and Leave the tutorial where a game
// offers Rematch and Lobby.
test('a tutorial that ends the match ends on its result: Tutorial complete, Play again and Leave the tutorial', async ({ page }, info) => {
  test.setTimeout(120000);
  const state = await mount(page, { endsMatch: true });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);

  await matchResult(page);
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#match-title')).toHaveText('Victory');
  await expect(page.locator('#match-again')).toHaveText('Play again');
  await expect(page.locator('#match-leave')).toHaveText('Leave the tutorial');
  await expect(page.locator('#match-leave')).toHaveAttribute('href', '/');
  // Rematch and Lobby give their places; the replay and Report or suggest stay.
  await expect(page.locator('#match-rematch')).toBeHidden(); await expect(page.locator('#match-lobby')).toBeHidden();
  await expect(page.locator('#match-replay')).toBeVisible(); await expect(page.locator('#feedback-open-match')).toBeVisible();
  await fitsViewport(page, '#match-again, #match-replay, #match-leave, #feedback-open-match, #match-dialog [aria-label="Close match result"]');
  // Each is read whole: on the smallest phone Replay the match and Leave the tutorial take a row each.
  expect(await page.evaluate(() => ['match-again', 'match-replay', 'match-leave'].filter(one => { const node = document.getElementById(one); return node.scrollWidth > node.clientWidth + 1; }))).toEqual([]);
  await page.screenshot({ path: info.outputPath('tutorial-result.png') });
  await expect.poll(() => state.said).toEqual(['done']);
  // Leaving from the result is leaving a finished tutorial: not skipped, and its practice let go of.
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#match-leave').click();
  await expect(page).toHaveTitle('Home');
  // The page leaves after 1.5 s at most, its requests sent with keepalive: the route may hear of them after Home shows.
  await expect.poll(() => state.left).toBe(1);
  expect(state.said).toEqual(['done']);
  expect(state.errors).toEqual([]);
});

test('Play again on a tutorial\'s result starts it over from its cut, and the result closed leaves the end card', async ({ page }) => {
  // The lesson is played twice to its result: six steps on a software-rendered canvas.
  test.setTimeout(240000);
  const state = await mount(page, { endsMatch: true });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await matchResult(page);
  // Closed, the coach card's end shows, as in a tutorial that ends before its match does.
  await page.keyboard.press('Escape');
  await expect(page.locator('#match-dialog')).toBeHidden();
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#coach-again')).toBeVisible();
  await matchResult(page);
  const opened = state.opened;

  state.answered = 0;
  await page.locator('#match-again').click();

  await expect.poll(() => state.opened).toBe(opened + 1);
  await expect(page.locator('#match-dialog')).toBeHidden();
  await expect(page.locator('#practice-brief')).toBeVisible();
  await expect(page.locator('#practice-restart')).toHaveText('Restart the round');

  // Played again to its end, the lesson ends on its result again: the host gave the new practice its own table,
  // and the result opens once per table and seat (#904).
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await expect(page.locator('#match-dialog')).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#match-title')).toHaveText('Victory');
  await expect(page.locator('#match-again')).toHaveText('Play again');
  await expect(page.locator('#match-leave')).toHaveText('Leave the tutorial');
  await expect(page.locator('#match-rematch')).toBeHidden(); await expect(page.locator('#match-lobby')).toBeHidden();
  // Each pass answered every step once, and each said the tutorial is done.
  await expect.poll(() => state.writes.length).toBe(2 * lesson.steps.length);
  await expect.poll(() => state.said).toEqual(['done', 'done']);
  expect(state.errors).toEqual([]);
});

// The view says the match is over but not that the tutorial is done (a table that could not go on). The result is
// still the tutorial's, with Play again and Leave the tutorial and no game's Rematch or Lobby, but it does not say
// Tutorial complete. Leaving from it tells the host nothing about the tutorial: it is neither done nor skipped (#912).
test('a match over before the host says the tutorial done is not called complete, and leaving it skips nothing', async ({ page }) => {
  test.setTimeout(120000);
  const state = await mount(page, { endsMatch: true });
  state.notYetDone = true;
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await matchResult(page);
  await expect(page.locator('#match-eyebrow')).not.toHaveText(/^TUTORIAL .* COMPLETE/);
  await expect(page.locator('#match-again')).toBeVisible(); await expect(page.locator('#match-leave')).toBeVisible();
  await expect(page.locator('#match-rematch')).toBeHidden(); await expect(page.locator('#match-lobby')).toBeHidden();
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));

  await page.locator('#match-leave').click();

  await expect(page).toHaveTitle('Home');
  // The page leaves after 1.5 s at most, its requests sent with keepalive: the route may hear of them after Home shows.
  await expect.poll(() => state.left).toBe(1);
  expect(state.said).toEqual([]);
  expect(state.errors).toEqual([]);
});

// Replay the match from a tutorial's result reads the lesson's casts, and closed it gives the tutorial's result
// back: Tutorial complete, Play again and Leave the tutorial (#904).
test('Replay the match from a tutorial\'s result, closed, gives back Tutorial complete', async ({ page }) => {
  test.setTimeout(120000);
  const state = await mount(page, { endsMatch: true });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await matchResult(page);
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');

  await page.locator('#match-replay').click();

  await expect(page.locator('#match-dialog')).toBeHidden();
  await expect(page.locator('#replay-controls')).toBeVisible();
  // The lesson's casts, then the match's result as the replay's last step.
  await expect(page.locator('#replay-position')).toHaveText(`Round 2 · 1 of ${lesson.steps.length + 1}`);
  await expect(page.locator('#replay-text')).toContainText('Shock');
  await page.locator('#replay-close').click();
  await expect(page.locator('#replay-controls')).toBeHidden();
  await expect(page.locator('#match-dialog')).toBeVisible();
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 1 OF 4 · COMPLETE');
  await expect(page.locator('#match-title')).toHaveText('Victory');
  await expect(page.locator('#match-again')).toHaveText('Play again');
  await expect(page.locator('#match-leave')).toHaveText('Leave the tutorial');
  await expect(page.locator('#match-rematch')).toBeHidden(); await expect(page.locator('#match-lobby')).toBeHidden();
  expect(state.errors).toEqual([]);
});

test('the end card names the next lesson the host lists, and Next leaves this one for it', async ({ page }) => {
  test.setTimeout(120000);
  const series = { tutorials: [{ id, order: 1, of: 4, title: 'Your first combat', link: `/battle?scenario=${id}` },
    { id: 'sc-repo-tutorial-who-acts-first', order: 2, of: 4, title: 'Who acts first', link: '/battle?scenario=sc-repo-tutorial-who-acts-first' }] };
  const state = await mount(page, { series });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await expect(page.locator('#coach-next')).toHaveText('Next: Who acts first');
  await fitsViewport(page, '#coach-next, #coach-again, #coach-leave');
  await page.route('**/battle?scenario=sc-repo-tutorial-who-acts-first', route => route.fulfill({ contentType: 'text/html', body: '<title>Next lesson</title>' }));
  await page.locator('#coach-next').click();
  await expect(page).toHaveTitle('Next lesson');
  // Finished, not skipped, and its practice let go of, as Leave does.
  await expect.poll(() => state.left).toBe(1);
  expect(state.said).toEqual(['done']);
  expect(state.errors).toEqual([]);
});

test('a step whose sheet is closed with nothing sent shows a hint naming the control, and the cast puts it away', async ({ page }) => {
  const state = await mount(page);
  await page.locator('#practice-brief-begin').click();
  // Two cards open at the first step: the eyebrow says how many stand between the person and the board, and the
  // card's own button holds the focus.
  await expect(page.locator('#concept-dialog')).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#concept-eyebrow')).toHaveText(`CONCEPT CARD · 1 OF ${lesson.steps[0].concepts.length}`);
  await expect(page.locator('#concept-next')).toBeFocused();
  while (await page.locator('#concept-dialog').evaluate(dialog => dialog.open)) await page.locator('#concept-next').click();
  await openSpells(page);
  await expect(page.locator('#coach-hint')).toBeHidden();
  await page.locator('#combat-dialog-close').click();
  await expect(page.locator('#combat-dialog')).toBeHidden();
  const words = (await page.locator('#combat-open').textContent()).trim();
  await expect(page.locator('#coach-hint')).toHaveText(`Next: ${words}.`);
  await openSpells(page);
  await page.locator('[data-spell="shock"]').click();
  await page.locator('[data-creature="4"]').click();
  await page.locator('#combat-confirm').click();
  await expect.poll(() => state.writes.length).toBe(1);
  await expect(page.locator('#coach-step')).toHaveText(`STEP 2 OF ${lesson.steps.length}`);
  await expect(page.locator('#coach-hint')).toBeHidden();
  expect(state.errors).toEqual([]);
});

const seriesOf = state => ({ tutorials: [1, 2, 3, 4].map(order => ({ id: order === 4 ? id : `sc-repo-tutorial-${order}`, order, of: 4, title: `Tutorial ${order}`, link: `/battle?scenario=sc-repo-tutorial-${order}`, state: order === 4 ? 'new' : state })) });

test('the last tutorial\'s result ends the series: Leave the tutorial is its filled way on', async ({ page }) => {
  test.setTimeout(120000);
  const state = await mount(page, { endsMatch: true, order: 4, series: seriesOf('done') });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await matchResult(page);
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 4 OF 4 · COMPLETE');
  await expect(page.locator('#match-series')).toHaveText('You finished every tutorial.');
  await expect(page.locator('#match-leave')).toHaveClass(/primary/);
  await expect(page.locator('#match-again')).not.toHaveClass(/primary/);
  await fitsViewport(page, '#match-leave, #match-again, #match-replay');
  // The match over, the end card is not laid over the dock: the result is the end, and the dock keeps its Match result.
  await expect(page.locator('#battle .command-dock > #coach')).toHaveCount(0);
  // Leave the tutorial stands first.
  expect(await page.evaluate(() => document.getElementById('match-leave').getBoundingClientRect().top <= document.getElementById('match-again').getBoundingClientRect().top)).toBe(true);
  expect(state.errors).toEqual([]);
});

test('the last tutorial played with an earlier one skipped does not say the series is finished', async ({ page }) => {
  test.setTimeout(120000);
  const state = await mount(page, { endsMatch: true, order: 4, series: seriesOf('skipped') });
  await page.locator('#practice-brief-begin').click();
  for (let index = 0; index < lesson.steps.length; index++) await playStep(page, state, index);
  await matchResult(page);
  await expect(page.locator('#match-eyebrow')).toHaveText('TUTORIAL 4 OF 4 · COMPLETE');
  await expect(page.locator('#match-series')).toBeHidden();
  await expect(page.locator('#match-leave')).toHaveClass(/primary/);
  expect(state.errors).toEqual([]);
});
