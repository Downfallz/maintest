import type Phaser from 'phaser';
import { calloutSpot } from '../../table/battle-layout.js';

// A caster's `Crit!` (ADR 0208): the word beside its head as its critical lands, gone with the result. A cast that
// could crit and did not shows nothing, and no die is drawn: the record keeps the face the roll showed (ADR 0154),
// the app says a chance as a percent and its outcome in a word. The word settles in; under reduced motion it is
// simply there.
export interface Crit { id: string; key: string }
type Spot = { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } };

const SETTLE_MS = 180;
// The word's colours: the pale gold a critical's caption had on the field, on the field labels' dark plate.
const CRIT_GOLD = '#ffe2a6', PLATE = '#0b1519d9';
// Text is drawn into a texture and then sampled at a fractional position: drawn at twice its size it stays as
// sharp as the page's own labels around it.
const SHARP = 2;

export class CritMark {
  private shown: { crit: Crit; label: Phaser.GameObjects.Text; born: number; spot: Spot; size: number } | null = null;
  // The time of the last frame drawn.
  private time = 0;

  constructor(private scene: Phaser.Scene) {}

  /** The critical the frame asks for, on the figure that stands for its caster; any other leaves. */
  show(crit: Crit | null, spot: Spot | null) {
    if (!crit || !spot) { this.clear(); return; }
    if (this.shown?.crit.key === crit.key) { this.shown.spot = spot; return; }
    this.clear();
    const label = this.scene.add.text(0, 0, 'Crit!', {
      fontFamily: 'system-ui, sans-serif', fontStyle: 'bold', color: CRIT_GOLD, backgroundColor: PLATE, padding: { x: 6, y: 2 },
    }).setOrigin(.5).setResolution(SHARP).setDepth(2000);
    // The word is born on the first frame that draws it. A board reaches a resting field between two of its steps,
    // so a word born at the last step's time would have less of its settling left when it is first drawn.
    this.shown = { crit, label, born: Number.NaN, spot, size: 0 };
  }

  /** Whether the word still settles in, which keeps the field at its full rate. */
  settling(reduced: boolean) {
    return !reduced && this.shown !== null && (Number.isNaN(this.shown.born) || this.time - this.shown.born < SETTLE_MS);
  }

  follow(time: number, reduced: boolean) {
    this.time = time;
    const one = this.shown;
    if (!one) return;
    if (Number.isNaN(one.born)) one.born = time;
    const fit = calloutSpot(one.spot.home, 0, this.scene.scale.width);
    // The word's texture is drawn again only when its size changes, never on every frame.
    if (fit.size !== one.size) { one.size = fit.size; one.label.setFontSize(Math.round(Math.max(13, fit.size * .38))); }
    // Beside the head, inside the field whole (calloutSpot).
    const { x, y } = calloutSpot(one.spot.home, one.label.width, this.scene.scale.width);
    const progress = reduced ? 1 : Math.min(1, (time - one.born) / SETTLE_MS);
    one.label.setPosition(x, y).setScale(.8 + .2 * progress).setAlpha(.4 + .6 * progress);
  }

  clear() {
    this.shown?.label.destroy();
    this.shown = null;
  }
}
