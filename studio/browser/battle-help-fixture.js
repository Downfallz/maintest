import { expect } from '@playwright/test';
// Existing command/history journeys run as returning players. Default-on first-use tips are exercised
// in battle-guidance.spec.js and the real-host battle.loop.js journeys, without this preference.
export async function returningBattlePlayer({ context }) {
  await context.addInitScript(() => {
    try { localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] })); } catch { /* Storage-denial fixtures remain meaningful. */ }
  });
}

// The mutation phase opens its tree by itself (ADR 0138); a journey reaches it whether it opened, stayed open
// after a previous pick, or was closed by the player and waits behind its button.
export async function mutationTree(page) {
  const dialog = page.locator('#planning-dialog'), button = page.locator('#planning-open');
  await expect(dialog.or(button).first()).toBeVisible({ timeout: 20000 });
  if (!await dialog.isVisible()) await button.click({ timeout: 2000 }).catch(() => {});
  await expect(dialog).toBeVisible();
}
