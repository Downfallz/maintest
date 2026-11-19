import Phaser from 'phaser';
import { IMPACT_MS, WINDUP_MS, VFX_MS, MOTION_SCALE } from '../../table/battle-timing.js';
import { stagingBeats } from '../../table/battle-staging.js';
import { BONE, FLIGHT, flightAt } from '../../table/battle-motion.js';
import type { SpellVisual } from './types';
import type { EffectsKit, StagedBeat } from './fx';

/** A figure's centre and radius, and the heights of its feet and its head (the craft book, "A beat on the ground"). */
export interface EffectAnchor { x: number; y: number; radius: number; feet?: number; head?: number }
const PALETTE: Record<string, number> = {
  physical: 0xd2bbac, biological: 0xadc777, elemental: 0x8dcfe5,
  necromantic: 0x85c9c2, astral: 0xb9a6e5, neutral: 0xaab8c0,
};
// A motif that carries its own colour whatever the family: embers are warm, a wound is blood.
// Blood is a deep brown red: the paler 0xb66f7e read pink over the sky (#919, the owner's choice).
const MOTIF_COLOR: Record<string, number> = { fire: 0xd58b75, wound: 0x9a3b34 };
// The colours a beat names (`table/battle-staging.js`): the palette's bone, which stays equal to `--brand-bone`
// (docs/art/branding.css), and the two a motif already carries. The target marker's gold is not among them.
const TINTS: Record<string, number> = { bone: BONE, ember: MOTIF_COLOR.fire, blood: MOTIF_COLOR.wound };

// Between two copies of one beat, at 1× (*house*, the craft book's 30 to 60 ms).
const STAGGER_MS = 40;

/**
 * Who a beat plays on: where it stands, which creature it is, and its picture when the scene has one. `drift` is
 * how far its body has been carried from that place now: a motif drawn on a caster still at its target goes with
 * it, and comes home with it (#919).
 */
export interface EffectFigure { id: string; anchor: EffectAnchor; image?: Phaser.GameObjects.Image; drift?: () => { x: number; y: number } }

// Short-lived vectors, not combat entities. The owner clears every object on skip, resize and seat changes.
// A spell with a staging is played beat by beat through the effects kit, once the kit has arrived (ADR 0196);
// without a staging, without the kit, or with a beat the kit does not know, it is drawn as its motif.
export class SpellEffects {
  private objects = new Set<Phaser.GameObjects.Graphics>();
  private timers = new Set<Phaser.Tweens.Tween>();
  private kit: EffectsKit | null = null;
  // How many targets the current window has played on: the copies of a beat are staggered, one target after another.
  private copies = 0;
  constructor(private scene: Phaser.Scene) {}

  /** The kit, once its bundle has loaded. Until then every spell is its motif. */
  attach(kit: EffectsKit) { this.kit = kit; }
  get equipped() { return this.kit !== null; }
  /** What the kit holds now: its objects, its particles alive, and the figures it changed and has not put back. */
  get playing() {
    return { objects: this.kit?.playing() ?? [], particles: this.kit?.particles() ?? 0, changes: this.kit?.changes() ?? 0 };
  }
  /** Where each of the kit's objects stands, for the bench and the browser specs. */
  get places() { return this.kit?.places() ?? []; }

  clear() {
    for (const timer of this.timers) if (!timer.isDestroyed()) timer.stop();
    this.timers.clear();
    this.copies = 0;
    this.kit?.clear();
    for (const object of this.objects) { this.scene.tweens.killTweensOf(object); object.destroy(); }
    this.objects.clear();
  }

  /** The beats of one window, or null when the spell is drawn as its motif. */
  private staged(visual: SpellVisual, window: 'windup' | 'impact'): StagedBeat[] | null {
    if (!this.kit) return null;
    const beats: StagedBeat[] | null = stagingBeats(visual, ['motif', ...this.kit.beats]);
    return beats && beats.filter(beat => beat.window === window);
  }

  private play(staged: StagedBeat, visual: SpellVisual, on: EffectFigure, rate: number, from?: EffectAnchor, toward?: EffectAnchor, copy = 0) {
    // The same beat on several targets does not land on one frame (the craft book, "Stagger and overlap"): each
    // copy starts a little after the one before, as far as the beat's window leaves room.
    const end = staged.window === 'windup' ? WINDUP_MS : WINDUP_MS + IMPACT_MS;
    const late = Math.max(0, Math.min(copy * STAGGER_MS, end - staged.at - (staged.lasts ?? 0)));
    const beat = late ? { ...staged, delay: staged.delay + late } : staged;
    const origin = from ?? on.anchor, aim = toward ?? on.anchor;
    const straight = origin.x === aim.x && origin.y === aim.y ? null : Math.atan2(aim.y - origin.y, aim.x - origin.x);
    // What a projectile knocks off its target goes on the way the projectile came in, down the end of its arc,
    // not along the straight line from its caster's centre.
    const heading = from && straight !== null && visual.delivery === 'projectile' ? flightAt(from, on.anchor, 1).angle : straight;
    const color = TINTS[beat.tint ?? ''] ?? PALETTE[visual.family] ?? PALETTE.neutral;
    // A projectile's trail keeps to the arc its quill flies.
    const flight = beat.beat === 'trail' && visual.delivery === 'projectile';
    this.kit!.play({ beat, at: on.anchor, creature: on.id, figure: on.image, color, rate, heading, from, flight });
  }

  /** The beats of a window that play once a cast: those on the caster, and the shake, which is the view's. */
  private once(beats: StagedBeat[], visual: SpellVisual, actor: EffectFigure, rate: number, toward?: EffectAnchor) {
    for (const beat of beats) {
      if (beat.beat !== 'motif' && beat.beat !== 'trail' && (beat.on === 'caster' || beat.beat === 'shake')) this.play(beat, visual, actor, rate, undefined, toward);
    }
  }

  /** The beats of a window that play on each target. A trail runs from the caster to it. */
  private each(beats: StagedBeat[], visual: SpellVisual, target: EffectFigure, rate: number, origin?: EffectAnchor) {
    const copy = this.copies++;
    for (const beat of beats) {
      if (beat.beat === 'motif' || beat.beat === 'shake') continue;
      // A trail keeps to the quill it follows: it is not staggered.
      if (beat.beat === 'trail') this.play(beat, visual, target, rate, origin);
      else if (beat.on === 'target') this.play(beat, visual, target, rate, origin, undefined, copy);
    }
  }

  // The kit has the same helper, and this one cannot borrow it: the motif is drawn here, kit or no kit.
  private after(ms: number, run: () => void) {
    if (ms <= 0) { run(); return; }
    const timer = this.scene.tweens.addCounter({ from: 0, to: 1, duration: ms, onComplete: () => { this.timers.delete(timer); run(); } });
    this.timers.add(timer);
  }

  private graphic(motif: string, creature: string, phase: string) {
    const graphic = this.scene.add.graphics().setDepth(2000).setName('spell-vfx');
    graphic.setData({ motif, creature, phase });
    this.objects.add(graphic);
    return graphic;
  }

  private fade(graphic: Phaser.GameObjects.Graphics, rate: number, duration: number, scale = 1.25, onUpdate?: () => void) {
    this.scene.tweens.add({ targets: graphic, alpha: 0, scaleX: scale, scaleY: scale,
      duration: duration / rate, ease: 'Quad.Out', onUpdate, onComplete: () => {
        this.objects.delete(graphic); graphic.destroy();
      } });
  }

  windup(visual: SpellVisual, caster: EffectFigure, destinations: EffectFigure[], rate: number) {
    if (visual.motif === 'fizzle') return;
    const actor = caster.anchor;
    const color = PALETTE[visual.family] ?? PALETTE.neutral;
    const beats = this.staged(visual, 'windup');
    if (beats) {
      this.once(beats, visual, caster, rate, destinations[0]?.anchor);
      for (const destination of destinations) this.each(beats, visual, destination, rate, actor);
    }
    if (visual.delivery === 'projectile') {
      for (const { id, anchor } of destinations) {
        // The quill leaves from just over its caster as the body throws, and not before: until then it is not drawn, where
        // it once stood on the body for the whole draw back and 300 ms of an eased flight (#909).
        const quill = this.graphic('quill', id, 'windup').setVisible(false);
        quill.fillStyle(0xdfd9c9).fillTriangle(-16, -3, 16, 0, -16, 3);
        quill.lineStyle(1.5, color).lineBetween(-18, 0, 5, 0);
        const fly = (share: number) => {
          if (!quill.active) return;
          const at = flightAt(actor, anchor, share);
          quill.setPosition(at.x, at.y).setRotation(at.angle);
        };
        fly(0);
        const flight = this.scene.tweens.addCounter({ from: 0, to: 1, duration: FLIGHT.lasts / rate, delay: FLIGHT.at / rate,
          onStart: () => quill.setVisible(true), onUpdate: tween => fly(tween.getValue() ?? 0), onComplete: () => this.timers.delete(flight) });
        this.timers.add(flight);
      }
      return;
    }
    // A staged spell gathers as its beats say, or not at all: the shared charge is the motif's.
    if (beats) return;
    const charge = this.graphic(visual.motif, '', 'windup').setPosition(actor.x, actor.y);
    const r = actor.radius * .55;
    charge.lineStyle(2, color, .7);
    if (visual.family === 'necromantic' || visual.family === 'astral') {
      charge.strokeEllipse(0, 0, r * 2, r);
      for (let i = 0; i < 6; i++) {
        const a = i * Math.PI / 3;
        charge.lineBetween(Math.cos(a) * r, Math.sin(a) * r * .5, Math.cos(a) * r * 1.2, Math.sin(a) * r * .7);
      }
    } else {
      charge.beginPath().arc(0, 0, r, -.8, 2.2).strokePath();
      charge.beginPath().arc(0, 0, r * .7, 2.4, 5).strokePath();
    }
    this.fade(charge, rate, WINDUP_MS, 1.6);
  }

  /** Whether the spell's own staging flashes its targets, now that the kit is here to play it: the scene then does not. */
  flashes(visual: SpellVisual) {
    return this.staged(visual, 'impact')?.some(beat => beat.beat === 'flash' && beat.on === 'target') ?? false;
  }

  /** Once a cast, before its recipients: the impact's beats on the caster, and the shake. */
  cast(visual: SpellVisual, caster: EffectFigure, rate: number, toward?: EffectAnchor) {
    const beats = this.staged(visual, 'impact');
    if (beats) this.once(beats, visual, caster, rate, toward);
  }

  impact(visual: SpellVisual, recipient: EffectFigure, rate: number, origin?: EffectAnchor) {
    const beats = this.staged(visual, 'impact');
    // A projectile's strokes run along the end of its arc: the motif is told it came from back along that line.
    const came = origin && visual.delivery === 'projectile' ? arrival(origin, recipient.anchor) : origin;
    if (!beats) { this.motif(visual, recipient.anchor, recipient.id, rate, came, VFX_MS, recipient.drift); return; }
    this.each(beats, visual, recipient, rate, origin);
    // The motif is one beat among the others: when the staging places it, for as long as it says.
    const motif = beats.find(beat => beat.beat === 'motif');
    if (motif) this.after(motif.delay / rate, () => this.motif(visual, recipient.anchor, recipient.id, rate, came, motif.lasts, recipient.drift));
  }

  private motif(visual: SpellVisual, target: EffectAnchor, id: string, rate: number, origin?: EffectAnchor, lasts = VFX_MS, drift?: EffectFigure['drift']) {
    const motif = visual.motif;
    const color = MOTIF_COLOR[motif] ?? PALETTE[visual.family] ?? PALETTE.neutral;
    const g = this.graphic(motif, id, 'impact');
    g.lineStyle(2.5, color, .95).fillStyle(color, .22);
    (PAINTERS[motif] ?? paintMark)(g, target.radius, color, motif, origin ? { x: origin.x - target.x, y: origin.y - target.y } : null);
    const motion = Object.hasOwn(ENVELOPES, motif) ? ENVELOPES[motif] : { scale: .82, rise: 0 };
    // Arcs stay pinned to both endpoints; other motifs arrive, hold briefly, then dissipate. The motif rises into
    // place, and goes wherever its figure's body is carried meanwhile.
    const place = { rise: target.radius * motion.rise };
    const track = () => {
      if (!g.active) return;
      const shift = drift?.() ?? STILL;
      g.setPosition(target.x + shift.x, target.y + place.rise + shift.y);
    };
    track();
    g.setScale(motion.scale).setAlpha(.35);
    // The arrival and the fade together are what the beat lasts: a short beat shortens both, a third to arrive.
    const arrive = Math.min(100 * MOTION_SCALE, lasts / 3);
    const rising = this.scene.tweens.add({ targets: place, rise: 0, duration: arrive / rate, ease: 'Cubic.Out', onUpdate: track,
      onComplete: () => this.timers.delete(rising) });
    this.timers.add(rising);
    this.scene.tweens.add({ targets: g, scaleX: 1, scaleY: 1, alpha: 1,
      duration: arrive / rate, ease: 'Cubic.Out', onComplete: () => this.fade(g, rate, lasts - arrive, FADE_SCALE[motif] ?? 1.35, drift && track) });
  }
}

const STILL = { x: 0, y: 0 };

/** Where a projectile seems to have come from, seen from its target: as far as its caster, back along the end of its arc. */
function arrival(from: EffectAnchor, to: EffectAnchor): EffectAnchor {
  const angle = flightAt(from, to, 1).angle, far = Math.hypot(to.x - from.x, to.y - from.y);
  return { ...from, x: to.x - Math.cos(angle) * far, y: to.y - Math.sin(angle) * far };
}

const ENVELOPES: Record<string, { scale: number; rise: number }> = {
  arc: { scale: 1, rise: 0 }, bones: { scale: .92, rise: .3 }, spirits: { scale: .72, rise: .3 },
  vital: { scale: .75, rise: .18 }, void: { scale: 1.2, rise: 0 },
  wave: { scale: .4, rise: 0 }, roar: { scale: .5, rise: 0 }, fizzle: { scale: 1, rise: 0 },
};

// An arc has absolute endpoints relative to its target; scaling it would move its source off the caster.
const FADE_SCALE: Record<string, number> = { arc: 1, void: .55 };

type Painter = (g: Phaser.GameObjects.Graphics, r: number, color: number, motif: string, from: { x: number; y: number } | null) => void;
const vector = (x: number, y: number) => new Phaser.Math.Vector2(x, y);

// The palette's soot (`--brand-soot`, docs/art/branding.css): the edge a pale stroke is read against on a pale hide.
const SOOT = 0x070d10;
// A blow has a direction, and its strokes run along it: from the caster's side of the figure to the far side
// (the craft book, "An impact that keeps the direction"). A slash cuts across it instead, 60° off the blow, as a
// claw rakes (#919, the owner's choice): with the blow vertical, the two motifs no longer read the same. With no
// caster to come from, a wound, the strokes lean as before.
const SLASH = Math.PI / 3;
const paintStrokes: Painter = (g, r, color, motif, from) => {
  const far = from ? Math.hypot(from.x, from.y) : 0;
  const blow = far > 0 ? Math.atan2(-from!.y, -from!.x) : null;
  const turn = motif === 'slash' ? SLASH : 0;
  const angle = blow === null ? Math.atan2(.9, .43) : blow + turn;
  const along = { x: Math.cos(angle), y: Math.sin(angle) };
  g.setData('strokes', angle * 180 / Math.PI);
  const across = { x: -along.y, y: along.x }, width = motif === 'impact' ? 4 : 2.5, half = r * .62;
  const stroke = (shift: number) => {
    const x = across.x * shift + along.x * r * .03, y = across.y * shift + along.y * r * .03;
    g.lineBetween(x - along.x * half, y - along.y * half, x + along.x * half, y + along.y * half);
  };
  // Every edge first, then every stroke: one stroke's edge never covers its neighbour.
  g.lineStyle(width + 3, SOOT, .6);
  for (let i = -1; i <= 1; i++) stroke(i * r * .38);
  g.lineStyle(width, color);
  for (let i = -1; i <= 1; i++) stroke(i * r * .38);
  if (motif === 'impact') g.lineStyle(2, color, .5).strokeCircle(0, 0, r * .75);
};
const paintBite: Painter = (g, r, color) => {
  for (const sign of [-1, 1]) for (let i = -1; i <= 1; i++) {
    const x = i * r * .5;
    g.fillStyle(color, .8).fillTriangle(x - r * .15, sign * r * .7, x + r * .15, sign * r * .7, x, sign * r * .25);
  }
};
const paintRings: Painter = (g, r, color, motif) => {
  for (let i = 1; i <= 3; i++) g.lineStyle(3 - i * .5, color, 1 - i * .17).strokeEllipse(0, r * .6, r * i, r * i * .35);
  if (motif !== 'wave') return;
  for (let i = 0; i < 5; i++) {
    const x = (i - 2) * r * .4;
    g.lineBetween(x, r * .6, x + r * .2, r * .3).lineBetween(x + r * .2, r * .3, x + r * .1, 0);
  }
};
const paintPlates: Painter = (g, r) => {
  for (let i = -1; i <= 1; i++) {
    const x = i * r * .6;
    const points = [vector(x - r * .25, -r * .5), vector(x + r * .25, -r * .5), vector(x + r * .3, r * .2), vector(x, r * .6), vector(x - r * .3, r * .2)];
    g.fillPoints(points, true).strokePoints(points, true);
  }
};
const paintSpores: Painter = (g, r, color) => {
  for (let i = 0; i < 9; i++) {
    const a = i * 2.4, d = r * (.3 + (i % 3) * .3);
    g.fillStyle(color, .2 + (i % 2) * .18).fillCircle(Math.cos(a) * d, Math.sin(a) * d, r * .18);
    g.lineStyle(1, color, .65).strokeCircle(Math.cos(a) * d, Math.sin(a) * d, r * .18);
  }
};
const paintArc: Painter = (g, r, color, _motif, from) => {
  const dx = from ? from.x : -r, dy = from ? from.y : -r;
  const points = Array.from({ length: 9 }, (_, i) => vector(dx * i / 8 + (i % 2 ? r * .18 : 0), dy * i / 8 - (i % 2 ? r * .25 : 0)));
  g.lineStyle(5, color, .22).strokePoints(points).lineStyle(1.8, 0xd2e5ed).strokePoints(points);
  g.strokeCircle(0, 0, r * .25);
};
const paintFire: Painter = (g, r, color) => {
  for (let i = -2; i <= 2; i++) {
    const x = i * r * .32, top = -r * (1 - Math.abs(i) * .17);
    g.fillStyle(color, .55).fillTriangle(x - r * .22, r * .5, x + r * .22, r * .5, x + r * .12, top);
    g.lineStyle(1.5, 0xe2baaa, .8).lineBetween(x, r * .3, x + r * .12, top + r * .2);
  }
};
const paintIce: Painter = (g, r) => {
  for (let i = 0; i < 6; i++) {
    const a = i * Math.PI / 3, x = Math.cos(a) * r, y = Math.sin(a) * r;
    g.lineBetween(0, 0, x, y).lineBetween(x * .65, y * .65, x * .65 + Math.cos(a + .8) * r * .3, y * .65 + Math.sin(a + .8) * r * .3);
  }
};
const paintBones: Painter = (g, r, color) => {
  for (let i = -2; i <= 2; i++) {
    const x = i * r * .36, y = Math.abs(i) * r * .12;
    g.lineStyle(r * .11, 0xd7d5c5).lineBetween(x, r * .6, x, -r * .65 + y);
    g.fillStyle(0xd7d5c5, .9).fillCircle(x - r * .06, -r * .65 + y, r * .09).fillCircle(x + r * .06, -r * .65 + y, r * .09);
  }
  g.lineStyle(2, color, .7).strokeEllipse(0, r * .65, r * 2.1, r * .5);
};
const paintSpirits: Painter = (g, r, color) => {
  for (let i = -1; i <= 1; i++) {
    const x = i * r * .65, y = -Math.abs(i) * r * .25;
    g.fillStyle(color, .3).fillEllipse(x, y, r * .48, r * .75);
    g.lineStyle(2, color, .9).beginPath().arc(x, y - r * .1, r * .22, Math.PI, 2 * Math.PI).strokePath();
    g.lineBetween(x - r * .2, y, x, y + r * .6).lineBetween(x, y + r * .6, x + r * .2, y);
    g.fillStyle(0xe2f3ee, .9).fillCircle(x - r * .08, y - r * .1, r * .035).fillCircle(x + r * .08, y - r * .1, r * .035);
  }
};
const paintVoid: Painter = (g, r, color) => {
  g.fillStyle(0x141526, .7).fillEllipse(0, 0, r * 1.2, r * 1.6);
  for (let i = 0; i < 3; i++) g.lineStyle(2, color, .9 - i * .2).strokeEllipse(0, 0, r * (1.3 + i * .5), r * (1.7 - i * .2));
};
const paintVital: Painter = (g, r, color) => {
  for (let i = 0; i < 3; i++) g.lineStyle(2, color, .9 - i * .2).strokeEllipse(0, (i - 1) * r * .45, r * (1.5 - i * .25), r * .5);
  g.lineBetween(0, -r, 0, r * .8);
};
const paintFizzle: Painter = (g, r, color) => {
  g.lineStyle(2, color, .6).beginPath().arc(0, 0, r * .5, .4, 2.3).strokePath();
  g.beginPath().arc(0, 0, r * .5, 3.5, 5.4).strokePath();
};
// The neutral mark, for focus and for any motif the page does not know how to draw.
const paintMark: Painter = (g, r) => { g.strokeEllipse(0, 0, r, r * .45).lineBetween(0, -r * .45, 0, r * .45); };

const PAINTERS: Record<string, Painter> = {
  impact: paintStrokes, slash: paintStrokes, quill: paintStrokes, wound: paintStrokes, bite: paintBite,
  wave: paintRings, roar: paintRings, plates: paintPlates, spores: paintSpores, arc: paintArc, fire: paintFire,
  ice: paintIce, bones: paintBones, spirits: paintSpirits, void: paintVoid, vital: paintVital, fizzle: paintFizzle, focus: paintMark,
};
