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
  return (kind, creature) => queues.get(`${kind}:${creature ?? ''}`)?.shift() ?? null;
}

async function answerEvolution(page, view, next) {
  await mutationTree(page);
  const offered = view.options.evolution.creatures.filter(one => one.availableTiers.length);
  const pick = offered.map(one => ({ one, recorded: next('Evolution', one.creature) })).find(({ one, recorded }) => recorded && one.availableTiers.includes(recorded.tier));
  if (!pick) { await page.locator('#planning-pass').click(); await confirm(page, '#planning-confirm'); return; }
  await page.locator(`#planning-creatures [data-creature="${pick.one.creature}"]`).click();
  await page.locator(`[data-mutation="${pick.recorded.tier}"]`).first().click();
  await confirm(page, '#planning-confirm');
}

async function answerSpeed(page, next) {
  await teamTempos(page);
  for (const tab of await page.locator('[data-tempo-tab]').evaluateAll(nodes => nodes.map(node => node.dataset.tempoTab))) {
    await page.locator(`[data-tempo-tab="${tab}"]`).click();
    await page.locator(`[data-speed="${next('Speed', Number(tab))?.speed ?? 'Standard'}"]`).click();
  }
  // One confirmation sends every creature's tempo, one decision each: the sheet closes once all are sent.
  await confirm(page, '#planning-confirm');
  await expect(page.locator('#planning-dialog')).toBeHidden({ timeout: 20000 });
}

async function answerAction(page, view, next) {
  await openSpells(page);
  const offered = view.options.action.spells, recorded = next('Action', view.waitingCreature);
  const spell = offered.find(one => one.spell === recorded?.spell) ?? offered.find(one => !one.spell.includes('wait')) ?? offered[0];
  await page.locator(`[data-spell="${spell.spell}"]`).click();
  const legal = spell.legalTargets, kept = (recorded?.spell === spell.spell ? recorded.targets : []).filter(id => legal.candidates.includes(id));
  const targets = kept.length >= legal.minTargets ? kept : legal.candidates.slice(0, legal.minTargets);
  for (const id of targets) await page.locator(`[data-creature="${id}"]`).click();
  await confirm(page, '#combat-confirm');
}

const done = async page => (await page.locator('#coach-step').textContent())?.trim() === 'TUTORIAL COMPLETE' && await page.locator('#coach').isVisible();
// What the person is asked shows: its sheet, which a step's concept cards hold back until they are read, or the
// command that opens it, once it has stood a moment with no card to read (a sheet that does not open by itself).
const sheetShown = async page => {
  for (const id of ['#planning-dialog', '#combat-dialog']) if (await page.locator(id).isVisible()) return true;
  return (await page.locator('#battle').getAttribute('data-combat')) === 'targets';
};
const commandShown = async page => await page.locator('#planning-open').isVisible() || await page.locator('#combat-open').isVisible();

// Reads what the battle shows until the person is asked something or the lesson is done: the upkeep, each
// result's OK, the revealed order's Continue, and the concept cards a step opens, each one read.
async function readUntilAsked(page, onCards) {
  const end = Date.now() + 60000;
  let commandSince = null;
  for (;;) {
    if (await done(page)) return;
    if (await page.locator('#concept-dialog').isVisible()) { commandSince = null; await onCards(); continue; }
    if (await page.locator('#battle').getAttribute('data-reading') !== 'true') {
      if (await sheetShown(page)) return;
      if (await commandShown(page)) { commandSince ??= Date.now(); if (Date.now() - commandSince > 1500) return; }
      else commandSince = null;
    }
    if (Date.now() > end) throw new Error('Timed out before the next question of the lesson');
    let pressed = false;
    for (const id of ['#upkeep-close', '#order-continue', '#playback-next']) {
      const button = page.locator(id);
      if (await button.isVisible() && await button.getAttribute('aria-disabled') !== 'true') { await button.click({ timeout: 2000 }).catch(() => {}); pressed = true; break; }
    }
    if (!pressed) await page.waitForTimeout(100);
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
    if (!await page.locator('#coach').isVisible()) return;
    const step = /^STEP (\d+) OF (\d+)$/.exec((await page.locator('#coach-step').textContent())?.trim() ?? '');
    if (!step || shown.has(Number(step[1]))) return;
    shown.add(Number(step[1]));
    expect(Number(step[2])).toBe(lesson.steps.length);
    for (const line of lesson.steps[Number(step[1]) - 1].says) await expect(page.locator('#coach-lines')).toContainText(line);
    await fitsViewport(page, '#coach-cards button, #practice-leave');
    // Where the sheet docks under the field, the card stands in it and leaves the sheet's first choice in view (#670).
    const sheet = await page.locator('#planning-dialog').isVisible() || await page.locator('#combat-dialog').isVisible();
    if (sheet && await page.evaluate(() => !matchMedia('(min-width: 760px), (orientation: landscape) and (max-height: 520px)').matches)) {
      await expect(page.locator('#coach')).toHaveClass(/in-sheet/);
      const first = { Speed: '[data-speed]', Evolution: '[data-mutation]', TieOrder: '#planning-keep', Action: '[data-spell]' }[view.waitingFor];
      await expect(page.locator(first).first()).toBeInViewport({ ratio: 0.5 });
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
    if (await done(page)) break;
    // An assisted question is answered by the host, never drawn: the person answers the kinds the lesson teaches.
    await expect.poll(() => lesson.teaches.includes(seat.latest?.waitingFor), { timeout: 15000 }).toBe(true);
    const view = structuredClone(seat.latest);
    await coach(view);
    if (view.waitingFor === 'Evolution') await answerEvolution(page, view, next);
    else if (view.waitingFor === 'Speed') await answerSpeed(page, next);
    else if (view.waitingFor === 'TieOrder') { await page.locator('#planning-open').click({ timeout: 2000 }).catch(() => {}); await page.locator('#planning-keep').click(); await confirm(page, '#planning-confirm'); }
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
  // Left, the home page says it is done, and opens nothing by itself.
  await page.locator('#coach-leave').click();
  await page.waitForURL(`${origin}/`);
  const card = page.locator(`#tutorial-cards [data-tutorial="${tutorial.id}"]`);
  await expect(card).toContainText('Done');
  await expect(card.locator('a')).toHaveText('Play again →');
  expect(errors).toEqual([]);
});
