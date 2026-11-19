// The pages' motion kit as a browser plays it (ADR 0197, table/identity.css, table/motion.js): every window
// enters and leaves, and under less motion, the system's or the account's,
// nothing animates a transform. The pages are read as the host serves them, with no script but the kit's own
// module: what is held here is the sheets and that module, not a page's own journey.
import { test, expect } from '@playwright/test';

const PAGES = ['lobby.html', 'index.html', 'rules.html', 'leaderboard.html', 'stats.html', 'admin.html', 'battle.html'];
const WINDOWS = 'dialog:not(.talent-window)';
const SETTINGS_KEY = 'downfall.arena.game-settings.v1';

async function open(page, name, { less = false } = {}) {
  await page.route('**/*.js', route => (new URL(route.request().url()).pathname.endsWith('/motion.js') ? route.continue() : route.abort()));
  await page.route('**/api/**', route => route.fulfill({ status: 404, json: { message: 'Not here.' } }));
  if (less) await page.addInitScript(key => localStorage.setItem(key, JSON.stringify({ reducedMotion: true })), SETTINGS_KEY);
  await page.goto(`/table/${name}`);
  await expect.poll(() => page.evaluate(() => document.documentElement.dataset.motion ?? 'full')).toBe(less ? 'less' : 'full');
}

/** What is animating now, as the properties each animation moves. */
const moving = page => page.evaluate(() => document.getAnimations().flatMap(animation =>
  animation.transitionProperty ? [animation.transitionProperty] : animation.effect.getKeyframes().flatMap(frame => Object.keys(frame))
).filter(name => !['offset', 'easing', 'composite', 'computedOffset'].includes(name)));

/** Plays each of the kit's uses on the page, and gives what moved while each played. */
async function playTheKit(page) {
  const seen = [];
  const dialogs = await page.locator(WINDOWS).count();
  for (let index = 0; index < dialogs; index++) {
    await page.evaluate(([at, windows]) => { const dialog = document.querySelectorAll(windows)[at]; dialog.close(); dialog.showModal(); }, [index, WINDOWS]);
    seen.push(...await moving(page));
    await page.evaluate(([at, windows]) => document.querySelectorAll(windows)[at].close(), [index, WINDOWS]);
    seen.push(...await moving(page));
    await expect(page.locator(WINDOWS).nth(index)).toBeHidden();
  }
  await page.evaluate(async () => {
    const { numberChanged } = await import('./motion.js');
    const arrival = Object.assign(document.createElement('p'), { className: 'motion-arrive', textContent: 'A turn waits' });
    const number = Object.assign(document.createElement('b'), { className: 'motion-number', textContent: '12' });
    const breath = Object.assign(document.createElement('div'), { className: 'motion-breath', textContent: 'Colossus' });
    const pressed = Object.assign(document.createElement('button'), { id: 'kit-press', textContent: 'Play' });
    for (const one of [arrival, number, breath, pressed]) one.style.cssText = 'position: fixed; top: 0; left: 0; z-index: 99; min-width: 60px; min-height: 44px;';
    document.body.append(arrival, number, breath, pressed);
    numberChanged(number);
  });
  seen.push(...await moving(page));
  const box = await page.locator('#kit-press').boundingBox();
  await page.mouse.move(box.x + 5, box.y + 5);
  await page.mouse.down();
  seen.push(...await moving(page));
  await page.mouse.up();
  return [...new Set(seen)];
}

const TRAVEL = ['transform', 'translate', 'scale', 'rotate'];

for (const name of PAGES) {
  test(`${name}: under full motion the kit travels, so the check below can see a transform`, async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await open(page, name);
    const moved = await playTheKit(page);
    expect(moved).toContain('opacity');
    expect(moved).toContain('scale');
  });

  test(`${name}: under the system's less motion nothing animates a transform, and a fade stays`, async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await open(page, name);
    const moved = await playTheKit(page);
    expect(moved.filter(property => TRAVEL.includes(property))).toEqual([]);
    expect(moved).toContain('opacity');
  });

  test(`${name}: under the account's less motion nothing animates a transform, and a fade stays`, async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await open(page, name, { less: true });
    const moved = await playTheKit(page);
    expect(moved.filter(property => TRAVEL.includes(property))).toEqual([]);
    expect(moved).toContain('opacity');
  });
}

for (const name of ['lobby.html', 'admin.html']) {
  for (const reducedMotion of ['no-preference', 'reduce']) {
    test(`${name}: every window enters and leaves (${reducedMotion})`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion });
      await open(page, name);
      // The classic table's mutation tree, index.html's one dialog, is no window of the kit: always open, shown
      // and put away by `hidden`.
      const ids = await page.locator(WINDOWS).evaluateAll(all => all.map(one => one.id));
      expect(ids.length).toBeGreaterThan(0);
      for (const id of ids) {
        const read = await page.evaluate(async id => {
          const dialog = document.getElementById(id);
          const settled = () => Promise.all(dialog.getAnimations().map(animation => animation.finished.catch(() => {})));
          const fades = () => dialog.getAnimations().some(animation => animation.transitionProperty === 'opacity');
          // One the page's own script would have closed starts closed here too.
          dialog.close();
          await settled();
          dialog.showModal();
          const enters = fades() && Number(getComputedStyle(dialog).opacity) < 1;
          await settled();
          const shown = getComputedStyle(dialog).opacity;
          dialog.close();
          const leaves = fades() && getComputedStyle(dialog).display !== 'none';
          await settled();
          return { enters, shown, leaves, gone: getComputedStyle(dialog).display };
        }, id);
        expect(read, id).toEqual({ enters: true, shown: '1', leaves: true, gone: 'none' });
      }
    });
  }
}

test('the match\'s windows open and close at once, as before: they are staged as one family later', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await open(page, 'battle.html');
  const ids = await page.locator(WINDOWS).evaluateAll(all => all.map(one => one.id));
  expect(ids.length).toBeGreaterThan(10);
  const moved = await page.evaluate(ids => ids.filter(id => {
    const dialog = document.getElementById(id);
    dialog.showModal();
    const entered = dialog.getAnimations().length > 0;
    dialog.close();
    return entered || getComputedStyle(dialog).display !== 'none';
  }), ids);
  expect(moved).toEqual([]);
});

test('one ambience plays a screen, in view, and none under less motion', async ({ page }) => {
  const breaths = () => page.evaluate(() => [...document.querySelectorAll('.motion-breath')].map(one => getComputedStyle(one).animationName === 'none' ? 'still' : getComputedStyle(one).animationPlayState));
  const add = () => page.evaluate(async () => {
    for (const top of ['300vh', '10px', '40px']) {
      const breath = Object.assign(document.createElement('div'), { className: 'motion-breath', textContent: 'Colossus' });
      breath.style.cssText = `position: absolute; top: ${top}; left: 0; width: 60px; height: 20px;`;
      document.body.append(breath);
    }
    (await import('./motion.js')).mountMotion().watch();
  });
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await open(page, 'rules.html');
  await add();
  await expect.poll(breaths).toEqual(['paused', 'running', 'paused']);

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect.poll(breaths).toEqual(['still', 'still', 'still']);
});
