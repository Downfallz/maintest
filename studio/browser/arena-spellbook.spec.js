import { test, expect } from '@playwright/test';
import { returningArenaPlayer } from './arena-help-fixture.js';
import { fitsViewport } from './fit.js';
test.beforeEach(returningArenaPlayer);
const legal = { candidates: [4], minTargets: 1, maxTargets: 1 };
const ids = [...Array.from({ length: 18 }, (_, i) => `spell-${i}`), 'unknown-spell'];
const creature = id => ({ id, name: `Creature ${id}`, health: 20, maxHealth: 20, energy: 3, isAlive: true, conditions: [], knownSpells: ids });
async function mount(page, kind = 'Intent') {
  const state = { writes: [], shown: [], reads: 0, errors: [], seatStatus: 200, view: {
    waitingFor: kind, waitingAsked: 21, waitingCreature: 1, feed: [], feedNext: 0,
    board: { slot: 'Player1', roundNumber: 9, phase: 'Combat', subPhase: kind, allies: [1, 2, 3].map(creature), enemies: [4, 5, 6].map(creature),
      timeline: [{ creature: 1, owner: 'Player1', speed: 'Standard' }, { creature: 2, owner: 'Player1', speed: 'Quick' }] },
    options: { intent: { creatures: [{ creature: 1, castableSpells: ids.filter(id => !['spell-1', 'spell-2'].includes(id)), unaffordableSpells: ['spell-2'] }] },
      action: { actor: 1, spells: ids.filter(id => id !== 'spell-1').map(spell => ({ spell, legalTargets: legal })) } },
    guidance: [{ spell: 'spell-1', unavailableReason: 'Cannot use while stunned' }, { spell: 'spell-2', energyAfterCost: -4, chosenSpeed: 'Standard', standardCriticalChance: .35 }] } };
  const cards = ids.slice(0, -1).map((id, index) => ({ id, name: index === 0 ? 'Focus' : `The exceptionally long ${['bone strike', 'protective ward', 'ritual of recovery'][index % 3]} ${index}`,
    type: ['Offense', 'Defense', 'Support'][index % 3], creatureClass: ['Brute', 'Predator', 'Warped'][index % 3],
    cost: index === 0 ? 0 : index === 2 ? 7 : 2, critical: index % 2 ? '20%' : '0%', targeting: 'One creature in the host offer',
    effects: ['A detailed effect that remains readable in the full reference.'], casterEffects: ['A separate effect on the caster.'], criticalNote: 'Critical note from the served catalogue.' }));
  page.on('pageerror', error => state.errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'seat' })));
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'spellbook', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ contentType: 'text/event-stream', body: '' });
    if (url.pathname.endsWith('/decision')) { state.writes.push(request.postDataJSON()); state.view.waitingFor = null; return route.fulfill({ status: 204 }); }
    if (url.pathname === '/api/seat/player1') {
      state.reads++;
      if (url.searchParams.has('shown')) state.shown.push(Number(url.searchParams.get('shown')));
      return route.fulfill({ status: state.seatStatus, json: state.view });
    }
    return route.fulfill({ status: 500 });
  });
  await page.goto('/table/arena.html'); await expect(page.locator('#arena')).toHaveAttribute('data-ready', 'true');
  await page.locator('#seat-choices button').click(); await page.locator('#combat-open').click(); return state;
}
const spell = (page, id) => page.locator(`[data-spell="${id}"]`);
test('a late multiclass spellbook groups served types and exposes truthful compact stats and full details', async ({ page }, info) => {
  const h = await mount(page);
  await expect(page.locator('.combat-spell-group')).toHaveCount(4);
  await expect(page.locator('#combat-context')).toContainText('Round 9 · Combat · Precision · 3 energy');
  await expect(spell(page, 'spell-0')).toContainText('0 energy');
  await expect(spell(page, 'spell-0').locator('.spell-critical')).toBeEmpty();
  await expect(spell(page, 'spell-2')).toContainText('35% crit · current');
  await expect(spell(page, 'spell-1')).toBeDisabled(); await expect(spell(page, 'spell-1')).toContainText('Cannot use while stunned');
  await expect(spell(page, 'spell-0').locator('.spell-detail')).toBeHidden();
  await fitsViewport(page, '#combat-details-mode, #combat-declare, #combat-dialog-close');
  await page.screenshot({ path: info.outputPath('au9-spellbook.png') });
  await page.locator('#combat-details-mode').click();
  await expect(spell(page, 'spell-1').locator('.spell-detail')).toBeVisible();
  await expect(spell(page, 'spell-1').locator('.spell-detail')).toContainText('separate effect on the caster');
  await expect(spell(page, 'unknown-spell')).toContainText('Catalogue details unavailable');
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('selection and same-question polls preserve card geometry, keyboard focus, scroll and confirmation position', async ({ page }) => {
  const h = await mount(page), node = spell(page, 'spell-12');
  await node.scrollIntoViewIfNeeded(); await node.focus();
  const before = await node.boundingBox(), footer = await page.locator('#combat-declare').boundingBox();
  await node.click();
  await expect(node).toHaveAttribute('aria-pressed', 'true');
  expect(await node.boundingBox()).toEqual(before); expect(await page.locator('#combat-declare').boundingBox()).toEqual(footer);
  const scroll = await page.locator('#combat-browser').evaluate(node => node.scrollTop), reads = h.reads;
  h.view.board.allies[0].energy = 4;
  await expect.poll(() => h.reads).toBeGreaterThan(reads);
  await expect(page.locator('#combat-context')).toContainText('4 energy');
  await expect(node).toBeFocused(); expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(scroll);
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('an offered energy plan and repeated taps never declare without the explicit footer', async ({ page }) => {
  const h = await mount(page); await spell(page, 'spell-2').dblclick();
  await expect(spell(page, 'spell-2')).toContainText('4 energy short · may fizzle');
  await expect(page.locator('#combat-declare')).toContainText('may fizzle'); expect(h.writes).toEqual([]);
  await page.locator('#combat-declare').click();
  await expect.poll(() => h.writes).toEqual([{ kind: 'Intent', creature: 1, spell: 'spell-2', asked: 21 }]); expect(h.errors).toEqual([]);
});
test('a changed creature clears reference position, details mode and the previous draft', async ({ page }) => {
  const h = await mount(page); await page.locator('#combat-details-mode').click(); await spell(page, 'spell-15').click();
  h.view.waitingAsked++; h.view.waitingCreature = 2; h.view.options.intent.creatures[0].creature = 2;
  await expect(page.locator('#combat-dialog')).toBeHidden({ timeout: 10000 }); await page.locator('#combat-open').click();
  await expect(page.locator('#combat-heading')).toContainText('Creature 2');
  await expect(page.locator('#combat-details-mode')).toHaveAttribute('aria-pressed', 'false');
  expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(0);
  await expect(page.locator('#combat-declare')).toBeDisabled(); expect(h.writes).toEqual([]);
});
test('unknown catalogue spells remain selectable and returning from targets preserves browsing', async ({ page }) => {
  const h = await mount(page, 'Action'); await page.locator('#combat-details-mode').click();
  const node = spell(page, 'unknown-spell'); await node.scrollIntoViewIfNeeded();
  const scroll = await page.locator('#combat-browser').evaluate(node => node.scrollTop);
  await node.dblclick(); await expect(page.locator('#combat-targets')).toBeVisible(); expect(h.writes).toEqual([]);
  await page.locator('#combat-back').click(); await expect(page.locator('#combat-details-mode')).toHaveAttribute('aria-pressed', 'true');
  expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(scroll);
  expect(h.errors).toEqual([]);
});
test('changed offers update availability without resetting deliberate browsing, and access loss erases it', async ({ page }) => {
  const h = await mount(page), node = spell(page, 'spell-12'); await node.scrollIntoViewIfNeeded(); await node.focus();
  const scroll = await page.locator('#combat-browser').evaluate(node => node.scrollTop);
  h.view.options.intent.creatures[0].castableSpells = h.view.options.intent.creatures[0].castableSpells.filter(id => id !== 'spell-0');
  await expect(spell(page, 'spell-0')).toBeDisabled({ timeout: 10000 }); await expect(node).toBeFocused();
  expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(scroll);
  h.seatStatus = 403; await expect(page.locator('#combat-dialog')).toBeHidden({ timeout: 10000 });
  await expect(page.locator('#combat-context')).toBeEmpty(); await expect(page.locator('#combat-spells')).toBeEmpty(); expect(h.writes).toEqual([]);
});
test('short landscape, small portrait and enlarged text keep the browser scrollable and footer reachable', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Own viewport matrix');
  await mount(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport);
    await fitsViewport(page, '#combat-declare, #combat-dialog-close, #combat-details-mode');
    expect(await page.locator('#combat-browser').evaluate(node => node.clientHeight)).toBeGreaterThan(60);
    await page.locator('#combat-details-mode').click();
    await fitsViewport(page, '#combat-declare, #combat-dialog-close, #combat-details-mode');
  }
  await page.addStyleTag({ content: '#combat-dialog { font-size: 200%; } #combat-dialog * { font-size: inherit !important; }' });
  await fitsViewport(page, '#combat-declare, #combat-dialog-close');
});

test('keyboard paging keeps unavailable spell references reachable without selecting or sending', async ({ page }) => {
  const h = await mount(page);
  h.view.options.intent.creatures[0].castableSpells = [];
  h.view.options.intent.creatures[0].unaffordableSpells = [];
  await expect(spell(page, 'spell-0')).toBeDisabled({ timeout: 10000 });
  await page.locator('#combat-browser').focus(); await page.keyboard.press('Home');
  await expect.poll(() => page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(0);
  await page.keyboard.press('PageDown');
  await expect.poll(() => page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBeGreaterThan(0);
  await expect(page.locator('#combat-declare')).toBeDisabled(); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
