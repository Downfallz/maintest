// Where a picture cut by its box centres the cut (picture.js, docs/art/page-pictures.md). It imports nothing, so
// scripts/ux/pictures.mjs checks a declaration with it even when the module it derives, page-art.js, is broken.

/** A focus is placed in steps of this many percent: identity.css declares one rule a step, on each axis. */
export const FOCUS_STEP = 5;

/**
 * Where a painting cut by its box centres the cut (`"30% 60%"`: across, then down): two attributes that
 * identity.css turns into the image's `object-position`, since the showcase's policy refuses a style attribute.
 * Nothing is cropped: the file is the same, only where CSS cuts it moves.
 */
export function focusAttributes(focus) {
  const parts = /^(0|[1-9]\d?|100)% (0|[1-9]\d?|100)%$/.exec(typeof focus === 'string' ? focus : '');
  const [x, y] = parts ? [Number(parts[1]), Number(parts[2])] : [];
  if (!parts || ![x, y].every(at => at % FOCUS_STEP === 0)) throw new Error(`a focus is two percentages from 0 to 100 in steps of ${FOCUS_STEP}, across then down ("30% 60%"), not ${focus}`);
  return { 'data-focus-x': String(x), 'data-focus-y': String(y) };
}
