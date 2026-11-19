// The picture kit's form (ADR 0198, docs/art/page-pictures.md): a painting on a page is an `<img>` with its width
// and its height, sent at the size it is drawn, once and twice. The sizes are derived by scripts/ux/pictures.mjs
// into page-art.js; a page names a picture and where it is, and this writes the element. The showcase's build
// writes the same form as text, with its own folder as `base`.
import { PAGE_ART } from './page-art.js';

/** Where the host serves the derived files. */
export const PAGE_ART_BASE = '/page-art/';

/** The most frames a strip may hold: identity.css declares one rule a count. */
export const STRIP_FRAMES = 12;

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
 * page draws it at another than the declaration's first; `alt` is empty for decoration.
 */
export function pictureAttributes(name, { page, alt = '', sizes, base = PAGE_ART_BASE, art = PAGE_ART } = {}) {
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
  return attributes;
}

const written = attributes => Object.entries(attributes).map(([name, value]) => `${name}="${escaped(value)}"`).join(' ');

/** A picture as text, for a page built before it is served. */
export function pictureHtml(name, options = {}) {
  return `<img ${written({ ...(options.class ? { class: options.class } : {}), ...pictureAttributes(name, options) })}>`;
}

/** A picture as an element, for a page that builds itself. */
export function pictureElement(doc, name, options = {}) {
  const image = doc.createElement('img');
  if (options.class) image.className = options.class;
  for (const [attribute, value] of Object.entries(pictureAttributes(name, options))) image.setAttribute(attribute, String(value));
  return image;
}

function stripOf(name, { page, art = PAGE_ART }) {
  const entry = entryOf(name, page, art);
  if (entry.frames < 2 || entry.frames > STRIP_FRAMES) throw new Error(`${name} is not a strip of 2 to ${STRIP_FRAMES} frames`);
  return entry;
}

/** What the strip's frame carries: its count, and its name for a screen reader, one for the whole strip. */
const frameAttributes = (frames, alt) => ({ class: 'motion-strip', 'data-frames': frames, ...(alt ? { role: 'img', 'aria-label': alt } : { 'aria-hidden': 'true' }) });

/**
 * A strip of frames as text: one file, its frames side by side, stepped across its frame by the motion kit while
 * it is the screen's ambience. Under less motion, and before it is in view, it is its first frame.
 */
export function stripHtml(name, options = {}) {
  const entry = stripOf(name, options);
  return `<span ${written(frameAttributes(entry.frames, options.alt))}>${pictureHtml(name, { ...options, alt: '' })}</span>`;
}

/** A strip of frames as an element. Call `watch()` of motion.js once it is on the page. */
export function stripElement(doc, name, options = {}) {
  const entry = stripOf(name, options), frame = doc.createElement('span');
  for (const [attribute, value] of Object.entries(frameAttributes(entry.frames, options.alt))) frame.setAttribute(attribute, String(value));
  frame.append(pictureElement(doc, name, { ...options, alt: '' }));
  return frame;
}
