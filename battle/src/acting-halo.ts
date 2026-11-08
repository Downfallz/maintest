import Phaser from 'phaser';

const GOLD = 0xeac88e;
const KEY = 'acting-glow';

// The acting creature, at a glance and without a word: a warm light behind its body and a pool of it at its feet,
// breathing slowly. It follows the figure through a strike and holds still under reduced motion.
export class ActingHalo {
  private aura: Phaser.GameObjects.Image;
  private pool: Phaser.GameObjects.Image;
  private target: { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } } | null = null;

  constructor(scene: Phaser.Scene) {
    // A soft round glow drawn once: a radial gradient, so its edge fades instead of cutting.
    if (!scene.textures.exists(KEY)) {
      const canvas = scene.textures.createCanvas(KEY, 128, 128)!;
      const context = canvas.getContext();
      const glow = context.createRadialGradient(64, 64, 0, 64, 64, 64);
      glow.addColorStop(0, 'rgba(255,255,255,1)'); glow.addColorStop(.45, 'rgba(255,255,255,.45)'); glow.addColorStop(1, 'rgba(255,255,255,0)');
      context.fillStyle = glow; context.fillRect(0, 0, 128, 128); canvas.refresh();
    }
    this.aura = scene.add.image(0, 0, KEY).setTint(GOLD).setBlendMode(Phaser.BlendModes.ADD).setVisible(false);
    this.pool = scene.add.image(0, 0, KEY).setTint(GOLD).setBlendMode(Phaser.BlendModes.ADD).setVisible(false);
  }

  /** The figure that acts now, or null; a fallen figure is never lit. */
  show(target: { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } } | null) {
    this.target = target;
    this.aura.setVisible(Boolean(target)); this.pool.setVisible(Boolean(target));
  }

  get visible() { return Boolean(this.target); }

  follow(time: number, reduced: boolean) {
    const target = this.target;
    if (!target) return;
    const { image, home } = target;
    const breath = reduced ? 1 : .82 + .18 * Math.sin(time / 520);
    const middle = image.getCenter();
    this.aura.setPosition(image.x, middle.y ?? image.y).setDisplaySize(home.size * 1.15, home.size * .95)
      .setAlpha(.20 * breath).setDepth(home.y - .9);
    this.pool.setPosition(home.x, home.y - 2).setDisplaySize(home.size * 1.05, Math.max(18, home.size * .22))
      .setAlpha(.42 * breath).setDepth(home.y - .8);
  }

  destroy() { this.aura.destroy(); this.pool.destroy(); }
}
