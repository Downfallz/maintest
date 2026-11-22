// The picture kit's form (ADR 0198, docs/art/page-pictures.md): a painting on a page is an `<img>` with its width
// and its height, sent at the size it is drawn, once and twice. The sizes are derived by scripts/ux/pictures.mjs
// into page-art.js; a page names a picture and where it is, and this writes the element. The showcase's build
// writes the same form as text, with its own folder as `base`.
import { PAGE_ART } from './page-art.js';
import { focusAttributes } from './picture-focus.js';

export { FOCUS_STEP, focusAttributes } from './picture-focus.js';

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
 * page draws it at another than the declaration's first, and for a strip the width of all its frames, since its
 * file holds them side by side; `alt` is empty for decoration. `focus` is where a box that cuts the painting
 * centres the cut, when this box wants another than the declaration's: one scene in two boxes. `lazy` loads it when
 * near even where its declaration's role is `first`: one painting drawn on the first screen and in a list below it
 * is one declaration, so the page takes one file (#985).
 */
export function pictureAttributes(name, { page, alt = '', sizes, focus, lazy = false, base = PAGE_ART_BASE, art = PAGE_ART } = {}) {
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
  if (role === 'lazy' || (lazy && role !== 'largest')) attributes.loading = 'lazy';
  const cut = focus === undefined ? entry.focus : focus;
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

/**
 * The three forms a scene takes in the table (docs/art/scene-paintings-plan.md, the app): a head, a band across
 * the top of a window, a sheet or a page, behind its eyebrow and title; a ground, a card's or a dialog's painting
 * dimmed behind its words; a plate, a picture on its own with its words under it. identity.css draws them, so a
 * page sheet only places them.
 */
export const SCENE_FORMS = ['head', 'ground', 'plate'];
const SIDES = new Set(['start', 'end']);

/**
 * Less data, as the browser asks for it (`Save-Data`): a ground goes, its card keeps its slate. Read where the
 * scene is built, so only `sceneElement` in the reader's browser honours it; `sceneHtml` is told by `saveData`.
 */
const savingData = () => globalThis.navigator?.connection?.saveData === true;

function sceneOf(name, { form, side, alt, page, art = PAGE_ART, saveData = savingData() }) {
  if (!SCENE_FORMS.includes(form)) throw new Error(`${name}: a scene is a head, a ground or a plate, not ${form}`);
  if (entryOf(name, page, art).frames > 1) throw new Error(`${name}: a strip is never a scene`);
  if (side !== undefined && form !== 'head') throw new Error(`${name}: only a head has a side`);
  if (form === 'head' && !SIDES.has(side ?? 'start')) throw new Error(`${name}: a head's words sit on its start or its end, not ${side}`);
  // Its words say what the screen is; the painting behind them says nothing of its own.
  if (alt && form !== 'plate') throw new Error(`${name}: a head's or a ground's painting is a decoration, and takes no alt`);
  if (!alt && form === 'plate') throw new Error(`${name}: a plate's painting says what it shows: it needs an alt`);
  const container = form === 'plate'
    ? { tag: 'figure', words: 'figcaption', attributes: {} }
    : { tag: 'div', words: 'div', attributes: form === 'head' ? { 'data-side': side ?? 'start' } : {} };
  const drawn = !(form === 'ground' && saveData);
  if (!drawn) container.attributes['data-saved'] = '';
  return { ...container, class: `scene scene-${form}`, drawn };
}

const pictureOptions = ({ form, side, words, saveData, ...options }) => options;

/** A scene as text, for a page built before it is served. `words` is the HTML that sits on it, or under a plate. */
export function sceneHtml(name, options = {}) {
  const scene = sceneOf(name, options), words = options.words ?? '';
  const image = scene.drawn ? pictureHtml(name, pictureOptions(options)) : '';
  const caption = words || options.form !== 'plate' ? `<${scene.words} class="scene-words">${words}</${scene.words}>` : '';
  return `<${scene.tag} ${written({ class: scene.class, ...scene.attributes })}>${image}${caption}</${scene.tag}>`;
}

/**
 * A scene worn by an element a page already holds, which keeps its children where they are: a window's heading takes
 * it as its head (`words`, the child that holds its eyebrow and title, sits on the scrim; the close keeps its place),
 * a card takes it as its ground (`words`, the children whose words lie on the painting, a list, so the walk measures
 * them). A plate is a figure of its own, drawn by `sceneElement`. Worn again, it is replaced.
 */
export function wearScene(doc, element, name, options = {}) {
  if (options.form === 'plate') throw new Error(`${name}: a plate is drawn by sceneElement, not worn`);
  if (options.form === 'head' && !options.words) throw new Error(`${name}: a head names the element that holds its words`);
  const holders = [options.words ?? []].flat();
  // A page that looks for its words after a first wear finds the painting that wear prepended.
  if (holders.some(holder => 'scene' in holder.dataset)) throw new Error(`${name}: its words are its own painting; find them before the first wear`);
  const scene = sceneOf(name, options);
  undressScene(element);
  element.classList.add(...scene.class.split(' '));
  for (const [attribute, value] of Object.entries(scene.attributes)) element.setAttribute(attribute, value);
  for (const holder of holders) holder.classList.add('scene-words');
  if (scene.drawn) {
    const image = pictureElement(doc, name, pictureOptions(options));
    image.dataset.scene = '';
    element.prepend(image);
  }
}

/**
 * A page's own head (docs/art/journeys/pages.md, the ladder): the band its HTML already holds (`.page-head`, with its
 * `data-side` and its `.scene-words`, so its room and its words are there at first paint) takes its painting. The band
 * spans the page's column, the lobby pages' 18 px gutters on a phone, up to 1280 px.
 */
export const PAGE_HEAD_SIZES = '(min-width: 1316px) 1280px, calc(100vw - 36px)';

export function wearPageHead(doc, name, options = {}) {
  const head = doc.querySelector('.page-head');
  if (!head) return;
  const words = head.querySelector(':scope > .scene-words');
  wearScene(doc, head, name, { form: 'head', side: head.dataset.side ?? 'start', words, sizes: PAGE_HEAD_SIZES, ...options });
}

/** Takes off a scene `wearScene` put on: the element is as it was, for a window that wears one in one state only. */
export function undressScene(element) {
  Array.from(element.children).find(child => 'scene' in child.dataset)?.remove();
  element.classList.remove('scene', ...SCENE_FORMS.map(form => `scene-${form}`));
  for (const attribute of ['data-side', 'data-saved']) element.removeAttribute(attribute);
  for (const child of element.children) child.classList?.remove('scene-words');
}

/** A scene as an element. `words` are the nodes (or strings) that sit on it, or under a plate. */
export function sceneElement(doc, name, options = {}) {
  const scene = sceneOf(name, options), words = options.words ?? [];
  const element = doc.createElement(scene.tag);
  element.className = scene.class;
  for (const [attribute, value] of Object.entries(scene.attributes)) element.setAttribute(attribute, value);
  if (scene.drawn) element.append(pictureElement(doc, name, pictureOptions(options)));
  if (words.length || options.form !== 'plate') {
    const holder = doc.createElement(scene.words);
    holder.className = 'scene-words';
    holder.append(...words.map(word => (typeof word === 'string' ? doc.createTextNode(word) : word)));
    element.append(holder);
  }
  return element;
}
