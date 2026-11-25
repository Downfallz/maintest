// What one screen of a journey measures (docs/art/ux-craft-plan.md, UX2): the words a player reads, the filled
// actions, the pictures, the smallest text, the targets a finger could miss, the controls that overlap, what
// still animates at rest, the layout's shift, and what axe finds. Read in the page, on the window a player is
// in: the modal window when one is open, the whole page otherwise. Nothing here reads a clock, so the same
// screen gives the same line twice, the shift aside (`TIMED`); a paint is measured apart (`paint.mjs`).

/** The WCAG 2.2 AA rules, as axe tags them: the level the pages are held to (the plan, question 2). */
export const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/** The floor of text size the plan proposes for the match's frame, and the two target sizes WCAG names. */
export const TEXT_FLOOR = 11;
export const TARGET = 44;
export const TARGET_MINIMUM = 24;

/**
 * Runs in the page: `page.evaluate(measureInPage, { floor, target, minimum })`. It uses nothing from this module.
 * Returns the screen's line, without its name and without axe's findings.
 */
export function measureInPage({ floor, target, minimum }) {
  const modals = [...document.querySelectorAll('dialog:modal')];
  const scope = modals.at(-1) ?? document.body;
  const shown = node => {
    if (!node.checkVisibility({ opacityProperty: true, visibilityProperty: true, contentVisibilityAuto: true })) return false;
    const box = node.getBoundingClientRect();
    // A box of one pixel is text kept for a screen reader, not something a player sees.
    return box.width > 1 && box.height > 1;
  };

  // The words, and the size each is set in.
  let words = 0, smallest = null, small = 0;
  const walker = document.createTreeWalker(scope, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const parent = node.parentElement;
    if (!parent || parent.closest('script, style, noscript, template, option') || !shown(parent)) continue;
    const count = node.textContent.split(/\s+/).filter(word => /[\p{L}\p{N}]/u.test(word)).length;
    if (!count) continue;
    const size = Number.parseFloat(getComputedStyle(parent).fontSize);
    // Text set at no size is hidden from the eye, whatever its box.
    if (size < 1) continue;
    words += count;
    if (smallest === null || size < smallest) smallest = size;
    if (size < floor) small += count;
  }

  // The brand's gold, as the page declares it: an action is filled when its background is made of it, a plain colour or
  // a gradient that starts gold. An outline's edge, a gradient that starts transparent, is no fill (#1128).
  const probe = document.createElement('span');
  const golds = ['--gold', '--accent-strong', '--brand-gold', '--brand-gold-bright'].map(token => {
    probe.style.backgroundColor = ''; probe.style.backgroundColor = `var(${token})`;
    document.body.append(probe);
    const colour = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return colour;
  }).filter(colour => colour && colour !== 'rgba(0, 0, 0, 0)');

  const controls = [...scope.querySelectorAll('a[href], button, input:not([type="hidden"]), select, textarea, summary, [role="button"], [role="tab"], [role="link"], [tabindex]:not([tabindex="-1"])')]
    .filter(node => shown(node) && !node.disabled && node.getAttribute('aria-disabled') !== 'true' && !node.closest('[inert]'));
  const boxes = new Map(controls.map(node => [node, node.getBoundingClientRect()]));
  let filled = 0, under = 0, tiny = 0;
  for (const [node, box] of boxes) {
    const style = getComputedStyle(node);
    if (golds.some(gold => style.backgroundColor === gold || style.backgroundImage.includes(`gradient(${gold}`))) filled++;
    if (box.width < target || box.height < target) under++;
    if (box.width < minimum || box.height < minimum) tiny++;
  }

  // Two controls that share more than a hair of the screen, neither holding the other. What of a control is on the
  // screen is its box cut by every ancestor on its containing-block chain that scrolls or clips it: a name of the order
  // row scrolled under its Order button is cut by the row, not on top of the button (#1276). A box placed `fixed`
  // escapes every ancestor but one that transforms, filters or contains it; one placed `absolute`, every ancestor up
  // to its positioned one; a modal window or an open popover sits in the top layer, cut by none.
  const establishes = style => style.transform !== 'none' || style.filter !== 'none' || /paint|layout|strict|content/.test(style.contain);
  const seen = node => {
    const box = boxes.get(node), cut = { left: box.left, top: box.top, right: box.right, bottom: box.bottom };
    let escaping = ['absolute', 'fixed'].includes(getComputedStyle(node).position) ? getComputedStyle(node).position : null;
    for (let child = node, parent = node.parentElement; parent && parent !== document.documentElement; child = parent, parent = parent.parentElement) {
      if (child.matches(':modal, :popover-open')) break;
      const style = getComputedStyle(parent);
      if (escaping === 'absolute' && (style.position !== 'static' || establishes(style))) escaping = null;
      if (escaping === 'fixed' && establishes(style)) escaping = null;
      if (!escaping && !(style.overflowX === 'visible' && style.overflowY === 'visible')) {
        const clip = parent.getBoundingClientRect();
        if (style.overflowX !== 'visible') { cut.left = Math.max(cut.left, clip.left); cut.right = Math.min(cut.right, clip.right); }
        if (style.overflowY !== 'visible') { cut.top = Math.max(cut.top, clip.top); cut.bottom = Math.min(cut.bottom, clip.bottom); }
      }
      if (style.position === 'fixed') escaping = 'fixed';
      else if (style.position === 'absolute' && escaping !== 'fixed') escaping = 'absolute';
    }
    return cut;
  };
  let overlaps = 0;
  for (let one = 0; one < controls.length; one++) {
    for (let other = one + 1; other < controls.length; other++) {
      const a = controls[one], b = controls[other];
      if (a.contains(b) || b.contains(a)) continue;
      const first = seen(a), second = seen(b);
      const wide = Math.min(first.right, second.right) - Math.max(first.left, second.left);
      const tall = Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top);
      if (wide > 2 && tall > 2) overlaps++;
    }
  }

  // The pictures: an <img>, a painting set as a CSS background, a canvas. A data: URI is a texture, not a picture.
  const images = [...scope.querySelectorAll('img')].filter(shown);
  let backgrounds = 0;
  for (const node of [scope, ...scope.querySelectorAll('*')]) {
    if (!shown(node)) continue;
    for (const pseudo of [null, '::before', '::after']) {
      const image = getComputedStyle(node, pseudo).backgroundImage;
      if (/url\("?(?!data:)/.test(image)) backgrounds++;
    }
  }
  // The width of the file the browser chose, as its srcset declares it (#966). `naturalWidth` is already divided by the
  // density of the srcset's candidate, so for a `w` candidate it is the width `sizes` names, not the file's. The
  // candidate's own descriptor gives the file's width.
  // A candidate is a URL and its descriptor, the candidates split on their commas. A data: URI holds a comma of its
  // own: it matches no candidate, and is read by its naturalWidth, as before #966.
  const candidates = text => text.split(',').map(part => part.trim().split(/\s+/)).filter(([url]) => url)
    .map(([url, descriptor = '']) => ({ url: new URL(url, document.baseURI).href, descriptor }));
  const sentWidth = image => {
    const sources = image.parentElement?.tagName === 'PICTURE' ? [...image.parentElement.querySelectorAll('source'), image] : [image];
    const chosen = sources.flatMap(source => candidates(source.getAttribute('srcset') ?? '')).find(one => one.url === image.currentSrc);
    const width = /^(\d+)w$/.exec(chosen?.descriptor ?? ''), density = /^(\d+(?:\.\d+)?)x$/.exec(chosen?.descriptor ?? '');
    if (width) return Number(width[1]);
    return density ? image.naturalWidth * Number(density[1]) : image.naturalWidth;
  };
  // The width the picture is drawn at, in CSS pixels: `object-fit` draws it wider than its box (cover) or narrower
  // (contain), as the showcase's hero ground is drawn about 710 px wide in a 390 px box (#966).
  const drawnWidth = image => {
    const box = image.getBoundingClientRect(), width = Math.max(1, box.width), ratio = image.naturalWidth / Math.max(1, image.naturalHeight);
    const contain = Math.min(width, box.height * ratio);
    switch (getComputedStyle(image).objectFit) {
      case 'cover': return Math.max(width, box.height * ratio);
      case 'contain': return contain;
      case 'none': return image.naturalWidth;
      case 'scale-down': return Math.min(contain, image.naturalWidth);
      default: return width;
    }
  };
  // A file over twice the pixels it is drawn on is sent too large; one under them is drawn blurred. A drawing has no
  // pixels to send: an SVG is neither, whatever its own width says. A picture not loaded yet (lazy) is not read.
  const sized = images.filter(image => image.complete && image.naturalWidth > 0 && !/\.svg(?:[?#]|$)/.test(image.currentSrc))
    .map(image => ({ file: image.currentSrc, sent: sentWidth(image), drawn: drawnWidth(image) * devicePixelRatio }));
  // A file that another picture on the screen draws at its own size is not oversized for this one: the browser reuses
  // it from its cache. The showcase's eclipse is the hero's ground file, drawn again lower down (#1082, #1179).
  const needed = new Set(sized.filter(one => one.sent <= 2 * one.drawn).map(one => one.file));
  const oversized = sized.filter(one => one.sent > 2 * one.drawn && !needed.has(one.file)).length;
  // A pixel of rounding is not a blurred picture.
  const undersized = sized.filter(one => one.sent < one.drawn - 1).length;

  const shift = globalThis.__journeyShift?.value ?? 0;
  const root = document.documentElement;
  return {
    window: modals.length ? scope.id || 'dialog' : null,
    height: Math.round(modals.length ? scope.scrollHeight : root.scrollHeight),
    wide: root.scrollWidth > root.clientWidth,
    words,
    filled,
    pictures: { img: images.length, background: backgrounds, canvas: [...scope.querySelectorAll('canvas')].filter(shown).length, oversized, undersized },
    text: { smallest, under: small },
    targets: { all: controls.length, under, tiny },
    overlaps,
    animating: document.getAnimations().filter(one => one.playState === 'running').length,
    shift: Math.round(shift * 100) / 100,
  };
}

/** Runs in the page before any of its scripts: keeps the layout's shift, summed as the Core Web Vital sums it. */
export function watchShiftInPage() {
  const kept = { value: 0 };
  globalThis.__journeyShift = kept;
  try {
    new PerformanceObserver(list => {
      for (const entry of list.getEntries()) if (!entry.hadRecentInput) kept.value += entry.value;
    }).observe({ type: 'layout-shift', buffered: true });
  } catch { /* A browser without the entry measures no shift. */ }
}

/** Runs in the page, once axe is in it: the rules that fail, each with the number of elements it fails on. */
export async function axeInPage(tags) {
  const found = await globalThis.axe.run(document, { runOnly: { type: 'tag', values: tags }, resultTypes: ['violations'] });
  return Object.fromEntries(found.violations.map(one => [one.id, one.nodes.length]).sort(([a], [b]) => a.localeCompare(b)));
}

/**
 * The measures of a journey as JSON Lines: a header, then a line a screen, in the order they were reached. The
 * header's `blocked` lists the choices the walk made by script because the page left them out of reach.
 */
export function measuresLines({ journey, viewport, axe, lessMotion = false, blocked = [], screens }) {
  const header = { journey, viewport: `${viewport.width}x${viewport.height}`, axe, ...(lessMotion ? { lessMotion: true } : {}), blocked };
  return `${[header, ...screens.map(({ name, staged, measures }) => ({ screen: name, ...(staged ? { staged: true } : {}), ...measures }))].map(line => JSON.stringify(line)).join('\n')}\n`;
}

export function parseMeasures(text) {
  const [header, ...screens] = text.trim().split('\n').map(line => JSON.parse(line));
  return { header, screens };
}

/**
 * What a screen's line holds that depends on when things arrived: the layout's shift moves by a hundredth with
 * the order a page's parts come in. It is written, and left out when two walks are compared.
 */
export const TIMED = ['shift'];

/** What differs between two walks of one journey, a line a difference; empty when they measure the same. */
export function differences(first, second) {
  const lines = [];
  const names = one => one.map(screen => screen.screen ?? screen.name);
  if (names(first).join() !== names(second).join()) lines.push(`screens: ${names(first).join(', ')} | ${names(second).join(', ')}`);
  const flat = (value, path = '', into = {}) => {
    if (value && typeof value === 'object') for (const [key, inner] of Object.entries(value)) flat(inner, path ? `${path}.${key}` : key, into);
    else into[path] = value;
    return into;
  };
  for (const [index, screen] of first.entries()) {
    const other = second[index];
    if (!other) break;
    const mine = flat(screen.measures ?? screen), theirs = flat(other.measures ?? other);
    for (const key of new Set([...Object.keys(mine), ...Object.keys(theirs)])) {
      if (!TIMED.includes(key) && mine[key] !== theirs[key]) lines.push(`${screen.screen ?? screen.name}: ${key} ${mine[key]} | ${theirs[key]}`);
    }
  }
  return lines;
}
