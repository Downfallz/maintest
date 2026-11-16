import Phaser from 'phaser';
import { capstoneEffect } from '../../table/battle-evolution-art.js';

type Effect = NonNullable<ReturnType<typeof capstoneEffect>>;
interface Silhouette { left: number; top: number; width: number; height: number; footX: number; footY: number; depth: number }

// One drawn capstone effect: what it is made of, and how it follows a silhouette at a time. It is animated from the
// scene's update clock rather than tweens, which the scene clears whenever an action's motion is cancelled.
interface Drawn { objects: Phaser.GameObjects.Shape[]; place(at: Silhouette, time: number, still: boolean): void }

const MOTES = 7;

function glow(scene: Phaser.Scene, effect: Effect): Drawn {
  // A warm aura behind the body, breathing slowly.
  const halo = scene.add.ellipse(0, 0, 10, 10, effect.colour, .22).setBlendMode(Phaser.BlendModes.ADD);
  return { objects: [halo], place(at, time, still) {
    const pulse = still ? 1 : 1 + Math.sin(time / 520) * .06;
    halo.setPosition(at.left + at.width / 2, at.top + at.height * .55).setDisplaySize(at.width * .95 * pulse, at.height * .9 * pulse)
      .setAlpha(still ? .2 : .16 + (Math.sin(time / 520) + 1) * .05).setDepth(at.depth - .0004).setVisible(true);
  } };
}

function motes(scene: Phaser.Scene, effect: Effect): Drawn {
  // Sparks rising from the body and fading as they climb.
  const sparks = Array.from({ length: MOTES }, () => scene.add.circle(0, 0, 2.2, effect.colour, .8).setBlendMode(Phaser.BlendModes.ADD));
  return { objects: sparks, place(at, time, still) {
    sparks.forEach((spark, index) => {
      const lane = (index + .5) / MOTES, rise = still ? .35 + (index % 3) * .2 : ((time / 1900) + index / MOTES) % 1;
      spark.setPosition(at.left + at.width * (.15 + lane * .7) + (still ? 0 : Math.sin(time / 400 + index) * 4), at.footY - at.height * (.1 + rise * .85))
        .setAlpha(still ? .55 : .85 * (1 - rise)).setDepth(at.depth + .0004).setVisible(true);
    });
  } };
}

function aura(scene: Phaser.Scene, effect: Effect): Drawn {
  // A rune ring under the feet, turning; its lit arc shows the turn.
  const ring = scene.add.ellipse(0, 0, 10, 10).setStrokeStyle(2, effect.colour, .75).setFillStyle(effect.colour, .06);
  const arc = scene.add.ellipse(0, 0, 10, 10).setStrokeStyle(3, effect.colour, .95);
  return { objects: [ring, arc], place(at, time, still) {
    const width = at.width * .78, height = Math.max(12, at.height * .16);
    ring.setPosition(at.footX, at.footY - 2).setDisplaySize(width, height).setDepth(at.depth - .0003).setVisible(true);
    const turn = still ? 0 : (time / 900) % (Math.PI * 2);
    arc.setPosition(at.footX + Math.cos(turn) * width * .42, at.footY - 2 + Math.sin(turn) * height * .42).setDisplaySize(7, 7).setDepth(at.depth - .0002).setVisible(!still);
  } };
}

const KINDS: Record<string, (scene: Phaser.Scene, effect: Effect) => Drawn> = { glow, motes, aura };

// A figure's capstone effects, in their composed order (battle-evolution-art.js): each on its own depth band around
// the figure, drawn over whatever image the figure shows, never touching it. Reduced motion stills every effect.
// Nothing here is interactive: targeting stays on the page's own creature buttons.
export class CapstoneEffects {
  private drawn: Drawn[];
  readonly ids: string[];
  constructor(scene: Phaser.Scene, ids: string[]) {
    this.ids = ids;
    this.drawn = ids.map(id => capstoneEffect(id)).filter((one): one is Effect => Boolean(one) && Boolean(KINDS[one!.kind]))
      .map(effect => KINDS[effect.kind](scene, effect));
  }
  follow(image: Phaser.GameObjects.Image, visible: boolean, time: number, still: boolean) {
    if (!this.drawn.length) return;
    const bounds = image.getBounds();
    const at = { left: bounds.left, top: bounds.top, width: bounds.width, height: bounds.height, footX: image.x, footY: image.y, depth: image.depth };
    for (const one of this.drawn) {
      one.place(at, time, still);
      if (!visible) for (const object of one.objects) object.setVisible(false);
    }
  }
  destroy() { for (const one of this.drawn) for (const object of one.objects) object.destroy(); this.drawn = []; }
}
