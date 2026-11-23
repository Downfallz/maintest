// How a walk plays (docs/art/ux-craft-plan.md, UX2): a lesson with the person's own recorded answers, as
// studio/browser/battle.tutorials.js plays it, and a game's questions answered with the first choice the host
// offers. The host decides what is legal; this reads its seat view and presses what the page shows.

import { readdirSync, readFileSync } from 'node:fs';
import { waitAnswered } from '../../studio/browser/battle-step.js';

const folder = new URL('../../scenarios/tutorials/', import.meta.url);
// The tutorial that plays to the match's end: it ends on the match result, the others on the coach's card.
const ENDS_THE_MATCH = 'to-the-end.json';

/** The series, in its order. */
export const lessons = readdirSync(folder).filter(name => name.endsWith('.json'))
  .map(name => ({ name, id: `sc-repo-tutorial-${name.slice(0, -'.json'.length)}`, file: JSON.parse(readFileSync(new URL(name, folder), 'utf8')) }))
  .map(lesson => ({ ...lesson, order: lesson.file.tutorial.order, endsTheMatch: lesson.name === ENDS_THE_MATCH }))
  .sort((one, other) => one.order - other.order);

/** The seat's view as the page last read it (studio/browser/seat-views.js, installed by the walk). */
export const latest = page => page.evaluate(() => globalThis.seatViews?.latest ?? null);

export async function confirm(page, selector) {
  const written = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
  await page.locator(selector).click();
  const status = (await written).status();
  if (status !== 204) throw new Error(`The host answered ${status} to the decision behind ${selector}.`);
}

// What the battle shows, in one read of the page.
const shows = page => page.evaluate(() => {
  const shown = id => {
    const node = document.getElementById(id), box = node?.getBoundingClientRect();
    return Boolean(box?.width && box.height) && !['hidden', 'collapse'].includes(getComputedStyle(node).visibility);
  };
  const battle = document.getElementById('battle');
  const step = shown('coach') ? document.getElementById('coach-step')?.textContent.trim() ?? '' : null;
  return {
    step, done: /^TUTORIAL .* COMPLETE$/.test(step), over: shown('match-dialog') || shown('match-open'),
    // A closed card fades out still displayed: open is what says it is there (table/AGENTS.md).
    cards: document.getElementById('concept-dialog').open, tip: shown('help-dialog'), reading: battle.dataset.reading === 'true',
    sheet: shown('planning-dialog') || shown('combat-dialog') || battle.dataset.combat === 'targets',
    command: shown('planning-open') || shown('combat-open'),
    press: ['upkeep-close', 'order-continue', 'playback-next'].find(id => shown(id) && document.getElementById(id).getAttribute('aria-disabled') !== 'true') ?? null,
  };
});

/**
 * Read what the battle shows until the person is asked something, the lesson is done or the match is over: the
 * upkeep, each result's OK, the revealed order's Continue, the phase tips and the concept cards. `look` is
 * called before anything is pressed, so a walk can picture what is about to be dismissed.
 */
export async function readUntilAsked(page, { look = async () => {} } = {}) {
  const end = Date.now() + 90000;
  let commandSince = null;
  for (;;) {
    const now = await shows(page);
    if (now.done || now.over) return now;
    await look(now);
    if (now.cards) { commandSince = null; await page.locator('#concept-next').click({ timeout: 2000 }).catch(() => {}); continue; }
    if (now.tip) { commandSince = null; await page.locator('#help-close').click({ timeout: 2000 }).catch(() => {}); continue; }
    if (!now.reading) {
      if (now.sheet) return now;
      if (now.command) { commandSince ??= Date.now(); if (Date.now() - commandSince > 1500) return now; } else commandSince = null;
    }
    if (Date.now() > end) throw new Error('Nothing was asked for 90 s.');
    if (now.press) await page.locator(`#${now.press}`).click({ timeout: 2000 }).catch(() => {});
    else await page.waitForTimeout(100);
  }
}

// The sheet of a question, open as the person sees it: it opens by itself, or waits behind its command.
async function openSheet(page, dialog, command, shown = () => page.locator(dialog).isVisible()) {
  const end = Date.now() + 20000;
  while (!await shown()) {
    if (Date.now() > end) throw new Error(`${dialog} did not open.`);
    if (await page.locator(command).isVisible()) await page.locator(command).click({ timeout: 2000 }).catch(() => {});
    else await page.waitForTimeout(100);
  }
}
export const openPlanning = page => openSheet(page, '#planning-dialog', '#planning-open');
const openSpells = page => openSheet(page, '#combat-dialog', '#combat-open',
  async () => await page.locator('#combat-dialog').isVisible() || await page.locator('#battle').getAttribute('data-combat') === 'targets');

// A choice pressed as a player presses it. Where the page leaves it out of reach (covered, or off the sheet), the
// walk says so through `blocked` and makes the choice by script, so the screens after it are still filmed.
export async function choose(page, selector, blocked) {
  const choice = page.locator(selector).first();
  try { await choice.click({ timeout: 5000 }); } catch (error) {
    if (!/Timeout/.test(error.message)) throw error;
    await blocked(selector);
    await choice.dispatchEvent('click');
  }
}

// The person's own recorded answers, taken in order, by the question they answer.
function recordedAnswers(file) {
  const queues = new Map();
  for (const one of file.after.filter(entry => entry.slot === file.seat)) {
    const key = `${one.kind}:${one.creature ?? ''}`;
    queues.set(key, [...(queues.get(key) ?? []), one]);
  }
  const next = (kind, creature) => queues.get(`${kind}:${creature ?? ''}`)?.shift() ?? null;
  next.peek = (kind, creature) => queues.get(`${kind}:${creature ?? ''}`)?.[0] ?? null;
  return next;
}
const nothingRecorded = Object.assign(() => null, { peek: () => null });

async function answerEvolution(page, view, next, blocked) {
  const offered = view.options.evolution.creatures.filter(one => one.availableTiers.length);
  // A lesson takes the recorded mutation; a game takes the first one offered.
  const one = next === nothingRecorded ? offered[0] : offered.find(creature => creature.availableTiers.includes(next.peek('Evolution', creature.creature)?.tier));
  if (!one) { await page.locator('#planning-pass').click(); await confirm(page, '#planning-confirm'); return; }
  const tier = next('Evolution', one.creature)?.tier ?? one.availableTiers[0];
  // A short portrait folds the creature tabs away (battle.css): there the creature is chosen on the field.
  const tab = page.locator(`#planning-creatures [data-creature="${one.creature}"]`);
  await choose(page, await tab.isVisible() ? `#planning-creatures [data-creature="${one.creature}"]` : `#actor-layer [data-creature="${one.creature}"]`, blocked);
  await choose(page, `[data-mutation="${tier}"]`, blocked);
  await confirm(page, '#planning-confirm');
}

// `tempo(index)` is a walk's own choice for the team's creature at that tab, when no answer is recorded.
async function answerSpeed(page, next, blocked, tempo = () => null) {
  const tabs = await page.locator('[data-tempo-tab]').evaluateAll(nodes => nodes.map(node => node.dataset.tempoTab));
  for (const [index, tab] of tabs.entries()) {
    await choose(page, `[data-tempo-tab="${tab}"]`, blocked);
    await choose(page, `[data-speed="${next('Speed', Number(tab))?.speed ?? tempo(index) ?? 'Standard'}"]`, blocked);
  }
  // One confirmation sends every creature's tempo: the sheet closes once all are sent.
  await confirm(page, '#planning-confirm');
  await page.locator('#planning-dialog').waitFor({ state: 'hidden', timeout: 20000 });
}

async function answerAction(page, view, next, look, blocked) {
  const offered = view.options.action.spells, recorded = next('Action', view.waitingCreature);
  const spell = offered.find(one => one.spell === recorded?.spell) ?? offered.find(one => !one.spell.includes('wait')) ?? offered[0];
  await choose(page, `[data-spell="${spell.spell}"]`, blocked);
  const legal = spell.legalTargets, kept = (recorded?.spell === spell.spell ? recorded.targets : []).filter(id => legal.candidates.includes(id));
  const targets = kept.length >= legal.minTargets ? kept : legal.candidates.slice(0, legal.minTargets);
  await look();
  for (const id of targets) await choose(page, `[data-creature="${id}"]`, blocked);
  await confirm(page, '#combat-confirm');
}

/**
 * The lesson's action asked now, cast with the person's recorded answer, its spell choice open: the spell, its
 * targets, Cast. What the lessons' first-cast strip films (#978).
 */
export async function castRecorded(page, lesson, blocked = async () => {}) {
  const view = await asked(page, ['Action']);
  await answerAction(page, view, recordedAnswers(lesson.file), async () => {}, blocked);
}

/** Answer what the seat is asked, its sheet open first. `look` is called once the sheet is open, and once targets are asked. */
export async function answer(page, view, { next = nothingRecorded, look = async () => {}, blocked = async () => {}, tempo } = {}) {
  if (view.waitingFor === 'Action') await openSpells(page); else await openPlanning(page);
  await look();
  if (view.waitingFor === 'Evolution') await answerEvolution(page, view, next, blocked);
  else if (view.waitingFor === 'Speed') await answerSpeed(page, next, blocked, tempo);
  else if (view.waitingFor === 'TieOrder') { await page.locator('#planning-keep').click(); await confirm(page, '#planning-confirm'); }
  else await answerAction(page, view, next, look, blocked);
  await waitAnswered(view, () => latest(page), { timeout: 15000 });
}

// The seat's view once it asks the person something the page can answer.
export async function asked(page, kinds = null) {
  const end = Date.now() + 20000;
  for (;;) {
    const view = await latest(page);
    if (view?.waitingFor && (!kinds || kinds.includes(view.waitingFor))) return view;
    if (view?.over) return view;
    if (Date.now() > end) throw new Error(`The seat was asked nothing the walk answers (${view?.waitingFor ?? 'no view'}).`);
    await page.waitForTimeout(100);
  }
}

/**
 * Play a lesson from its first card to its end with the recorded answers. `look(now)` is called at each read
 * of the page, and `step(number)` once a step's sheet is open, before it is answered.
 */
export async function playLesson(page, lesson, { look = async () => {}, step = async () => {}, blocked = async () => {} } = {}) {
  const tutorial = lesson.file.tutorial, next = recordedAnswers(lesson.file);
  for (let turn = 0; turn < 120; turn++) {
    const now = await readUntilAsked(page, { look });
    if (now.done) return;
    // An assisted question is answered by the host: the person answers the kinds the lesson teaches.
    const view = await asked(page, tutorial.teaches);
    await answer(page, view, { next, blocked, look: async () => { const number = /^STEP (\d+)/.exec((await shows(page)).step ?? '')?.[1]; if (number) await step(Number(number)); } });
  }
  throw new Error(`Tutorial ${lesson.order} did not end in 120 questions.`);
}

/** Play a game's questions until `until(view)` holds for the seat's view at a question. */
export async function playUntil(page, until, { look = async () => {}, blocked = async () => {}, tempo } = {}) {
  for (let turn = 0; turn < 60; turn++) {
    const now = await readUntilAsked(page, { look });
    if (now.over) return;
    const view = await asked(page);
    if (view.over || until(view)) return;
    await answer(page, view, { look, blocked, tempo });
  }
  throw new Error('The game did not get there in sixty questions.');
}

/** Follow an invitation signed in, take the place, and wait for the battle to be live. */
export async function takeSeat(page, invitation) {
  await page.goto(invitation);
  await page.getByRole('button', { name: /Take the place/ }).click({ timeout: 30000 });
  await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
}
