import { test, expect } from '@playwright/test';
import { startHost, stopHost } from './host.js';
import { collectSeatViews } from './seat-views.js';
import { keyboardActivate } from './keys.js';

// A whole turn by keyboard on the real engine, told in one live region (docs/art/ux-craft-plan.md, UX13). A
// returning player, tips muted, plays round 1 from its Upkeep to the next round's: the mutation, the team's tempos,
// the order, each cast and its targets, and every result read, each by Tab and Enter alone. Nothing is clicked and
// no control is focused by script. What each resolved action did is said in `#playback-announcement`, and no other
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
      if (!text || last.get(region) === text) continue;
      last.set(region, text);
      window.liveSaid.push({ region: region.id || region.className || region.tagName, text, sequence: Number(document.getElementById('battle')?.dataset.sequence ?? NaN) });
    }
  };
  new MutationObserver(read).observe(document.body, { subtree: true, childList: true, characterData: true });
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

    const visible = selector => page.locator(selector).isVisible();
    const open = selector => page.locator(selector).evaluate(dialog => dialog.open).catch(() => false);
    let casts = 0;
    for (let step = 0; step < 200; step++) {
      const view = await seat.latest();
      if (view?.board?.roundNumber >= 2 && await page.locator('#battle').getAttribute('data-reading') !== 'true') break;
      if (await open('#upkeep-dialog')) { await keyboardActivate(page, '#upkeep-close'); continue; }
      if (await open('#help-dialog')) { await keyboardActivate(page, '#help-close'); continue; }
      if (await visible('#order-continue')) { await keyboardActivate(page, '#order-continue'); continue; }
      if (await visible('#playback-next') && await page.locator('#playback-next').getAttribute('aria-disabled') === 'false') {
        await keyboardActivate(page, '#playback-next'); continue;
      }
      const planning = page.locator('#planning-dialog');
      if (await open('#planning-dialog')) {
        const kind = await planning.getAttribute('data-kind');
        const sending = await page.locator('#planning-confirm').textContent().then(text => text.includes('Sending'));
        if (sending) { await page.waitForTimeout(150); continue; }
        if (kind === 'Speed') {
          const unset = planning.locator('[data-speed="Standard"]:not([aria-pressed="true"])').first();
          if (await page.locator('#planning-confirm').isEnabled() && !await unset.isVisible()) await keyboardActivate(page, '#planning-confirm');
          else if (await unset.isVisible()) await keyboardActivate(page, unset);
          else await keyboardActivate(page, '#planning-confirm');
        } else if (kind === 'TieOrder') {
          if (await page.locator('#planning-confirm').isEnabled()) await keyboardActivate(page, '#planning-confirm');
          else await keyboardActivate(page, '#planning-keep');
        } else {
          const pick = planning.locator('[data-mutation]').first();
          if (await page.locator('#planning-selection').textContent().then(text => !text.includes('·'))) await keyboardActivate(page, pick);
          else await keyboardActivate(page, '#planning-confirm');
        }
        continue;
      }
      if (await visible('#planning-open')) { await keyboardActivate(page, '#planning-open'); continue; }
      if (view?.waitingFor === 'Action' && view.waitingAsked === (await seat.latest())?.waitingAsked) {
        if (!await open('#combat-dialog') && await visible('#combat-open')) { await keyboardActivate(page, '#combat-open'); continue; }
        if (await open('#combat-dialog') || await page.locator('#battle').getAttribute('data-combat') === 'targets') {
          const choice = view.options.action.spells[0];
          if (await page.locator(`#combat-dialog [data-spell="${choice.spell}"]`).isVisible()) {
            await keyboardActivate(page, `#combat-dialog [data-spell="${choice.spell}"]`); continue;
          }
          const wanted = choice.legalTargets.candidates.slice(0, choice.legalTargets.minTargets);
          const missing = [];
          for (const id of wanted) if (await page.locator(`[data-creature="${id}"]`).first().getAttribute('aria-pressed') !== 'true') missing.push(id);
          if (missing.length) { await keyboardActivate(page, page.locator(`[data-creature="${missing[0]}"]`).first()); continue; }
          if (await visible('#combat-confirm') && await page.locator('#combat-confirm').isEnabled()) {
            await keyboardActivate(page, '#combat-confirm'); casts++;
            await expect.poll(async () => (await seat.latest())?.waitingAsked).not.toBe(view.waitingAsked);
            continue;
          }
        }
      }
      await page.waitForTimeout(150);
    }
    const after = await seat.latest();
    expect(after.board.roundNumber, 'round 1 was played to its end by keyboard').toBeGreaterThanOrEqual(2);
    expect(casts, 'the player cast by keyboard').toBeGreaterThan(0);
    expect(errors).toEqual([]);

    // Each resolved action of round 1 the page played was told in the one region, and in no other.
    const said = await page.evaluate(() => window.liveSaid);
    const actions = [...(await seat.actions()).entries()].filter(([, entry]) => entry.event.roundId === 1);
    expect(actions.length).toBeGreaterThan(0);
    const told = said.filter(one => one.region === 'playback-announcement');
    expect(told.length, 'the round was told in #playback-announcement').toBeGreaterThan(0);
    for (const one of told) {
      const elsewhere = said.filter(other => other.region !== 'playback-announcement' && other.text === one.text);
      expect(elsewhere, `"${one.text}" is told once`).toEqual([]);
    }
    for (const [sequence] of actions) {
      expect(told.some(one => one.sequence === sequence), `action ${sequence} was told`).toBe(true);
    }
  } finally {
    await page.close();
    await stopHost(host);
  }
});
