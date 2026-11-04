import Phaser from 'phaser';
import type { MutationLayer } from './types';

// Static, bounded accents owned by one figure. No timelines, emitters or gameplay state.
// Their anchors follow the actual image transform, including lunge, recoil, idle and enemy mirroring.
export class MutationCues {
  private layers: { graphic: Phaser.GameObjects.Graphics; x: number; y: number; size: number }[] = [];

  constructor(scene: Phaser.Scene, layers: readonly MutationLayer[], creature: string, form: string) {
    for (const cue of layers.slice(0, 3)) {
      const graphic = scene.add.graphics().setName('mutation-cue').setData({ creature, ...cue });
      const head = ['crown', 'fangs', 'precision'].includes(cue.motif);
      const foot = ['talons', 'reinforcement'].includes(cue.motif);
      // Reviewed artwork has different head heights; source pixels are never cropped or modified.
      let x = head ? .76 : foot ? .68 : .47;
      let y = head ? (form === 'predator' ? .49 : .40) : foot ? .82 : .40;
      if (cue.slot === 'accent') { x = .33; y = .31; }
      if (cue.slot === 'capstone' && cue.motif === 'circuit') { x = .62; y = .56; }
      const size = cue.slot === 'accent' ? .15 : cue.stage >= 3 ? .29 : .21;
      this.draw(graphic, cue);
      this.layers.push({ graphic, x, y, size });
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
        .setScale(size * facing, size).setRotation(image.rotation).setDepth(image.depth + .1)
        .setAlpha(image.alpha * .8).setVisible(enabled);
    }
  }

  destroy() { for (const layer of this.layers) layer.graphic.destroy(); this.layers = []; }

  private draw(g: Phaser.GameObjects.Graphics, cue: MutationLayer) {
    const strong = cue.stage >= 3;
    const n = strong ? 5 : 3;
    const ivory = 0xb6b6a4, ghost = 0x83b8b6;
    g.lineStyle(.035, ivory, .9).fillStyle(0x4c5b60, .8);
    switch (cue.motif) {
      case 'plates':
      case 'reinforcement':
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .19;
          const y = (i % 2) * .09;
          const plate = [{ x: x - .17, y: y + .08 }, { x: x - .10, y: y - .20 },
            { x: x + .07, y: y - .32 }, { x: x + .18, y: y - .07 },
            { x: x + .11, y: y + .24 }, { x: x - .05, y: y + .31 }].map(point => new Phaser.Math.Vector2(point.x, point.y));
          g.fillStyle(i % 2 ? 0x646b67 : 0x535c5a, .85).fillPoints(plate, true);
          g.lineStyle(.022, 0xabb1a2, .5).strokePoints(plate, true);
          g.lineStyle(.035, 0x303b3c, .7).lineBetween(x - .10, y + .14, x + .10, y - .10);
        }
        break;
      case 'crown':
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .21;
          g.fillStyle(ivory, .6).fillTriangle(x - .09, .26, x + .07, -.45 - (i % 2) * .12, x + .12, .18);
        }
        g.lineBetween(-.4, .28, .4, .28);
        break;
      case 'talons':
      case 'fangs':
      case 'precision':
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .20;
          g.fillStyle(ivory, .65).fillTriangle(x - .08, -.25, x + .10, .42, x + .04, -.08);
          g.lineBetween(x - .08, -.25, x + .10, .42);
        }
        break;
      case 'fissures':
        g.lineStyle(.055, 0x985647, .8);
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .21;
          g.beginPath().moveTo(x - .12, -.42).lineTo(x + .06, -.14).lineTo(x - .03, .08).lineTo(x + .18, .38).strokePath();
        }
        break;
      case 'glands':
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .20, y = (i % 2) * .15;
          g.lineStyle(.025, 0x83966b, .9).fillStyle(0x647f59, .5);
          g.fillEllipse(x, y, .23, .42).strokeEllipse(x, y, .23, .42);
          g.lineBetween(x, y + .2, x + .1, .47);
        }
        break;
      case 'shards':
        for (let i = 0; i < n; i++) {
          const x = (i - (n - 1) / 2) * .23;
          g.lineStyle(.025, i % 2 ? 0xa86657 : 0x96bdcb, .85).fillStyle(i % 2 ? 0x8b5449 : 0x7199a9, .4);
          g.fillTriangle(x - .10, .3, x + .06, -.45, x + .15, .12).strokeTriangle(x - .10, .3, x + .06, -.45, x + .15, .12);
        }
        break;
      case 'souls':
        g.lineStyle(.025, ghost, .75).fillStyle(ghost, .16);
        for (let i = 0; i < (strong ? 3 : 2); i++) {
          const x = (i - (strong ? 1 : .5)) * .35, y = (i % 2) * .16 - .1;
          g.fillEllipse(x, y, .26, .35).strokeEllipse(x, y, .26, .35);
          g.lineBetween(x - .12, y + .1, x, y + .46).lineBetween(x, y + .46, x + .12, y + .1);
          g.fillStyle(0x192e35, .8).fillCircle(x - .045, y, .025).fillCircle(x + .045, y, .025);
        }
        break;
      case 'astral':
        g.lineStyle(.025, 0xa3a2bf, .65);
        g.strokeEllipse(-.10, 0, .65, .88).strokeEllipse(.12, -.1, .65, .88);
        if (strong) g.strokeEllipse(0, .05, 1.0, .53);
        break;
      case 'circuit':
        g.lineStyle(.03, ghost, .7);
        g.beginPath().moveTo(-.45, .2).lineTo(-.18, -.3).lineTo(.14, .18).lineTo(.44, -.24).strokePath();
        for (const [x, y] of [[-.45, .2], [-.18, -.3], [.14, .18], [.44, -.24]]) g.strokeCircle(x, y, .06);
        break;
    }
  }
}
