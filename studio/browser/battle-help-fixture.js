import { expect } from '@playwright/test';
// Existing command/history journeys run as returning players who chose Auto and muted the phase tips in their
// game settings (ADR 0140). A new player's defaults (Step, 1×, tips on) are exercised in battle-guidance.spec.js,
// battle-pacing.spec.js and the real-host battle.loop.js journeys, without these settings.
export async function returningBattlePlayer({ context }) {
  await context.addInitScript(() => {
    try {
      localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] }));
      // History opens on its summary; the journeys that read the full record start there.
      localStorage.setItem('downfall.battle.history-view.v1', 'events');
      localStorage.setItem('downfall.arena.game-settings.v1', JSON.stringify({ pace: 'auto', speed: 1, help: false, reducedMotion: false }));
    } catch { /* Storage-denial fixtures remain meaningful. */ }
  });
}

// The mutation phase opens its tree by itself (ADR 0138), and the team's tempos their sheet (ADR 0150); a journey
// reaches either whether it opened, stayed open after a previous pick, or was closed and waits behind its button.
export async function mutationTree(page) { await planningSheet(page); }
export async function teamTempos(page) { await planningSheet(page); await expect(page.locator('#planning-dialog')).toHaveAttribute('data-kind', 'Speed'); }
async function planningSheet(page) {
  const dialog = page.locator('#planning-dialog'), button = page.locator('#planning-open');
  await acknowledge(page, async () => await dialog.isVisible() || await button.isVisible());
  // Tried until the sheet opens, as the match result is: one click with its failure swallowed, then one 5 s wait,
  // failed under load without saying whether the click was refused or the sheet was slow (#859).
  await openBehind(page, dialog, button, 'The planning sheet');
}

// The battle holds every confirmed action for the player's OK (ADR 0139). A journey reads them as a player
// would: it presses OK whenever one is waiting, until the condition it is after holds.
// A revealed turn order holds the field until Continue (ADR 0144): a journey reads it too, through `onOrder` when
// it checks the order, then continues.
// The deadline restarts at each OK or Continue that goes through, instead of running from the call. A loaded machine
// reads a long round slowly but steadily, and a whole round's results outlasted one deadline set at the call (#832).
// A page with nothing to press for `timeout` is stuck, and the error says what it showed. A press the page ignores
// still counts, so the whole call stops at three times `timeout` all the same.
export async function acknowledge(page, until, timeout = 45000, onOrder = null) {
  const next = page.locator('#playback-next'), order = page.locator('#order-continue'), cap = Date.now() + 3 * timeout;
  let end = Date.now() + timeout, pressed = 0;
  const moved = () => { pressed++; end = Math.min(cap, Date.now() + timeout); };
  for (;;) {
    if (await until()) return;
    if (Date.now() > end) {
      const shown = await page.evaluate(() => ({ reading: document.querySelector('#battle')?.getAttribute('data-reading'),
        next: document.querySelector('#playback-next')?.getAttribute('aria-disabled'), title: document.querySelector('#reading-title')?.textContent }))
        .catch(() => null);
      throw new Error(`Timed out reading confirmed results: nothing to press for ${timeout / 1000} s after ${pressed} presses `
        + `(or ${3 * timeout / 1000} s in all). The page showed ${shown ? JSON.stringify(shown) : 'nothing it could say'}.`);
    }
    if (await order.isVisible()) {
      // A journey that reads the order presses Continue itself: that press is progress too.
      if (onOrder) { await onOrder(); if (!await order.isVisible()) moved(); }
      if (await order.isVisible()) await order.click({ timeout: 2000 }).then(moved, () => {});
    } else if (await next.isVisible() && await next.getAttribute('aria-disabled') === 'false') {
      // What is now waiting may be the very thing the journey stops at: look again before pressing.
      if (await until()) return;
      await next.click({ timeout: 2000 }).then(moved, () => {});
    }
    else await page.waitForTimeout(100);
  }
}
export const readResults = (page, onOrder = null) => acknowledge(page, async () => await page.locator('#battle').getAttribute('data-reading') !== 'true', 45000, onOrder);

// The match result opens by itself once a finished match's last result is read; a journey reaches it whether it
// already opened or still waits behind its button.
export async function matchResult(page) {
  await openBehind(page, page.locator('#match-dialog'), page.locator('#match-open'), 'The match result');
}

// A dialog that opens by itself, or waits behind its button. A loaded machine can take longer than one short click to
// reach the button: the click is tried until the dialog is open, instead of once with its failure swallowed (#841).
// When it still fails, the page says why: each refused click's error is kept, with the frames the page draws in
// the second after (a page that draws two cannot answer a click's stability check, which waits for frames, #842).
// Best effort: a test timeout that falls before this budget ends reports itself first.
async function openBehind(page, dialog, button, name) {
  const refused = [];
  try {
    await expect(async () => {
      if (!await dialog.isVisible()) await button.click({ timeout: 2000 }).catch(error => { refused.push(String(error.message).split('\n').slice(0, 4).join(' | ')); throw error; });
      await expect(dialog).toBeVisible({ timeout: 2000 });
    }).toPass({ timeout: 20000 });
  } catch (error) {
    const frames = await page.evaluate(() => new Promise(done => { let n = 0; const end = performance.now() + 1000;
      setTimeout(() => done(n), 1500);
      const tick = () => { n++; if (performance.now() < end) requestAnimationFrame(tick); else done(n); }; requestAnimationFrame(tick); })).catch(() => 'unknown');
    const clicks = refused.length ? `Each refused click said:\n${refused.join('\n')}` : 'No click was refused: the dialog was slow to show.';
    throw new Error(`${name} did not open (${frames} frames drawn in the second after). ${clicks}`, { cause: error });
  }
}

// What a result journey reads is the result and its controls, not the canvas. Phaser draws it sixty times a second
// all the same, and on a runner that renders in software the page's compositing of it starves the page until a
// click's stability check (two frames) cannot finish: #match-open was visible and in place, and five 2 s clicks were
// refused (#819). The canvas is left undisplayed (Phaser still renders into it; the page no longer composites it).
// Only a journey that does not read the field may use it.
export async function withoutCanvas({ context }) {
  await context.addInitScript(() => {
    addEventListener('DOMContentLoaded', () => {
      const style = document.createElement('style');
      style.textContent = '#phaser-mount canvas { display: none !important }';
      document.head.append(style);
    });
  });
}
