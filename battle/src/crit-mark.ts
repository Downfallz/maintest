import type Phaser from 'phaser';
import { calloutSpot } from '../../table/battle-layout.js';

// A caster's `Crit!` (ADR 0208): the word beside its head as its critical lands, gone with the result. A cast that
// could crit and did not shows nothing, and no die is drawn: the record keeps the face the roll showed (ADR 0154),
// the app says a chance as a percent and its outcome in a word. The word settles in; under reduced motion it is
// simply there.
export interface Crit { id: string; key: string }
// The caster's figure: its image, its place, and its scale at rest (never the breathed or the stretched one).
type Spot = { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number }; scale: number };

// The word's storyboard is docs/art/storyboards/movement.md, "A critical's word": it enters with the camera's punch
// and lands softly, without travelling.
const SETTLE_MS = 200;
const easeOut = (progress: number) => 1 - (1 - progress) ** 3;
// How far the acting halo's outer rim grows past the figure (acting-halo.ts): the word stands clear of it.
const RIM_GROWTH = 1.13;
// The word's colours: the page's ink on the field labels' dark plate. Gold stays the acting and target marker's, and a
// gold word over the gold rim read as part of it.
const CRIT_INK = '#ece9dc', PLATE = '#0b1519d9';
// Text is drawn into a texture and then sampled at a fractional position: drawn at twice its size it stays as
// sharp as the page's own labels around it.
const SHARP = 2;

export class CritMark {
  private shown: { crit: Crit; label: Phaser.GameObjects.Text; born: number; spot: Spot; size: number } | null = null;
  // The time of the last frame drawn.
  private time = 0;

  constructor(private readonly scene: Phaser.Scene) {}

  /** The critical the frame asks for, on the figure that stands for its caster; any other leaves. */
  show(crit: Crit | null, spot: Spot | null) {
    if (!crit || !spot) { this.clear(); return; }
    if (this.shown?.crit.key === crit.key) { this.shown.spot = spot; return; }
    this.clear();
    const label = this.scene.add.text(0, 0, 'Crit!', {
      fontFamily: 'system-ui, sans-serif', fontStyle: 'bold', color: CRIT_INK, backgroundColor: PLATE, padding: { x: 6, y: 2 },
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
    const type = calloutSpot(one.spot.home, 0, this.scene.scale.width).type;
    // The word's texture is drawn again only when its size changes, never on every frame.
    if (type !== one.size) { one.size = type; one.label.setFontSize(type); }
    // Beside the head and clear of the acting rim's top, inside the field whole (calloutSpot). The rim is measured
    // on the figure at rest: a caster that lunged or breathes does not carry the word with it.
    const { image, scale } = one.spot;
    const rise = image.height * scale * image.originY * RIM_GROWTH;
    const { x, y } = calloutSpot(one.spot.home, one.label.width, this.scene.scale.width, { rise, wordsHeight: one.label.height });
    const landed = reduced ? 1 : easeOut(Math.min(1, Math.max(0, (time - one.born) / SETTLE_MS)));
    one.label.setPosition(x, y).setScale(.8 + .2 * landed).setAlpha(.4 + .6 * landed);
  }

  /** A word still entering is settled at once: a skip that lands on its own action's result leaves it whole. */
  settle() {
    if (this.shown) this.shown.born = Number.NEGATIVE_INFINITY;
  }

  clear() {
    this.shown?.label.destroy();
    this.shown = null;
  }
}
