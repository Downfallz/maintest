import Phaser from 'phaser';
import { DRAW_MS, STRIKE_MS, WINDUP_MS, VFX_MS, MOTION_SCALE } from '../../table/battle-timing.js';
import type { SpellVisual } from './types';

export interface EffectAnchor { x: number; y: number; radius: number; }
const PALETTE: Record<string, number> = {
  physical: 0xd2bbac, biological: 0xadc777, elemental: 0x8dcfe5,
  necromantic: 0x85c9c2, astral: 0xb9a6e5, neutral: 0xaab8c0,
};

// Short-lived vectors, not combat entities. The owner clears every object on skip, resize and seat changes.
export class SpellEffects {
  private objects = new Set<Phaser.GameObjects.Graphics>();
  constructor(private scene: Phaser.Scene) {}

  clear() {
    for (const object of this.objects) { this.scene.tweens.killTweensOf(object); object.destroy(); }
    this.objects.clear();
  }

  private graphic(motif: string, creature: string, phase: string) {
    const graphic = this.scene.add.graphics().setDepth(2000).setName('spell-vfx');
    graphic.setData({ motif, creature, phase });
    this.objects.add(graphic);
    return graphic;
  }

  private fade(graphic: Phaser.GameObjects.Graphics, rate: number, duration: number, scale = 1.25) {
    this.scene.tweens.add({ targets: graphic, alpha: 0, scaleX: scale, scaleY: scale,
      duration: duration / rate, ease: 'Quad.Out', onComplete: () => {
        this.objects.delete(graphic); graphic.destroy();
      } });
  }

  windup(visual: SpellVisual, actor: EffectAnchor, destinations: { id: string; anchor: EffectAnchor }[], rate: number) {
    if (visual.motif === 'fizzle') return;
    const color = PALETTE[visual.family] ?? PALETTE.neutral;
    if (visual.delivery === 'projectile') {
      for (const { id, anchor } of destinations) {
        const quill = this.graphic('quill', id, 'windup').setPosition(actor.x, actor.y);
        quill.fillStyle(0xdfd9c9).fillTriangle(-16, -3, 16, 0, -16, 3);
        quill.lineStyle(1.5, color).lineBetween(-18, 0, 5, 0);
        quill.setRotation(Math.atan2(anchor.y - actor.y, anchor.x - actor.x));
        this.scene.tweens.add({ targets: quill, x: anchor.x, y: anchor.y, duration: STRIKE_MS / rate,
          delay: DRAW_MS / rate, ease: 'Cubic.In' });
      }
      return;
    }
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

  impact(visual: SpellVisual, target: EffectAnchor, id: string, rate: number, origin?: EffectAnchor) {
    const motif = visual.motif;
    const color = MOTIF_COLOR[motif] ?? PALETTE[visual.family] ?? PALETTE.neutral;
    const g = this.graphic(motif, id, 'impact').setPosition(target.x, target.y);
    g.lineStyle(2.5, color, .95).fillStyle(color, .22);
    (PAINTERS[motif] ?? paintMark)(g, target.radius, color, motif, origin ? { x: origin.x - target.x, y: origin.y - target.y } : null);
    const motion = Object.hasOwn(ENVELOPES, motif) ? ENVELOPES[motif] : { scale: .82, rise: 0 };
    // Arcs stay pinned to both endpoints; other motifs arrive, hold briefly, then dissipate.
    g.setScale(motion.scale).setAlpha(.35);
    g.y += target.radius * motion.rise;
    this.scene.tweens.add({ targets: g, scaleX: 1, scaleY: 1, y: target.y, alpha: 1,
      duration: 100 * MOTION_SCALE / rate, ease: 'Cubic.Out', onComplete: () => this.fade(g, rate, VFX_MS - 100 * MOTION_SCALE, FADE_SCALE[motif] ?? 1.35) });
  }
}

const ENVELOPES: Record<string, { scale: number; rise: number }> = {
  arc: { scale: 1, rise: 0 }, bones: { scale: .92, rise: .3 }, spirits: { scale: .72, rise: .3 },
  vital: { scale: .75, rise: .18 }, void: { scale: 1.2, rise: 0 },
  wave: { scale: .4, rise: 0 }, roar: { scale: .5, rise: 0 }, fizzle: { scale: 1, rise: 0 },
};

// A motif that carries its own colour whatever the family: embers are warm, a wound is blood.
const MOTIF_COLOR: Record<string, number> = { fire: 0xd58b75, wound: 0xb66f7e };
// An arc has absolute endpoints relative to its target; scaling it would move its source off the caster.
const FADE_SCALE: Record<string, number> = { arc: 1, void: .55 };

type Painter = (g: Phaser.GameObjects.Graphics, r: number, color: number, motif: string, from: { x: number; y: number } | null) => void;
const vector = (x: number, y: number) => new Phaser.Math.Vector2(x, y);

const paintStrokes: Painter = (g, r, color, motif) => {
  for (let i = -1; i <= 1; i++) {
    const offset = i * r * .38;
    g.lineStyle(motif === 'impact' ? 4 : 2.5, color).lineBetween(offset - r * .25, -r * .65, offset + r * .3, r * .5);
  }
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
