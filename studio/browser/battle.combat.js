import { test, expect } from '@playwright/test';
import { openSpells } from './battle-spells-fixture.js';
import { returningBattlePlayer, readResults } from './battle-help-fixture.js';
import { startHost, stopHost } from './host.js';
import { decided } from './decision.js';
import { collectSeatViews } from './seat-views.js';
test.beforeEach(returningBattlePlayer);

for (const declaration of ['at-activation', 'hidden-intent']) test(`${declaration}: battle combat commands resolve through the real engine`, async ({ page, context }, info) => {
  test.setTimeout(90000);
  const { host, join } = await startHost(declaration);
  try {
    await page.goto(join); await page.locator('#invitation-take').click();
    await page.waitForURL(/\/battle(?:\?|$)/, { timeout: 20000 }); // The accepted seat goes on to the battle view (ADR 0157).
    const tokens = await page.evaluate(() => JSON.parse(localStorage.getItem('downfall.table.seats')));
    const origin = new URL(join).origin, headers = { 'X-Seat-Token': tokens.player1 };
    const read = async () => (await context.request.get(`${origin}/api/seat/player1`, { headers })).json();
    async function nextQuestion() {
      let view;
      await expect.poll(async () => { view = await read(); return view?.waitingFor ?? (view?.over ? 'over' : null); }, { timeout: 15000 }).not.toBeNull();
      if (!view) throw new Error('The host did not supply a question.');
      return view;
    }
    // The driver answers only planning questions; every combat decision comes from the visible battle.
    const planning = async view => {
      const data = view.waitingFor === 'Evolution' ? { kind: 'Evolution', pass: true }
        : view.waitingFor === 'Speed' ? { kind: 'Speed', creature: view.waitingCreature, speed: 'Standard' }
          : view.waitingFor === 'TieOrder' ? { kind: 'TieOrder', order: view.options.tieOrder.ties.flat() } : null;
      if (!data) return false;
      expect((await context.request.post(`${origin}/api/seat/player1/decision`, { headers, data: { ...data, asked: view.waitingAsked } })).status()).toBe(204);
      return true;
    };
    const sent = [], shown = [];
    // A new tab, as in battle.growth.js (#776): the tab that took the seat may already have shown this round's Upkeep
    // sheet, which a tab shows once (sessionStorage); a new tab shares the seat (localStorage) and not that mark.
    await page.close(); page = await context.newPage();
    // The host frames are collected where the page reads them, in the page, before the page itself gets the body:
    // a frame the page shows is then always held (#903, #908). Collected from Playwright's response events, a frame
    // could still be parsing when the spec read what the page showed, and the reads before the listeners were
    // attached (the first one, which may replay what the host kept as seen, ADR 0194) were never collected.
    await page.addInitScript(() => {
      const fetch = window.fetch.bind(window); window.hostFrames = {};
      window.fetch = async (...args) => {
        const response = await fetch(...args);
        if (response.ok && new URL(response.url, location.href).pathname.endsWith('/api/seat/player1')) {
          const body = await response.clone().json().catch(() => null);
          for (const entry of body?.feed ?? []) if (entry.event?.frame) window.hostFrames[entry.sequence] = entry.event.frame;
        }
        return response;
      };
    });
    page.on('request', request => {
      const url = new URL(request.url());
      if (url.searchParams.has('shown')) shown.push(Number(url.searchParams.get('shown')));
      if (url.pathname.endsWith('/decision')) sent.push({ command: decided(request), seen: [...shown] });
    });
    // The seat's latest view is collected in the page too, never from a response handler (#921).
    const seat = await collectSeatViews(page);
    await page.goto(`${origin}/battle`);
    await expect(page.locator('#battle')).toHaveAttribute('data-ready', 'true'); await expect(page.locator('#loading')).toBeHidden();
    await expect(page.locator('#upkeep-dialog')).toBeVisible();
    // The listeners hold the first load's reads too: the Upkeep sheet holds the field, so none of them says shown.
    expect(shown).toEqual([]); expect(sent).toEqual([]);
    await page.locator('#upkeep-close').click();
    await page.evaluate(() => {
      window.battleFrames = [];
      new MutationObserver(() => {
        const battle = document.getElementById('battle');
        if (!['windup', 'impact'].includes(battle.dataset.playback)) return;
        window.battleFrames.push({ sequence: Number(battle.dataset.sequence), stage: battle.dataset.playback,
          creatures: [...document.querySelectorAll('.creature')].map(node => ({ id: Number(node.dataset.creature), text: node.querySelector('.creature-stats').textContent })) });
      }).observe(document.getElementById('battle'), { attributes: true, attributeFilter: ['data-playback', 'data-sequence'] });
    });
    let casts = 0, intents = 0, captured = false; const orderRounds = new Set();
    for (let step = 0; step < 40 && casts < 3; step++) {
      const view = await nextQuestion(); expect(view.over).toBe(false);
      if (await planning(view)) continue;
      expect(['Action', 'Intent', 'Target']).toContain(view.waitingFor);
      await expect.poll(async () => (await seat.latest())?.waitingAsked, { timeout: 10000 }).toBe(view.waitingAsked);
      // A round's public order is revealed once, before its first combat question, and holds the field
      // until Continue (AU11, ADR 0144). Reading it writes nothing.
      const readOrder = async () => {
        if (orderRounds.has(view.board.roundNumber)) return;
        const beforeShown = shown.length, beforeSent = sent.length;
        await expect(page.locator('#order-continue')).toBeVisible({ timeout: 20000 });
        await page.locator('#order-continue').click(); orderRounds.add(view.board.roundNumber);
        // The field it held is free again, so the spell choice opens and acknowledges this asking (ADR 0149): that
        // is the one thing said to the host, and nothing is sent.
        expect(shown.slice(beforeShown).every(asked => Number(asked) === view.waitingAsked)).toBe(true);
        expect(shown.length - beforeShown).toBeLessThanOrEqual(1); expect(sent.length).toBe(beforeSent);
      };
      await readResults(page, readOrder); await readOrder();
      await openSpells(page);
      let spell = null;
      if (view.waitingFor !== 'Target') {
        const options = view.waitingFor === 'Action' ? view.options.action.spells.map(one => one.spell)
          : view.options.intent.creatures.find(one => one.creature === view.waitingCreature).castableSpells;
        // A damaging cast where the host offers one, so the confirmed capture shows a strike landing.
        spell = options.find(id => id.includes('heavy_strike')) ?? options[0]; expect(spell).toBeTruthy();
        if (!captured) await page.screenshot({ path: info.outputPath(`${declaration}-spells.jpg`), type: 'jpeg', quality: 85 });
        await page.locator(`[data-spell="${spell}"]`).click();
      }
      let targets = [];
      if (view.waitingFor !== 'Intent') {
        const target = view.waitingFor === 'Action' ? view.options.action.spells.find(one => one.spell === spell) : view.options.target;
        targets = target.legalTargets.candidates.slice(0, target.legalTargets.minTargets);
        for (const id of targets) await page.locator(`[data-creature="${id}"]`).click();
        if (!captured) { await page.screenshot({ path: info.outputPath(`${declaration}-targets.jpg`), type: 'jpeg', quality: 85 }); captured = true; }
      }
      const selectedSpell = spell ?? view.options.target?.spell;
      const guide = view.guidance?.find(one => one.spell === selectedSpell);
      const estimate = page.locator(view.waitingFor === 'Intent' ? '#intent-preview' : '#combat-preview');
      await expect(estimate).toContainText('Estimate');
      if (guide) await expect(estimate).toContainText(`${guide.cost} energy → ${guide.energyAfterCost} left`);
      else await expect(estimate).toContainText('Preview unavailable for this question');
      const resolvedBefore = Math.max(-1, ...(await seat.actions()).keys());
      const response = page.waitForResponse(response => response.url().endsWith('/decision') && response.request().method() === 'POST');
      await page.locator(view.waitingFor === 'Intent' ? '#combat-declare' : '#combat-confirm').click();
      expect((await response).status()).toBe(204);
      const last = sent.at(-1); expect(last.command.asked).toBe(view.waitingAsked); expect(last.seen).toContain(view.waitingAsked);
      expect(last.command.kind).toBe(view.waitingFor);
      if (view.waitingFor === 'Intent') intents++; else { expect(last.command.targets).toEqual(targets); casts++; }
      await expect.poll(async () => (await read()).waitingAsked).not.toBe(view.waitingAsked);
      if (view.waitingFor !== 'Intent' && casts === 1) {
        // The cast plays to its impact, or is settled without playing: a view that brings it with the next question
        // already drafting, as a loaded runner's late read does, shows it at once on the latest board
        // (table/battle-playback.js, ADR 0141, #1075). Either way the page has shown it before the capture.
        const allies = new Set(view.board.allies.map(one => one.id));
        await expect.poll(async () => {
          const cast = [...(await seat.actions()).entries()].find(([sequence, entry]) => sequence > resolvedBefore && allies.has(entry.event.resolution?.action?.actor))?.[0];
          if (cast === undefined) return 'not resolved';
          return page.evaluate(sequence => {
            if (window.battleFrames.some(frame => frame.stage === 'impact' && frame.sequence === sequence)) return 'played';
            const seen = Math.max(-1, ...JSON.parse(localStorage.getItem('downfall.battle.seen.v1') ?? '[]').map(one => one.sequence));
            return seen >= sequence && document.getElementById('battle').dataset.reading === 'false' ? 'settled' : 'not shown';
          }, cast);
        }, { timeout: 10000 }).toMatch(/^(played|settled)$/);
        await page.screenshot({ path: info.outputPath(`${declaration}-confirmed.jpg`), type: 'jpeg', quality: 85 });
      }
    }
    expect(casts).toBe(3); expect(intents).toBe(declaration === 'hidden-intent' ? 3 : 0);
    expect(new Set(sent.map(one => one.command.asked)).size).toBe(sent.length);
    // One read of the page: what it showed, and the frames it was given before it could show them.
    const { displayed, frames } = await page.evaluate(() => ({ displayed: window.battleFrames, frames: window.hostFrames }));
    expect(displayed.some(frame => frame.stage === 'windup')).toBe(true);
    expect(displayed.some(frame => frame.stage === 'impact')).toBe(true);
    for (const shown of displayed) {
      const frame = frames[shown.sequence]; expect(frame).toBeTruthy();
      for (const one of frame[shown.stage === 'windup' ? 'before' : 'after']) {
        expect(shown.creatures.find(creature => creature.id === one.id)?.text).toBe(`${one.health}/${one.maxHealth} HP · ${one.energy} EN`);
      }
    }
    // Each confirmed action waits for its OK (ADR 0139); read them all as a player would.
    await readResults(page);
    const after = await nextQuestion();
    await expect.poll(async () => (await seat.latest())?.waitingAsked, { timeout: 10000 }).toBe(after.waitingAsked);
    for (const one of [...after.board.allies, ...after.board.enemies]) {
      await expect(page.locator(`[data-creature="${one.id}"] .creature-stats`)).toContainText(`${one.health}/${one.maxHealth} HP`);
    }
  } finally {
    await page.close();
    await stopHost(host);
  }
});
