// The ground's numbers (docs/art/storyboards/ground.md, BC9 of docs/art/battle-craft-plan.md): the air that moves
// over the field at rest, the depth its layers take when the view moves, and the light that answers a blow. Every
// one is a pure function of what it is handed: the ambient clock, the field, the host's confirmed outcome. Nothing
// here draws a random number or reads a wall clock, so two films of the field at rest are the same film.
import { lossOf, weightOf } from './battle-motion.js';

/** The source painting, `battle-art/eclipse-battle.webp`: where its eclipse and the rows of its horizon are. */
export const PAINTING = Object.freeze({ width: 1672, height: 941, eclipse: Object.freeze({ x: 846, y: 135, radius: 112 }) });

/** The tints the ground rests at: the painting and its plates, and the arch cut out of it. */
export const REST_TINT = Object.freeze({ painting: 0xb5bbc2, arch: 0xa5b1b3 });

/**
 * The band of fog on the horizon: the painting's rows it lies between, its slate (taken from the painted mist,
 * never a colour that means something on the page), and its drift in pixels a millisecond. `tile` is the texture
 * drawn at boot, tileable across; `puffs` are its soft ellipses, each [x, y, radius across, radius down, alpha].
 */
export const FOG = Object.freeze({
  rows: Object.freeze([330, 470]), tint: 0x8aa29c, alpha: .30, drift: .003,
  tile: Object.freeze({ width: 256, height: 64 }),
  puffs: Object.freeze([[18, 30, 58, 15, .55], [70, 38, 46, 12, .5], [112, 26, 64, 14, .6], [150, 40, 40, 11, .45],
    [188, 30, 56, 15, .55], [226, 36, 44, 12, .5], [128, 34, 96, 9, .35]].map(Object.freeze)),
});

/** How far the fog has drifted at a time of the ambient clock. With less motion it stands still. */
export const fogDrift = (clock, reduced = false) => reduced ? 0 : clock * FOG.drift;

// The steps that spread the embers: one for the column, another for the place along the rise (Roberts' R2
// sequence). One step for both would make the place follow the column, and the embers would rise on one slant line.
const ACROSS = .7549, ALONG = .5698;
const fraction = value => value - Math.floor(value);

/**
 * The embers that rise from the cracked floor, in place of the gold motes: far ones small, dim and slow near the
 * horizon, near ones larger and faster. `rows` is the rise, from a row of the painting up to another; `speed` is a
 * share of the field's height a second; `size` is a width in pixels. None rises over the sky.
 */
export const EMBERS = Object.freeze({
  tint: 0xd18d51, sway: 3,
  far: Object.freeze({ count: 16, size: 2, alpha: .30, speed: .012, rows: Object.freeze([600, 420]) }),
  near: Object.freeze({ count: 8, size: 4, alpha: .40, speed: .025, rows: Object.freeze([900, 480]) }),
  glow: Object.freeze({ least: .6, fullAt: 12 }),
});
export const EMBER_COUNT = EMBERS.far.count + EMBERS.near.count;

/** How bright the embers are at a round: the floor breaks as the rounds pass, and its embers with it. */
export function emberGlow(round) {
  const passed = Math.min(1, Math.max(0, ((round ?? 1) - 1) / (EMBERS.glow.fullAt - 1)));
  return EMBERS.glow.least + (1 - EMBERS.glow.least) * passed;
}

/**
 * Where one ember is at a time of the ambient clock. `field` is the field's size and how the painting lies on
 * it: `top`, the y of the painting's first row, and `scale`, pixels a source row. The first embers are the far ones.
 */
export function emberAt(index, clock, field) {
  const far = index < EMBERS.far.count;
  const kind = far ? EMBERS.far : EMBERS.near;
  const [from, to] = kind.rows;
  // The rise lasts what its length takes at the kind's pace, so a taller field does not make an ember faster.
  const lasts = Math.max(1, (from - to) * field.scale / (kind.speed * field.height) * 1000);
  const progress = fraction(clock / lasts + index * ALONG);
  const period = 5000 + (index * 977) % 4000;
  return {
    far,
    x: fraction(.31 + index * ACROSS) * field.width + EMBERS.sway * Math.sin(clock / period * Math.PI * 2 + index),
    y: field.top + (from - (from - to) * progress) * field.scale,
    size: kind.size,
    // It fades in and out along its rise: none appears or vanishes at once.
    alpha: kind.alpha * Math.sin(Math.PI * progress),
  };
}

/**
 * How deep each layer lies: 1 moves with the floor and the figures, less moves less when the view does. The
 * painting carries the floor, and the arch is cut out of it: both stay at 1, or they would slide under the
 * figures or show their painted twin.
 */
export const DEPTH = Object.freeze({ painting: 1, floor: 1, arch: 1, sky: .5, fog: .75, far: .8, near: 1 });

/**
 * Where a layer is laid while the view is off its fitted place by `view.x`, `view.y` and zoomed by `view.zoom`
 * over its fitted zoom: shifted against the offset and scaled against the zoom about `centre`, so that on the
 * screen it moves and zooms by its factor only. Returns the shift, and the scale over the layer's own.
 */
export function depthShift(factor, view) {
  const zoom = view.zoom || 1;
  // What lies on the floor is the camera's alone.
  if (factor === 1) return { x: 0, y: 0, scale: 1 };
  return { x: view.x * (1 - factor), y: view.y * (1 - factor), scale: (1 + factor * (zoom - 1)) / zoom };
}
export function depthPlace(point, factor, view, centre) {
  const { x, y, scale } = depthShift(factor, view);
  return { x: centre.x + x + scale * (point.x - centre.x), y: centre.y + y + scale * (point.y - centre.y), scale };
}

/**
 * The light that answers a blow: the eclipse's corona brightens in the family's colour and the ground's tint goes
 * part of the way to it, once, and is gone within 320 ms of the contact, as every bright beat is. The corona is
 * a ring around the disc, in shares of the disc's radius: nothing inside `inner`, brightest at `peak`, nothing
 * past `outer`. The disc stays dark: a light that filled it read as a second sun.
 */
export const LIGHT = Object.freeze({ rise: 100, fall: 200, alpha: .8, tint: .5,
  ring: Object.freeze({ inner: .9, peak: 1.08, outer: 1.7 }),
  strengths: Object.freeze({ light: 0, medium: .5, heavy: 1 }) });

/**
 * How much of its strength the light shows, a time after the contact at 1×: up fast and slowing (`Quad.Out`),
 * then down slowly at first (`Sine.In`), on one clock, so no step is lost between the rise and the fall.
 */
export function lightAt(ms) {
  if (ms <= 0 || ms >= LIGHT.rise + LIGHT.fall) return 0;
  if (ms < LIGHT.rise) { const left = 1 - ms / LIGHT.rise; return 1 - left * left; }
  return Math.cos((ms - LIGHT.rise) / LIGHT.fall * Math.PI / 2);
}

/** The families the ground answers in, and each one's colour (the palette of `battle/src/spell-effects.ts`). */
export const LIGHT_COLOURS = Object.freeze({ physical: 0xd2bbac, biological: 0xadc777, elemental: 0x8dcfe5, necromantic: 0x85c9c2, astral: 0xb9a6e5 });

/**
 * How strongly the ground answers an action: by the health the host confirmed lost by the creatures other than
 * the caster, added up, on the movement's own bands. A base action's few points, a heal, a ward, a blood cost
 * alone, a fizzle and an action of no family get nothing: the place answers harm, and not every turn.
 */
export function lightOf(cue, actor) {
  if (!cue || cue.kind === 'fizzle' || !Object.hasOwn(LIGHT_COLOURS, cue.visual?.family ?? '')) return { strength: 0, colour: null };
  let lost = 0, defeated = false;
  for (const id of new Set((cue.recipients ?? []).map(String))) {
    if (id === String(actor)) continue;
    const loss = lossOf(cue, id);
    lost += loss.lost;
    defeated ||= loss.defeated;
  }
  const strength = LIGHT.strengths[weightOf({ lost, defeated })];
  return { strength, colour: strength > 0 ? LIGHT_COLOURS[cue.visual.family] : null };
}

/** A colour a share of the way from one to another, channel by channel. */
export function mix(from, to, share) {
  const t = Math.min(1, Math.max(0, share));
  const channel = shift => Math.round(((from >> shift) & 255) + (((to >> shift) & 255) - ((from >> shift) & 255)) * t);
  return (channel(16) << 16) | (channel(8) << 8) | channel(0);
}
