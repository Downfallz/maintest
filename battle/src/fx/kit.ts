import type Phaser from 'phaser';
import { SCENE_PARTICLES } from '../../../table/battle-staging.js';

// The effects kit (ADR 0196): the primitives a spell's staging is played with. It is a bundle of its own,
// loaded after the first frame, and it holds no engine: the live bundle hands it the Phaser it already carries.
// So nothing here imports Phaser but as a type; `build.mjs` refuses the kit if it does.

export type Engine = typeof Phaser;
export interface Anchor { x: number; y: number; radius: number }
/** One beat as `table/battle-staging.js` reads it: its defaults filled in, its delay inside its window. */
export interface StagedBeat {
  beat: string; at: number; delay: number; window: string; on: string;
  lasts?: number; count?: number; shape?: string; tint?: string; blend?: string; direction?: string;
  spread?: number; reach?: number; strength?: number;
}
/** One beat to play: where, on which figure, from where the blow came, in which colour, at which rate. */
export interface BeatPlay {
  beat: StagedBeat; at: Anchor; creature: string; color: number; rate: number;
  /** The direction of the blow, in radians, from the caster to the target; null when there is none. */
  heading: number | null;
  from?: Anchor;
  figure?: Phaser.GameObjects.Image;
}
/** What the kit asks of the scene's view: the trauma is the scene's, one value for every hit. */
export interface KitView { shake(strength: number): void }
export type Primitive = (kit: KitContext, play: BeatPlay) => void;

/** The kit's one texture: on a phone a batch holds one texture, so every shape is a frame of it. */
export const TEXTURE = 'fx-kit';
const FRAME = 32;
const DEPTH = 2000;
const UP = -Math.PI / 2;

/** Where a beat heads, in radians: along the blow, back against it, or up. A blow with no direction heads up. */
export function headingOf(direction: string | undefined, heading: number | null) {
  if (direction === 'along') return heading ?? UP;
  if (direction === 'back') return heading === null ? UP : heading + Math.PI;
  return UP;
}
export const degrees = (radians: number) => radians * 180 / Math.PI;

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

  /** The four white shapes, drawn once on one canvas: a soft dot, a spark, a shard and a ring. A tint colours them. */
  private texture() {
    const textures = this.scene.textures;
    if (textures.exists(TEXTURE)) return;
    const canvas = textures.createCanvas(TEXTURE, FRAME * 4, FRAME)!;
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
    return [...this.objects].map(object => ({ ...object.data.getAll() as { beat: string; creature: string } }));
  }

  get changes() { return this.undo.size; }

  clear() {
    for (const tween of this.tweens) if (!tween.isDestroyed()) tween.stop();
    this.tweens.clear();
    // A copy: each undo takes itself out of the set it is read from (`once` above).
    for (const undo of [...this.undo]) undo();
    for (const emitter of this.emitters.keys()) emitter.killAll();
    this.emitters.clear();
    for (const object of this.objects) { this.scene.tweens.killTweensOf(object); object.destroy(); }
    this.objects.clear();
  }
}
