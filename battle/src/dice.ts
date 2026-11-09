import Phaser from 'phaser';

// The d20s the field shows beside the creatures that rolled them (ADR 0154): a roll-off's last throw while the turn
// order is read, a caster's critical roll as its action lands. A die is a small hexagonal d20 with its face, gold
// when it crit, slate when it fell short, ivory for a roll-off. It tumbles in, its number running, and settles;
// under reduced motion it is simply there.
//
// Each die says what it was for. A critical roll carries the face it needed under it ("Crit on 14+", then
// "Crit!" or "Needed 14+"). Roll-off dice of one tie share a colour, a line runs between them with "vs" on it,
// and each says where its creature finished in that tie ("1st", "2nd").
export interface Die { id: string; value: number; tone: 'crit' | 'miss' | 'roll'; key: string; group?: number; place?: number; rivals?: number; needed?: number | null }
type Spot = { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } };

const TONES = {
  crit: { fill: 0xeac88e, line: 0xfff0c8, text: '#142126' },
  miss: { fill: 0x2a3a40, line: 0x8a9ea2, text: '#d8e2dc' },
  roll: { fill: 0xe8e2d0, line: 0xfffaf0, text: '#142126' },
};
// One colour per tie, so two ties broken in the same round read apart.
const TIES = [0x6fc3e8, 0xf0a46e, 0xb79cf2, 0x8fdc9c];
const TIE_TEXT = ['#6fc3e8', '#f0a46e', '#b79cf2', '#8fdc9c'];
const TUMBLE_MS = 520;
const ORDINAL = ['1st', '2nd', '3rd', '4th', '5th', '6th'];

interface Shown {
  die: Die; box: Phaser.GameObjects.Container; shape: Phaser.GameObjects.Graphics; label: Phaser.GameObjects.Text;
  caption: Phaser.GameObjects.Text | null; born: number; spot: Spot; drawn: string; at: { x: number; y: number; size: number };
}

export class DiceLayer {
  private shown = new Map<string, Shown>();
  private links: Phaser.GameObjects.Graphics;
  private versus = new Map<number, Phaser.GameObjects.Text>();

  constructor(private scene: Phaser.Scene) {
    this.links = scene.add.graphics().setDepth(1999);
  }

  /** The dice the frame asks for, on the figures that stand for their creatures; the others leave. */
  show(dice: Die[], spotOf: (id: string) => Spot | null, now: number) {
    const wanted = new Set(dice.map(die => die.key));
    for (const [key, one] of this.shown) if (!wanted.has(key)) { one.box.destroy(); this.shown.delete(key); }
    for (const die of dice) {
      const spot = spotOf(die.id);
      if (!spot) continue;
      const current = this.shown.get(die.key);
      if (current) { current.spot = spot; continue; }
      const shape = this.scene.add.graphics(), label = this.scene.add.text(0, 0, '', { fontFamily: 'Georgia, serif', fontStyle: 'bold', color: TONES[die.tone].text })
        .setOrigin(.5);
      const words = this.captionOf(die, false);
      const caption = words === null ? null : this.scene.add.text(0, 0, words, {
        fontFamily: 'system-ui, sans-serif', fontStyle: 'bold', color: this.captionColour(die, false),
        backgroundColor: '#0b1519d9', padding: { x: 6, y: 2 },
      }).setOrigin(.5, 0);
      const box = this.scene.add.container(0, 0, caption ? [shape, label, caption] : [shape, label]).setDepth(2000);
      this.shown.set(die.key, { die, box, shape, label, caption, born: now, spot, drawn: '', at: { x: 0, y: 0, size: 0 } });
    }
    const groups = new Set([...this.shown.values()].map(one => one.die.group).filter((group): group is number => Boolean(group)));
    for (const [group, text] of this.versus) if (!groups.has(group)) { text.destroy(); this.versus.delete(group); }
    if (!this.shown.size) this.links.clear();
  }

  /** How many dice stand on the field now, for the page's own record of what it shows. */
  get count() { return this.shown.size; }

  private captionOf(die: Die, settled: boolean): string | null {
    if (die.tone === 'roll') return die.place && (die.rivals ?? 0) > 0 ? ORDINAL[die.place - 1] ?? `#${die.place}` : null;
    if (!die.needed) return settled ? (die.tone === 'crit' ? 'Crit!' : 'No crit') : null;
    if (!settled) return `Crit on ${die.needed}+`;
    return die.tone === 'crit' ? `Crit! ${die.needed}+` : `Needed ${die.needed}+`;
  }

  private captionColour(die: Die, settled: boolean) {
    if (die.tone === 'roll') return TIE_TEXT[((die.group ?? 1) - 1) % TIE_TEXT.length];
    if (!settled) return '#ece9dc';
    return die.tone === 'crit' ? '#ffe2a6' : '#adb8bd';
  }

  follow(time: number, reduced: boolean) {
    for (const one of this.shown.values()) {
      const { home } = one.spot;
      const size = Math.max(28, Math.min(46, home.size * .3));
      // Beside the head, on the figure's open side, never off the top of the field.
      const x = home.x + home.size * .42, y = Math.max(size * .7, home.y - home.size * .92);
      one.at = { x, y, size };
      const age = reduced ? TUMBLE_MS : time - one.born;
      const settled = age >= TUMBLE_MS;
      const progress = Math.min(1, age / TUMBLE_MS);
      const shownValue = settled ? one.die.value : 1 + Math.floor(((time / 45) + one.die.value * 7) % 20);
      // A settled die changes nothing frame to frame: its shape and its number (a text texture) are drawn again
      // only when the size or the face changes, never on every frame.
      const drawn = `${size}/${shownValue}/${settled}`;
      if (drawn !== one.drawn) {
        one.drawn = drawn;
        const tone = TONES[one.die.tone];
        // A roll-off's die is ringed in its tie's colour.
        const ring = one.die.tone === 'roll' && one.die.group ? TIES[(one.die.group - 1) % TIES.length] : tone.line;
        one.shape.clear();
        const points = Array.from({ length: 6 }, (_, index) => {
          const angle = Math.PI / 6 + index * Math.PI / 3;
          return new Phaser.Math.Vector2(Math.cos(angle) * size / 2, Math.sin(angle) * size / 2);
        });
        one.shape.fillStyle(tone.fill, 1).fillPoints(points, true).lineStyle(one.die.group ? 3 : 1.5, ring, .95).strokePoints(points, true, true);
        // The facets of a d20 seen face on: a triangle within the hexagon.
        one.shape.lineStyle(1, tone.line, .35).strokeTriangle(0, -size * .36, size * .32, size * .2, -size * .32, size * .2);
        one.label.setText(String(shownValue)).setFontSize(Math.round(size * (shownValue >= 10 ? .42 : .5)));
        const words = this.captionOf(one.die, settled);
        if (one.caption) {
          // The caption stays upright and readable while the die spins: it is counter-rotated below.
          one.caption.setText(words ?? '').setVisible(Boolean(words)).setColor(this.captionColour(one.die, settled))
            .setFontSize(Math.round(Math.max(11, size * .3))).setPosition(0, size * .5 + 3);
        }
      }
      const spin = settled ? 0 : (1 - progress) * Math.PI * 3;
      const lift = settled ? 0 : Math.sin(progress * Math.PI) * size * .5;
      const scale = settled ? 1 : .6 + .4 * progress;
      one.box.setPosition(x, y - lift).setRotation(spin).setScale(scale).setAlpha(settled ? 1 : .4 + .6 * progress);
      one.caption?.setRotation(-spin);
    }
    this.drawLinks();
  }

  // The dice of one tie are joined by a dashed line in its colour, "vs" at its middle: these rolled against each
  // other. A tie with one die on the field (its rival fell) draws no line.
  private drawLinks() {
    this.links.clear();
    const byGroup = new Map<number, Shown[]>();
    for (const one of this.shown.values()) if (one.die.group && one.at.size) byGroup.set(one.die.group, [...(byGroup.get(one.die.group) ?? []), one]);
    for (const [group, members] of byGroup) {
      const colour = TIES[(group - 1) % TIES.length];
      const ordered = [...members].sort((a, b) => (a.die.place ?? 0) - (b.die.place ?? 0));
      if (ordered.length < 2) { this.versus.get(group)?.setVisible(false); continue; }
      for (let index = 1; index < ordered.length; index++) this.dashed(ordered[index - 1].at, ordered[index].at, colour);
      const first = ordered[0].at, second = ordered[1].at;
      let label = this.versus.get(group);
      if (!label) {
        label = this.scene.add.text(0, 0, 'vs', { fontFamily: 'system-ui, sans-serif', fontStyle: 'bold', color: '#0b1519', backgroundColor: TIE_TEXT[(group - 1) % TIE_TEXT.length], padding: { x: 6, y: 1 } })
          .setOrigin(.5).setDepth(2001);
        this.versus.set(group, label);
      }
      label.setVisible(true).setFontSize(Math.round(Math.max(11, first.size * .3))).setPosition((first.x + second.x) / 2, (first.y + second.y) / 2);
    }
  }

  private dashed(from: { x: number; y: number; size: number }, to: { x: number; y: number; size: number }, colour: number) {
    const dx = to.x - from.x, dy = to.y - from.y, length = Math.hypot(dx, dy);
    if (length < 1) return;
    // From the edge of one die to the edge of the other, not through their faces.
    const start = from.size * .55, end = length - to.size * .55, dash = 7, gap = 5;
    this.links.lineStyle(2.5, colour, .85);
    for (let at = start; at < end; at += dash + gap) {
      const stop = Math.min(end, at + dash);
      this.links.lineBetween(from.x + dx * at / length, from.y + dy * at / length, from.x + dx * stop / length, from.y + dy * stop / length);
    }
  }

  clear() {
    for (const one of this.shown.values()) one.box.destroy();
    this.shown.clear(); this.links.clear();
    for (const text of this.versus.values()) text.destroy();
    this.versus.clear();
  }
}
