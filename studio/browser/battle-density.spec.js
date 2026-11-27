import { test, expect } from '@playwright/test';
import { fitsBattlefield, fitsViewport } from './fit.js';

test.use({ deviceScaleFactor: 3, reducedMotion: 'reduce' });

test('the dense framebuffer keeps the battlefield and touch targets in CSS coordinates after rotation', async ({ page }, info) => {
  test.skip(info.project.name !== 'phone', 'One explicit 3× phone profile covers both orientations.');
  await page.goto('/table/battle-lab.html');
  await expect(page.locator('#lab')).toHaveAttribute('data-ready', 'true');
  for (const size of [{ width: 390, height: 844 }, { width: 844, height: 390 }]) {
    await page.setViewportSize(size);
    const canvas = page.locator('#phaser-mount canvas');
    await expect.poll(() => canvas.evaluate(node => [node.width / node.clientWidth, node.height / node.clientHeight])).toEqual([3, 3]);
    await fitsBattlefield(page);
    await page.locator('#choose').click();
    await page.locator('[data-creature="b2"]').tap();
    await expect(page.locator('[data-creature="b2"]')).toHaveClass(/is-selected/);
    await page.locator('#cancel').click();
  }
});

// The match's frame (docs/art/ux-craft-plan.md, UX11; docs/art/journeys/match.md): every word around the canvas
// at the floor of 11 px, and no two of its controls on one spot, on a seat signed in, whose site menu, notices
// and account fold into the round's bar. Read at 320 and 390 px, upright, while a creature acts.
const fighter = (id, name) => ({ id, name, health: 21, maxHealth: 30, energy: 5, totalDefense: 2, currentInitiative: 7, isAlive: true,
  isStunned: false, isStunImmune: false, knownSpells: ['spell:host:v1'], conditions: [] });
const acting = { seat: 'player1', waitingFor: 'Action', waitingAsked: 11, feedNext: 83, feed: [{ sequence: 72 }], over: false,
  board: { slot: 'Player1', roundNumber: 4, phase: 'Combat', subPhase: 'Activation', activationCursor: 1,
    allies: [fighter(1, 'Ashen Brute'), fighter(2, 'Tide Warden'), fighter(3, 'Gloom Stalker')],
    enemies: [fighter(4, 'Ember Hound'), fighter(5, 'Rift Seer'), fighter(6, 'Bone Herald')],
    timeline: [1, 2, 3, 4, 5, 6].map(id => ({ creature: id, owner: id < 4 ? 'Player1' : 'Player2', speed: id < 4 ? 'Quick' : 'Standard' })) } };

async function signedInSeat(page) {
  await page.addInitScript(() => {
    localStorage.setItem('downfall.table.seats', JSON.stringify({ player1: 'first' }));
    localStorage.setItem('downfall.arena.help.v1', JSON.stringify({ version: 1, muted: true, seen: [] }));
    localStorage.setItem('downfall.arena.game-settings.v1', JSON.stringify({ pace: 'auto', speed: 1, help: false, reducedMotion: true }));
  });
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/me') return route.fulfill({ json: { accounts: 'dev', account: { id: 'acct_one', username: 'Walker' } } });
    if (url.pathname === '/api/friends') return route.fulfill({ json: { friends: [], incoming: [], outgoing: [] } });
    if (url.pathname === '/api/lobby/resume') return route.fulfill({ json: { tables: [{ table: { id: 'one', format: '3v3',
      seats: [{ seat: 'player1', kind: 'person', name: 'Walker', account: 'acct_one' }, { seat: 'player2', kind: 'bot', name: 'Bot' }] }, seats: [{ seat: 'player1', token: 'first' }] }] } });
    if (url.pathname === '/api/lobby/invitations') return route.fulfill({ json: { invitations: [] } });
    if (url.pathname === '/api/catalogue') return route.fulfill({ json: { cards: [{ id: 'spell:host:v1', name: 'Strike' }], packages: [] } });
    if (url.pathname.endsWith('/events')) return route.fulfill({ status: 200, contentType: 'text/event-stream', body: 'event: ready\ndata: 0\n\n' });
    if (url.pathname === '/api/seat/player1') return route.fulfill({ json: acting });
    return route.fulfill({ status: 204 });
  });
  await page.goto('/table/battle.html');
  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');
  await expect(page.locator('body')).toHaveAttribute('data-home', 'member');
  await expect(page.locator('#opponent-open')).toBeVisible();
}

// What a player reads of the frame under 11 px, and the pairs of controls that share more than a hair of the screen.
const frameReading = page => page.evaluate(() => {
  const shown = node => node.checkVisibility({ opacityProperty: true, visibilityProperty: true }) && node.getBoundingClientRect().width > 1;
  const small = new Set();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const parent = node.parentElement;
    if (!parent || !/[\p{L}\p{N}]/u.test(node.textContent) || parent.closest('dialog, script, style, option') || !shown(parent)) continue;
    const size = Number.parseFloat(getComputedStyle(parent).fontSize);
    if (size >= 1 && size < 11) small.add(`${node.textContent.trim().slice(0, 24)} (${size} px)`);
  }
  const controls = [...document.querySelectorAll('a[href], button, select, [tabindex]:not([tabindex="-1"])')]
    .filter(node => !node.closest('dialog') && shown(node) && !node.disabled);
  // What of a control is on the screen: its box cut by every ancestor that scrolls or clips it (the order row
  // scrolls under its Order button).
  const seen = node => {
    const box = node.getBoundingClientRect(), cut = { left: box.left, top: box.top, right: box.right, bottom: box.bottom };
    for (let parent = node.parentElement; parent; parent = parent.parentElement) {
      if (getComputedStyle(parent).overflow === 'visible') continue;
      const clip = parent.getBoundingClientRect();
      cut.left = Math.max(cut.left, clip.left); cut.top = Math.max(cut.top, clip.top);
      cut.right = Math.min(cut.right, clip.right); cut.bottom = Math.min(cut.bottom, clip.bottom);
    }
    return cut;
  };
  const overlaps = [];
  for (const [index, one] of controls.entries()) for (const other of controls.slice(index + 1)) {
    if (one.contains(other) || other.contains(one) || one.matches('.creature') && other.matches('.creature')) continue;
    const a = seen(one), b = seen(other);
    if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 2 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 2)
      overlaps.push([one, other].map(node => node.id || node.className || node.textContent.trim().slice(0, 16) || node.getAttribute('aria-label')).join(' over ') + JSON.stringify([a, b].map(r => [r.left, r.top, r.right, r.bottom].map(Math.round))));
  }
  return { small: [...small], overlaps };
});

test('the match frame is written at 11 px at least and no two of its controls share a spot', async ({ page }, info) => {
  test.skip(!['small-phone', 'phone'].includes(info.project.name), 'The frame is read at 320 and 390 px.');
  for (const height of [568, 844]) {
    await page.setViewportSize({ width: page.viewportSize().width, height });
    await signedInSeat(page);
    await expect.poll(() => frameReading(page), { timeout: 10000 }).toEqual({ small: [], overlaps: [] });
    // The creature acting opens its spell sheet docked under the field: a caption keeps its name, its health and its
    // defense, and no other creature's tag shows.
    await expect(page.locator('#battle')).toHaveAttribute('data-docked', 'true');
    await expect(page.locator('#actor-layer .creature-name').first()).toBeVisible();
    await expect(page.locator('#actor-layer .stat-health').first()).toBeVisible();
    await expect(page.locator('#actor-layer .creature-defense').first()).toBeVisible();
    for (const hidden of ['.creature-init', '.stat-energy', '#ally-label']) await expect(page.locator(hidden).first()).toBeHidden();
    await expect(page.locator('#actor-layer .creature:not([data-sheet-focus="true"], [data-mutation-focus="true"]) .creature-tag:visible')).toHaveCount(0);
    // The opponent's label names the opponent at 568 px tall too (#1000), at the floor and over nothing.
    await expect(page.locator('#enemy-label')).toBeVisible();
    await fitsViewport(page, '#recap-open, #opponent-open, #order-open, #history-open, #help-open, #settings, .home-menu-toggle, .inbox-button, .account-chip');
  }
  // Under 360 px History is off the bar: its unread mark is a dot on Help, which opens it (#1000).
  const narrowBar = page.viewportSize().width < 360;
  // The count is written as history.ts writes it, again on each read: a redraw of the journal may clear it between two.
  const unread = count => page.locator('#help-open').evaluate((node, count) => {
    const mark = document.getElementById('history-unread'); mark.dataset.count = count; mark.textContent = count ? ` · ${count} new` : '';
    return getComputedStyle(node, '::after').content;
  }, count);
  await expect.poll(() => unread('2')).toBe(narrowBar ? '""' : 'none');
  // The dot is drawn; the words are spoken: Help is described by History's unread count (table/battle-frame.test.js).
  await expect.poll(() => unread('')).toBe('none');
  // Under 420 px the bell is in the menu, its count on the menu's button (home-navigation.md, "The bell"; #1276).
  const narrow = page.viewportSize().width < 420;
  await expect(page.locator('#inbox-open')).toBeVisible({ visible: !narrow });
  await page.locator('#home-menu-toggle').click();
  await expect(page.locator('#inbox-open-menu')).toBeVisible({ visible: narrow });
  if (narrow) { await page.locator('#inbox-open-menu').click(); await expect(page.locator('#inbox-dialog')).toBeVisible(); }
});

// The round's bar holds its longest phase, the recap's mark and the opponent beside History, Help and Settings, with
// the site menu and the account at its right end, on every phone from 360 to 419 px (#1276): at 390 the menu covered
// 15 px of Settings while the round read COMMAND.
test('the round\'s bar keeps Settings clear of the site menu on every phone width, whatever the phase', async ({ page }, info) => {
  test.skip(info.project.name !== 'phone', 'The widths are set here: one project reads them.');
  await signedInSeat(page);
  for (const width of [360, 375, 390, 393, 412, 419, 420, 430]) {
    await page.setViewportSize({ width, height: 844 });
    // Every word the bar can say under 760 px: the phases, and the short forms of a round's start and end.
    for (const phase of ['COMMAND', 'COMBAT', 'WAITING', '+12', 'ENERGY', 'START', '12 ENDED', '6 IMMUNE', 'END']) {
      const gap = await page.evaluate(phase => {
        document.getElementById('round-phase').textContent = phase;
        document.getElementById('round-number').textContent = '12';
        document.getElementById('recap-open').disabled = false;
        const right = ['#recap-open', '#opponent-open', '#history-open', '#help-open', '#settings']
          .map(one => document.querySelector(one)).filter(one => one.checkVisibility())
          .reduce((most, one) => Math.max(most, one.getBoundingClientRect().right), 0);
        const header = [...document.querySelectorAll('.home-header > :is(button, a, details, .account-chip), .home-header #home-menu-toggle')]
          .filter(one => one.checkVisibility()).reduce((least, one) => Math.min(least, one.getBoundingClientRect().left), Infinity);
        return header - right;
      }, phase);
      expect(gap, `${width} px, ${phase}`).toBeGreaterThanOrEqual(0);
    }
  }
});
