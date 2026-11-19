import type Phaser from 'phaser';
import { SCENE_PARTICLES } from '../../../table/battle-staging.js';

// The effects kit (ADR 0196): the primitives a spell's staging is played with. It is a bundle of its own,
// loaded after the first frame, and it holds no engine: the live bundle hands it the Phaser it already carries.
// So nothing here imports Phaser but as a type; `build.mjs` refuses the kit if it does.

export type Engine = typeof Phaser;
/** A figure's place: its centre and its radius, and when the scene knows them the heights of its feet and its head. */
export interface Anchor { x: number; y: number; radius: number; feet?: number; head?: number }
/** One beat as `table/battle-staging.js` reads it: its defaults filled in, its delay inside its window. */
export interface StagedBeat {
  beat: string; at: number; delay: number; window: string; on: string;
  lasts?: number; count?: number; shape?: string; tint?: string; blend?: string; direction?: string;
  spread?: number; reach?: number; strength?: number; level?: string;
}
/** One beat to play: where, on which figure, from where the blow came, in which colour, at which rate. */
export interface BeatPlay {
  beat: StagedBeat; at: Anchor; creature: string; color: number; rate: number;
  /** The direction of the blow, in radians, from the caster to the target; null when there is none. */
  heading: number | null;
  from?: Anchor;
  /** The beat follows a projectile: its head keeps to the arc the projectile flies (`flightAt`). */
  flight?: boolean;
  figure?: Phaser.GameObjects.Image;
}
/**
 * What the kit asks of the scene's view: the trauma is the scene's, one value for every hit; and while a halo
 * swells on a figure the scene quiets the gold rim it draws around the acting one, so the halo is read.
 */
export interface KitView { shake(strength: number): void; quiet?(creature: string, on: boolean): void }
export type Primitive = (kit: KitContext, play: BeatPlay) => void;

/** The kit's one texture: on a phone a batch holds one texture, so every shape is a frame of it. */
export const TEXTURE = 'fx-kit';
const FRAME = 32;
/** The shock ring's own frame, and the radius it is drawn at: a ring stretched from a 32 px frame would be a band. */
const WAVE = 128;
export const WAVE_RADIUS = 58;
const DEPTH = 2000;
const UP = -Math.PI / 2;
/** How flat the ground lies under the eye: a distance toward or away from it is drawn at this share (*house*). */
export const GROUND = .42;

/**
 * Where a beat starts on its figure (the craft book, "A beat on the ground"): at its feet, its centre or its
 * head. A figure the scene gave no feet or head to has them a body's height apart, about three radii.
 */
export function placed(at: Anchor, level: string | undefined): Anchor {
  if (level === 'feet') return { ...at, y: at.feet ?? at.y + at.radius * 1.4 };
  if (level === 'head') return { ...at, y: at.head ?? at.y - at.radius * 1.4 };
  return at;
}

/** Whether a beat's pieces travel on the ground: along or back against a blow struck across it, or anywhere but up from the feet. */
export function grounded(beat: StagedBeat) {
  return beat.direction === 'along' || beat.direction === 'back' || (beat.level === 'feet' && beat.direction !== 'up');
}

/** A heading on the screen turned onto the ground: the angle a ring lying there opens around. */
export const onGround = (heading: number) => Math.atan2(Math.sin(heading) / GROUND, Math.cos(heading));

/** Where a beat heads, in radians: along the blow, back against it, or up. A blow with no direction heads up. */
export function headingOf(direction: string | undefined, heading: number | null) {
  if (direction === 'along') return heading ?? UP;
  if (direction === 'back') return heading === null ? UP : heading + Math.PI;
  return UP;
}
export const degrees = (radians: number) => radians * 180 / Math.PI;
/**
 * How large a beat's particles are drawn, over their frame: by the figure they play on, never under `least`,
 * and by the beat's `strength` when its author gave one, from half as large again (1) down to half (near 0).
 */
export const sized = (beat: StagedBeat, base: number, least: number) => Math.max(least, base) * (.5 + (beat.strength ?? .5));

type Emitter = Phaser.GameObjects.Particles.ParticleEmitter;
type EmitterConfig = Phaser.Types.GameObjects.Particles.ParticleEmitterConfig;
type Owned = Phaser.GameObjects.GameObject;

/**
 * What every primitive plays through. It owns each object, each tween and each change made to a figure, so one
 * `clear` ends them all: the path a skip, a rotation, a hidden tab and a seat change already run.
 */
export class KitContext {
  private readonly objects = new Set<Owned>();
  // Each living emitter with the most particles it may hold.
  private readonly emitters = new Map<Emitter, number>();
  private readonly tweens = new Set<Phaser.Tweens.Tween>();
  private readonly undo = new Set<() => void>();

  constructor(readonly engine: Engine, readonly scene: Phaser.Scene, private readonly reduced: () => boolean, readonly view?: KitView) {}

  /** Less motion was asked for: a primitive returns before it makes anything. */
  get still() { return this.reduced(); }

  /**
   * The white shapes, drawn once on one canvas: a soft dot, a spark, a shard and a ring for the particles, and
   * beside them a thin wide ring, the shock ring a glow opens. A tint colours them.
   */
  private texture() {
    const textures = this.scene.textures;
    if (textures.exists(TEXTURE)) return;
    const canvas = textures.createCanvas(TEXTURE, FRAME * 4 + WAVE, WAVE)!;
    const context = canvas.getContext(), half = FRAME / 2;
    const soft = context.createRadialGradient(half, half, 0, half, half, half);
    soft.addColorStop(0, 'rgba(255,255,255,1)'); soft.addColorStop(.45, 'rgba(255,255,255,.5)'); soft.addColorStop(1, 'rgba(255,255,255,0)');
    context.fillStyle = soft; context.fillRect(0, 0, FRAME, FRAME);
    context.fillStyle = '#fff';
    // The spark and the shard point right: a particle is turned to its heading.
    context.beginPath(); context.moveTo(FRAME + 2, half); context.lineTo(FRAME + 12, half - 3); context.lineTo(FRAME * 2 - 2, half); context.lineTo(FRAME + 12, half + 3); context.fill();
    context.beginPath(); context.moveTo(FRAME * 2 + 5, half - 7); context.lineTo(FRAME * 3 - 4, half - 1); context.lineTo(FRAME * 2 + 9, half + 8); context.fill();
    context.lineWidth = 3; context.strokeStyle = '#fff';
    context.beginPath(); context.arc(FRAME * 3 + half, half, half - 3, 0, Math.PI * 2); context.stroke();
    for (const [index, name] of ['dot', 'spark', 'shard', 'ring'].entries()) canvas.add(name, 0, index * FRAME, 0, FRAME, FRAME);
    // Two strokes, a wide faint one under a thin full one: the ring's edge is soft without a gradient.
    for (const [width, alpha] of [[9, .3], [3.5, 1]]) {
      context.lineWidth = width; context.strokeStyle = `rgba(255,255,255,${alpha})`;
      context.beginPath(); context.arc(FRAME * 4 + WAVE / 2, WAVE / 2, WAVE_RADIUS - 2, 0, Math.PI * 2); context.stroke();
    }
    canvas.add('wave', 0, FRAME * 4, 0, WAVE, WAVE);
    canvas.refresh();
  }

  blend(name: string | undefined) {
    const modes = this.engine.BlendModes;
    if (name === 'add') return modes.ADD;
    return name === 'screen' ? modes.SCREEN : modes.NORMAL;
  }

  private own<T extends Owned>(object: T, play: BeatPlay, lit: boolean) {
    object.setName('spell-fx').setData({ beat: play.beat.beat, creature: play.creature });
    // Every lit object is drawn after every plain one, so a change of blend mode breaks the batch once.
    (object as unknown as Phaser.GameObjects.Components.Depth).setDepth(lit ? DEPTH + 1 : DEPTH);
    this.objects.add(object);
    return object;
  }

  /** Destroy one object when its beat ends. */
  release(object: Owned) {
    this.objects.delete(object);
    this.emitters.delete(object as Emitter);
    this.scene.tweens.killTweensOf(object);
    object.destroy();
  }

  /**
   * How many of the particles a beat asks for the field still has room for. What counts is what each living
   * emitter may hold, not what it holds now: a stream that has just started holds none yet.
   */
  room(count: number) {
    let reserved = 0;
    for (const cap of this.emitters.values()) reserved += cap;
    return Math.max(0, Math.min(count, SCENE_PARTICLES - reserved));
  }

  alive() {
    let alive = 0;
    for (const emitter of this.emitters.keys()) alive += emitter.getAliveParticleCount();
    return alive;
  }

  /** An emitter on the kit's texture, capped, in the beat's shape and blend. */
  emitter(play: BeatPlay, cap: number, config: EmitterConfig) {
    this.texture();
    const { beat, at, color } = play;
    const emitter = this.scene.add.particles(at.x, at.y, TEXTURE, {
      frame: beat.shape, tint: color, blendMode: this.blend(beat.blend), maxAliveParticles: cap, ...config });
    this.emitters.set(emitter, cap);
    return this.own(emitter, play, beat.blend !== 'normal');
  }

  /** One frame of the kit's texture as an image. */
  image(play: BeatPlay, frame: string) {
    this.texture();
    const image = this.scene.add.image(play.at.x, play.at.y, TEXTURE, frame).setTint(play.color).setBlendMode(this.blend(play.beat.blend));
    return this.own(image, play, play.beat.blend !== 'normal');
  }

  /** A tween the kit ends with everything else. */
  tween(config: Phaser.Types.Tweens.TweenBuilderConfig) {
    const tween = this.scene.tweens.add(config);
    this.tweens.add(tween);
    tween.once('complete', () => this.tweens.delete(tween));
    return tween;
  }

  /**
   * Run something later, on the clock every tween of the scene runs on: a beat stays in step with the figures,
   * a film can step it, and whatever ends the scene's tweens ends it too.
   */
  after(ms: number, run: () => void) {
    if (ms <= 0) { run(); return; }
    const timer = this.scene.tweens.addCounter({ from: 0, to: 1, duration: ms, onComplete: () => { this.tweens.delete(timer); run(); } });
    this.tweens.add(timer);
  }

  /** A change to something the kit does not own, a figure's tint or a filter: `undo` runs once, at the end or on clear. */
  restore(undo: () => void) {
    const once = () => { if (this.undo.delete(once)) undo(); };
    this.undo.add(once);
    return once;
  }

  /** What the bench and the browser specs read: every object alive, with its beat and its creature. */
  playing() {
    return [...this.objects].map(object => {
      const { beat, creature } = object.data.getAll() as { beat: string; creature: string };
      return { beat, creature };
    });
  }

  /** Where each object alive stands, and what else it says of itself, for the bench and the browser specs. */
  places() {
    return [...this.objects].map(object => {
      const { x, y } = object as unknown as { x: number; y: number };
      return { ...object.data.getAll() as { beat: string; creature: string }, x, y, kind: object.type };
    });
  }

  /** A Graphics the kit owns, for a shape no frame of its texture draws. */
  graphics(play: BeatPlay) {
    const graphics = this.scene.add.graphics({ x: play.at.x, y: play.at.y }).setBlendMode(this.blend(play.beat.blend));
    return this.own(graphics, play, play.beat.blend !== 'normal');
  }

  get changes() { return this.undo.size; }

  clear() {
    for (const tween of this.tweens) if (!tween.isDestroyed()) tween.stop();
    this.tweens.clear();
    for (const undo of this.undo) undo();
    for (const emitter of this.emitters.keys()) emitter.killAll();
    this.emitters.clear();
    for (const object of this.objects) { this.scene.tweens.killTweensOf(object); object.destroy(); }
    this.objects.clear();
  }
}
