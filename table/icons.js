// The pages' icon set (ADR 0198, docs/art/ux-craft.md#icons-drawn-in-the-identitys-line): one drawing a meaning,
// in place of the glyphs and emoji each system draws its own way. Carved like the identity: straight strokes,
// square ends, mitred corners, on a grid of 24. An icon takes the colour and the size of the text it sits in
// (`.icon`, identity.css) and says nothing to a screen reader: the control's name is its text or its label.
// It imports nothing, so the showcase's build reads it too.

/** Each icon as the path of its strokes. */
export const ICONS = Object.freeze({
  close: 'M6 6l12 12M18 6L6 18',
  check: 'M5 12.5l4.5 4.5L19 7',
  menu: 'M4 7h16M4 12h16M4 17h11',
  previous: 'M14.5 5l-7 7 7 7',
  next: 'M9.5 5l7 7-7 7',
  open: 'M9 6h9v9M18 6L6 18',
  settings: 'M12 3l7.8 4.5v9L12 21l-7.8-4.5v-9zM12 9l3 3-3 3-3-3z',
  reaction: 'M4 5h16v11h-9l-5 4v-4H4z',
  sound: 'M4 9.5h4l5-4.5v14l-5-4.5H4zM17 8.5l2.5 3.5-2.5 3.5',
  reset: 'M19 12a7 7 0 1 1-2.5-5.4M19 4v5h-5',
  history: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM12 8v4.5l3 2',
  bell: 'M6 17v-6a6 6 0 0 1 12 0v6l1.5 1.5h-15zM10 21h4',
  leave: 'M10 4H5v16h5M9 12h11M16 8l4 4-4 4',
  critical: 'M12 3l2.2 6.8L21 12l-6.8 2.2L12 21l-2.2-6.8L3 12l6.8-2.2z',
  health: 'M12 20l-8-8V7.5L7.5 4 12 8l4.5-4L20 7.5V12z',
  energy: 'M13.5 3L6 13.5h5.5l-1 7.5L18 10.5h-5.5z',
  defense: 'M12 3l7.5 3v6.5L12 21l-7.5-8.5V6z',
  initiative: 'M6 13l6-6 6 6M6 18.5l6-6 6 6',
  stun: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM6.5 17.5l11-11',
  damage: 'M20 4l-2 6.5-7.5 7.5-4-4L14 6.5zM5.5 12.5l6 6M8.5 15.5l-4 4',
});

const SVG = 'http://www.w3.org/2000/svg';
const ATTRIBUTES = { class: 'icon', viewBox: '0 0 24 24', width: '24', height: '24', 'aria-hidden': 'true', focusable: 'false' };

function pathOf(name) {
  const path = ICONS[name];
  if (!path) throw new Error(`${name} is not an icon of the set`);
  return path;
}

/** An icon as text, for a page's HTML. */
export function iconHtml(name) {
  const path = pathOf(name);
  return `<svg ${Object.entries(ATTRIBUTES).map(([attribute, value]) => `${attribute}="${value}"`).join(' ')}><path d="${path}"/></svg>`;
}

/** An icon as an element, for a page that builds itself. */
export function iconElement(doc, name) {
  const path = pathOf(name), svg = doc.createElementNS(SVG, 'svg'), drawn = doc.createElementNS(SVG, 'path');
  for (const [attribute, value] of Object.entries(ATTRIBUTES)) svg.setAttribute(attribute, value);
  drawn.setAttribute('d', path);
  svg.append(drawn);
  return svg;
}
