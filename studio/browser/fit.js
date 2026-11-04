import { expect } from '@playwright/test';

// Every control the selector names inside the viewport and at least 44px, the document no larger than the
// viewport, read until the layout has settled: hit targets follow the scene's own resize, a frame or two after
// the viewport. One in-page read a poll: separate round trips to a page rendering a 1440x1000 canvas in
// software outlast the poll on their own. Controls hidden from the page (display none, or visibility hidden)
// are not controls a finger could miss.
export async function fitsViewport(page, selector) {
  await expect.poll(() => page.evaluate(({ width, height, selector }) => {
    const wrong = [];
    const root = document.documentElement;
    if (root.scrollWidth > width || root.scrollHeight > height) wrong.push(`document ${root.scrollWidth}x${root.scrollHeight}`);
    for (const node of document.querySelectorAll(selector)) {
      const box = node.getBoundingClientRect();
      if (box.width === 0 || box.height === 0 || ['hidden', 'collapse'].includes(getComputedStyle(node).visibility)) continue;
      if (box.width < 44 || box.height < 44 || box.left < -1 || box.top < -1 || box.right > width + 1 || box.bottom > height + 1) {
        wrong.push(`${node.id || node.dataset.creature} ${Math.round(box.left)},${Math.round(box.top)} ${Math.round(box.width)}x${Math.round(box.height)}`);
      }
    }
    return wrong;
  }, { ...page.viewportSize(), selector }), { timeout: 15000 }).toEqual([]);
}


// Opening a replay resizes the battlefield without resizing the window. Its canvas and hit targets must
// follow the mount, rather than cover the historical result controls with the previous canvas dimensions.
export async function fitsBattlefield(page) {
  await expect.poll(() => page.evaluate(() => {
    const stage = document.getElementById('battle-zone').getBoundingClientRect();
    const canvas = document.querySelector('#phaser-mount canvas').getBoundingClientRect();
    const wrong = [];
    if (Math.abs(canvas.height - stage.height) > 1 || Math.abs(canvas.width - stage.width) > 1) wrong.push('canvas');
    for (const node of document.querySelectorAll('.creature')) {
      const box = node.getBoundingClientRect();
      if (box.top < stage.top - 1 || box.bottom > stage.bottom + 1 || box.left < stage.left - 1 || box.right > stage.right + 1) wrong.push(node.dataset.creature);
    }
    return wrong;
  }), { timeout: 10000 }).toEqual([]);
}
