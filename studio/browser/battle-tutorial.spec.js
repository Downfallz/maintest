import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { openSpells } from './battle-spells-fixture.js';
import { fitsViewport } from './fit.js';
import { decided } from './decision.js';

// A tutorial in the battle view (ADR 0187, onboarding-plan.md O3b): the band says Tutorial n of the series, the
// coach card shows the lesson step of the question asked, a step opens its concept cards before the spell choice,
// an assisted question is not drawn, and the phase tips stay silent. The lesson is tutorial 1's own, read from its
// file; the board and the host's answers are this fixture's.
test.describe.configure({ mode: 'default' });
const file = JSON.parse(readFileSync(new URL('../../scenarios/tutorials/your-first-combat.json', import.meta.url), 'utf8'));
const lesson = file.tutorial;
const id = 'sc-repo-tutorial-your-first-combat';
const legal = candidates => ({ candidates, minTargets: 1, maxTargets: 1 });
const creature = (number, health = 30) => ({ id: number, name: `Creature ${number}`, health, maxHealth: 30, energy: 4, totalDefense: number === 5 ? 2 : 0,
  currentInitiative: 5, isAlive: true, isStunned: false, isStunImmune: false, knownSpells: ['shock'], conditions: [], acquiredTiers: [] });

async function mount(page, { assistedFirst = false } = {}) {
  const state = { errors: [], writes: [], said: [], asked: 20, answered: 0, handedBack: false };
  page.on('pageerror', error => state.errors.push(error.message));
  const order = lesson.steps.map(step => step.at.creature);
  const view = () => {
    const done = state.answered >= order.length, assisted = assistedFirst && state.answered === 0 && !state.released;
    const board = { slot: 'Player1', roundNumber: done ? 3 : 2, phase: done || assisted ? 'Planning' : 'Combat', subPhase: done || assisted ? 'Speed' : 'Activation', activationCursor: 0,
      allies: [1, 2, 3].map(number => creature(number)), enemies: [creature(4, Math.max(1, 9 - 3 * state.answered)), creature(5), creature(6)],
      timeline: done || assisted ? [] : [{ creature: order[state.answered], owner: 'Player1', speed: 'Standard' }], speedChoices: [] };
    const tutorial = { order: lesson.order, of: 4, title: lesson.title, teaches: lesson.teaches,
      assisted: state.handedBack ? [] : ['Evolution', 'Speed', 'TieOrder', 'Intent', 'Target'], steps: lesson.steps, shown: Math.min(order.length, state.answered + (assisted ? 0 : 1)), done };
    const question = assisted || (done && state.handedBack) ? { waitingFor: 'Speed', waitingAsked: state.asked, waitingCreature: 1, options: { speed: { missing: [1, 2, 3] } } }
      : done ? { waitingFor: null }
        : { waitingFor: 'Action', waitingAsked: state.asked, waitingCreature: order[state.answered], options: { action: { actor: order[state.answered], spells: [{ spell: 'shock', legalTargets: legal([4, 5, 6]) }] } } };
    return { over: false, recording: false, feed: [], feedNext: 0, practice: { scenario: id, title: file.title, goal: file.goal, round: 2, tutorial }, board, ...question };
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
      return route.fulfill({ json: { seat: 'player1', token: 'lesson', table: { id: 'practice-lesson', format: '3v3', seats: [{ seat: 'player1', name: 'Learner', kind: 'person' }, { seat: 'player2', name: 'Recorded opponent', kind: 'bot' }] }, practice: view().practice } });
    }
    // What the page says of the account's progress (O4): skipped, or done once the host's view says so.
    if (url.pathname.startsWith(`/api/tutorials/${id}/`) && request.method() === 'POST') { const what = url.pathname.split('/').at(-1); state.said.push(what); return route.fulfill({ json: { id, state: what === 'done' ? 'done' : 'skipped' } }); }
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_learnerlearnerlea', username: 'Learner' } } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { rules: { roundCap: 12, combatDeclaration: 'AtActivation' }, cards: [{ id: 'shock', name: 'Shock', cost: 2, targeting: 'One enemy', effects: ['Damage 7'] }], packages: [] } });
    if (url.pathname.endsWith('/decision')) { state.writes.push(decided(request)); state.answered++; state.asked++; return route.fulfill({ status: 204 }); }
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: view() });
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto(`/table/battle.html?scenario=${id}`);
  await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true', { timeout: 20000 });
  return state;
}

// One lesson step as a person plays it: the concept cards it opens, read one after the other, then the coach card
// beside the spell choice, then the cast.
async function playStep(page, state, index, capture) {
  const step = lesson.steps[index], dialog = page.locator('#concept-dialog');
  await expect(dialog).toBeVisible({ timeout: 15000 });
  await expect(page.locator('#combat-dialog')).toBeHidden();
  for (let card = 0; card < step.concepts.length; card++) {
    await expect(page.locator('#concept-title')).toHaveText(new RegExp(`^${step.concepts[card]}$`, 'i'));
    await expect(page.locator('#concept-rulebook')).toHaveText(/^Rulebook, /);
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
  await expect.poll(() => state.writes.length).toBe(index + 1);
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
  await expect(page.locator('#coach-step')).toHaveText('TUTORIAL COMPLETE');
  await expect(page.locator('#practice-detail')).toHaveText('Tutorial complete');
  await expect(page.locator('#coach')).toHaveAttribute('data-done', 'true');
  await expect(page.locator('#practice-leave')).toBeVisible();
  // The host said it is done: the page has the account keep it, once.
  await expect.poll(() => state.said).toEqual(['done']);
  // The tempos of the next round are still answered for the person: no sheet opens for them.
  await expect(page.locator('#planning-dialog')).toBeHidden(); await expect(page.locator('#planning-open')).toBeHidden();
  await capture('tutorial-complete');
  await fitsViewport(page, '#coach-continue, #coach-leave, #practice-restart, #practice-leave');
  await expect(page.locator('#coach-leave')).toHaveAttribute('href', '/');
  // Keep playing puts the end card away; the band still says where the person is, with Leave.
  await page.locator('#coach-continue').click();
  await expect(page.locator('#coach')).toBeHidden();
  await expect(band).toBeVisible();
  // Leaving a finished tutorial is only leaving: nothing is said to be skipped.
  await page.route('**/', route => route.fulfill({ contentType: 'text/html', body: '<title>Home</title>' }));
  await page.locator('#practice-leave').click();
  await expect(page).toHaveTitle('Home');
  expect(state.said).toEqual(['done']);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
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
  await expect(page.locator('#concept-title')).toHaveText('Health');
  await expect(page.locator('#concept-next')).toHaveText('Back to the battle');
  await page.locator('#concept-next').click();
  await expect(page.locator('#concept-dialog')).toBeHidden();
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
  expect(state.errors).toEqual([]);
});
