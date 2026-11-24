// The checks of accessibility the gate runs on every screen of the journeys (docs/art/ux-craft-plan.md, UX13;
// ADR 0197): what axe finds, with the elements it finds it on; forced colours; more contrast; the text spacing of
// WCAG 1.4.12; and text at 200%. Each in-page function uses nothing from this module, as `measureInPage` does.

/** WCAG 1.4.12: a line height of 1.5, 2 after a paragraph, a letter spacing of 0.12 and a word spacing of 0.16 times the size. */
export const TEXT_SPACING = `*:not(svg, svg *) { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; }
p { margin-bottom: 2em !important; }`;

/**
 * Runs in the page: WCAG 1.4.4, text at 200%. Many sizes of the pages are in pixels, which the root's size does not
 * reach, so every element's computed size is read first and then set, inline, to twice it: rem, em and px alike.
 * `undo` puts each element's own inline size back. The SVG and canvas are drawings, not text, and are left alone.
 */
export function doubleTextInPage({ undo = false } = {}) {
  if (undo) {
    for (const [node, [value, priority]] of globalThis.__doubledText ?? []) {
      if (value) node.style.setProperty('font-size', value, priority); else node.style.removeProperty('font-size');
    }
    delete globalThis.__doubledText;
    return 0;
  }
  const nodes = [document.documentElement, ...document.querySelectorAll('body, body *')]
    .filter(node => !node.closest('svg, canvas, script, style, template'));
  const sizes = nodes.map(node => Number.parseFloat(getComputedStyle(node).fontSize));
  globalThis.__doubledText = nodes.map(node => [node, [node.style.getPropertyValue('font-size'), node.style.getPropertyPriority('font-size')]]);
  nodes.forEach((node, index) => node.style.setProperty('font-size', `${sizes[index] * 2}px`, 'important'));
  return nodes.length;
}

/** The rule of WCAG 1.4.6 (7:1) that axe names, run under the system's wish for more contrast. */
export const ENHANCED_CONTRAST = 'color-contrast-enhanced';

/** Runs in the page, once axe is in it: the rules that fail, each with the elements it fails on, by selector. */
export async function axeFindingsInPage({ tags = null, rules = null }) {
  const only = rules ? { type: 'rule', values: rules } : { type: 'tag', values: tags };
  const found = await globalThis.axe.run(document, { runOnly: only, resultTypes: ['violations'] });
  return Object.fromEntries(found.violations.map(one => [one.id, one.nodes.map(node => node.target.join(' '))])
    .sort(([a], [b]) => a.localeCompare(b)));
}

/**
 * Runs in the page first: the helpers the checks below share, kept on `globalThis.__access` because a function
 * Playwright passes to the page takes nothing from this module. `scope` is the topmost modal window, or the body;
 * `shown` an element drawn larger than a pixel; `name` an element's selector by its id or its place;
 * `controls` what can be focused or pressed in the scope.
 */
export function accessKitInPage() {
  const scope = () => [...document.querySelectorAll('dialog:modal')].at(-1) ?? document.body;
  const shown = node => {
    if (!node.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return false;
    const box = node.getBoundingClientRect();
    return box.width > 1 && box.height > 1;
  };
  const name = node => {
    if (node.id) return '#' + node.id;
    const parent = node.parentElement;
    const tag = node.tagName.toLowerCase();
    const index = parent ? [...parent.children].filter(one => one.tagName === node.tagName).indexOf(node) + 1 : 1;
    const own = `${tag}:nth-of-type(${index})`;
    return parent ? name(parent) + ' > ' + own : own;
  };
  const controls = within => [...within.querySelectorAll('a[href], button, input:not([type="hidden"]), select, textarea, summary, [role="button"], [role="tab"], [tabindex]:not([tabindex="-1"])')]
    .filter(node => shown(node) && !node.disabled && !node.closest('[inert]'));
  globalThis.__access = { scope, shown, name, controls };
}

/**
 * Runs in the page, under forced colours, after `accessKitInPage`. What forced colours take away and a player then
 * misses:
 * - `focus`: the page, when no rule of it keeps an outline for the focus under forced colours. Forced colours drop
 *   `box-shadow`, so a ring drawn with one alone disappears.
 * - `icons`: a drawn icon inside a control painted in a colour of its own, not the control's: forced colours repaint
 *   the control's text, not an SVG's own `fill` or `stroke`.
 * - `state`: a control pressed, selected, checked or current that looks like its unpressed sibling: forced colours
 *   repaint the backgrounds, so a state carried by a background alone is lost.
 * Returns each as a list of selectors.
 */
export function forcedColoursInPage() {
  const { scope, shown, name, controls } = globalThis.__access;
  const all = controls(scope());

  // The state: a control in a state, against a sibling of the same kind that is not in it. Its outline is not read:
  // the ring is the focus's. A border is read only where it is drawn: the colour of a border of no width is nothing
  // a player sees.
  const edge = (style, side) => {
    const kind = style['border' + side + 'Style'], width = style['border' + side + 'Width'];
    return kind === 'none' || width === '0px' ? 'none' : [kind, width, style['border' + side + 'Color']].join(' ');
  };
  const look = node => {
    const style = getComputedStyle(node);
    return [style.color, style.backgroundColor, ...['Top', 'Right', 'Bottom', 'Left'].map(side => edge(style, side)),
      style.textDecorationLine, style.fontWeight].join('|');
  };
  const inState = node => ['aria-pressed', 'aria-selected', 'aria-checked', 'aria-current'].some(attribute => {
    const value = node.getAttribute(attribute);
    return value && value !== 'false';
  });
  const kin = node => [...(node.parentElement?.children ?? [])]
    .filter(one => one !== node && one.tagName === node.tagName && shown(one) && !inState(one) && one.className === node.className);
  const lost = node => { const others = kin(node); return others.length > 0 && others.every(one => look(one) === look(node)); };
  const state = all.filter(inState).filter(lost).map(node => name(node));

  // The icons: each shape's paint, against the control's own colour.
  const own = paint => paint !== 'none' && !/rgba\(\d+, \d+, \d+, 0\)/.test(paint);
  const repainted = (shape, colour) => {
    const style = getComputedStyle(shape);
    return [style.fill, style.stroke].filter(own).some(paint => paint !== colour);
  };
  const icons = all.filter(node => {
    const colour = getComputedStyle(node).color;
    return [...node.querySelectorAll('svg path, svg circle, svg rect, svg line, svg polyline, svg polygon, svg ellipse')]
      .some(shape => shown(shape.closest('svg')) && repainted(shape, colour));
  }).map(node => name(node));

  // The ring: the focus is never moved here, since a walk's page answers it (a notice hides on a blur, a coach
  // moves on). A page keeps a ring under forced colours when a rule it holds draws an outline for `:focus-visible`
  // inside `@media (forced-colors: active)`; `focus` lists the page itself when none does.
  const rules = sheet => { try { return [...sheet.cssRules]; } catch { return []; } };
  const ring = inner => /:focus-visible/.test(inner.selectorText ?? '') && /outline/.test(inner.style?.cssText ?? '');
  const rings = list => list.some(rule => {
    if (!rule.cssRules) return false;
    if (rule.media && /forced-colors:\s*active/.test(rule.media.mediaText)) return [...rule.cssRules].some(ring);
    return rings([...rule.cssRules]);
  });
  const focus = [...document.styleSheets].some(sheet => rings(rules(sheet))) ? [] : [location.pathname];
  return { focus, icons, state };
}

/**
 * Runs in the page, with the text spaced or doubled, after `accessKitInPage`. What is lost:
 * - `wide`: the page scrolls sideways, at a width that should reflow (WCAG 1.4.10);
 * - `clipped`: words cut by a box that hides what overflows it, by selector;
 * - `overlaps`: two controls on top of each other.
 * The canvas of the battle is a game's field and exempt; the DOM around it is not.
 */
export function lossInPage() {
  const { scope, shown, name, controls } = globalThis.__access;
  const within = scope();
  const hides = value => value === 'hidden' || value === 'clip';
  const cuts = node => {
    const style = getComputedStyle(node);
    const across = node.scrollWidth > node.clientWidth + 1;
    return (hides(style.overflowX) && across) || (hides(style.overflowY) && node.scrollHeight > node.clientHeight + 1)
      || (style.textOverflow === 'ellipsis' && across);
  };
  // Text of its own, not a box of boxes: the innermost box that cuts words is the one to name.
  const words = node => [...node.childNodes].some(child => child.nodeType === Node.TEXT_NODE && /[\p{L}\p{N}]/u.test(child.textContent));
  const clipped = [within, ...within.querySelectorAll('*')]
    .filter(node => !node.closest('svg, canvas, script, style, template') && shown(node) && words(node) && cuts(node))
    .map(node => name(node));

  const meet = (a, b) => {
    if (a.contains(b) || b.contains(a)) return false;
    const first = a.getBoundingClientRect(), second = b.getBoundingClientRect();
    const wide = Math.min(first.right, second.right) - Math.max(first.left, second.left);
    const tall = Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top);
    return wide > 2 && tall > 2;
  };
  const all = controls(within);
  const overlaps = all.flatMap((a, one) => all.slice(one + 1).filter(b => meet(a, b)).map(b => name(a) + ' | ' + name(b)));
  const root = document.documentElement;
  return { wide: root.scrollWidth > root.clientWidth + 1, clipped, overlaps };
}

/**
 * Every check on the screen a page is on, axe already in it: axe on the rules of AA, then under more contrast the
 * rule of 7:1, then under forced colours, then with the text spaced, then doubled. Each emulation and style is put
 * back, even when a read throws. The focus is never moved: a walk's page answers it.
 */
export async function checkScreen(page, tags) {
  await page.evaluate(accessKitInPage);
  const result = { axe: await page.evaluate(axeFindingsInPage, { tags }) };
  try {
    await page.emulateMedia({ contrast: 'more' });
    result.contrast = await page.evaluate(axeFindingsInPage, { rules: [ENHANCED_CONTRAST] });
    await page.emulateMedia({ contrast: null, forcedColors: 'active' });
    result.forced = await page.evaluate(forcedColoursInPage);
  } finally {
    await page.emulateMedia({ contrast: null, forcedColors: null });
  }
  const settle = () => page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
  const tag = await page.addStyleTag({ content: TEXT_SPACING });
  try {
    await settle();
    result.spacing = await page.evaluate(lossInPage);
  } finally {
    await tag.evaluate(node => node.remove()).catch(() => {});
  }
  try {
    await page.evaluate(doubleTextInPage);
    await settle();
    result.doubled = await page.evaluate(lossInPage);
  } finally {
    await page.evaluate(doubleTextInPage, { undo: true }).catch(() => {});
  }
  await page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
  return result;
}

/** The lines one rule of a check found: a line an element, or the rule alone when it holds of the screen. */
function ruleLines(prefix, rule, nodes) {
  if (Array.isArray(nodes)) return nodes.map(node => `${prefix} ${rule} ${node}`);
  return nodes === true ? [`${prefix} ${rule}`] : [];
}

/** The lines a check found on one screen, or none: what a failed gate prints, a line an element. */
export function findingLines(screen, found) {
  return Object.entries(found).flatMap(([check, value]) => {
    const prefix = `${screen}: ${check}`;
    if (value === true) return [prefix];
    if (Array.isArray(value)) return value.map(one => `${prefix} ${one}`);
    if (value && typeof value === 'object') return Object.entries(value).flatMap(([rule, nodes]) => ruleLines(prefix, rule, nodes));
    return [];
  });
}

/**
 * A finding's line as the list of what is known reads it: an element's place among its siblings left out, so a
 * list of a different length (a lobby's games, a round's casts) names the same finding.
 */
export function knownLine(line) {
  return line.replace(/:nth-of-type\(\d+\)/g, '');
}

/**
 * A line of the list marked `~ ` is unsteady: a finding some walks of the same tree make and others do not (a
 * creature on the field or a coach's card placed a frame sooner or later). It is never new, and never gone.
 */
export const UNSTEADY = '~ ';

/** Lines in the order of their code units, the order the list is kept in. */
const byText = (a, b) => (a < b ? -1 : Number(a > b));

/** The findings no list knows: what a change brought. `known` is the list's lines, `found` a walk's. */
export function unknownFindings(found, known) {
  const knows = new Set(known.map(line => (line.startsWith(UNSTEADY) ? line.slice(UNSTEADY.length) : line)));
  return [...new Set(found.map(knownLine))].filter(line => !knows.has(line)).sort(byText);
}

/**
 * The lines a list knows that the walk no longer finds: what a change fixed. They fail the journey as a new finding
 * does, so the list is cut in the change that fixed them and a finding that comes back is seen. An unsteady line is
 * not held to it.
 */
export function goneFindings(found, known) {
  const finds = new Set(found.map(knownLine));
  return known.filter(line => !line.startsWith(UNSTEADY) && !finds.has(line)).sort(byText);
}
