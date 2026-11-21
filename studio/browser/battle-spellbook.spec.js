import { test, expect } from '@playwright/test';
import { openSpells, servePageArt } from './battle-spells-fixture.js';
import { returningBattlePlayer } from './battle-help-fixture.js';
import { fitsViewport } from './fit.js';
import { decided } from './decision.js';
test.beforeEach(returningBattlePlayer);
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
    guidance: [{ spell: 'unknown-spell', cost: 4 }, { spell: 'spell-1', unavailableReason: 'Cannot use while stunned' }, { spell: 'spell-2', energyAfterCost: -4, chosenSpeed: 'Standard', standardCriticalChance: .35 }] } };
  const cards = ids.slice(0, -1).map((id, index) => ({ id, visual: index === 0 ? { portrait: 'focus' } : null, name: index === 0 ? 'Focus' : `The exceptionally long ${['bone strike', 'protective ward', 'ritual of recovery'][index % 3]} ${index}`,
    type: ['Offensive', 'Defensive', 'Passive'][index % 3], creatureClass: ['Brawler', 'Scoundrel', 'Sorcerer'][index % 3],
    cost: index === 0 ? 0 : index === 2 ? 7 : 2, critical: index % 2 ? '20%' : '0%', criticalThreshold: index % 2 ? 17 : null, targeting: 'One creature in the host offer',
    effects: ['A detailed effect that remains readable in the full reference.'], casterEffects: ['A separate effect on the caster.'], criticalNote: 'Critical note from the served catalogue.',
    // The host's numbers of each line (CardMark.Figures): what a face in its column shows beside the stat's icon.
    marks: index === 4 ? null : index === 5 ? [{ kind: 'Unmarked', onCaster: false, figures: '9' }, { kind: 'Unmarked', onCaster: true, figures: '9' }]
      : [{ kind: 'Damage', onCaster: false, figures: '3' }, { kind: 'EnergyGain', onCaster: true, figures: '+1' }] }));
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
  await servePageArt(page);
  await page.route('**/api/**', route => {
    const request = route.request(), url = new URL(request.url());
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'spellbook', format: '3v3' }, seats: [{ seat: 'player1', token: 'seat' }] }] } });
    // The host's cue for each kind (EffectCue): the stat a line moves, which draws its icon beside its numbers.
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards, effects: [{ kind: 'Damage', glyph: '⚔', harmful: true }, { kind: 'EnergyGain', glyph: 'ϟ', harmful: false }] } });
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
  await expect(page.locator('#loading')).toBeHidden(); await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live'); await openSpells(page); return state;
}
const spell = (page, id) => page.locator(`[data-spell="${id}"]`);
const notify = page => page.evaluate(() => window.notifyBattle());
const scrollOf = page => page.locator('#combat-browser').evaluate(node => node.scrollTop);
// Two cards a row never fit every card in view either, so the browser is scrolled for real before anything is kept.
async function browseTo(page, id) {
  const node = spell(page, id); await node.scrollIntoViewIfNeeded(); await node.focus();
  expect(await scrollOf(page)).toBeGreaterThan(0); return node;
}
const columns = page => page.locator('.combat-spell-grid').first().evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length);
test('the spellbook groups the served types two a row, each face its portrait, name, targeting, chips, availability and numbers', async ({ page }, info) => {
  const h = await mount(page);
  await expect(page.locator('.combat-spell-group')).toHaveCount(4);
  // Two spells a row at every width, the side panel included (docs/art/journeys/windows.md): no mode makes it one.
  expect(await columns(page)).toBe(2);
  await expect(page.locator('#combat-spells')).not.toHaveAttribute('data-details', /.*/);
  // The header is the creature and its chips: the round is in the field's header, choosing then targeting is what
  // the panel shows, so no context line, sentence or toggle takes the room.
  await expect(page.locator('#combat-dialog .eyebrow, #combat-context, #combat-details-mode')).toHaveCount(0);
  await expect(page.locator('#combat-help')).toHaveText('Declare a hidden intent. Its targets will be chosen when it resolves.');
  // Under the acting creature's name: its place in the order, its tempo and its energy.
  await expect(page.locator('#combat-actor .actor-chip')).toHaveText(['1 in turn order', '✦ Precision', '3 energy']);
  // The chips keep the chance and the die's face; the hint and the crit note leave the face for the chosen card.
  await expect(spell(page, 'spell-3').locator('.spell-chip-crit')).toContainText('20%');
  await expect(spell(page, 'spell-3').locator('.spell-chip-crit')).toContainText('d20 17+');
  await expect(spell(page, 'spell-3').locator('.spell-critical, .spell-card-note')).toHaveCount(0);
  await expect(spell(page, 'spell-2').locator('.spell-chip-crit')).toContainText('35%');
  await expect(spell(page, 'spell-2')).not.toContainText('at this tempo');
  // Each effect line is its icon and the host's numbers; its words are kept for a screen reader, never shown.
  await expect(spell(page, 'spell-3').locator('.spell-card-figures')).toHaveText(['3', '+1']);
  await expect(spell(page, 'spell-3').locator('.spell-card-effect > .spell-card-sr')).toHaveText(['A detailed effect that remains readable in the full reference.', 'A separate effect on the caster.']);
  // A line the host gave no numbers for keeps its words, and so does one whose kind has no stat to draw.
  for (const id of ['spell-4', 'spell-5']) {
    await expect(spell(page, id).locator('.spell-card-effect')).toHaveText(['A detailed effect that remains readable in the full reference.', 'A separate effect on the caster.']);
    await expect(spell(page, id).locator('.spell-card-figures')).toHaveCount(0);
  }
  // The button is named by what it shows, the spell's name first, then the words the face leaves.
  await expect(spell(page, 'spell-3')).toHaveAccessibleName(/^The exceptionally long bone strike 3 ?One creature in the host offer.*Available ?A detailed effect that remains readable in the full reference\. ?A separate effect on the caster\.$/);
  await expect(spell(page, 'spell-3')).not.toHaveAttribute('aria-label', /.*/);
  // A long name is written whole: it wraps, it is never cut.
  expect(await spell(page, 'spell-5').locator('.spell-card-name').evaluate(node => node.scrollWidth <= node.clientWidth + 1 && getComputedStyle(node).textOverflow !== 'ellipsis')).toBe(true);
  // The portrait is drawn at 40 px on a screen 660 px tall or more, and at 24 px under it, from its own files.
  const portrait = spell(page, 'spell-0').locator('.spell-portrait');
  await expect(portrait).toHaveAttribute('sizes', '(orientation: portrait) and (max-height: 660px) 24px, 40px');
  await expect(portrait).toHaveAttribute('loading', 'lazy');
  await expect.poll(() => portrait.evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  expect(await portrait.evaluate(image => image.getBoundingClientRect().width)).toBe(40);
  await expect(spell(page, 'unknown-spell').locator('img')).toHaveCount(0);
  await expect(spell(page, 'spell-3').locator('img')).toHaveCount(0);
  await expect(spell(page, 'spell-0')).toContainText('0 energy');
  await expect(spell(page, 'spell-0').locator('.spell-chip-crit')).toHaveCount(0);
  await expect(spell(page, 'spell-1')).toBeDisabled(); await expect(spell(page, 'spell-1')).toContainText('Cannot use while stunned');
  // Two cards of a row stand side by side, 8 px apart at the least, each 44 px at the least.
  const [left, right] = await Promise.all([spell(page, 'spell-0').boundingBox(), spell(page, 'spell-3').boundingBox()]);
  expect(Math.abs(left.y - right.y)).toBeLessThan(1); expect(right.x - (left.x + left.width)).toBeGreaterThanOrEqual(8);
  expect(Math.min(left.width, left.height)).toBeGreaterThanOrEqual(44);
  await fitsViewport(page, '#combat-declare, #combat-dialog-close');
  await page.screenshot({ path: info.outputPath('ux12-p3-spellbook.png') });
  await expect(spell(page, 'unknown-spell')).toContainText('Catalogue details unavailable');
  // A spell the catalogue does not hold keeps the cost the host's guide gives it.
  await expect(spell(page, 'unknown-spell').locator('.spell-chip-cost')).toContainText('4');
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('the chosen card is read in full above the foot before it is declared', async ({ page }) => {
  const h = await mount(page); await spell(page, 'spell-3').click();
  const preview = page.locator('#intent-preview');
  await expect(preview).toBeVisible();
  // The words the face left: each effect line, the crit note, and what the chance holds for.
  await expect(preview.locator('.spell-card-effect')).toHaveText(['A detailed effect that remains readable in the full reference.', 'A separate effect on the caster.']);
  await expect(preview.locator('.spell-card-note')).toHaveText('Critical note from the served catalogue.');
  await expect(preview).toContainText('Precision only · d20 17+ · catalogue');
  await expect(page.locator('#combat-declare')).toBeVisible(); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('selection and same-question polls preserve card geometry, keyboard focus, scroll and confirmation position', async ({ page }) => {
  const h = await mount(page), node = await browseTo(page, 'spell-17');
  const before = await node.boundingBox(), footer = await page.locator('#combat-declare').boundingBox();
  await node.click();
  await expect(node).toHaveAttribute('aria-pressed', 'true');
  expect(await node.boundingBox()).toEqual(before); expect(await page.locator('#combat-declare').boundingBox()).toEqual(footer);
  const scroll = await scrollOf(page), reads = h.reads;
  h.view.board.allies[0].energy = 4; await notify(page);
  await expect.poll(() => h.reads).toBeGreaterThan(reads);
  await expect(page.locator('#combat-actor [data-kind="energy"]')).toHaveText('4 energy');
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
test('at the slot, the field\'s estimate shows the chosen card in full above the confirmation', async ({ page }) => {
  const h = await mount(page, 'Action'); await spell(page, 'spell-3').click();
  await expect(page.locator('#combat-targets')).toBeVisible();
  const preview = page.locator('#combat-preview');
  await expect(preview.locator('.spell-card-effect')).toHaveText(['A detailed effect that remains readable in the full reference.', 'A separate effect on the caster.']);
  await expect(preview.locator('.spell-card-note')).toHaveText('Critical note from the served catalogue.');
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('a changed creature clears reference position and the previous draft', async ({ page }) => {
  const h = await mount(page); await (await browseTo(page, 'spell-17')).click();
  h.view.waitingAsked++; h.view.waitingCreature = 2; h.view.options.intent.creatures[0].creature = 2; await notify(page);
  // The old creature's choice closes and the new one's opens by itself at once (ADR 0149), fresh.
  await openSpells(page); await expect(page.locator('#combat-heading')).toContainText('Creature 2');
  expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(0);
  await expect(page.locator('#combat-declare')).toBeDisabled(); expect(h.writes).toEqual([]);
});
test('returning from targets keeps the place and the chosen card in focus, and choosing at the slot needs no sentence', async ({ page }) => {
  const h = await mount(page, 'Action'); await expect(page.locator('#combat-help')).toBeHidden();
  const node = spell(page, 'unknown-spell'); await node.scrollIntoViewIfNeeded();
  const scroll = await scrollOf(page); expect(scroll).toBeGreaterThan(0);
  await node.dblclick(); await expect(page.locator('#combat-targets')).toBeVisible(); expect(h.writes).toEqual([]);
  // A spell the catalogue does not hold has no card to read in full on the field's estimate.
  await expect(page.locator('#combat-preview .target-card')).toHaveCount(0);
  await page.locator('#combat-back').click(); await expect(spell(page, 'spell-0')).toBeVisible();
  expect(await scrollOf(page)).toBe(scroll); await expect(node).toBeFocused();
  expect(h.errors).toEqual([]);
});
test('changed offers update availability without resetting deliberate browsing, and access loss erases it', async ({ page }) => {
  const h = await mount(page), node = await browseTo(page, 'spell-17'); const scroll = await scrollOf(page);
  h.view.options.intent.creatures[0].castableSpells = h.view.options.intent.creatures[0].castableSpells.filter(id => id !== 'spell-0');
  await notify(page); await expect(spell(page, 'spell-0')).toBeDisabled(); await expect(node).toBeFocused();
  expect(await page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(scroll);
  h.seatStatus = 403; await notify(page); await expect(page.locator('#combat-dialog')).toBeHidden();
  await expect(page.locator('#combat-heading')).toBeEmpty(); await expect(page.locator('#combat-spells')).toBeEmpty(); expect(h.writes).toEqual([]);
});
test('a poll that adds an offered spell rebuilds the list without moving the focused card or the scroll', async ({ page }) => {
  const h = await mount(page), node = await browseTo(page, 'spell-17');
  const scroll = await scrollOf(page), box = await node.boundingBox();
  h.view.options.intent.creatures[0].castableSpells = [...h.view.options.intent.creatures[0].castableSpells, 'host-only-spell'];
  await notify(page); await expect(spell(page, 'host-only-spell')).toBeVisible();
  await expect(node).toBeFocused(); expect(await scrollOf(page)).toBe(scroll); expect(await node.boundingBox()).toEqual(box);
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('only the spells scroll: the panel keeps its header in place under a long swipe', async ({ page }) => {
  await mount(page);
  const heading = await page.locator('#combat-heading').boundingBox();
  await page.locator('#combat-browser').hover(); await page.mouse.wheel(0, 4000);
  await expect.poll(() => scrollOf(page)).toBeGreaterThan(0);
  expect(await page.locator('#combat-dialog').evaluate(node => node.scrollTop)).toBe(0);
  // Nothing in the spellbook reaches past the panel: a panel with room for its header has nothing of its own to
  // scroll, so a swipe that starts on its header cannot carry the header away (the cards' screen-reader text once did).
  expect(await page.locator('#combat-dialog').evaluate(node => node.scrollHeight - node.clientHeight)).toBeLessThanOrEqual(1);
  expect(await page.locator('#combat-heading').boundingBox()).toEqual(heading);
  // The creature's name opens the panel: nothing above it but the panel's own padding.
  const panel = await page.locator('#combat-dialog').boundingBox();
  expect(heading.y - panel.y).toBeLessThan(24);
});
test('short landscape, small portrait and enlarged text keep the browser scrollable and footer reachable', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Own viewport matrix');
  await mount(page);
  for (const viewport of [{ width: 320, height: 568 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(viewport);
    await fitsViewport(page, '#combat-declare, #combat-dialog-close');
    expect(await page.locator('#combat-browser').evaluate(node => node.clientHeight)).toBeGreaterThan(60);
    expect(await columns(page)).toBe(2);
  }
  await page.addStyleTag({ content: '#combat-dialog { font-size: 200%; } #combat-dialog * { font-size: inherit !important; }' });
  await fitsViewport(page, '#combat-declare, #combat-dialog-close');
  expect(await columns(page)).toBe(2);
  // A chosen hidden intent adds its estimate under the spells: they keep a readable height, the panel scrolls as a
  // fallback, and the declaration stays pinned in reach.
  await spell(page, 'spell-2').click(); await expect(page.locator('#intent-preview')).toBeVisible();
  expect(await page.locator('#combat-browser').evaluate(node => node.clientHeight)).toBeGreaterThanOrEqual(90);
  await fitsViewport(page, '#combat-declare');
  await expect(page.locator('#combat-declare')).toBeEnabled();
});
test('at 320 by 568 the portrait is 24 px in the card\'s head, before the name, sent from its own files', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Own viewport');
  // Set before the page loads: a browser keeps a larger file it already holds when the screen shrinks.
  await page.setViewportSize({ width: 320, height: 568 }); const h = await mount(page);
  expect(await columns(page)).toBe(2);
  const portrait = spell(page, 'spell-0').locator('.spell-portrait');
  await expect.poll(() => portrait.evaluate(image => image.getBoundingClientRect().width)).toBe(24);
  const [image, name] = await Promise.all([portrait.boundingBox(), spell(page, 'spell-0').locator('.spell-card-name').boundingBox()]);
  expect(image.x + image.width).toBeLessThanOrEqual(name.x); expect(Math.abs(image.y - name.y)).toBeLessThan(12);
  await expect.poll(() => portrait.evaluate(image => image.currentSrc)).toMatch(/spell-focus-24\.[0-9a-f]+\.webp$/);
  await fitsViewport(page, '#combat-declare, #combat-dialog-close');
  await page.screenshot({ path: info.outputPath('ux12-p3-spellbook-320x568.png') });
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
test('keyboard paging keeps unavailable spell references reachable without selecting or sending', async ({ page }) => {
  const h = await mount(page);
  h.view.options.intent.creatures[0].castableSpells = [];
  h.view.options.intent.creatures[0].unaffordableSpells = [];
  await notify(page); await expect(spell(page, 'spell-0')).toBeDisabled();
  await page.locator('#combat-browser').focus(); await page.keyboard.press('Home');
  await expect.poll(() => page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBe(0);
  await page.keyboard.press('PageDown');
  await expect.poll(() => page.locator('#combat-browser').evaluate(node => node.scrollTop)).toBeGreaterThan(0);
  await expect(page.locator('#combat-declare')).toBeDisabled(); expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
// UX12 P8: at Instinct a spell does not crit; its chip stays, grey, with the host's chance at Precision.
for (const tempo of ['Standard', 'Quick']) test(`at ${tempo === 'Quick' ? 'Instinct' : 'Precision'} the crit chip shows the host's chance at Precision, ${tempo === 'Quick' ? 'grey and said so' : 'in gold'}`, async ({ page }) => {
  const h = await mount(page);
  h.view.guidance = h.view.guidance.map(one => one.spell === 'spell-2' ? { ...one, chosenSpeed: tempo, quickCriticalChance: 0, standardCriticalChance: .35 } : one);
  h.view.waitingAsked++; await notify(page);
  const chip = spell(page, 'spell-2').locator('.spell-chip-crit');
  await expect(chip).toContainText('35%');
  if (tempo === 'Quick') {
    await expect(chip).toHaveClass(/is-idle/);
    await expect(chip.locator('.spell-card-sr')).toHaveText(' crit at Precision');
    await expect(spell(page, 'spell-2')).toHaveAccessibleName(/35% crit at Precision/);
    expect(await chip.evaluate(node => getComputedStyle(node).color)).toBe('rgb(174, 187, 180)');
  } else {
    await expect(chip).not.toHaveClass(/is-idle/);
    await expect(chip.locator('.spell-card-sr')).toHaveText(' crit');
    expect(await chip.evaluate(node => getComputedStyle(node).color)).toBe('rgb(248, 232, 176)');
  }
  expect(h.writes).toEqual([]); expect(h.errors).toEqual([]);
});
