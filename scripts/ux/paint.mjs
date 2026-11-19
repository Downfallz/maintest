// The first paints of the journeys' doors on a slow phone (docs/art/ux-craft-plan.md, UX2). A paint depends on
// the machine, so it is no line of the measures: each page is loaded several times with nothing cached, the
// network and the processor held back, and what is reported is the median with the fastest and the slowest.
// One run is a sample.

import { raiseSet } from './set.mjs';

export const PAINT_RUNS = 5;

/** The slow phone: Lighthouse's slow 4G (1.6 Mbit/s down, 750 kbit/s up, 150 ms) and a processor four times slower. */
export const SLOW_PHONE = { download: 1.6 * 1024 * 1024 / 8, upload: 750 * 1024 / 8, latency: 150, cpu: 4 };

const VIEWPORT = { width: 390, height: 844 };

function watchPaintInPage() {
  const kept = { largest: null, element: null, shift: 0 };
  globalThis.__journeyPaint = kept;
  new PerformanceObserver(list => {
    const last = list.getEntries().at(-1);
    kept.largest = last.startTime;
    kept.element = last.url ? 'picture' : last.element?.tagName.toLowerCase() ?? 'text';
  }).observe({ type: 'largest-contentful-paint', buffered: true });
  // What arrives late moves the page only on a slow network: the shift is read here too.
  new PerformanceObserver(list => {
    for (const entry of list.getEntries()) if (!entry.hadRecentInput) kept.shift += entry.value;
  }).observe({ type: 'layout-shift', buffered: true });
}

function readPaintInPage() {
  const first = performance.getEntriesByName('first-contentful-paint')[0]?.startTime ?? null;
  const pictures = performance.getEntriesByType('resource').filter(entry => /\.(webp|png|jpe?g|avif|svg)(\?|$)/.test(entry.name));
  return {
    first: first === null ? null : Math.round(first),
    largest: globalThis.__journeyPaint.largest === null ? null : Math.round(globalThis.__journeyPaint.largest),
    element: globalThis.__journeyPaint.element,
    shift: Math.round(globalThis.__journeyPaint.shift * 100) / 100,
    pictureBytes: pictures.reduce((sum, entry) => sum + entry.encodedBodySize, 0),
  };
}

async function onePaint(browser, url, prepare) {
  const context = await browser.newContext({ viewport: VIEWPORT, isMobile: true, hasTouch: true, deviceScaleFactor: 1 });
  try {
    await prepare?.(context);
    await context.addInitScript(watchPaintInPage);
    const page = await context.newPage();
    const session = await context.newCDPSession(page);
    await session.send('Network.enable');
    await session.send('Network.setCacheDisabled', { cacheDisabled: true });
    await session.send('Network.emulateNetworkConditions', { offline: false, latency: SLOW_PHONE.latency, downloadThroughput: SLOW_PHONE.download, uploadThroughput: SLOW_PHONE.upload });
    await session.send('Emulation.setCPUThrottlingRate', { rate: SLOW_PHONE.cpu });
    await page.goto(url, { waitUntil: 'load', timeout: 120000 });
    // A page shows its panel once its script has asked who is there, and the paintings come after: the paint is
    // final once two readings, two seconds apart, agree. Nothing touches the page, so the largest paint stays open.
    let last = null;
    for (let calm = 0, tries = 0; calm < 2 && tries < 30; tries++) {
      await page.waitForTimeout(2000);
      const now = await page.evaluate(readPaintInPage);
      calm = JSON.stringify(now) === JSON.stringify(last) ? calm + 1 : 0;
      last = now;
    }
    return last;
  } finally {
    await context.close();
  }
}

/** The median, the fastest and the slowest of one reading over the runs, the median kept to `decimals` places. */
export function spread(values, decimals = 0) {
  const sorted = values.filter(value => value !== null).sort((a, b) => a - b);
  if (!sorted.length) return null;
  const middle = sorted.length >> 1;
  return { median: sorted.length % 2 ? sorted[middle] : Number(((sorted[middle - 1] + sorted[middle]) / 2).toFixed(decimals)), fastest: sorted[0], slowest: sorted.at(-1) };
}

export function summary(runs) {
  return {
    runs: runs.length,
    firstPaintMs: spread(runs.map(run => run.first)),
    largestPaintMs: spread(runs.map(run => run.largest)),
    largestIs: [...new Set(runs.map(run => run.element))].join(', '),
    shift: spread(runs.map(run => run.shift ?? null), 2),
    pictureBytes: spread(runs.map(run => run.pictureBytes)).median,
  };
}

/** The three doors: the showcase's home, the game's sign-in panel, and the home of a player with nothing. */
export async function paints(browser, showcase, { runs = PAINT_RUNS } = {}) {
  const set = await raiseSet(showcase);
  try {
    const signIn = async context => {
      const origin = { Origin: set.game };
      await context.request.post(`${set.game}/api/account/dev`, { data: { username: 'Walker' }, headers: origin });
      await context.request.post(`${set.game}/api/tutorials/sc-repo-tutorial-your-first-combat/skip`, { data: {}, headers: origin });
    };
    const pages = [['showcase-home', `${set.showcase}/`, null], ['sign-in-panel', `${set.game}/`, null], ['home-new', `${set.game}/`, signIn]];
    const result = { profile: { viewport: `${VIEWPORT.width}x${VIEWPORT.height}`, ...SLOW_PHONE }, pages: {} };
    // One load after the other, on purpose: two at once would share the processor that is being held back.
    for (const [name, url, prepare] of pages) {
      const taken = [];
      for (let run = 0; run < runs; run++) taken.push(await onePaint(browser, url, prepare));
      result.pages[name] = summary(taken);
    }
    return result;
  } finally {
    await set.close();
  }
}
