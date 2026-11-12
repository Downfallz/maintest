import Phaser from 'phaser';

// Contours in the original eclipse-battle.webp. Both pieces meet at the keystone.
// Reusing the painted stone keeps its palette and detail without another boot download.
const standing = 'M48 328 L51 186 L64 177 L61 151 L71 144 L70 129 L59 125 L65 98 L64 89 L74 83 L79 48 L92 41 L100 20 L118 24 L127 17 L145 21 L151 37 L171 46 L174 65 L207 80 L218 86 L214 101 L222 111 L213 121 L215 136 Q188 151 184 182 L180 199 L183 204 L178 213 L183 324 L194 334 L190 348 L74 350 Z';
const falling = 'M215 136 L213 121 L222 111 L214 101 L218 86 L234 93 L237 100 L255 106 L257 98 L278 94 L285 97 L300 104 L299 130 L302 135 L299 149 L299 202 L295 211 L298 221 L296 265 L299 277 L297 330 L304 337 L302 350 L235 350 L237 333 L243 325 L243 208 L240 204 L243 197 Q240 156 219 143 Z';
const FALL_MS = 950;

export class HorizonArch {
  private readonly pier: Phaser.GameObjects.Image;
  private readonly span: Phaser.GameObjects.Image;
  private round: number | null = null;
  private elapsed = FALL_MS;
  private x = 0;
  private y = 0;
  private scale = 1;

  constructor(scene: Phaser.Scene, artwork: HTMLImageElement | HTMLCanvasElement) {
    const piece = (name: string, contour: string) => {
      const key = 'horizon-' + name;
      if (!scene.textures.exists(key)) {
        const texture = scene.textures.createCanvas(key, 264, 344)!;
        const ctx = texture.context;
        ctx.save();
        ctx.translate(-44, -12);
        ctx.clip(new Path2D(contour));
        ctx.drawImage(artwork, 0, 0);
        // The base recedes into the haze at the far edge of the field.
        ctx.globalCompositeOperation = 'destination-in';
        const haze = ctx.createLinearGradient(0, 323, 0, 351);
        haze.addColorStop(0, '#fff');
        haze.addColorStop(1, 'transparent');
        ctx.fillStyle = haze;
        ctx.fillRect(44, 12, 264, 344);
        ctx.restore();
        texture.refresh();
      }
      // Both pieces share the right foot as their pivot, so the split cannot drift on resize.
      return scene.add.image(0, 0, key).setName(key).setOrigin(.85, .98).setDepth(-9).setVisible(false)
        .setTint(0xa5b1b3).setAlpha(.91);
    };
    this.pier = piece('pier', standing);
    this.span = piece('span', falling);
  }

  layout(width: number, height: number, horizon: number) {
    this.scale = Math.min(width * .40, height * .28, 190) / 344;
    // The portrait formation has an opponent under the eclipse. Keep the keystone in the gap to its left.
    const center = width < 620 && matchMedia('(orientation: portrait)').matches ? .34 : .5;
    this.x = width * center + 92 * this.scale;
    this.y = horizon;
    this.pier.setPosition(this.x, this.y).setScale(this.scale);
    this.drawSpan();
  }

  present(round: number | null, animate: boolean) {
    if (!animate || round !== this.round) {
      this.elapsed = animate && this.round === 6 && round === 7 ? 0 : FALL_MS;
    }
    this.round = round;
    this.pier.setVisible(round !== null);
    this.drawSpan();
  }

  update(delta: number) {
    if (this.elapsed === FALL_MS) return;
    this.elapsed = Math.min(FALL_MS, this.elapsed + delta);
    this.drawSpan();
  }

  private drawSpan() {
    const progress = this.round !== null && this.round >= 7 ? this.elapsed / FALL_MS : 0;
    const drop = progress * progress;
    this.span.setVisible(this.round !== null && progress < 1).setScale(this.scale)
      .setPosition(this.x + 34 * this.scale * drop, this.y + 48 * this.scale * drop)
      .setAngle(57 * drop).setAlpha(.91 * (1 - drop));
  }
}
