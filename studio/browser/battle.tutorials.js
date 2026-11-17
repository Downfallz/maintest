import { test, expect } from '@playwright/test';
import { readdirSync, readFileSync } from 'node:fs';
import { mkdtemp, rm } from 'node:fs/promises';
import { join as pathJoin } from 'node:path';
import { tmpdir } from 'node:os';
import { startHost, stopHost } from './host.js';
import { mutationTree, teamTempos } from './battle-help-fixture.js';
import { openSpells } from './battle-spells-fixture.js';
import { fitsViewport } from './fit.js';

// The series on the real engine (ADR 0187, onboarding-plan.md O5): each tutorial of scenarios/tutorials/ opened
// by a signed-in account on a host that records, started from its first card, and played to its end with the
// person's own recorded answers, as its lesson expects. Every step's coach card shows, the host says the lesson
// is done, and the account keeps it: the home page's Learn to play says Done. The first one is reached as a new
// account reaches it, by its first visit to the home page.
test.describe.configure({ mode: 'default' });
const folder = new URL('../../scenarios/tutorials/', import.meta.url);
const series = readdirSync(folder).filter(name => name.endsWith('.json'))
  .map(name => ({ name, id: `sc-repo-tutorial-${name.slice(0, -'.json'.length)}`, file: JSON.parse(readFileSync(new URL(name, folder), 'utf8')) }))
  .sort((one, other) => one.file.tutorial.order - other.file.tutorial.order);

// The cards are read from the official rulebook the table serves (rulebook-plan.md R4), as the page reads it.
const book = JSON.parse(readFileSync(new URL('../../table/rules.json', import.meta.url), 'utf8'));
const cardTitles = new Map(book.parts.flatMap(part => part.sections).filter(one => one.card).map(one => [one.card.id, one.card.title]));

let hostInfo, origin, directory, signedIn = 0;
test.beforeAll(async () => {
  directory = await mkdtemp(pathJoin(tmpdir(), 'battle-tutorials-'));
  hostInfo = await startHost('at-activation', ['--accounts', 'dev', '--record', pathJoin(directory, 'sessions')]);
  origin = new URL(hostInfo.join).origin;
});
test.afterAll(async () => { await stopHost(hostInfo?.host); if (directory) await rm(directory, { recursive: true, force: true }); });

// A new account each test: the series is new to it, so the home page opens tutorial 1 by itself.
async function signIn(page) {
  const username = `Learner${Date.now().toString(36)}${++signedIn}`;
  const answer = await page.request.post(`${origin}/api/account/dev`, { data: { username } });
  expect(answer.ok()).toBe(true);
}

const decisionWritten = page => page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
async function confirm(page, selector) {
  const response = decisionWritten(page);
  await page.locator(selector).click(); expect((await response).status()).toBe(204);
}

// The person's own recorded answers, taken in order, by the question they answer.
function recordedAnswers(file) {
  const queues = new Map();
  for (const one of file.after.filter(entry => entry.slot === file.seat)) {
    const key = `${one.kind}:${one.creature ?? ''}`;
    queues.set(key, [...(queues.get(key) ?? []), one]);
  }
  const next = (kind, creature) => queues.get(`${kind}:${creature ?? ''}`)?.shift() ?? null;
  // The answer a question would take, left in place.
  next.peek = (kind, creature) => queues.get(`${kind}:${creature ?? ''}`)?.[0] ?? null;
  return next;
}

// Each answer acts on the sheet the lesson opened before its step was read, and opens nothing again.
async function answerEvolution(page, view, next) {
  const offered = view.options.evolution.creatures.filter(one => one.availableTiers.length);
  const one = offered.find(creature => creature.availableTiers.includes(next.peek('Evolution', creature.creature)?.tier));
  if (!one) { await page.locator('#planning-pass').click(); await confirm(page, '#planning-confirm'); return; }
  const recorded = next('Evolution', one.creature);
  await page.locator(`#planning-creatures [data-creature="${one.creature}"]`).click();
  await page.locator(`[data-mutation="${recorded.tier}"]`).first().click();
  await confirm(page, '#planning-confirm');
}

async function answerSpeed(page, next) {
  for (const tab of await page.locator('[data-tempo-tab]').evaluateAll(nodes => nodes.map(node => node.dataset.tempoTab))) {
    await page.locator(`[data-tempo-tab="${tab}"]`).click();
    await page.locator(`[data-speed="${next('Speed', Number(tab))?.speed ?? 'Standard'}"]`).click();
  }
  // One confirmation sends every creature's tempo, one decision each: the sheet closes once all are sent.
  await confirm(page, '#planning-confirm');
  await expect(page.locator('#planning-dialog')).toBeHidden({ timeout: 20000 });
}

// The order the page proposes is kept; it must be the recorded one, or the rest of the lesson plays another match.
async function answerTieOrder(page, next) {
  const recorded = next('TieOrder', null);
  const sent = page.waitForRequest(request => request.url().endsWith('/decision') && request.method() === 'POST');
  await page.locator('#planning-keep').click(); await confirm(page, '#planning-confirm');
  if (recorded) expect((await sent).postDataJSON().order).toEqual(recorded.order);
}

async function answerAction(page, view, next) {
  const offered = view.options.action.spells, recorded = next('Action', view.waitingCreature);
  const spell = offered.find(one => one.spell === recorded?.spell) ?? offered.find(one => !one.spell.includes('wait')) ?? offered[0];
  await page.locator(`[data-spell="${spell.spell}"]`).click();
  const legal = spell.legalTargets, kept = (recorded?.spell === spell.spell ? recorded.targets : []).filter(id => legal.candidates.includes(id));
  const targets = kept.length >= legal.minTargets ? kept : legal.candidates.slice(0, legal.minTargets);
  for (const id of targets) await page.locator(`[data-creature="${id}"]`).click();
  await confirm(page, '#combat-confirm');
}

// What the battle shows, in one in-page read. The lesson is read in a loop that runs some seventy times, and every
// separate read is a round trip to a page rendering a 1440x1000 canvas in software, recorded by the trace CI keeps:
// read one at a time, a dozen a poll, they cost 30 s of tutorial 4's 204 s, and a CI runner took it past its 230 s
// (#735). Visible as Playwright reads it: a box, and not hidden.
//   step: the coach's step line, null while the coach is hidden; done: it says the lesson is complete.
//   cards: a step's concept cards wait to be read.
//   sheet: what the person is asked shows, its sheet or the targets on the field; command: the button that opens
//   it, for a sheet that does not open by itself. press: the button that reads on (upkeep, order, result's OK).
const shows = page => page.evaluate(() => {
  const shown = id => {
    const node = document.getElementById(id), box = node?.getBoundingClientRect();
    return Boolean(box?.width && box.height) && !['hidden', 'collapse'].includes(getComputedStyle(node).visibility);
  };
  const battle = document.getElementById('battle');
  const step = shown('coach') ? document.getElementById('coach-step')?.textContent.trim() ?? '' : null;
  return {
    step, done: step === 'TUTORIAL COMPLETE',
    cards: shown('concept-dialog'), reading: battle.getAttribute('data-reading') === 'true',
    sheet: shown('planning-dialog') || shown('combat-dialog') || battle.getAttribute('data-combat') === 'targets',
    command: shown('planning-open') || shown('combat-open'),
    press: ['upkeep-close', 'order-continue', 'playback-next'].find(id => shown(id) && document.getElementById(id).getAttribute('aria-disabled') !== 'true') ?? null,
  };
});

// Reads what the battle shows until the person is asked something or the lesson is done: the upkeep, each
// result's OK, the revealed order's Continue, and the concept cards a step opens, each one read. What is asked
// shows as its sheet, which a step's concept cards hold back until they are read, or as the command that opens
// it, once that has stood a moment with no card to read.
async function readUntilAsked(page, onCards) {
  const end = Date.now() + 60000;
  let commandSince = null;
  for (;;) {
    const now = await shows(page);
    if (now.done) return;
    if (now.cards) { commandSince = null; await onCards(); continue; }
    if (!now.reading) {
      if (now.sheet) return;
      if (now.command) { commandSince ??= Date.now(); if (Date.now() - commandSince > 1500) return; }
      else commandSince = null;
    }
    if (Date.now() > end) throw new Error('Timed out before the next question of the lesson');
    if (now.press) await page.locator(`#${now.press}`).click({ timeout: 2000 }).catch(() => {});
    else await page.waitForTimeout(100);
  }
}

// The seat's view as the page last read it, from before the tutorial opens: its first question may come with the
// view the page reads as it loads.
function seatViews(page) {
  const seat = { latest: null };
  page.on('response', async response => {
    if (!/\/api\/seat\/player[12](?:\?|$)/.test(response.url())) return;
    const value = await response.json().catch(() => null);
    if (value?.board) seat.latest = value;
  });
  return seat;
}

async function playLesson(page, tutorial, seat, info) {
  const lesson = tutorial.file.tutorial, next = recordedAnswers(tutorial.file), shown = new Set(), opened = [];
  const coach = async view => {
    const step = /^STEP (\d+) OF (\d+)$/.exec((await shows(page)).step ?? '');
    if (!step || shown.has(Number(step[1]))) return;
    shown.add(Number(step[1]));
    expect(Number(step[2])).toBe(lesson.steps.length);
    for (const line of lesson.steps[Number(step[1]) - 1].says) await expect(page.locator('#coach-lines')).toContainText(line);
    await fitsViewport(page, '#coach-cards button, #practice-leave');
    // Where the sheet docks under the field, the card stands in it and leaves the sheet's first choice in view (#670).
    if (await page.evaluate(() => !matchMedia('(min-width: 760px), (orientation: landscape) and (max-height: 520px)').matches)) {
      await expect(page.locator('#coach')).toHaveClass(/in-sheet/);
      const first = { Speed: '[data-speed]', Evolution: '[data-mutation]', Action: '[data-spell]' }[view.waitingFor];
      if (first) await expect(page.locator(first).first()).toBeInViewport({ ratio: 0.5 });
    }
    await page.screenshot({ path: info.outputPath(`step-${step[1]}.png`) });
  };
  const readCards = async () => {
    while (await page.locator('#concept-dialog').isVisible()) {
      opened.push(await page.locator('#concept-title').textContent());
      await fitsViewport(page, '#concept-next');
      await page.locator('#concept-next').click();
    }
  };
  for (let turn = 0; turn < 120; turn++) {
    await readUntilAsked(page, readCards);
    if ((await shows(page)).done) break;
    // An assisted question is answered by the host, never drawn: the person answers the kinds the lesson teaches.
    await expect.poll(() => lesson.teaches.includes(seat.latest?.waitingFor), { timeout: 15000 }).toBe(true);
    const view = structuredClone(seat.latest);
    // The question's sheet open, as the person sees it, before its step is read.
    if (view.waitingFor === 'Action') await openSpells(page);
    else if (view.waitingFor === 'Speed') await teamTempos(page); else await mutationTree(page);
    await coach(view);
    if (view.waitingFor === 'Evolution') await answerEvolution(page, view, next);
    else if (view.waitingFor === 'Speed') await answerSpeed(page, next);
    else if (view.waitingFor === 'TieOrder') await answerTieOrder(page, next);
    else await answerAction(page, view, next);
    await expect.poll(() => seat.latest?.waitingAsked, { timeout: 15000 }).not.toBe(view.waitingAsked);
  }
  // Every step showed, in the lesson's order, and the cards they open were read.
  expect([...shown]).toEqual(lesson.steps.map((_, index) => index + 1));
  expect(new Set(opened)).toEqual(new Set(lesson.steps.flatMap(step => step.concepts).map(id => cardTitles.get(id))));
  await expect(page.locator('#practice-detail')).toHaveText('Tutorial complete');
  await page.screenshot({ path: info.outputPath('tutorial-complete.png') });
}

for (const tutorial of series) test(`tutorial ${tutorial.file.tutorial.order}, ${tutorial.file.tutorial.title}, is played to its end on the real engine`, async ({ page }, info) => {
  // A lesson reads the way a person reads it: every result's OK, the upkeep, the order. Four rounds of it, on a
  // software-rendered canvas.
  test.setTimeout(240000);
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await signIn(page);
  const seat = seatViews(page);
  const kept = page.waitForResponse(response => response.url().endsWith(`/api/tutorials/${tutorial.id}/done`) && response.request().method() === 'POST', { timeout: 230000 });
  if (tutorial.file.tutorial.order === 1) {
    // A new account's first plain visit opens the first tutorial by itself (O4).
    await page.goto(`${origin}/`);
    await page.waitForURL(`${origin}/battle?scenario=${tutorial.id}`, { timeout: 20000 });
  } else {
    // An account that skipped the first one: the home page it comes back to opens nothing by itself.
    const skipped = await page.request.post(`${origin}/api/tutorials/${series[0].id}/skip`, { data: {}, headers: { Origin: origin } });
    expect(skipped.status(), await skipped.text()).toBe(200);
    await page.goto(`${origin}/battle?scenario=${tutorial.id}`);
  }
  await expect(page.locator('#practice-brief')).toBeVisible({ timeout: 20000 });
  await expect(page.locator('#practice-brief-eyebrow')).toHaveText(`TUTORIAL ${tutorial.file.tutorial.order} OF ${series.length}`);
  await expect(page.locator('#practice-title')).toHaveText(tutorial.file.tutorial.title);
  await page.locator('#practice-brief-begin').click();
  await playLesson(page, tutorial, seat, info);
  expect((await kept).status()).toBe(200);
  // Left, the home page says it is done, and opens nothing by itself. A lesson that ends the match ends under its
  // result, which is closed first.
  if (await page.locator('#match-dialog').isVisible()) await page.keyboard.press('Escape');
  await page.locator('#coach-leave').click();
  await page.waitForURL(`${origin}/`);
  const card = page.locator(`#tutorial-cards [data-tutorial="${tutorial.id}"]`);
  await expect(card).toContainText('Done');
  await expect(card.locator('a')).toHaveText('Play again →');
  expect(errors).toEqual([]);
});
