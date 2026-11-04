import Phaser from 'phaser';
import type { MutationLayer } from './types';

// Static, bounded accents owned by one figure. No timelines, emitters or gameplay state.
// Their anchors follow the actual image transform, including lunge, recoil, idle and enemy mirroring.
export class MutationCues {
  private layers: { graphic: Phaser.GameObjects.Graphics; x: number; y: number; size: number }[] = [];

  constructor(scene: Phaser.Scene, layers: readonly MutationLayer[], creature: string, form: string) {
    for (const cue of layers.slice(0, 3)) {
      const graphic = scene.add.graphics().setName('mutation-cue').setData({ creature, ...cue });
      const { x, y } = anchorOf(cue, form);
      paint(graphic, cue);
      this.layers.push({ graphic, x, y, size: sizeOf(cue) });
    }
  }

  follow(image: Phaser.GameObjects.Image, bounds: readonly number[], ground: readonly number[], enabled: boolean) {
    const [left, top, right, bottom] = bounds;
    const width = right - left, height = bottom - top;
    const facing = image.flipX ? -1 : 1;
    const cos = Math.cos(image.rotation), sin = Math.sin(image.rotation);
    for (const layer of this.layers) {
      const x = (left + layer.x * width - ground[0]) * image.scaleX * facing;
      const y = (top + layer.y * height - ground[1]) * image.scaleY;
      const size = Math.min(width * image.scaleX, height * image.scaleY) * layer.size;
      layer.graphic.setPosition(image.x + x * cos - y * sin, image.y + x * sin + y * cos)
        .setScale(size * facing, size).setRotation(image.rotation).setDepth(image.depth + .0005)
        .setAlpha(image.alpha * .8).setVisible(enabled);
    }
  }

  destroy() { for (const layer of this.layers) layer.graphic.destroy(); this.layers = []; }
}

// Where a motif sits on the silhouette, as a share of its bounds: head motifs on the brow, foot motifs at the
// forelegs, the rest on the flank. Reviewed artwork has different head heights; source pixels are never cropped.
const HEAD = new Set(['crown', 'fangs', 'precision']);
const FOOT = new Set(['talons', 'reinforcement']);
function anchorOf(cue: MutationLayer, form: string) {
  if (cue.slot === 'accent') return { x: .33, y: .31 };
  if (cue.slot === 'capstone' && cue.motif === 'circuit') return { x: .62, y: .56 };
  if (HEAD.has(cue.motif)) return { x: .76, y: form === 'predator' ? .49 : .40 };
  if (FOOT.has(cue.motif)) return { x: .68, y: .82 };
  return { x: .47, y: .40 };
}
function sizeOf(cue: MutationLayer) {
  if (cue.slot === 'accent') return .15;
  return cue.stage >= 3 ? .29 : .21;
}

type Graphics = Phaser.GameObjects.Graphics;
type Painter = (g: Graphics, n: number, strong: boolean) => void;
const IVORY = 0xb6b6a4, GHOST = 0x83b8b6;
const vector = (x: number, y: number) => new Phaser.Math.Vector2(x, y);
// Motifs are drawn in a unit box, with more and denser geometry past stage two.
function paint(graphic: Graphics, cue: MutationLayer) {
  const strong = cue.stage >= 3;
  graphic.lineStyle(.035, IVORY, .9).fillStyle(0x4c5b60, .8);
  PAINTERS[cue.motif]?.(graphic, strong ? 5 : 3, strong);
}

const paintPlates: Painter = (g, n) => {
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .19, y = (i % 2) * .09;
    const plate = [vector(x - .17, y + .08), vector(x - .10, y - .20), vector(x + .07, y - .32), vector(x + .18, y - .07), vector(x + .11, y + .24), vector(x - .05, y + .31)];
    g.fillStyle(i % 2 ? 0x646b67 : 0x535c5a, .85).fillPoints(plate, true);
    g.lineStyle(.022, 0xabb1a2, .5).strokePoints(plate, true);
    g.lineStyle(.035, 0x303b3c, .7).lineBetween(x - .10, y + .14, x + .10, y - .10);
  }
};
const paintCrown: Painter = (g, n) => {
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .21;
    g.fillStyle(IVORY, .6).fillTriangle(x - .09, .26, x + .07, -.45 - (i % 2) * .12, x + .12, .18);
  }
  g.lineBetween(-.4, .28, .4, .28);
};
const paintClaws: Painter = (g, n) => {
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .20;
    g.fillStyle(IVORY, .65).fillTriangle(x - .08, -.25, x + .10, .42, x + .04, -.08);
    g.lineBetween(x - .08, -.25, x + .10, .42);
  }
};
const paintFissures: Painter = (g, n) => {
  g.lineStyle(.055, 0x985647, .8);
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .21;
    g.beginPath().moveTo(x - .12, -.42).lineTo(x + .06, -.14).lineTo(x - .03, .08).lineTo(x + .18, .38).strokePath();
  }
};
const paintGlands: Painter = (g, n) => {
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .20, y = (i % 2) * .15;
    g.lineStyle(.025, 0x83966b, .9).fillStyle(0x647f59, .5);
    g.fillEllipse(x, y, .23, .42).strokeEllipse(x, y, .23, .42);
    g.lineBetween(x, y + .2, x + .1, .47);
  }
};
const paintShards: Painter = (g, n) => {
  for (let i = 0; i < n; i++) {
    const x = (i - (n - 1) / 2) * .23;
    g.lineStyle(.025, i % 2 ? 0xa86657 : 0x96bdcb, .85).fillStyle(i % 2 ? 0x8b5449 : 0x7199a9, .4);
    g.fillTriangle(x - .10, .3, x + .06, -.45, x + .15, .12).strokeTriangle(x - .10, .3, x + .06, -.45, x + .15, .12);
  }
};
const paintSouls: Painter = (g, _n, strong) => {
  g.lineStyle(.025, GHOST, .75).fillStyle(GHOST, .16);
  for (let i = 0; i < (strong ? 3 : 2); i++) {
    const x = (i - (strong ? 1 : .5)) * .35, y = (i % 2) * .16 - .1;
    g.fillEllipse(x, y, .26, .35).strokeEllipse(x, y, .26, .35);
    g.lineBetween(x - .12, y + .1, x, y + .46).lineBetween(x, y + .46, x + .12, y + .1);
    g.fillStyle(0x192e35, .8).fillCircle(x - .045, y, .025).fillCircle(x + .045, y, .025);
  }
};
const paintAstral: Painter = (g, _n, strong) => {
  g.lineStyle(.025, 0xa3a2bf, .65);
  g.strokeEllipse(-.10, 0, .65, .88).strokeEllipse(.12, -.1, .65, .88);
  if (strong) g.strokeEllipse(0, .05, 1.0, .53);
};
const paintCircuit: Painter = g => {
  g.lineStyle(.03, GHOST, .7);
  g.beginPath().moveTo(-.45, .2).lineTo(-.18, -.3).lineTo(.14, .18).lineTo(.44, -.24).strokePath();
  for (const [x, y] of [[-.45, .2], [-.18, -.3], [.14, .18], [.44, -.24]]) g.strokeCircle(x, y, .06);
};

const PAINTERS: Record<string, Painter> = {
  plates: paintPlates, reinforcement: paintPlates, crown: paintCrown, talons: paintClaws, fangs: paintClaws, precision: paintClaws,
  fissures: paintFissures, glands: paintGlands, shards: paintShards, souls: paintSouls, astral: paintAstral, circuit: paintCircuit,
};
