// A ResizeObserver whose callback changes layout (a canvas resized, a CSS variable set, links redrawn) can make
// the browser observe another resize in the same frame. It then holds those notifications back and reports a
// "ResizeObserver loop" error, which WebKit raises on iOS whenever its toolbars move. Doing the work on the next
// frame, once per frame however many notifications arrive, lets each resize settle in its own frame.
export function onNextFrame(work: () => void) {
  let pending = 0;
  const run = () => { pending = 0; work(); };
  return Object.assign(() => { if (!pending) pending = requestAnimationFrame(run); },
    { cancel: () => { if (pending) cancelAnimationFrame(pending); pending = 0; } });
}
