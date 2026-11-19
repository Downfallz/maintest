// The seat's views as the page receives them, collected in the page (#921), as battle.combat.js collects its frames
// (#916). Playwright does not await response handlers. If a spec parses bodies there, two overlapping reads can finish
// out of order and set the latest view back to an older one, so the spec can read a view older than the one the page
// drew (#903, #908). Here `fetch` is wrapped before the page loads: each seat body is parsed from a clone before the
// page gets it, and a view replaces the latest only if its read was asked later. Every seat read with a board counts,
// the page's history read (`since` 0) too: a body the page drops after a write still names the seat's cursor.

// Wraps `scope.fetch` and keeps what the seat reads bring in `scope.seatViews`. Runs in the page, so it uses nothing
// from this module: `collectSeatViews` sends its source. `studio/seat-views.test.js` runs it in Node with stubs.
//   seats: the seat paths whose bodies count; skipShown: leave out the reads that say an asking was drawn.
export function installSeatViews(scope, { seats = ['player1'], skipShown = false } = {}) {
  const fetch = scope.fetch.bind(scope), paths = seats.map(seat => `/api/seat/${seat}`), views = { latest: null, order: 0, actions: {} };
  let asked = 0;
  scope.seatViews = views;
  scope.fetch = async (...args) => {
    const order = ++asked, response = await fetch(...args), url = new URL(response.url, scope.location.href);
    if (!response.ok || !paths.some(path => url.pathname.endsWith(path)) || (skipShown && url.searchParams.has('shown'))) return response;
    const body = await response.clone().json().catch(() => null);
    if (!body?.board) return response;
    for (const entry of body.feed ?? []) if (entry.event?.kind === 'CombatActionResolved') views.actions[entry.sequence] = entry;
    // The guard that keeps the order: a read asked earlier that finishes later never replaces a newer view.
    if (order > views.order) Object.assign(views, { latest: body, order });
    return response;
  };
}

// Call it before the page's first `goto`, so the reads the page makes as it loads are collected too. Returns
// `latest()`, the seat's latest view or null, and `actions()`, every resolved combat action the feeds carried, keyed
// by sequence. Each is one `page.evaluate` that returns only what it names: a step's polls make many (#735). Both
// start empty on each new document, and an evaluate during a navigation rejects: read them between navigations.
export async function collectSeatViews(page, options = {}) {
  await page.addInitScript(`(${installSeatViews})(window, ${JSON.stringify(options)});`);
  return {
    latest: () => page.evaluate(() => window.seatViews?.latest ?? null),
    actions: async () => new Map(Object.entries(await page.evaluate(() => window.seatViews?.actions ?? {}))
      .map(([sequence, entry]) => [Number(sequence), entry])),
  };
}
