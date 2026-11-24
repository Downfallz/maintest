// Less motion across every screen (docs/art/ux-craft-plan.md, UX14; ADR 0197): a journey walked with the system's wish for
// less motion records, screen by screen, every animation that moved something across the screen since the last screen.
// Under less motion travel becomes a fade and ambience stops, so a walk is held to none. The in-page function uses
// nothing from this module, as `measureInPage` does.

/** What moves a thing across the screen. A fade, a colour or a size of text is not travel. */
export const TRAVEL = ['transform', 'translate', 'scale', 'rotate'];

/** The page's way out: what the watch sees leaves the page at once, so a navigation or a second person loses nothing. */
export const TRAVEL_BINDING = '__journeyTravelKeep';

/**
 * Runs in the page before any of its scripts (`addInitScript`): hands `binding` every CSS transition, CSS animation and
 * `animate` call that moves one of `travel` and lasts a millisecond or more, on an element or on its `::before` and
 * `::after`, and every scroll asked to be smooth. The classic sheet's net cuts a duration to 0.01 ms under less motion:
 * that plays nothing a player sees. Marks the page as watched, so a page without the watch is not read as still.
 */
export function watchTravelInPage({ travel, binding }) {
  globalThis.__journeyTravelWatched = true;
  const name = (node, pseudo = '') => {
    if (!(node instanceof Element)) return 'the page';
    if (node.id) return `#${node.id}${pseudo}`;
    const classes = [...node.classList].slice(0, 2).map(one => `.${one}`).join('');
    return `${node.tagName.toLowerCase()}${classes}${pseudo}`;
  };
  // A property travels when its keyframes do not all hold the same value: a frame that leaves it out takes the
  // element's own, so it counts as a different one.
  const moved = frames => travel.filter(property => {
    const values = frames.map(frame => frame[property]);
    return values.some(value => value !== undefined) && new Set(values).size > 1;
  });
  const lasting = animation => {
    const timing = animation.effect?.getComputedTiming?.();
    return Boolean(timing) && (timing.activeDuration === Infinity || timing.activeDuration >= 1);
  };
  const keep = (node, what, pseudo = '') => globalThis[binding]?.(`${name(node, pseudo)} ${what}`);
  // A transition's length, read from the style of the element or its pseudo-element: one of 0.01 ms has ended before
  // its event arrives.
  const transitionLength = (node, property, pseudo) => {
    const style = getComputedStyle(node, pseudo || null);
    const properties = style.transitionProperty.split(',').map(one => one.trim());
    const lengths = style.transitionDuration.split(',').map(one => Number.parseFloat(one) * (one.trim().endsWith('ms') ? 1 : 1000));
    // The last entry naming the property, or `all`, wins, as in the cascade of a transition list.
    const index = Math.max(properties.lastIndexOf(property), properties.lastIndexOf('all'));
    return index < 0 ? 0 : lengths[index % lengths.length];
  };
  addEventListener('transitionrun', event => {
    if (travel.includes(event.propertyName) && transitionLength(event.target, event.propertyName, event.pseudoElement) >= 1) {
      keep(event.target, `transition ${event.propertyName}`, event.pseudoElement);
    }
  }, true);
  addEventListener('animationstart', event => {
    // `subtree` is what lists a pseudo-element's animations; the target and the pseudo pick this one among them.
    const animation = event.target.getAnimations?.({ subtree: true }).find(one => one.animationName === event.animationName
      && one.effect?.target === event.target && (one.effect?.pseudoElement ?? '') === (event.pseudoElement ?? ''));
    if (!animation || !lasting(animation)) return;
    for (const property of moved(animation.effect.getKeyframes())) keep(event.target, `animation ${event.animationName} ${property}`, event.pseudoElement);
  }, true);
  const animate = Element.prototype.animate;
  Element.prototype.animate = function (keyframes, options) {
    const animation = animate.call(this, keyframes, options);
    if (lasting(animation)) for (const property of moved(animation.effect.getKeyframes())) keep(this, `animate ${property}`, options?.pseudoElement ?? '');
    return animation;
  };
  // A smooth scroll glides the page however the sheets cut their durations.
  const smooth = (owner, method) => {
    const original = owner[method];
    owner[method] = function (...args) {
      if (args[0]?.behavior === 'smooth') keep(this, `${String(method)} smooth`);
      return original.apply(this, args);
    };
  };
  for (const method of ['scrollIntoView', 'scrollTo', 'scrollBy', 'scroll']) smooth(Element.prototype, method);
  for (const method of ['scrollTo', 'scrollBy', 'scroll']) smooth(window, method);
}

/** Runs in the page: whether the watch is on it. A page without it would read as still. */
export function travelWatchedInPage() {
  return globalThis.__journeyTravelWatched === true;
}

/**
 * Watch every page of a browser's context: returns `take()`, what travelled on any of its pages since the last take,
 * sorted, once each.
 */
export async function watchTravel(context) {
  const kept = new Set();
  await context.exposeBinding(TRAVEL_BINDING, (_source, line) => { kept.add(line); });
  await context.addInitScript(watchTravelInPage, { travel: TRAVEL, binding: TRAVEL_BINDING });
  return {
    take() {
      const lines = [...kept].sort((a, b) => a.localeCompare(b));
      kept.clear();
      return lines;
    },
  };
}

/**
 * What a walk under less motion found, a line a fault: a screen reached after something travelled, or a screen
 * still animating at rest. Empty when the walk holds.
 */
export function lessMotionFaults(screens) {
  if (!Array.isArray(screens) || !screens.length) throw new Error('No screens to read: a walk under less motion reaches at least one.');
  return screens.flatMap(screen => {
    const measures = screen.measures ?? screen;
    const name = screen.name ?? screen.screen;
    if (!Array.isArray(measures.travel)) throw new Error(`${name} was not walked under less motion: it has no travel.`);
    return [
      ...measures.travel.map(line => `${name}: ${line}`),
      ...(measures.animating ? [`${name}: ${measures.animating} still animating at rest`] : []),
    ];
  });
}
