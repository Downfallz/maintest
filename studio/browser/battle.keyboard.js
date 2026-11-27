import { test, expect } from '@playwright/test';
import { startHost, stopHost } from './host.js';
import { collectSeatViews } from './seat-views.js';
import { keyboardActivate } from './keys.js';

// A whole turn by keyboard on the real engine, told in one live region (docs/art/ux-craft-plan.md, UX13). A
// returning player, tips muted, plays round 1 from its Round start to the next round's: the mutation, the team's tempos,
// the order, each cast and its targets, and every result read, each by Tab and Enter alone. Past the invitation's door,
// where the seat is taken, nothing is clicked and no control is focused by script. What each resolved action did is said in `#playback-announcement`, and no other
// live region says it again.
test.beforeEach(async ({ context }) => {
  await context.addInitScript(() => {
    try {
      localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] }));
      localStorage.setItem('downfall.arena.game-settings.v1', JSON.stringify({ pace: 'auto', speed: 1, help: false, reducedMotion: false }));
    } catch { /* The journey needs storage. */ }
  });
});

// Every live region of the page as served, and every change to its words, in order.
function listen() {
  window.liveSaid = [];
  const regions = () => [...document.querySelectorAll('[aria-live], [role="status"], [role="log"], [role="alert"], output')];
  const last = new Map();
  const read = () => {
    for (const region of regions()) {
      const text = region.textContent.replace(/\s+/g, ' ').trim();
      // A region tells the action it names (#1267). If it names none, it tells the action being played. A result
      // settled without being played, such as the player's own cast arriving under a sheet, is told while no action plays.
      const sequence = region.dataset.sequence ?? document.getElementById('battle')?.dataset.sequence ?? '';
      // A region says the same words again for another action: the action is what is new, not the words.
      const said = `${text}\u0000${sequence}`;
      if (!text || last.get(region) === said) continue;
      last.set(region, said);
      window.liveSaid.push({ region: region.id || region.className || region.tagName, text, sequence: sequence === '' ? Number.NaN : Number(sequence) });
    }
  };
  new MutationObserver(read).observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ['data-sequence'] });
}

const visible = (page, selector) => page.locator(selector).isVisible();
const open = (page, selector) => page.locator(selector).evaluate(dialog => dialog.open).catch(() => false);

// The windows that stand between two presses of the round: Round start, a tip, the order of play, the next result.
async function windowStep(page) {
  if (await open(page, '#upkeep-dialog')) { await keyboardActivate(page, '#upkeep-close'); return true; }
  if (await open(page, '#help-dialog')) { await keyboardActivate(page, '#help-close'); return true; }
  if (await visible(page, '#order-continue')) { await keyboardActivate(page, '#order-continue'); return true; }
  if (await visible(page, '#playback-next') && await page.locator('#playback-next').getAttribute('aria-disabled') === 'false') {
    await keyboardActivate(page, '#playback-next'); return true;
  }
  return false;
}

// The plan: the mutation, the ties and the tempos, one press each, then the open plan's button.
async function planningStep(page) {
  const planning = page.locator('#planning-dialog');
  if (!await open(page, '#planning-dialog')) {
    if (!await visible(page, '#planning-open')) return false;
    await keyboardActivate(page, '#planning-open'); return true;
  }
  const confirm = page.locator('#planning-confirm');
  if ((await confirm.textContent()).includes('Sending')) { await page.waitForTimeout(150); return true; }
  const kind = await planning.getAttribute('data-kind');
  if (kind === 'Speed') {
    const unset = planning.locator('[data-speed="Standard"]:not([aria-pressed="true"])').first();
    await keyboardActivate(page, await unset.isVisible() ? unset : '#planning-confirm');
  } else if (kind === 'TieOrder') {
    await keyboardActivate(page, await confirm.isEnabled() ? '#planning-confirm' : '#planning-keep');
  } else {
    const picked = (await page.locator('#planning-selection').textContent()).includes('·');
    await keyboardActivate(page, picked ? '#planning-confirm' : planning.locator('[data-mutation]').first());
  }
  return true;
}

// A cast: the spell, each of its least targets, then the confirmation. 'cast' once it is sent, true for a press,
// false when the round asks nothing of the seat now.
async function combatStep(page, seat, view) {
  if (view?.waitingFor !== 'Action' || view.waitingAsked !== (await seat.latest())?.waitingAsked) return false;
  const dialog = await open(page, '#combat-dialog');
  if (!dialog && await visible(page, '#combat-open')) { await keyboardActivate(page, '#combat-open'); return true; }
  if (!dialog && await page.locator('#battle').getAttribute('data-combat') !== 'targets') return false;
  const choice = view.options.action.spells[0];
  const spell = `#combat-dialog [data-spell="${choice.spell}"]`;
  if (await page.locator(spell).isVisible()) { await keyboardActivate(page, spell); return true; }
  for (const id of choice.legalTargets.candidates.slice(0, choice.legalTargets.minTargets)) {
    const target = page.locator(`[data-creature="${id}"]`).first();
    if (await target.getAttribute('aria-pressed') !== 'true') { await keyboardActivate(page, target); return true; }
  }
  if (!await visible(page, '#combat-confirm') || !await page.locator('#combat-confirm').isEnabled()) return false;
  await keyboardActivate(page, '#combat-confirm');
  await expect.poll(async () => (await seat.latest())?.waitingAsked).not.toBe(view.waitingAsked);
  return 'cast';
}

test('a whole round is played by keyboard and each action is told in one live region', async ({ page, context }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The keyboard plays the phone: one project walks it.');
  test.setTimeout(240000);
  const { host, join } = await startHost('at-activation');
  try {
    // Taking the seat is the door's matter, not the turn's.
    await page.goto(join); await page.locator('#invitation-take').click();
    await page.waitForURL(/\/battle(?:\?|$)/, { timeout: 20000 });
    await page.close(); page = await context.newPage();
    const seat = await collectSeatViews(page);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(new URL('/battle', join).href);
    await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
    await page.evaluate(listen);

    let casts = 0;
    // Until round 2 is reached and read: the test's own timeout bounds it, never a count of steps, which a slow
    // shard spends on waits while the round is still being played (studio/AGENTS.md). Each step is one press, in
    // the order a player meets them; the awaits run one after another because each press changes what comes next.
    for (;;) {
      const view = await seat.latest();
      if (view?.board?.roundNumber >= 2 && await page.locator('#battle').getAttribute('data-reading') !== 'true') break;
      if (await windowStep(page) || await planningStep(page)) continue;
      const cast = await combatStep(page, seat, view);
      if (cast === 'cast') casts++;
      if (!cast) await page.waitForTimeout(150);
    }
    const after = await seat.latest();
    expect(after.board.roundNumber, 'round 1 was played to its end by keyboard').toBeGreaterThanOrEqual(2);
    expect(casts, 'the player cast by keyboard').toBeGreaterThan(0);
    expect(errors).toEqual([]);

    // Each resolved action of round 1 the page played was told in the one region, and in no other. Round 2 can
    // arrive before the last result's words are written, on a slow shard: wait for what the region says.
    const actions = [...(await seat.actions()).entries()].filter(([, entry]) => entry.event.roundId === 1);
    expect(actions.length).toBeGreaterThan(0);
    const untold = async () => {
      const sequences = new Set((await page.evaluate(() => window.liveSaid))
        .filter(one => one.region === 'playback-announcement').map(one => one.sequence));
      return actions.map(([sequence]) => sequence).filter(sequence => !sequences.has(sequence));
    };
    try {
      await expect.poll(untold, { timeout: 20000 }).toEqual([]);
    } catch (error_) {
      // What was said last and what was not told, so a red run on a runner names the action without its trace.
      const said = (await page.evaluate(() => window.liveSaid)).filter(one => one.region === 'playback-announcement').slice(-6);
      const missing = new Set(await untold());
      const events = actions.filter(([sequence]) => missing.has(sequence)).map(([sequence, entry]) => ({ sequence, event: entry.event }));
      throw new Error(`every action of round 1 was told: untold ${JSON.stringify(events)}; last told ${JSON.stringify(said)}`, { cause: error_ });
    }
    const said = await page.evaluate(() => window.liveSaid);
    const told = said.filter(one => one.region === 'playback-announcement');
    expect(told.length, 'the round was told in #playback-announcement').toBeGreaterThan(0);
    for (const one of told) {
      const elsewhere = said.filter(other => other.region !== 'playback-announcement' && other.text.includes(one.text));
      expect(elsewhere, `"${one.text}" is told once`).toEqual([]);
    }
    for (const [sequence, entry] of actions) {
      expect(told.some(one => one.sequence === sequence), `action ${sequence} was told`).toBe(true);
      // The words said under an action's sequence are that action's: they name its caster, as its result does.
      const caster = `Creature ${entry.event.resolution.action.actor} `;
      expect(told.filter(one => one.sequence === sequence).every(one => one.text.startsWith(caster)), `action ${sequence} was told as its own`).toBe(true);
    }
  } finally {
    await page.close();
    await stopHost(host);
  }
});
