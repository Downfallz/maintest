// The picture kit's form (ADR 0198, docs/art/page-pictures.md): a painting on a page is an `<img>` with its width
// and its height, sent at the size it is drawn, once and twice. The sizes are derived by scripts/ux/pictures.mjs
// into page-art.js; a page names a picture and where it is, and this writes the element. The showcase's build
// writes the same form as text, with its own folder as `base`.
import { PAGE_ART } from './page-art.js';

/** Where the host serves the derived files. */
export const PAGE_ART_BASE = '/page-art/';

/** The most frames a strip may hold: identity.css declares one rule a count. */
export const STRIP_FRAMES = 12;

/** A focus is placed in steps of this many percent: identity.css declares one rule a step, on each axis. */
export const FOCUS_STEP = 5;

/**
 * Where a painting cut by its box centres the cut (`"30% 60%"`: across, then down): two attributes that
 * identity.css turns into the image's `object-position`, since the showcase's policy refuses a style attribute.
 * Nothing is cropped: the file is the same, only where CSS cuts it moves.
 */
export function focusAttributes(focus) {
  const parts = /^(\d{1,3})% (\d{1,3})%$/.exec(typeof focus === 'string' ? focus : '');
  const [x, y] = parts ? [Number(parts[1]), Number(parts[2])] : [];
  if (!parts || ![x, y].every(at => at <= 100 && at % FOCUS_STEP === 0)) throw new Error(`a focus is two percentages from 0 to 100 in steps of ${FOCUS_STEP}, across then down ("30% 60%"), not ${focus}`);
  return { 'data-focus-x': String(x), 'data-focus-y': String(y) };
}

const escaped = text => String(text).replace(/[&<>"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[char]);

function entryOf(name, page, art) {
  const entry = art[name];
  if (!entry) throw new Error(`${name} is not a picture of docs/art/page-pictures.json`);
  // A page that draws a picture says so in the declaration: that is what its budget counts.
  if (!entry.pages[page]) throw new Error(`${name} is not declared for ${page} in docs/art/page-pictures.json`);
  return entry;
}

/**
 * The attributes of a picture's `<img>`, in the order they are written. `page` is the page's name in the
 * declaration, which says whether the picture is asked for first. `sizes` is the width it is drawn at when the
 * page draws it at another than the declaration's first, and for a strip the width of all its frames, since its
 * file holds them side by side; `alt` is empty for decoration. `focus` is where a box that cuts the painting
 * centres the cut, when this box wants another than the declaration's: one scene in two boxes.
 */
export function pictureAttributes(name, { page, alt = '', sizes, focus, base = PAGE_ART_BASE, art = PAGE_ART } = {}) {
  const entry = entryOf(name, page, art), role = entry.pages[page], whole = entry.drawn[0] * entry.frames;
  const shown = entry.sizes.find(size => size.width >= whole) ?? entry.sizes.at(-1);
  const attributes = {
    src: base + shown.file,
    srcset: entry.sizes.map(size => `${base}${size.file} ${size.width}w`).join(', '),
    sizes: sizes ?? `${whole}px`,
    width: shown.width,
    height: shown.height,
    alt,
  };
  // The largest paint is asked for first and never lazy; what is below the first screen loads when near.
  if (role === 'largest') attributes.fetchpriority = 'high';
  else attributes.decoding = 'async';
  if (role === 'lazy') attributes.loading = 'lazy';
  const cut = focus ?? entry.focus;
  return cut === undefined ? attributes : { ...attributes, ...focusAttributes(cut) };
}

const written = attributes => Object.entries(attributes).map(([name, value]) => `${name}="${escaped(value)}"`).join(' ');

/** Every picture of the kit is a `.picture`: never wider than its room, its height following (identity.css). */
const classOf = options => (options.class ? `picture ${options.class}` : 'picture');

/** A picture as text, for a page built before it is served. */
export function pictureHtml(name, options = {}) {
  return `<img ${written({ class: classOf(options), ...pictureAttributes(name, options) })}>`;
}

/** A picture as an element, for a page that builds itself. */
export function pictureElement(doc, name, options = {}) {
  const image = doc.createElement('img');
  image.className = classOf(options);
  for (const [attribute, value] of Object.entries(pictureAttributes(name, options))) image.setAttribute(attribute, String(value));
  return image;
}

function stripOf(name, { page, focus, art = PAGE_ART }) {
  const entry = entryOf(name, page, art);
  if (focus !== undefined) throw new Error(`${name}: a strip is never cut, and takes no focus`);
  if (entry.frames < 2 || entry.frames > STRIP_FRAMES) throw new Error(`${name} is not a strip of 2 to ${STRIP_FRAMES} frames`);
  return entry;
}

/**
 * What the strip's frame carries: its count, the frame it rests on under less motion when that is not the first
 * (ADR 0199), and its name for a screen reader, one for the whole strip.
 */
function frameAttributes(frames, alt, still) {
  if (still !== undefined && !(Number.isInteger(still) && still >= 1 && still <= frames)) throw new Error(`A strip of ${frames} frames rests on a frame from 1 to ${frames}, not ${still}`);
  return { class: 'motion-strip', 'data-frames': frames, ...(still > 1 ? { 'data-still': still } : {}), ...(alt ? { role: 'img', 'aria-label': alt } : { 'aria-hidden': 'true' }) };
}

/**
 * A strip of frames as text: one file, its frames side by side, stepped across its frame by the motion kit while
 * it is the screen's ambience. Before it is in view it is its first frame; under less motion it is `still`, the
 * frame that says what the strip shows (the first by default).
 */
export function stripHtml(name, options = {}) {
  const entry = stripOf(name, options);
  return `<span ${written(frameAttributes(entry.frames, options.alt, options.still))}>${pictureHtml(name, { ...options, alt: '' })}</span>`;
}

/** A strip of frames as an element. Call `watch()` of motion.js once it is on the page. */
export function stripElement(doc, name, options = {}) {
  const entry = stripOf(name, options), frame = doc.createElement('span');
  for (const [attribute, value] of Object.entries(frameAttributes(entry.frames, options.alt, options.still))) frame.setAttribute(attribute, String(value));
  frame.append(pictureElement(doc, name, { ...options, alt: '' }));
  return frame;
}
