// The classic table is archived (ADR 0157): /play hands every seat on to the battle view, except in a browser that
// asked for the archive by storing `classic`. The classic table's own tests are that browser.
export const archivedTable = () => { try { localStorage.setItem('downfall.presentation', 'classic'); } catch { /* storage-denial fixtures */ } };
// The host's word that the table changed (ADR 0119, 0164): a seat's stream that says so often, so a test that serves
// another board sees it read, as a page does when the other player acts. Nothing else on the page reads on a clock.
export const announcingHost = () => {
  const original = window.fetch;
  window.fetch = (url, options) => /\/api\/seat\/player[12]\/events$/.test(String(url))
    ? Promise.resolve(new Response(new ReadableStream({ start(controller) {
      const say = text => { try { controller.enqueue(new TextEncoder().encode(text)); } catch { clearInterval(timer); } };
      say('event: ready\ndata: 0\n\n');
      let version = 0;
      const timer = setInterval(() => say(`event: changed\ndata: ${++version}\n\n`), 500);
      options?.signal?.addEventListener('abort', () => { clearInterval(timer); try { controller.close(); } catch { /* closed */ } });
    } }), { headers: { 'Content-Type': 'text/event-stream' } }))
    : original(url, options);
};
export async function classicTable({ context }) { await context.addInitScript(archivedTable); await context.addInitScript(announcingHost); }
