// The checks of accessibility the gate runs on every screen of the journeys (docs/art/ux-craft-plan.md, UX13;
// ADR 0197): what axe finds, with the elements it finds it on; forced colours; more contrast; the text spacing of
// WCAG 1.4.12; and text at 200%. Each in-page function uses nothing from this module, as `measureInPage` does.

/** WCAG 1.4.12: a line height of 1.5, 2 after a paragraph, a letter spacing of 0.12 and a word spacing of 0.16 times the size. */
export const TEXT_SPACING = `*:not(svg, svg *) { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; }
p { margin-bottom: 2em !important; }`;

/** WCAG 1.4.4: text at 200%. Every size of the pages is in rem, so the root's size is the reader's own setting. */
export const TEXT_DOUBLED = 'html { font-size: 200% !important; }';

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
 * Runs in the page, under forced colours. What forced colours take away and a player then misses:
 * - `focus`: the page, when no rule of it keeps an outline for the focus under forced colours. Forced colours drop
 *   `box-shadow`, so a ring drawn with one alone disappears.
 * - `icons`: a drawn icon inside a control painted in a colour of its own, not the control's: forced colours repaint
 *   the control's text, not an SVG's own `fill` or `stroke`.
 * - `state`: a control pressed, selected, checked or current that looks like its unpressed sibling: forced colours
 *   repaint the backgrounds, so a state carried by a background alone is lost.
 * Returns each as a list of selectors.
 */
export function forcedColoursInPage() {
  const modals = [...document.querySelectorAll('dialog:modal')];
  const scope = modals.at(-1) ?? document.body;
  const shown = node => {
    if (!node.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return false;
    const box = node.getBoundingClientRect();
    return box.width > 1 && box.height > 1;
  };
  const name = node => {
    if (node.id) return `#${node.id}`;
    const parent = node.parentElement;
    const tag = node.tagName.toLowerCase();
    const index = parent ? [...parent.children].filter(one => one.tagName === node.tagName).indexOf(node) + 1 : 1;
    return `${parent ? `${name(parent)} > ` : ''}${tag}:nth-of-type(${index})`;
  };
  const controls = [...scope.querySelectorAll('a[href], button, input:not([type="hidden"]), select, textarea, summary, [role="button"], [role="tab"], [tabindex]:not([tabindex="-1"])')]
    .filter(node => shown(node) && !node.disabled && !node.closest('[inert]'));

  // The state: a control in a state, against a sibling of the same kind that is not in it. Its outline is not read:
  // the ring is the focus's.
  const look = node => {
    const style = getComputedStyle(node);
    // A border is read only where it is drawn: the colour of a border of no width is nothing a player sees.
    const edge = side => (style[`border${side}Style`] === 'none' || style[`border${side}Width`] === '0px'
      ? 'none' : `${style[`border${side}Style`]} ${style[`border${side}Width`]} ${style[`border${side}Color`]}`);
    return [style.color, style.backgroundColor, edge('Top'), edge('Right'), edge('Bottom'), edge('Left'),
      style.textDecorationLine, style.fontWeight].join('|');
  };
  const inState = node => ['aria-pressed', 'aria-selected', 'aria-checked', 'aria-current'].some(attribute => {
    const value = node.getAttribute(attribute);
    return value && value !== 'false';
  });
  const state = [];
  for (const node of controls.filter(inState)) {
    const kin = [...(node.parentElement?.children ?? [])]
      .filter(one => one !== node && one.tagName === node.tagName && shown(one) && !inState(one) && one.className === node.className);
    if (kin.length && kin.every(one => look(one) === look(node))) state.push(name(node));
  }
  // The icons: each shape's paint, against the control's own colour.
  const icons = [];
  for (const node of controls) {
    const colour = getComputedStyle(node).color;
    for (const shape of node.querySelectorAll('svg path, svg circle, svg rect, svg line, svg polyline, svg polygon, svg ellipse')) {
      if (!shown(shape.closest('svg'))) continue;
      const style = getComputedStyle(shape);
      const paints = [style.fill, style.stroke].filter(paint => paint !== 'none' && !/rgba\(\d+, \d+, \d+, 0\)/.test(paint));
      if (paints.some(paint => paint !== colour)) { icons.push(name(node)); break; }
    }
  }

  // The ring: the focus is never moved here, since a walk's page answers it (a notice hides on a blur, a coach
  // moves on). A page keeps a ring under forced colours when a rule it holds draws an outline for `:focus-visible`
  // inside `@media (forced-colors: active)`; `focus` lists the page itself when none does.
  const rules = sheet => { try { return [...sheet.cssRules]; } catch { return []; } };
  const rings = list => list.some(rule => (rule.media && /forced-colors:\s*active/.test(rule.media.mediaText)
    ? rule.cssRules && [...rule.cssRules].some(inner => /:focus-visible/.test(inner.selectorText ?? '') && /outline/.test(inner.style?.cssText ?? ''))
    : rule.cssRules && rings([...rule.cssRules])));
  const focus = [...document.styleSheets].some(sheet => rings(rules(sheet))) ? [] : [location.pathname];
  return { focus, icons, state };
}

/**
 * Runs in the page, with the text spaced or doubled. What is lost:
 * - `wide`: the page scrolls sideways, at a width that should reflow (WCAG 1.4.10);
 * - `clipped`: words cut by a box that hides what overflows it, by selector;
 * - `overlaps`: two controls on top of each other.
 * The canvas of the battle is a game's field and exempt; the DOM around it is not.
 */
export function lossInPage() {
  const modals = [...document.querySelectorAll('dialog:modal')];
  const scope = modals.at(-1) ?? document.body;
  const shown = node => {
    if (!node.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return false;
    const box = node.getBoundingClientRect();
    return box.width > 1 && box.height > 1;
  };
  const name = node => {
    if (node.id) return `#${node.id}`;
    const parent = node.parentElement;
    const tag = node.tagName.toLowerCase();
    const index = parent ? [...parent.children].filter(one => one.tagName === node.tagName).indexOf(node) + 1 : 1;
    return `${parent ? `${name(parent)} > ` : ''}${tag}:nth-of-type(${index})`;
  };
  const hides = value => value === 'hidden' || value === 'clip';
  const clipped = [];
  for (const node of [scope, ...scope.querySelectorAll('*')]) {
    if (node.closest('svg, canvas, script, style, template') || !shown(node)) continue;
    const style = getComputedStyle(node);
    const across = hides(style.overflowX) && node.scrollWidth > node.clientWidth + 1;
    const down = hides(style.overflowY) && node.scrollHeight > node.clientHeight + 1;
    const ellipsis = style.textOverflow === 'ellipsis' && node.scrollWidth > node.clientWidth + 1;
    // Text of its own, not a box of boxes: the innermost box that cuts words is the one to name.
    const words = [...node.childNodes].some(child => child.nodeType === Node.TEXT_NODE && /[\p{L}\p{N}]/u.test(child.textContent));
    if ((across || down || ellipsis) && words) clipped.push(name(node));
  }
  const controls = [...scope.querySelectorAll('a[href], button, input:not([type="hidden"]), select, textarea, summary, [role="button"], [role="tab"], [tabindex]:not([tabindex="-1"])')]
    .filter(node => shown(node) && !node.disabled && !node.closest('[inert]'));
  const overlaps = [];
  for (let one = 0; one < controls.length; one++) {
    for (let other = one + 1; other < controls.length; other++) {
      const a = controls[one], b = controls[other];
      if (a.contains(b) || b.contains(a)) continue;
      const first = a.getBoundingClientRect(), second = b.getBoundingClientRect();
      const wide = Math.min(first.right, second.right) - Math.max(first.left, second.left);
      const tall = Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top);
      if (wide > 2 && tall > 2) overlaps.push(`${name(a)} | ${name(b)}`);
    }
  }
  const root = document.documentElement;
  return { wide: root.scrollWidth > root.clientWidth + 1, clipped, overlaps };
}

/**
 * Every check on the screen a page is on, axe already in it: axe on the rules of AA, then under more contrast the
 * rule of 7:1, then under forced colours, then with the text spaced, then doubled. Each emulation and style is put
 * back, even when a read throws. The focus is never moved: a walk's page answers it.
 */
export async function checkScreen(page, tags) {
  const result = { axe: await page.evaluate(axeFindingsInPage, { tags }) };
  try {
    await page.emulateMedia({ contrast: 'more' });
    result.contrast = await page.evaluate(axeFindingsInPage, { rules: [ENHANCED_CONTRAST] });
    await page.emulateMedia({ contrast: null, forcedColors: 'active' });
    result.forced = await page.evaluate(forcedColoursInPage);
  } finally {
    await page.emulateMedia({ contrast: null, forcedColors: null });
  }
  for (const [key, css] of [['spacing', TEXT_SPACING], ['doubled', TEXT_DOUBLED]]) {
    const tag = await page.addStyleTag({ content: css });
    try {
      await page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
      result[key] = await page.evaluate(lossInPage);
    } finally {
      await tag.evaluate(node => node.remove()).catch(() => {});
    }
  }
  await page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
  return result;
}

/** The lines a check found on one screen, or none: what a failed gate prints, a line an element. */
export function findingLines(screen, found) {
  const lines = [];
  for (const [check, value] of Object.entries(found)) {
    if (value === true) lines.push(`${screen}: ${check}`);
    else if (Array.isArray(value)) for (const one of value) lines.push(`${screen}: ${check} ${one}`);
    else if (value && typeof value === 'object') for (const [rule, nodes] of Object.entries(value)) {
      if (Array.isArray(nodes)) for (const node of nodes) lines.push(`${screen}: ${check} ${rule} ${node}`);
      else if (nodes === true) lines.push(`${screen}: ${check} ${rule}`);
    }
  }
  return lines;
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

/** The findings no list knows: what a change brought. `known` is the list's lines, `found` a walk's. */
export function unknownFindings(found, known) {
  const knows = new Set(known.map(line => (line.startsWith(UNSTEADY) ? line.slice(UNSTEADY.length) : line)));
  return [...new Set(found.map(knownLine))].filter(line => !knows.has(line)).sort();
}

/**
 * The lines a list knows that the walk no longer finds: what a change fixed. They fail the journey as a new finding
 * does, so the list is cut in the change that fixed them and a finding that comes back is seen. An unsteady line is
 * not held to it.
 */
export function goneFindings(found, known) {
  const finds = new Set(found.map(knownLine));
  return known.filter(line => !line.startsWith(UNSTEADY) && !finds.has(line)).sort();
}
