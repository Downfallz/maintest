// Less motion across every screen (docs/art/ux-craft-plan.md, UX14; ADR 0197): a journey walked with the system's
// wish for less motion records, screen by screen, every animation that moved something across the screen since the
// last screen. Under less motion travel becomes a fade and ambience stops, so a walk is held to none. Each in-page
// function uses nothing from this module, as `measureInPage` does.

/** What moves a thing across the screen. A fade, a colour or a size of text is not travel. */
export const TRAVEL = ['transform', 'translate', 'scale', 'rotate'];

/**
 * Runs in the page before any of its scripts (`addInitScript`): keeps on `globalThis.__journeyTravel` every CSS
 * transition, CSS animation and `element.animate` call that moves one of `TRAVEL` and lasts a millisecond or more.
 * The classic sheet's net cuts a duration to 0.01 ms under less motion: that plays nothing a player sees.
 */
export function watchTravelInPage(travel) {
  const seen = new Set();
  globalThis.__journeyTravel = seen;
  const name = node => {
    if (!(node instanceof Element)) return 'the page';
    if (node.id) return `#${node.id}`;
    const classes = [...node.classList].slice(0, 2).map(one => `.${one}`).join('');
    return `${node.tagName.toLowerCase()}${classes}`;
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
  const keep = (node, what) => seen.add(`${name(node)} ${what}`);
  // A transition's length, read from the element's style: one of 0.01 ms has ended before its event arrives.
  const transitionLength = (node, property) => {
    const style = getComputedStyle(node);
    const properties = style.transitionProperty.split(',').map(one => one.trim());
    const lengths = style.transitionDuration.split(',').map(one => Number.parseFloat(one) * (one.trim().endsWith('ms') ? 1 : 1000));
    const index = properties.includes(property) ? properties.lastIndexOf(property) : properties.lastIndexOf('all');
    return index < 0 ? 0 : lengths[index % lengths.length];
  };
  addEventListener('transitionrun', event => {
    if (travel.includes(event.propertyName) && transitionLength(event.target, event.propertyName) >= 1) keep(event.target, `transition ${event.propertyName}`);
  }, true);
  addEventListener('animationstart', event => {
    const animation = event.target.getAnimations?.().find(one => one.animationName === event.animationName);
    if (!animation || !lasting(animation)) return;
    for (const property of moved(animation.effect.getKeyframes())) keep(event.target, `animation ${event.animationName} ${property}`);
  }, true);
  const animate = Element.prototype.animate;
  Element.prototype.animate = function (...args) {
    const animation = animate.apply(this, args);
    if (lasting(animation)) for (const property of moved(animation.effect.getKeyframes())) keep(this, `animate ${property}`);
    return animation;
  };
}

/** Runs in the page: what travelled since the last read, sorted, and forgets it. */
export function takeTravelInPage() {
  const seen = globalThis.__journeyTravel;
  if (!seen) return null;
  const lines = [...seen].sort();
  seen.clear();
  return lines;
}

/**
 * What a walk under less motion found, a line a fault: a screen reached after something travelled, or a screen
 * still animating at rest (ambience that did not stop). Empty when the walk holds.
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
