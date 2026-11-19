import Phaser from 'phaser';
import { BONE, CAST, DEATH, DEFEATED_ALPHA, DRAINED, THROW, meleeBeats, reaction } from '../../table/battle-motion.js';
import type { Placement } from './types';

// How a figure's body plays an action (docs/art/storyboards/movement.md): it loads, strikes, is struck, falls.
// One picture and no skeleton, so everything is a tween of its place, its lean, its scale about the feet (the
// image's origin is its ground anchor), its tint and its alpha. Every beat is a tween or a counter of the scene:
// the cancel path's `killAll` ends them all, and the scene then puts the figure back (`clearMotion`).

/** What moves: the picture, its shadow, where it stands, and its own scale, which a stretch returns to. */
export interface Body { image: Phaser.GameObjects.Image; shadow: Phaser.GameObjects.Ellipse; home: Placement; scale: number }
/** The line of a blow: the unit vector from the caster's home to the target's, and where the caster stops. */
export interface Line { x: number; y: number; dx: number; dy: number }

const WHITE = 0xffffff;
const side = (line: Line) => line.dx < 0 ? -1 : 1;

/** A stretch along the main axis of the line, the other axis narrowed to keep the volume. A negative amount squashes. */
function stretched(body: Body, line: Line | null, amount: number) {
  const along = body.scale * (1 + amount), across = body.scale / (1 + amount);
  return !line || Math.abs(line.dy) >= Math.abs(line.dx) ? { scaleX: across, scaleY: along } : { scaleX: along, scaleY: across };
}
const scaleOf = ({ scaleX, scaleY }: { scaleX: number; scaleY: number }) => [scaleX, scaleY] as const;
const rested = (body: Body) => ({ scaleX: body.scale, scaleY: body.scale });

/** The caster of a melee blow: the draw back, the hold, the strike that lands as the windup ends, the stretch into it. */
export function meleeWindup(scene: Phaser.Scene, body: Body, line: Line, weight: string, rate: number) {
  const beats = meleeBeats(weight), { image, home } = body, lean = side(line);
  scene.tweens.add({ targets: image, x: home.x - line.dx * beats.draw.distance, y: home.y - line.dy * beats.draw.distance,
    angle: -lean * beats.draw.lean, duration: beats.draw.lasts / rate, ease: 'Cubic.Out' });
  scene.tweens.add({ targets: image, x: line.x, y: line.y, angle: lean * beats.strike.lean,
    delay: beats.strike.at / rate, duration: beats.strike.lasts / rate, ease: 'Cubic.In' });
  scene.tweens.add({ targets: image, ...stretched(body, line, beats.stretch.amount),
    delay: beats.stretch.at / rate, duration: beats.stretch.lasts / rate, ease: 'Quad.In' });
}

/** After the contact: the caster holds for the stop, lets its stretch go, then goes home a little past it. */
export function meleeReturn(scene: Phaser.Scene, body: Body, line: Line | null, weight: string, rate: number) {
  const beats = meleeBeats(weight), { image, home } = body;
  // The contact pose is the one the eye keeps, and the strike eases in: its last frame carries the most travel.
  // So a windup still in flight when the impact arrives is put on its last frame, not stopped a frame short.
  const lunging = line !== null && scene.tweens.getTweensOf(image).length > 0;
  scene.tweens.killTweensOf(image);
  if (lunging) image.setPosition(line.x, line.y).setAngle(side(line) * beats.strike.lean).setScale(...scaleOf(stretched(body, line, beats.stretch.amount)));
  scene.tweens.add({ targets: image, ...rested(body), delay: beats.stop / rate, duration: beats.release.lasts / rate, ease: 'Quad.Out' });
  scene.tweens.add({ targets: image, x: home.x, y: home.y, angle: 0, delay: (beats.home.at - beats.release.at + beats.stop) / rate,
    duration: beats.home.lasts / rate, ease: 'Back.Out', easeParams: [beats.home.overshoot] });
}

/** A cast: the body loads where it stands, and rises to its tallest on the contact frame. No move. */
export function castWindup(scene: Phaser.Scene, body: Body, rate: number) {
  scene.tweens.add({ targets: body.image, ...stretched(body, null, -CAST.load.squash), duration: CAST.load.lasts / rate, ease: 'Sine.InOut' });
  scene.tweens.add({ targets: body.image, ...stretched(body, null, CAST.rise.stretch),
    delay: CAST.rise.at / rate, duration: CAST.rise.lasts / rate, ease: 'Cubic.In' });
}

/** A projectile's caster: it loads away from its target, throws as the projectile leaves, and is home when it lands. */
export function throwWindup(scene: Phaser.Scene, body: Body, line: Line, rate: number) {
  const { image, home } = body, { load, toss } = THROW;
  scene.tweens.add({ targets: image, x: home.x - line.dx * load.distance, y: home.y - line.dy * load.distance,
    ...stretched(body, null, -load.squash), duration: load.lasts / rate, ease: 'Cubic.Out' });
  scene.tweens.add({ targets: image, x: home.x + line.dx * toss.distance, y: home.y + line.dy * toss.distance,
    ...stretched(body, line, toss.stretch), delay: toss.at / rate, duration: toss.lasts / rate, ease: 'Quad.Out' });
  scene.tweens.add({ targets: image, x: home.x, y: home.y, ...rested(body),
    delay: THROW.home.at / rate, duration: THROW.home.lasts / rate, ease: 'Sine.InOut' });
}

/** After the contact, a caster who did not lunge lets go: back to rest, a little past it. */
export function castRelease(scene: Phaser.Scene, body: Body, risen: boolean, rate: number) {
  const { image, home } = body;
  // A cast is at its tallest on the contact frame; a thrower is already home.
  const loading = risen && scene.tweens.getTweensOf(image).length > 0;
  scene.tweens.killTweensOf(image);
  if (loading) image.setScale(...scaleOf(stretched(body, null, CAST.rise.stretch)));
  scene.tweens.add({ targets: image, x: home.x, y: home.y, angle: 0, ...rested(body),
    duration: CAST.release.lasts / rate, ease: 'Back.Out', easeParams: [CAST.release.overshoot] });
}

// The figure filled flat in bone, then its tint cleared: a counter, so the cancel path's `killAll` ends it and
// `clearMotion` clears the tint. It is the scene's own flash: it plays before the effects kit has arrived.
function flash(scene: Phaser.Scene, body: Body, ms: number) {
  const { image } = body;
  image.setTint(BONE).setTintMode(Phaser.TintModes.FILL);
  scene.tweens.addCounter({ from: 0, to: 1, duration: ms, onComplete: () => { if (image.active) image.clearTint(); } });
}

// The stop, on the struck figure: it trembles across the line of the blow for as long as the two figures hold.
function tremble(scene: Phaser.Scene, body: Body, line: Line, tremor: { distance: number; swing: number; swings: number }, rate: number) {
  const { image, home } = body;
  scene.tweens.add({ targets: image, x: home.x - line.dy * tremor.distance, y: home.y + line.dx * tremor.distance,
    duration: tremor.swing / rate, yoyo: true, repeat: tremor.swings - 1 });
}

/**
 * A struck figure: the flash when it lost health, the stop, then the recoil along the line of the blow, out, held
 * and back. `melee` says a body struck it; from a distance the stop is shorter. `flashes` is false when the
 * spell's own staging flashes the figure: one flash a figure an action (the craft book, "Flash limits").
 */
export function struck(scene: Phaser.Scene, body: Body, line: Line, loss: { lost: number; defeated: boolean }, melee: boolean, rate: number, flashes = true) {
  const { image, home } = body, answer = reaction(loss, melee), { recoil } = answer;
  if (answer.flash && flashes) flash(scene, body, answer.flash / rate);
  tremble(scene, body, line, answer.tremor, rate);
  // The recoil leaves from home and comes back to it, wherever the tremor stands when the stop ends: at 2× a swing
  // is shorter than a frame, and a recoil that left from the tremor's far end would come back to it.
  scene.tweens.add({ targets: image, x: { from: home.x, to: home.x + line.dx * recoil.distance }, y: { from: home.y, to: home.y + line.dy * recoil.distance },
    angle: { from: 0, to: side(line) * recoil.tilt },
    delay: recoil.at / rate, duration: recoil.out / rate, hold: recoil.stay / rate, yoyo: true, ease: 'Quad.Out' });
}

const mix = (from: number, to: number, share: number) => {
  const channel = (shift: number) => Math.round(((from >> shift) & 255) + (((to >> shift) & 255) - ((from >> shift) & 255)) * share) << shift;
  return channel(16) | channel(8) | channel(0);
};

/**
 * A death (the craft book, "Death"): the flash, the stop, the recoil with no return, a hold, then the fall. The
 * figure slumps and tips, sinks back onto its own spot, fades to the settled alpha and drains toward slate; the
 * last of it eases the slump out, so the action ends exactly on the settled frame. `line` is null for a figure
 * no blow threw (a caster felled by its own cost): it only falls. `done` runs when the fall is over.
 */
export function fall(scene: Phaser.Scene, body: Body, line: Line | null, melee: boolean, rate: number, flashes: boolean, done: () => void) {
  const { image, shadow, home } = body, lean = line ? side(line) : 1;
  const { recoil, fall: down, settle } = DEATH;
  if (flashes) flash(scene, body, reaction({ lost: 0, defeated: true }, melee).flash / rate);
  if (line) {
    const answer = reaction({ lost: 0, defeated: true }, melee);
    tremble(scene, body, line, answer.tremor, rate);
    scene.tweens.add({ targets: image, x: home.x + line.dx * recoil.distance, y: home.y + line.dy * recoil.distance, angle: lean * recoil.tilt,
      delay: answer.stop / rate, duration: recoil.lasts / rate, ease: 'Quad.Out' });
  }
  const tilt = lean * ((line ? recoil.tilt : 0) + down.tip);
  scene.tweens.add({ targets: image, x: home.x, y: home.y, angle: tilt, scaleX: body.scale * down.width, scaleY: body.scale * down.height,
    alpha: { value: DEFEATED_ALPHA, ease: 'Sine.In' }, delay: down.at / rate, duration: down.lasts / rate, ease: 'Sine.InOut' });
  scene.tweens.add({ targets: shadow, alpha: DEFEATED_ALPHA, delay: down.at / rate, duration: down.lasts / rate, ease: 'Sine.In' });
  // The colour drained: a multiply tint toward a cold grey of the palette, and back as the figure settles. No filter: a tint is
  // a vertex attribute, and by the time a colour matrix would show, the figure has faded.
  const drain = (from: number, to: number, delay: number, lasts: number, end?: () => void) => scene.tweens.addCounter({ from: 0, to: 1,
    delay: delay / rate, duration: lasts / rate,
    onUpdate: tween => { if (image.active) image.setTint(mix(from, to, tween.getValue() ?? 0)); }, onComplete: end });
  drain(WHITE, DRAINED, down.at, down.lasts);
  drain(DRAINED, WHITE, settle.at, settle.lasts, () => { if (image.active) image.clearTint(); done(); });
  scene.tweens.add({ targets: image, angle: 0, ...rested(body), delay: settle.at / rate, duration: settle.lasts / rate, ease: 'Quad.Out' });
}
