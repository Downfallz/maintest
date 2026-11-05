import { test, expect } from '@playwright/test';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { startHost, stopHost } from './host.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningArenaPlayer);

test('the real catalogue tree follows public purchases without acknowledging the reference', async ({ page, context }, info) => {
  test.setTimeout(90000);
  const { host, join } = await startHost('at-activation');
  const shown = [], writes = [], errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(join); await page.locator('#invitation-take').click();
    await expect(page.locator('#table, #pass').filter({ visible: true }).first()).toBeVisible({ timeout: 20000 });
    const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
    const origin = new URL(join).origin, headers = { 'X-Seat-Token': tokens.player1 };
    const read = async () => (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    const catalogue = await (await context.request.get(`${origin}/api/catalogue`, { headers })).json();
    await page.goto(`${origin}/arena`);
    page.on('request', request => {
      const url = new URL(request.url());
      if (url.searchParams.has('shown')) shown.push(url.searchParams.get('shown'));
      if (url.pathname.endsWith('/decision')) writes.push(request.postDataJSON());
    });
    await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
    await expect(page.locator('#upkeep-dialog')).toBeVisible(); await page.locator('#upkeep-close').click();
    await page.locator('#help-open').click(); await page.locator('#help-dialog [data-growth-open]').click();
    const initial = await read();
    await expect(page.locator('#growth-next')).toHaveText(`Mutation opportunity this round · Round ${initial.board.nextEvolutionRound}`);
    await expect(page.locator('#growth-tree > details')).toHaveCount(3);
    expect(shown).toEqual([]); expect(writes).toEqual([]);
    await fitsViewport(page, '#growth-close, #growth-creature');
    await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
    // Buy one offered tier through the arena; pass any remaining picks to reach the public reveal.
    const offer = initial.options.evolution.creatures.find(one => one.availableTiers.length);
    const tier = offer.availableTiers[0], pack = catalogue.packages.find(one => one.id === tier);
    await page.locator('#planning-open').click(); await page.locator(`#planning-creatures [data-creature="${offer.creature}"]`).click();
    await page.locator(`[data-mutation="${tier}"]`).click();
    const confirmed = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
    await page.locator('#planning-confirm').click(); expect((await confirmed).status()).toBe(204);
    let next;
    await expect.poll(async () => { next = await read(); return next.waitingAsked; }).not.toBe(initial.waitingAsked);
    if (next.waitingFor === 'Evolution') {
      await page.locator('#planning-open').click(); await page.locator('#planning-pass').click();
      const passed = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
      await page.locator('#planning-confirm').click(); expect((await passed).status()).toBe(204);
    }
    await expect(page.locator('#growth-receipt')).toBeVisible({ timeout: 15000 });
    const count = shown.length, written = writes.length;
    await page.locator('#growth-receipt [data-growth-open]').click(); await page.locator('#growth-creature').selectOption(String(offer.creature));
    await expect(page.locator(`#growth-tree [data-package="${tier}"] > summary`)).toContainText('Acquired');
    await page.locator('#growth-reveal-details summary').click(); await expect(page.locator('#growth-reveals')).toContainText(pack.name);
    for (const id of pack.spells) await expect(page.locator('#growth-reveals')).toContainText(catalogue.cards.find(card => card.id === id).name);
    await page.locator(`#growth-tree [data-package="${tier}"] > details:not([data-package]) > summary`).first().click();
    await fitsViewport(page, '#growth-close, #growth-creature');
    await page.screenshot({ path: info.outputPath('mutation-reference-real-host.png') });
    await page.keyboard.press('Escape');
    const revealed = await read();
    for (const creature of [revealed.board.allies.find(one => one.id === offer.creature), revealed.board.enemies[0]]) {
      await page.locator(`#actor-layer [data-creature="${creature.id}"]`).click();
      await expect(page.locator('#inspect-stats')).toHaveText(`${creature.health}/${creature.maxHealth} HP · ${creature.energy} energy · ${creature.totalDefense} defense · ${creature.currentInitiative} initiative`);
      await expect(page.locator('#inspect-context')).toHaveText(`Round ${revealed.board.roundNumber} · Current state`);
      for (const tier of creature.acquiredTiers) await expect(page.locator('#inspect-mutations')).toContainText(catalogue.packages.find(pack => pack.id === tier).name);
      await expect(page.locator('#inspect-spells details')).toHaveCount(creature.knownSpells.length);
      for (const spell of creature.knownSpells) await expect(page.locator(`#inspect-spells [data-inspect-spell="${spell}"] summary`)).toContainText(catalogue.cards.find(card => card.id === spell).name);
      const first = creature.knownSpells[0];
      if (first) {
        await page.locator(`#inspect-spells [data-inspect-spell="${first}"] summary`).click();
        await expect(page.locator(`#inspect-spells [data-inspect-spell="${first}"]`)).toContainText(catalogue.cards.find(card => card.id === first).targeting);
      }
      await fitsViewport(page, '#inspect-close');
      if (creature.id === offer.creature) await page.screenshot({ path: info.outputPath('creature-inspection-real-host.jpg'), type: 'jpeg', quality: 85 });
      await page.keyboard.press('Escape');
    }
    expect(shown.length).toBe(count); expect(writes.length).toBe(written); expect(errors).toEqual([]);
  } finally { await page.close(); await stopHost(host); }
});
