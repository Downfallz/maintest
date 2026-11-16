import { expect } from '@playwright/test';

// The player's spell choice opens by itself on their turn, docked beside the field (ADR 0149): its targets for a
// Target question, its spells otherwise. A test that needs it waits for that, and opens it from the dock only when
// it was closed (by the test, or because something else held the field when the question came). The command can
// be shown a moment before the choice opens by itself and hides it, so a click is tried, never waited on. A try
// is given five seconds: on a loaded runner the scene keeps the main thread busy, and Playwright took more than one
// second to find a shown, enabled, uncovered Choose spell clickable, every try timing out until the poll gave up.
export async function openSpells(page, timeout = 15000) {
  const battle = page.locator('#battle'), command = page.locator('#combat-open'), dialog = page.locator('#combat-dialog');
  // Shown, not merely open: a docked choice steps aside while results are read (ADR 0139).
  const shown = async () => await dialog.isVisible() || (await battle.getAttribute('data-combat')) === 'targets';
  await expect.poll(async () => {
    if (await shown()) return true;
    if (await command.isVisible()) await command.click({ timeout: 5000 }).catch(() => {});
    return shown();
  }, { timeout }).toBe(true);
}
