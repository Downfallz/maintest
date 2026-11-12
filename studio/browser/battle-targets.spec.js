import { test, expect } from '@playwright/test';
import { openSpells } from './battle-spells-fixture.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport, fitsBattlefield } from './fit.js';
import { decided } from './decision.js';
test.beforeEach(returningBattlePlayer);
const legal = { candidates: [4, 5, 6], minTargets: 1, maxTargets: 2 };
const ids = ['spell-0', 'spell-1', 'spell-2', 'unknown-spell'];
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 3, isAlive: true, conditions: [], knownSpells: ids });
async function mount(page, kind = 'Action') {
  const state = { writes: [], shown: [], reads: 0, errors: [], seatStatus: 200, view: {
    waitingFor: kind, waitingAsked: 21, waitingCreature: 1, feed: [], feedNext: 0,
    board: { slot: 'Player1', roundNumber: 9, phase: 'Combat', subPhase: kind, allies: [1, 2, 3].map(creature), enemies: [4, 5, 6].map(creature),
      timeline: [{ creature: 1, owner: 'Player1', speed: 'Standard' }, { creature: 2, owner: 'Player1', speed: 'Quick' }] },
    options: { intent: { creatures: [{ creature: 1, castableSpells: ids.filter(id => !['spell-1', 'spell-2'].includes(id)), unaffordableSpells: ['spell-2'] }] },
      action: { actor: 1, spells: ids.filter(id => id !== 'spell-1').map(spell => ({ spell, legalTargets: legal })) } },
    guidance: [{ spell: 'spell-0', cost: 2, energyAfterCost: 1, standardCriticalChance: .25, quickCriticalChance: 0, targets: [4, 5, 6].map(target => ({ target, plain: [{ kind: 'DamageOutcome', amount: 3 }], critical: [{ kind: 'DamageOutcome', amount: 6 }] })), casterEffects: [{ kind: 'HealOutcome', amount: 2, onCaster: true }] }, { spell: 'spell-1', unavailableReason: 'Cannot use while stunned' }, { spell: 'spell-2', energyAfterCost: -4, chosenSpeed: 'Standard', standardCriticalChance: .35 }] } };
  const cards = ids.slice(0, -1).map((id, index) => ({ id, name: index === 0 ? 'Host Strike' : `The exceptionally long ${['bone strike', 'protective ward', 'ritual of recovery'][index % 3]} ${index}`,
    type: ['Offensive', 'Defensive', 'Passive'][index % 3], creatureClass: ['Brawler', 'Scoundrel', 'Sorcerer'][index % 3],
    cost: index === 0 ? 0 : index === 2 ? 7 : 2, critical: index % 2 ? '20%' : '0%', criticalThreshold: index % 2 ? 17 : null, targeting: 'One creature in the host offer',
    effects: ['A detailed effect that remains readable in the full reference.'], casterEffects: ['A separate effect on the caster.'], criticalNote: 'Critical note from the served catalogue.' }));
  page.on('pageerror', error => state.errors.push(error.message));
  // The host's change notice, so a test refreshes the seat now instead of waiting out the 5 s poll.
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' }));
    const original = window.fetch;
    window.fetch = (url, options) => String(url).endsWith('/events') ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      // A host says it is still there every twenty seconds (ADR 0119): without it the page takes a quiet stream for a
      // dropped one after 45 seconds (ADR 0164) and opens another, and a long test would notify the closed one.
      const alive = setInterval(() => { try { controller.enqueue(new TextEncoder().encode(': keep-alive\n\n')); } catch { clearInterval(alive); } }, 20000);
      window.notifyBattle = () => controller.enqueue(new TextEncoder().encode('event: changed\ndata: 1\n\n'));
    } }), { headers: { 'Content-Type': 'text/event-stream' } })) : original(url, options);
  });
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'spellbook', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ contentType: 'text/event-stream', body: 'event: ready\ndata: 0\n\n' });
    if (url.pathname.endsWith('/decision')) { state.writes.push(decided(request)); state.view.waitingFor = null; return route.fulfill({ status: 204 }); }
    if (url.pathname === '/api/seat/player1') {
      state.reads++;
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ status: state.seatStatus, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/battle.html'); await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true');
  await expect(page.locator('#loading')).toBeHidden(); await openSpells(page); return state;
}
const notify = page => page.evaluate(() => window.notifyBattle());
const target = (page, id) => page.locator(`.creature[data-creature="${id}"]`);
const preview = page => page.locator('#combat-preview');
const choose = page => page.locator('[data-spell="spell-0"]').click();
test('selected effects and cost stay beside a fixed explicit confirmation', async ({ page }, info) => {
  const h = await mount(page); await choose(page);
  // The caster carries the acting halo, without a word: the scene lights the creature whose slot it is.
  await expect(page.locator('#battle')).toHaveAttribute('data-halo', '1');
  // Every figure stands above its caption: the name hangs under the feet, not over the body.
  const lift = await page.evaluate(() => [...document.querySelectorAll('#actor-layer .creature')].map(node => node.offsetHeight - node.querySelector('.caption').offsetHeight));
  expect(lift.every(height => height > 40)).toBe(true);
  await expect(preview(page)).toContainText('2 energy → 1 left');
  await expect(preview(page)).not.toContainText('Damage 3');
  await expect(target(page, 1)).toHaveAttribute('data-target-state', 'unavailable');
  await expect(target(page, 1)).toHaveCSS('opacity', '1');
  await expect(target(page, 1).locator('.caption')).toHaveCSS('opacity', '1');
  await expect(target(page, 4)).toContainText('+ Select target');
  await expect(page.locator('#playback-pacing')).toBeHidden();
  const box = await page.locator('#combat-confirm').boundingBox();
  await target(page, 4).click();
  // The estimate is the spell card's design: the target's chips, plain and on a crit, and the caster's once.
  const estimate = preview(page).locator('.target-estimate').first();
  await expect(estimate.locator('.target-name')).toHaveText('Creature 4');
  await expect(estimate.locator('.preview-chips').first().locator('.effect-chip[data-kind="damage"]')).toContainText('Damage 3');
  await expect(estimate.locator('.target-crit .effect-chip[data-kind="damage"]')).toContainText('Damage 6');
  await expect(preview(page).locator('.spell-chip-crit')).toContainText('25%');
  await expect(preview(page).locator('.target-caster .effect-chip[data-kind="heal"]')).toContainText('Heal 2');
  await target(page, 5).click(); await expect(target(page, 6)).toContainText('Selection full');
  await expect(target(page, 6)).toBeDisabled(); await expect(target(page, 4)).toContainText('✓ Selected');
  expect(await page.locator('#combat-confirm').boundingBox()).toEqual(box);
  await fitsViewport(page, '.creature, #combat-confirm, #combat-back, #combat-cancel'); await fitsBattlefield(page);
  await page.screenshot({ path: info.outputPath('au10-target-preview.png') });
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
  await page.locator('#combat-confirm').click();
  await expect.poll(() => h.writes).toEqual([{ kind: 'Action', creature: 1, spell: 'spell-0', targets: [4, 5], asked: 21 }]);
  await expect(page.locator('#playback-pacing')).toBeHidden();
});
test('a lasting condition shows how long it lasts on its chip, a ticking one per round too', async ({ page }) => {
  const h = await mount(page);
  h.view.guidance[0].targets = [4, 5, 6].map(target => ({ target, critical: [],
    plain: [{ kind: 'ConditionOutcome', effect: { kind: 'InitiativeDebuff', amount: 3, duration: { rounds: 2 } } },
      { kind: 'ConditionOutcome', effect: { kind: 'Bleed', amountPerRound: 2, duration: { rounds: 3 } } }] }));
  await notify(page); await choose(page); await target(page, 4).click();
  const lasting = preview(page).locator('.target-estimate').first().locator('.chip-lasting');
  await expect(lasting).toHaveText(['2 rds', '/ rd · 3 rds']); await expect(lasting.first()).toBeVisible();
  await expect(preview(page).locator('.target-estimate').first()).toContainText('Initiative Debuff 3 · 2 rounds');
  expect(h.writes).toEqual([]);
});
test('a stunned legal target keeps its stun visible and spoken while targets are chosen', async ({ page }) => {
  const h = await mount(page); h.view.board.enemies[1].isStunned = true; await notify(page);
  await expect(target(page, 5)).toContainText('STUNNED'); await choose(page);
  await expect(target(page, 5).locator('.creature-tag')).toHaveText('+ Stunned');
  await expect(target(page, 5)).toHaveAttribute('aria-label', /Stunned\. Select target\. Target selection\./);
  await expect(target(page, 4).locator('.creature-tag')).toHaveText('+ Select target');
  await target(page, 5).click(); await expect(target(page, 5).locator('.creature-tag')).toHaveText('✓ Stunned');
  await expect(target(page, 5)).toHaveAttribute('data-target-state', 'selected');
  await fitsBattlefield(page); expect(h.writes).toEqual([]);
});
test('same-question changes replace cost, remove dead targets and erase missing guidance', async ({ page }) => {
  const h = await mount(page); await choose(page); await target(page, 4).click();
  h.view.guidance[0].energyAfterCost = 0; h.view.options.action.spells[0].legalTargets = { candidates: [5, 6], minTargets: 1, maxTargets: 2 };
  h.view.board.enemies[0].isAlive = false; await notify(page);
  await expect(preview(page)).toContainText('2 energy → 0 left'); await expect(page.locator('#combat-confirm')).toBeDisabled();
  await expect(preview(page)).not.toContainText('Damage 3'); await expect(target(page, 4)).toBeDisabled();
  h.view.guidance = []; await notify(page); await expect(preview(page)).toContainText('Preview unavailable for this question');
  await expect(preview(page)).not.toContainText(' left'); expect(h.writes).toEqual([]);
});
test('hidden intent displays its host cost without binding or revealing targets', async ({ page }) => {
  const h = await mount(page, 'Intent'); await choose(page);
  await expect(page.locator('#intent-preview')).toContainText('2 energy → 1 left');
  await expect(page.locator('#intent-preview')).toContainText('Targets are chosen when this hidden spell resolves');
  await expect(page.locator('#intent-preview')).not.toContainText('Damage 3');
  await fitsViewport(page, '#combat-declare, #combat-dialog-close'); expect(h.writes).toEqual([]);
  await page.locator('#combat-declare').click(); await expect.poll(() => h.writes).toEqual([{ kind: 'Intent', creature: 1, spell: 'spell-0', asked: 21 }]);
});
test('revoking access clears target estimates rather than leaving private text in the dock', async ({ page }) => {
  const h = await mount(page); await choose(page); await target(page, 4).click();
  h.seatStatus = 403; await notify(page); await expect(preview(page)).toBeHidden(); await expect(preview(page)).toBeEmpty(); expect(h.writes).toEqual([]);
});
test('short portrait, landscape and enlarged preview text preserve touch targets and confirmation', async ({ page }) => {
  await mount(page); await choose(page); await target(page, 4).click();
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport); await fitsViewport(page, '.creature, #combat-confirm, #combat-back, #combat-cancel'); await fitsBattlefield(page);
    expect(await preview(page).evaluate(node => node.clientHeight)).toBeGreaterThan(40);
  }
  await page.addStyleTag({ content: '.target-preview, .target-preview * { font-size: 24px !important; }' });
  await fitsViewport(page, '.creature, #combat-confirm, #combat-back, #combat-cancel'); await fitsBattlefield(page);
  await preview(page).focus(); await page.keyboard.press('End');
  await expect.poll(() => preview(page).evaluate(node => node.scrollTop)).toBeGreaterThan(0);
});
test('an interrupted connection stays visible beside the estimate and prevents confirmation until refreshed', async ({ page }) => {
  const h = await mount(page); await choose(page); await target(page, 4).click();
  h.seatStatus = 503; await notify(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'stale');
  await expect(page.locator('#connection')).toBeVisible();
  await expect(page.locator('#connection')).toContainText('Connection interrupted');
  await expect(page.locator('#combat-confirm')).toBeDisabled(); expect(h.writes).toEqual([]);
  h.seatStatus = 200; h.view.guidance[0].energyAfterCost = 0; await notify(page);
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  await expect(preview(page)).toContainText('2 energy → 0 left');
  await expect(page.locator('#combat-confirm')).toBeEnabled(); expect(h.writes).toEqual([]);
});
