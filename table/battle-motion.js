// The movement every action shares (docs/art/storyboards/movement.md, BC5 of docs/art/battle-craft-plan.md):
// how far a figure draws back, how long two figures hold at contact, how far the struck one is thrown, how a
// fallen one goes down, how a standing one breathes, and how much the view trembles. Numbers only, at 1×: the
// scene divides every duration by the playback rate, and each beat stays inside the windows of battle-timing.js.
// The weight of a blow is the host's confirmed outcome (a cue's `changes`): the view decides nothing.

import { DRAW_MS, WINDUP_MS, IMPACT_MS, RETURN_MS } from './battle-timing.js';

/** The scale of a blow, light to heavy. Every number is a house number the film strips tuned. */
export const WEIGHTS = Object.freeze({
  light: Object.freeze({ draw: 20, lean: 8, hold: 0, stretch: .05, stop: 60, recoil: 12, tilt: 4, stay: 0, trauma: .25 }),
  medium: Object.freeze({ draw: 26, lean: 10, hold: 40, stretch: .065, stop: 90, recoil: 18, tilt: 6, stay: 40, trauma: .40 }),
  heavy: Object.freeze({ draw: 32, lean: 12, hold: 80, stretch: .08, stop: 120, recoil: 26, tilt: 8, stay: 80, trauma: .55 }),
});
const ORDER = ['light', 'medium', 'heavy'];

/** What a creature lost to an action, from the cue's changes: its health lost, and whether the action defeated it. */
export function lossOf(cue, id) {
  const change = (cue?.changes ?? []).find(one => String(one.creature) === String(id));
  return { lost: Math.max(0, -(change?.health ?? 0)), defeated: change?.defeated === true };
}

/** Light is 0 to 3 health lost, medium 4 to 6, heavy 7 or more, or a defeat. */
export function weightOf({ lost = 0, defeated = false } = {}) {
  if (defeated || lost >= 7) return 'heavy';
  return lost >= 4 ? 'medium' : 'light';
}

/** The creatures an action reached other than its caster. */
const others = (cue, actor) => (cue?.recipients ?? []).filter(id => String(id) !== String(actor));

/** The weight of the whole blow: the heaviest among the creatures it reached. It sets the anticipation and the stop. */
export function blowWeight(cue, actor) {
  const ranks = others(cue, actor).map(id => ORDER.indexOf(weightOf(lossOf(cue, id))));
  return ORDER[Math.max(0, ...ranks)];
}

// The lean into the blow, the ease of the stretch's release, the pause between the stop and the way home.
const STRIKE_LEAN = 9, STRETCH_MS = 120, RELEASE_MS = 80, RETURN_AFTER_MS = 60;
const RECOIL_OUT_MS = 280, TREMOR_SWING_MS = 30;

// The two colours the movement puts on a figure, both the palette's (docs/art/branding.css): the hit flash is
// `--brand-bone`, never the target marker's gold, and a fallen figure drains toward `--brand-line`, a cold grey.
export const BONE = 0xd8c7a7;
export const DRAINED = 0x52636b;
// The battle's own colours (docs/art/spell-vfx.md, "The battle's colours"), named once here. Soot is the palette's
// `--brand-soot`, the edge a pale stroke is read against; the three others are the owner's choices: blood, a deep
// brown red (#919), the ember, warm enough to read as fire (#934), and hide, bone in shadow, a flake of a figure's
// own hide that must not read as the bright chip of a blow (#943).
export const SOOT = 0x070d10;
export const BLOOD = 0x9a3b34;
export const EMBER = 0xe0763f;
export const HIDE = 0x8c7f68;

/**
 * The caster of a melee blow, in milliseconds from the start of the action: the draw back, the hold at its far
 * end, the strike, which always ends with the windup, the stretch over the strike's last 120 ms, then, after the
 * stop, the stretch released and the way home.
 */
export function meleeBeats(weight) {
  const scale = WEIGHTS[weight] ?? WEIGHTS.light;
  const strikeAt = DRAW_MS + scale.hold;
  return {
    draw: { at: 0, lasts: DRAW_MS, distance: scale.draw, lean: scale.lean },
    strike: { at: strikeAt, lasts: WINDUP_MS - strikeAt, lean: STRIKE_LEAN },
    stretch: { at: WINDUP_MS - STRETCH_MS, lasts: STRETCH_MS, amount: scale.stretch },
    stop: scale.stop,
    release: { at: WINDUP_MS + scale.stop, lasts: RELEASE_MS },
    home: { at: WINDUP_MS + scale.stop + RETURN_AFTER_MS, lasts: RETURN_MS, overshoot: 1.2 },
  };
}

/** A caster who does not lunge. A cast loads in the body and lets go at contact; a projectile's caster throws. */
export const CAST = Object.freeze({
  load: Object.freeze({ at: 0, lasts: 600, squash: .04 }),
  rise: Object.freeze({ at: 600, lasts: WINDUP_MS - 600, stretch: .04 }),
  release: Object.freeze({ at: WINDUP_MS, lasts: 360, overshoot: 1.2 }),
});
export const THROW = Object.freeze({
  load: Object.freeze({ at: 0, lasts: DRAW_MS, distance: 8, squash: .04 }),
  toss: Object.freeze({ at: DRAW_MS, lasts: 120, distance: 6, stretch: .04 }),
  home: Object.freeze({ at: DRAW_MS + 120, lasts: WINDUP_MS - DRAW_MS - 120 }),
});

/**
 * A projectile's flight (the craft book, "Arcing travel"): it leaves just over the thrower's head with the toss,
 * at an even pace, on a low arc, and lands as the windup ends (#909: eased in, it stood on the body for 300 ms
 * after the throw). `over` is how far above the top of the thrower it starts, in radii; `lift` is how high the
 * arc's control point stands above the middle of the flight, as a share of its length. Both are house numbers.
 * The lift is height, and height is up the screen: thrown across the field the flight bows; thrown straight up
 * or down it, the height lies along the line, and the flight is that line at the same even pace.
 */
export const FLIGHT = Object.freeze({ at: THROW.toss.at, lasts: WINDUP_MS - THROW.toss.at, over: .3, lift: .16 });
const FLIGHT_STEPS = 24;

/** Where a projectile is at a share `t` of its flight from one figure to another, and the angle it points at, in radians. */
export function flightAt(from, to, t) {
  const top = from.head ?? from.y - from.radius * 1.4;
  const start = { x: from.x, y: top - from.radius * FLIGHT.over };
  // One control point above the middle: a quadratic curve, by hand, since the effects kit holds no engine.
  const control = { x: (start.x + to.x) / 2, y: (start.y + to.y) / 2 - Math.hypot(to.x - start.x, to.y - start.y) * FLIGHT.lift };
  const at = share => {
    const rest = 1 - share;
    return { x: rest * rest * start.x + 2 * rest * share * control.x + share * share * to.x,
      y: rest * rest * start.y + 2 * rest * share * control.y + share * share * to.y };
  };
  // An even pace is an even length of the curve a moment, and the curve's own parameter is not that: it runs
  // fast where the control point pulls. So the length is measured in steps, and the share of it turned back
  // into the curve's parameter.
  const lengths = [0];
  for (let step = 1, last = start; step <= FLIGHT_STEPS; step++) {
    const now = at(step / FLIGHT_STEPS);
    lengths.push(lengths[step - 1] + Math.hypot(now.x - last.x, now.y - last.y));
    last = now;
  }
  const wanted = Math.min(1, Math.max(0, t)) * lengths[FLIGHT_STEPS];
  let step = 1;
  while (step < FLIGHT_STEPS && lengths[step] < wanted) step++;
  const span = lengths[step] - lengths[step - 1];
  const share = (step - 1 + (span > 0 ? (wanted - lengths[step - 1]) / span : 0)) / FLIGHT_STEPS, rest = 1 - share;
  return { ...at(share),
    angle: Math.atan2(rest * (control.y - start.y) + share * (to.y - control.y), rest * (control.x - start.x) + share * (to.x - control.x)) };
}

export const FLASH_MS = 80;

/**
 * How a struck figure answers, in milliseconds from the contact. `melee` says the blow was a body's: from a
 * distance the stop is half as long and the tremor half as wide. The flash says "lost health": none without a loss.
 */
export function reaction(loss, melee) {
  const scale = WEIGHTS[weightOf(loss)];
  const stop = melee ? scale.stop : scale.stop / 2;
  // The tremor fills the stop and ends with it, so the recoil never starts while the figure still trembles: whole
  // swings out and back, about 30 ms each way, their length worked back from the stop.
  const swings = Math.max(1, Math.round(stop / (TREMOR_SWING_MS * 2)));
  return {
    flash: loss.lost > 0 || loss.defeated ? FLASH_MS : 0,
    stop, tremor: { distance: melee ? 2 : 1, swing: stop / (swings * 2), swings },
    recoil: { at: stop, out: RECOIL_OUT_MS, stay: scale.stay, back: RECOIL_OUT_MS, distance: scale.recoil, tilt: scale.tilt },
  };
}

/**
 * A death, in milliseconds from the contact: thrown back with no return, held, then the fall, which brings the
 * figure back onto its own spot while it fades, and the last of it, which ends on the settled frame.
 */
export const DEATH = Object.freeze({
  recoil: Object.freeze({ lasts: 320, distance: WEIGHTS.heavy.recoil, tilt: WEIGHTS.heavy.tilt }),
  fall: Object.freeze({ at: 600, lasts: 760, height: .9, width: 1.05, tip: 6 }),
  settle: Object.freeze({ at: 1360, lasts: 200 }),
});
export const DEFEATED_ALPHA = .2;

/** The breath at rest: the height rises and falls by 1.5% from top to bottom, once every 3600 ms. */
export const BREATH = Object.freeze({ period: 3600, amount: .0075, apart: .37 });

/** What a standing figure's height is multiplied by, at a time of the ambient clock, for its place in the roster. */
export function breath(ms, place = 0) {
  return 1 + BREATH.amount * Math.sin((ms / BREATH.period + place * BREATH.apart) * Math.PI * 2);
}

/** The view's trauma: what a defeat adds, its ceiling, how fast it falls, and the reach of a full shake. */
export const TRAUMA = Object.freeze({ defeat: .10, ceiling: .6, fall: .25 / 120, reach: .05 });
/** A punch of the zoom, on a defeat or a critical hit. */
export const PUNCH = Object.freeze({ amount: .03, lasts: 200 });
/**
 * The slow push on a capstone (the craft book): how much closer the view comes at a push of full strength, and how
 * long it takes to let go once the impact lands. Both *house*: in more than the punch, since it builds over the
 * windup, and back a little slower than the punch comes back.
 */
export const PUSH = Object.freeze({ amount: .06, release: 240 });

/**
 * How far in the view stands, as a share of its fitted zoom, `elapsed` ms into a push that builds over `lasts` ms
 * to `strength` (Sine.InOut, then held); and, once `released` ms have passed since the impact let go of `from`,
 * how far it still stands (Quad.Out back to the fitted zoom).
 */
export function pushOf(elapsed, lasts, strength) {
  const share = Math.min(1, Math.max(0, elapsed / lasts));
  return PUSH.amount * strength * (1 - Math.cos(Math.PI * share)) / 2;
}
export function releaseOf(from, released) {
  const left = 1 - Math.min(1, Math.max(0, released / PUSH.release));
  return from * left * left;
}

/**
 * What an action adds to the trauma: each creature other than the caster that lost health adds by its loss, and
 * a defeat a little more. A blood cost, a heal, a ward, a blow that took nothing and a fizzle add nothing.
 */
export function traumaOf(cue, actor) {
  let added = 0;
  for (const id of others(cue, actor)) {
    const loss = lossOf(cue, id);
    if (loss.lost > 0 || loss.defeated) added += WEIGHTS[weightOf(loss)].trauma + (loss.defeated ? TRAUMA.defeat : 0);
  }
  return Math.min(TRAUMA.ceiling, added);
}

/** Trauma after some time, and how much of the full shake it gives: squared, so a small hit is barely felt. */
export const traumaAfter = (trauma, ms) => Math.max(0, trauma - TRAUMA.fall * ms);
export const shakeOf = trauma => trauma * trauma;

/**
 * A smooth wander between -1 and 1 on two axes, from a time alone: no random draw, so two films of one hit agree.
 * Two sines a axis, at periods that share no beat.
 */
export function wander(ms) {
  return {
    x: (Math.sin(ms * .0731) + Math.sin(ms * .1193 + 1.3)) / 2,
    y: (Math.sin(ms * .0877 + 2.1) + Math.sin(ms * .1361 + .7)) / 2,
  };
}

/** Whether the zoom punches: on a defeat, or on a critical hit that took health. A critical heal or ward does not. */
export function punches(cue, actor) {
  return others(cue, actor).some(id => lossOf(cue, id).defeated) || (cue?.roll?.critical === true && traumaOf(cue, actor) > 0);
}

/** The end of every beat of the movement, in milliseconds from the start of the action: what a window must hold. */
export function lastBeat(weight) {
  // The least loss that gives each weight.
  const lost = { light: 1, medium: 4, heavy: 7 }[weight] ?? 1;
  const beats = meleeBeats(weight), struck = reaction({ lost }, true);
  return Math.max(beats.home.at + beats.home.lasts, beats.release.at + beats.release.lasts,
    WINDUP_MS + struck.recoil.at + struck.recoil.out + struck.recoil.stay + struck.recoil.back,
    WINDUP_MS + DEATH.settle.at + DEATH.settle.lasts, CAST.release.at + CAST.release.lasts);
}
export const ACTION_END_MS = WINDUP_MS + IMPACT_MS;
