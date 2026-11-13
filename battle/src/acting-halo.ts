import Phaser from 'phaser';

// A saturated gold: the pale UI gold reads as part of the bone-coloured illustrations.
const GOLD = 0xffb938;
const KEY = 'acting-glow';

type Lit = { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } };

/** A halo's colour: the rim's tint, and its pool's texture and the light that texture is drawn in (r,g,b). */
export interface HaloTone { tint: number; key: string; light: string; edge: string }
const ACTING: HaloTone = { tint: GOLD, key: KEY, light: '246,214,150', edge: '234,200,142' };
/** The ice blue the field lights a creature in when the turn order or a tie points at it: never the acting gold. */
export const FOCUS: HaloTone = { tint: 0x6fc3ff, key: 'focus-glow', light: '178,224,255', edge: '111,195,255' };

// The acting creature, at a glance and without a word: a gold rim around its own silhouette and a pool of light at
// its feet, breathing slowly. The rim is the figure's own image filled with gold just behind it, a little larger, so
// it hugs whatever illustration the creature shows; it follows the figure through a strike and holds still under
// reduced motion.
export class ActingHalo {
  private rims: Phaser.GameObjects.Image[];
  private pool: Phaser.GameObjects.Image;
  private target: Lit | null = null;

  constructor(scene: Phaser.Scene, tone: HaloTone = ACTING) {
    // The pool is a round glow drawn once, its colour in the texture and its edge fading to nothing, blended
    // normally: an additive blend lit the texture's transparent corners too, a faint square rather than a glow.
    if (!scene.textures.exists(tone.key)) {
      const canvas = scene.textures.createCanvas(tone.key, 128, 128)!;
      const context = canvas.getContext();
      const glow = context.createRadialGradient(64, 64, 0, 64, 64, 64);
      glow.addColorStop(0, `rgba(${tone.light},1)`); glow.addColorStop(.4, `rgba(${tone.edge},.55)`); glow.addColorStop(.75, `rgba(${tone.edge},.16)`);
      glow.addColorStop(1, `rgba(${tone.edge},0)`);
      context.fillStyle = glow; context.fillRect(0, 0, 128, 128); canvas.refresh();
    }
    // A tight bright rim and a wider faint one: together they read as a glow rather than a hard outline.
    this.rims = [0, 1].map(() => scene.add.image(0, 0, tone.key).setTint(tone.tint).setTintMode(Phaser.TintModes.FILL).setVisible(false));
    this.pool = scene.add.image(0, 0, tone.key).setVisible(false);
  }

  /** The figure that acts now, or null; a fallen figure is never lit. */
  show(target: Lit | null) {
    this.target = target;
    for (const rim of this.rims) rim.setVisible(Boolean(target));
    this.pool.setVisible(Boolean(target));
  }

  get visible() { return Boolean(this.target); }

  follow(time: number, reduced: boolean) {
    const target = this.target;
    if (!target) return;
    const { image, home } = target;
    const breath = reduced ? 1 : .82 + .18 * Math.sin(time / 520);
    for (const [index, rim] of this.rims.entries()) {
      if (rim.texture.key !== image.texture.key || rim.frame.name !== image.frame.name) rim.setTexture(image.texture.key, image.frame.name);
      const grow = index === 0 ? 1.06 : 1.13;
      rim.setOrigin(image.originX, image.originY).setFlipX(image.flipX).setAngle(image.angle)
        .setScale(image.scaleX * grow, image.scaleY * grow).setPosition(image.x, image.y + (index === 0 ? 2 : 4))
        .setAlpha((index === 0 ? 1 : .42) * breath * image.alpha).setDepth(image.depth - .0004 - index * .0001)
        .setVisible(image.visible);
    }
    this.pool.setPosition(home.x, home.y - 2).setDisplaySize(home.size * 1.15, Math.max(22, home.size * .26))
      .setAlpha(.85 * breath).setDepth(home.y - .8);
  }

  destroy() { for (const rim of this.rims) rim.destroy(); this.pool.destroy(); }
}
