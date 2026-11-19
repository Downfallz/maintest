// The seat's views as the page reads them, collected in the page (#921), as battle.combat.js collects its frames (#916).
// Parsed from Playwright's response events, a body is parsed in a handler Playwright does not await: two reads that
// overlap can resolve out of order and set the latest view back to an older one, and what the spec reads can lag
// behind what the page already drew (#903, #908). Here `fetch` is wrapped before the page loads: each seat body is
// parsed from a clone before the page gets it, and a view replaces the latest only if its read was asked later.
// Call it before the page's first `goto`, so the reads the page makes as it loads are collected too.
//   seats: the seat paths whose bodies count; skipShown: leave out the reads that say an asking was drawn.
// Returns `latest()`, the seat's latest view or null, and `actions()`, every resolved combat action the feeds
// carried, keyed by sequence; both read the page, so both are awaited, and both start empty on each new document.
export async function collectSeatViews(page, { seats = ['player1'], skipShown = false } = {}) {
  await page.addInitScript(({ seats, skipShown }) => {
    const fetch = window.fetch.bind(window), paths = seats.map(seat => `/api/seat/${seat}`);
    let asked = 0;
    window.seatViews = { latest: null, order: 0, actions: {} };
    window.fetch = async (...args) => {
      const order = ++asked, response = await fetch(...args), url = new URL(response.url, location.href);
      if (!response.ok || !paths.some(path => url.pathname.endsWith(path)) || (skipShown && url.searchParams.has('shown'))) return response;
      const body = await response.clone().json().catch(() => null);
      if (!body?.board) return response;
      for (const entry of body.feed ?? []) if (entry.event?.kind === 'CombatActionResolved') window.seatViews.actions[entry.sequence] = entry;
      if (order > window.seatViews.order) Object.assign(window.seatViews, { latest: body, order });
      return response;
    };
  }, { seats, skipShown });
  const read = () => page.evaluate(() => window.seatViews ?? { latest: null, actions: {} });
  return {
    latest: async () => (await read()).latest,
    actions: async () => new Map(Object.entries((await read()).actions).map(([sequence, entry]) => [Number(sequence), entry])),
  };
}
