// The flash guard (docs/art/ux-craft-plan.md, UX13; the craft book, "No flash"). WCAG 2.3.1: nothing flashes more
// than three times in any one second. A flash is a pair of opposing changes of relative luminance of 10% or more
// where the darker of the two is under 0.80, over enough of the screen to count: a quarter of it, the share WCAG
// gives of a ten-degree field, which a phone held in the hand fills. A window that opens bright over a dark page is
// one change, not a flash, unless it repeats.

/** The least change of relative luminance that counts, and the darker value under which it counts (WCAG 2.3.1). */
export const LUMINANCE_STEP = 0.1;
export const DARKER_UNDER = 0.8;
/** The share of the screen a change must cover to count. */
export const AREA = 0.25;
/** The most flashes in any one second. */
export const MOST_A_SECOND = 3;

/** Relative luminance of an sRGB colour, each channel 0 to 255 (WCAG's definition). */
export function luminance(red, green, blue) {
  const linear = channel => {
    const value = channel / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * linear(red) + 0.7152 * linear(green) + 0.0722 * linear(blue);
}

/**
 * The changes of one cell across a film: each `{ at, direction }`, the frame at which the cell's luminance has moved
 * `step` or more from its extreme since the last change, `up` or `down`. The extreme is the cell's running one, not
 * the frame before: a light that ramps up over several frames, each step smaller than `step`, is one change once
 * the ramp adds up, and a ramp that keeps going the same way is the same change, not another.
 */
export function cellChanges(values, { step = LUMINANCE_STEP, darker = DARKER_UNDER } = {}) {
  const changes = [];
  let low = values[0], high = values[0], last = null;
  for (let at = 1; at < values.length; at++) {
    const value = values[at];
    if (last !== 'up' && value - low >= step && low < darker) {
      changes.push({ at, direction: 'up' });
      last = 'up';
      low = high = value;
    } else if (last !== 'down' && high - value >= step && value < darker) {
      changes.push({ at, direction: 'down' });
      last = 'down';
      low = high = value;
    } else {
      // Going on the way it changed moves the extreme it will turn from; going back leaves it.
      if (last !== 'down') high = Math.max(high, value);
      if (last !== 'up') low = Math.min(low, value);
    }
  }
  return changes;
}

/**
 * The most flashes in any one second of a film whose frames are `interval` ms apart. A flash is two opposing
 * changes of one cell, up then down or down then up, the second ending it. Each frame is an array of the same
 * length, a luminance a cell. The screen flashes `n` times in a second when at least `area` of its cells each
 * flash `n` times or more in that second.
 */
export function flashesASecond(frames, interval, { step = LUMINANCE_STEP, darker = DARKER_UNDER, area = AREA } = {}) {
  if (!Array.isArray(frames) || frames.length < 2) throw new Error('The flash guard needs two frames or more.');
  if (!Number.isFinite(interval) || interval <= 0) throw new Error('The flash guard needs the time between frames, in ms.');
  const size = frames[0].length;
  if (!size || frames.some(frame => frame.length !== size)) throw new Error('Every frame of the flash guard has the same cells, and at least one.');
  const ends = [];
  for (let cell = 0; cell < size; cell++) {
    const changes = cellChanges(frames.map(frame => frame[cell]), { step, darker });
    // Each second change ends a flash: up, down is one; up, down, up, down is two.
    ends.push(changes.filter((_, index) => index % 2 === 1).map(change => change.at * interval));
  }
  const enough = Math.max(1, Math.ceil(area * size));
  let most = 0;
  for (let from = 0; from < frames.length; from++) {
    const start = from * interval;
    const counts = ends.map(times => times.filter(time => time >= start && time < start + 1000).length).sort((a, b) => b - a);
    most = Math.max(most, counts[enough - 1]);
  }
  return most;
}

/**
 * Runs in the page: each PNG, given as base64, drawn on a canvas `columns` cells wide and read as the luminance of
 * each cell. The page needs no module of its own: Playwright passes the function's text.
 */
export async function luminanceInPage({ pictures, columns = 48 }) {
  const lit = channel => {
    const value = channel / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };
  const frames = [];
  for (const base64 of pictures) {
    const blob = await (await fetch(`data:image/png;base64,${base64}`)).blob();
    const bitmap = await createImageBitmap(blob);
    const rows = Math.max(1, Math.round(columns * bitmap.height / bitmap.width));
    const canvas = new OffscreenCanvas(columns, rows);
    const context = canvas.getContext('2d');
    context.drawImage(bitmap, 0, 0, columns, rows);
    const { data } = context.getImageData(0, 0, columns, rows);
    const cells = [];
    for (let at = 0; at < data.length; at += 4) cells.push(0.2126 * lit(data[at]) + 0.7152 * lit(data[at + 1]) + 0.0722 * lit(data[at + 2]));
    frames.push(cells);
  }
  return frames;
}
