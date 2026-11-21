// The contrast of words laid on a painting (docs/art/scene-paintings-plan.md, S6; ADR 0197): each box of text on
// a scene's head or ground is read against the lightest pixel under it, on the screen as the walk pictures it,
// once the words themselves are hidden. A screen with no words on a painting measures nothing, so its line keeps
// the keys it had. The walk runs it on every screen (scripts/ux/journey.mjs) and writes it in the screen's line;
// a box under 4.5:1 is counted in `under`, which the baseline's page shows. It does not stop the walk, as no
// other measure does.

/** The ratio WCAG 2.2 AA asks of body text, and of every word on a painting here, whatever its size. */
export const CONTRAST = 4.5;

const channel = value => {
  const c = value / 255;
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};

/** The relative luminance of an sRGB colour, [r, g, b] from 0 to 255. */
export function luminance([r, g, b]) {
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

/** WCAG's contrast ratio between two colours, from 1 to 21, in either order. */
export function contrastRatio(one, other) {
  const [light, dark] = [luminance(one), luminance(other)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}

/**
 * The lowest ratio of a text colour, [r, g, b, a] with `a` from 0 to 1 (opaque when left out), against the pixels
 * of its box, RGBA in a flat list. A text not quite opaque is blended over each pixel first. A fully transparent
 * pixel is skipped: the screenshot drew nothing there.
 */
export function lowestContrast([r, g, b, a = 1], pixels) {
  let lowest = Infinity;
  for (let at = 0; at + 3 < pixels.length; at += 4) {
    if (pixels[at + 3] === 0) continue;
    const under = [pixels[at], pixels[at + 1], pixels[at + 2]];
    const text = [r, g, b].map((value, index) => a * value + (1 - a) * under[index]);
    const ratio = contrastRatio(text, under);
    if (ratio < lowest) lowest = ratio;
  }
  return lowest;
}

/** A screen's measure from the ratio of each box: how many, the lowest (rounded down), how many under 4.5:1. */
export function contrastMeasure(ratios) {
  if (!ratios.length) return null;
  const lowest = Math.min(...ratios);
  return { texts: ratios.length, lowest: Math.floor(lowest * 100) / 100, under: ratios.filter(ratio => ratio < CONTRAST).length };
}

/**
 * Runs in the page: the boxes of text on a painting in view, in CSS pixels, each cut to its scene and to the
 * screen, with its text's colour as sRGB bytes and an alpha. A ground not drawn under less data has no painting,
 * and is the card's own slate. A scene out of view is left out before its painting is waited for: a lazy image
 * out of view never decodes.
 */
export async function sceneTextInPage() {
  const scope = [...document.querySelectorAll('dialog:modal')].at(-1) ?? document.body;
  const width = document.documentElement.clientWidth, height = document.documentElement.clientHeight;
  const cut = (rect, room) => {
    const left = Math.max(room.left, rect.left), top = Math.max(room.top, rect.top);
    return { left, top, width: Math.min(room.right, rect.right) - left, height: Math.min(room.bottom, rect.bottom) - top };
  };
  const screen = { left: 0, top: 0, right: width, bottom: height };
  const scenes = [...scope.querySelectorAll('.scene-head, .scene-ground:not([data-saved])')].filter(scene => {
    const box = cut(scene.getBoundingClientRect(), screen);
    return box.width > 1 && box.height > 1;
  });
  const waited = image => Promise.race([image.decode().catch(() => {}), new Promise(done => setTimeout(done, 3000))]);
  await Promise.all(scenes.flatMap(scene => [...scene.querySelectorAll(':scope > img')]).map(waited));
  // The browser's own reading of a colour, whatever its syntax (a color-mix() serializes as color(srgb ...)).
  const pen = new OffscreenCanvas(1, 1).getContext('2d', { willReadFrequently: true });
  const bytes = colour => {
    pen.clearRect(0, 0, 1, 1);
    pen.fillStyle = colour;
    pen.fillRect(0, 0, 1, 1);
    const [r, g, b, a] = pen.getImageData(0, 0, 1, 1).data;
    return [r, g, b, a / 255];
  };
  const boxes = [];
  for (const scene of scenes) {
    const room = cut(scene.getBoundingClientRect(), screen);
    const inside = { left: room.left, top: room.top, right: room.left + room.width, bottom: room.top + room.height };
    for (const words of scene.querySelectorAll(':scope > .scene-words')) {
      const walker = document.createTreeWalker(words, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const parent = node.parentElement;
        if (!node.textContent.trim() || !parent.checkVisibility({ opacityProperty: true, visibilityProperty: true })) continue;
        const colour = bytes(getComputedStyle(parent).color);
        const range = document.createRange();
        range.selectNodeContents(node);
        // Overflow does not clip a range's rectangles: the part outside its scene is not on the painting.
        for (const rect of range.getClientRects()) {
          const box = cut(rect, inside);
          if (box.width > 1 && box.height > 1) boxes.push({ ...box, colour });
        }
      }
    }
  }
  return boxes;
}

const HIDDEN = 'ux-contrast-hidden';

/** Runs in the page: hides the words on the scenes, keeping their room, or shows them again. */
export function hideSceneWordsInPage({ hide, id }) {
  document.getElementById(id)?.remove();
  if (!hide) return;
  const style = Object.assign(document.createElement('style'), { id });
  style.textContent = '.scene-words, .scene-words * { color: transparent !important; -webkit-text-fill-color: transparent !important; text-shadow: none !important; caret-color: transparent !important; }';
  document.head.append(style);
}

/** Runs in the page: the pixels of each box of a screenshot (base64 PNG), at the screen's density. */
export async function boxPixelsInPage({ png, boxes }) {
  const bytes = Uint8Array.from(atob(png), char => char.charCodeAt(0));
  const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
  const canvas = new OffscreenCanvas(bitmap.width, bitmap.height), pen = canvas.getContext('2d', { willReadFrequently: true });
  pen.drawImage(bitmap, 0, 0);
  const scale = bitmap.width / document.documentElement.clientWidth;
  return boxes.map(box => {
    const x = Math.floor(box.left * scale), y = Math.floor(box.top * scale);
    const w = Math.max(1, Math.min(bitmap.width - x, Math.ceil(box.width * scale))), h = Math.max(1, Math.min(bitmap.height - y, Math.ceil(box.height * scale)));
    return Array.from(pen.getImageData(x, y, w, h).data);
  });
}

/** The contrast of a screen's words on its paintings, or null when it lays none. `page` is a Playwright page. */
export async function measureContrast(page) {
  const boxes = await page.evaluate(sceneTextInPage);
  if (!boxes.length) return null;
  await page.evaluate(hideSceneWordsInPage, { hide: true, id: HIDDEN });
  let png;
  try { png = await page.screenshot({ animations: 'disabled', caret: 'hide' }); } finally {
    await page.evaluate(hideSceneWordsInPage, { hide: false, id: HIDDEN });
  }
  const pixels = await page.evaluate(boxPixelsInPage, { png: png.toString('base64'), boxes });
  return contrastMeasure(boxes.map((box, index) => lowestContrast(box.colour, pixels[index])));
}
