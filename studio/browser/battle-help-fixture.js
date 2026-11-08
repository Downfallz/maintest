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
  if (!await dialog.isVisible()) await button.click({ timeout: 2000 }).catch(() => {});
  await expect(dialog).toBeVisible();
}

// The battle holds every confirmed action for the player's OK (ADR 0139). A journey reads them as a player
// would: it presses OK whenever one is waiting, until the condition it is after holds.
// A revealed turn order holds the field until Continue (ADR 0144): a journey reads it too, through `onOrder` when
// it checks the order, then continues.
export async function acknowledge(page, until, timeout = 45000, onOrder = null) {
  const next = page.locator('#playback-next'), order = page.locator('#order-continue'), end = Date.now() + timeout;
  for (;;) {
    if (await until()) return;
    if (Date.now() > end) throw new Error('Timed out reading confirmed results');
    if (await order.isVisible()) {
      if (onOrder) await onOrder();
      if (await order.isVisible()) await order.click({ timeout: 2000 }).catch(() => {});
    } else if (await next.isVisible() && await next.getAttribute('aria-disabled') === 'false') {
      // What is now waiting may be the very thing the journey stops at: look again before pressing.
      if (await until()) return;
      await next.click({ timeout: 2000 }).catch(() => {});
    }
    else await page.waitForTimeout(100);
  }
}
export const readResults = (page, onOrder = null) => acknowledge(page, async () => await page.locator('#battle').getAttribute('data-reading') !== 'true', 45000, onOrder);

// The match result opens by itself once a finished match's last result is read; a journey reaches it whether it
// already opened or still waits behind its button.
export async function matchResult(page) {
  const dialog = page.locator('#match-dialog'), button = page.locator('#match-open');
  await expect.poll(async () => await dialog.isVisible() || await button.isVisible()).toBe(true);
  if (!await dialog.isVisible()) await button.click({ timeout: 2000 }).catch(() => {});
  await expect(dialog).toBeVisible();
}
