import Phaser from 'phaser';
import { conditionAuras } from '../../table/battle-auras.js';

// Where a figure's drawn body stands: its foot on the ground, the height and width of its silhouette.
export interface Body { footX: number; footY: number; width: number; height: number; depth: number }
interface Drawn { objects: Phaser.GameObjects.Shape[]; place(at: Body, time: number, still: boolean): void }

const STAR = 0xeac88e;
const BLOOD = 0xc9463d;
const MEND = 0x9fe0b4;
const WARD = 0xb9d4e6;
const BROKEN = 0xd18472;

// Three small stars circling over the head, passing behind it on the far side of their orbit.
function stun(scene: Phaser.Scene): Drawn {
  const stars = Array.from({ length: 3 }, () => scene.add.star(0, 0, 5, 3, 7, STAR, .95).setStrokeStyle(1, 0x3a2a12, .8));
  return { objects: stars, place(at, time, still) {
    const radius = Phaser.Math.Clamp(at.width * .2, 14, 28), y = at.footY - at.height * 1.04;
    stars.forEach((star, index) => {
      const turn = (still ? 0 : time / 700) + index * Math.PI * 2 / 3, front = Math.sin(turn) > 0;
      star.setPosition(at.footX + Math.cos(turn) * radius, y + Math.sin(turn) * radius * .32)
        .setAlpha(front ? .95 : .55).setScale(front ? 1 : .8).setAngle(still ? 0 : time / 9)
        .setDepth(at.depth + (front ? .0006 : -.0006)).setVisible(true);
    });
  } };
}

// A drop now and then, falling from the body and fading before the ground: a bleed.
function bleed(scene: Phaser.Scene): Drawn {
  const drops = Array.from({ length: 3 }, () => scene.add.ellipse(0, 0, 4, 6, BLOOD, .9));
  return { objects: drops, place(at, time, still) {
    drops.forEach((drop, index) => {
      const fall = still ? .3 + index * .15 : ((time / 1500) + index / 3) % 1;
      drop.setPosition(at.footX + at.width * (-.14 + index * .14), at.footY - at.height * (.62 - fall * .5))
        .setAlpha(still ? .55 : .85 * (1 - fall) * Math.min(1, fall * 6)).setDepth(at.depth + .0004).setVisible(true);
    });
  } };
}

// A few pale motes rising slowly from the body: a regeneration.
function mend(scene: Phaser.Scene): Drawn {
  const motes = Array.from({ length: 4 }, () => scene.add.circle(0, 0, 2.2, MEND, .8).setBlendMode(Phaser.BlendModes.ADD));
  return { objects: motes, place(at, time, still) {
    motes.forEach((mote, index) => {
      const rise = still ? .25 + index * .15 : ((time / 2600) + index / 4) % 1;
      mote.setPosition(at.footX + at.width * (-.2 + index * .13) + (still ? 0 : Math.sin(time / 500 + index) * 3), at.footY - at.height * (.15 + rise * .7))
        .setAlpha(still ? .5 : .75 * (1 - rise)).setDepth(at.depth + .0004).setVisible(true);
    });
  } };
}

// A thin shell around the body, breathing: its defense raised. Lowered, the same shell in ember, faltering.
function shell(colour: number, broken: boolean) {
  return (scene: Phaser.Scene): Drawn => {
    const ring = scene.add.ellipse(0, 0, 10, 10).setStrokeStyle(1.5, colour, 1).setFillStyle(colour, .05).setBlendMode(Phaser.BlendModes.ADD);
    return { objects: [ring], place(at, time, still) {
      const wave = Math.sin(time / (broken ? 140 : 650));
      const alpha = still ? .4 : broken ? (wave > .55 ? .15 : .45) : .3 + (wave + 1) * .1;
      // Sized, not scaled: a scaled ellipse scales its stroke too, and the shell would turn into a bubble.
      const width = Math.round(at.width * .78), height = Math.round(at.height * 1.04);
      if (ring.width !== width || ring.height !== height) ring.setSize(width, height);
      ring.setPosition(at.footX, at.footY - at.height * .5).setAlpha(alpha).setDepth(at.depth + .0003).setVisible(true);
    } };
  };
}

const KINDS: Record<string, (scene: Phaser.Scene) => Drawn> = {
  stun, 'health-loss': bleed, 'health-gain': mend, 'defense-gain': shell(WARD, false), 'defense-loss': shell(BROKEN, true),
};

// A figure's lasting conditions, drawn on it (battle-auras.js says which: a stun above, one condition on the body).
// Animated from the scene's update clock, like the capstone effects; reduced motion stills them. Never interactive.
export class ConditionAuras {
  private drawn: Drawn[] = [];
  key = '';
  constructor(private scene: Phaser.Scene) {}
  set(statuses: readonly string[]) {
    const aura = conditionAuras(statuses);
    const key = [aura.above, aura.body].filter(Boolean).join('+');
    if (key === this.key) return aura;
    this.destroy(); this.key = key;
    this.drawn = [aura.above, aura.body].filter((one): one is string => Boolean(one) && Boolean(KINDS[one!])).map(one => KINDS[one](this.scene));
    return aura;
  }
  follow(at: Body, visible: boolean, time: number, still: boolean) {
    for (const one of this.drawn) {
      if (visible) one.place(at, time, still);
      else for (const object of one.objects) object.setVisible(false);
    }
  }
  destroy() { for (const one of this.drawn) for (const object of one.objects) object.destroy(); this.drawn = []; this.key = ''; }
}
