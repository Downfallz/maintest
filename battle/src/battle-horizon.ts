import Phaser from 'phaser';
import { HorizonArch } from './horizon-arch';

// The sky feathers into the floor across source rows 399–479. When both are present they reconstruct
// the final painting. The background still owns the original crop and camera in every orientation.
const plates = [
  { file: 'breach', round: 6, top: 0, depth: -9.8 },
  { file: 'shattered-sky', round: 10, top: 0, depth: -9.5 },
  { file: 'broken-floor', round: 12, top: 399, depth: -9.6 },
] as const;
const TRANSITION_MS = 950;

export class BattleHorizon {
  private readonly arch: HorizonArch;
  private readonly faults: Phaser.GameObjects.Graphics;
  private readonly requested = new Set<string>();
  private readonly images = new Map<string, Phaser.GameObjects.Image>();
  private round: number | null = null;
  private elapsed = TRANSITION_MS;
  private width = 0;
  private height = 0;

  constructor(private scene: Phaser.Scene, private backdrop: Phaser.GameObjects.Image, private assetRoot: string) {
    this.arch = new HorizonArch(scene, backdrop.texture.getSourceImage() as HTMLImageElement);
    this.faults = scene.add.graphics().setDepth(-9.25).setName('horizon-faults');
  }

  layout(width: number, height: number) {
    this.width = width;
    this.height = height;
    this.arch.layout(width, height, this.backdrop.y + this.backdrop.displayHeight * .42);
    for (const plate of plates) {
      this.images.get(plate.file)?.setScale(this.backdrop.scaleX, this.backdrop.scaleY)
        .setPosition(this.backdrop.x, this.backdrop.y + plate.top * this.backdrop.scaleY);
    }
    this.draw();
  }

  present(round: number | null, animate: boolean) {
    if (round !== this.round) {
      // The lab advances between consecutive milestones. A seek or reconnect applies its destination at once.
      const adjacent = this.round !== null && round !== null && round % 2 === 0
        && Math.floor(this.round / 2) + 1 === round / 2;
      this.elapsed = animate && adjacent ? 0 : TRANSITION_MS;
      this.round = round;
    }
    if (!animate) this.elapsed = TRANSITION_MS;
    if (round !== null) {
      for (const plate of plates) {
        // A late join needs only the visible tiles. The breach also backs the feathered sky until round 12.
        if (round >= plate.round - 1 && (plate.round !== 6 || round < 12)) this.load(plate);
      }
    }
    this.draw();
  }

  private load(plate: typeof plates[number]) {
    if (this.requested.has(plate.file)) return;
    this.requested.add(plate.file);
    const key = 'horizon-' + plate.file;
    this.scene.load.once('filecomplete-image-' + key, () => {
      const image = this.scene.add.image(0, 0, key).setName(key).setOrigin(.5, 0)
        .setTint(0xb5bbc2).setDepth(plate.depth);
      this.images.set(plate.file, image);
      // The player may have rewound or rotated while the file was in flight.
      this.layout(this.width, this.height);
    });
    this.scene.load.image(key, this.assetRoot + 'horizon/' + plate.file + '.webp');
    if (!this.scene.load.isLoading()) this.scene.load.start();
  }

  update(delta: number) {
    if (this.elapsed === TRANSITION_MS) return;
    this.elapsed = Math.min(TRANSITION_MS, this.elapsed + delta);
    this.draw();
  }

  private draw() {
    const progress = this.elapsed / TRANSITION_MS;
    this.arch.draw(this.round, progress);
    for (const plate of plates) {
      const visible = this.round !== null && this.round >= plate.round;
      this.images.get(plate.file)?.setVisible(visible).setAlpha(this.round === plate.round ? progress : 1);
    }
    this.drawFaults(progress);
  }

  // After the floor gives way, another fault opens on each even round. Work stays bounded for
  // custom long matches, and replay reconstructs the same visible cracks directly from the round.
  private drawFaults(progress: number) {
    const round = this.round ?? 0;
    const count = Math.max(0, Math.floor(round / 2) - 6);
    this.faults.clear().setVisible(count > 0);
    for (let index = Math.max(0, count - 8); index < count; index++) {
      const opening = index === count - 1 && round % 2 === 0 ? progress : 1;
      const start = .14 + .72 * ((.31 + index * .618) % 1);
      const direction = index % 2 === 0 ? 1 : -1;
      const points = Array.from({ length: 9 }, (_, step) => {
        const along = step / 8;
        const bend = Math.sin(index * 2.3 + step * 2.7) * .023;
        return new Phaser.Math.Vector2(this.width * (start + direction * (along * .12 + bend)),
          this.height * (1 - along * .44 * opening));
      });
      const lip = points.map((point, step) => new Phaser.Math.Vector2(point.x + this.width * .013 * (1 - step / 8) * opening, point.y));
      this.faults.fillStyle(0x100f12, .86).fillPoints([...points, ...lip.reverse()], true)
        .lineStyle(Math.max(1, this.width * .0016), 0xd18d51, .67 * opening).strokePoints(points);
    }
  }
}
