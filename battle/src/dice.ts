import Phaser from 'phaser';

// The d20s the field shows beside the creatures that rolled them (ADR 0154): a roll-off's last throw while the turn
// order is read, a caster's critical roll as its action lands. A die is a small hexagonal d20 with its face, gold
// when it crit, slate when it fell short, ivory for a roll-off. It tumbles in, its number running, and settles;
// under reduced motion it is simply there.
export interface Die { id: string; value: number; tone: 'crit' | 'miss' | 'roll'; key: string }
type Spot = { image: Phaser.GameObjects.Image; home: { x: number; y: number; size: number } };

const TONES = {
  crit: { fill: 0xeac88e, line: 0xfff0c8, text: '#142126' },
  miss: { fill: 0x2a3a40, line: 0x8a9ea2, text: '#d8e2dc' },
  roll: { fill: 0xe8e2d0, line: 0xfffaf0, text: '#142126' },
};
const TUMBLE_MS = 520;

interface Shown { die: Die; box: Phaser.GameObjects.Container; shape: Phaser.GameObjects.Graphics; label: Phaser.GameObjects.Text; born: number; spot: Spot; drawn: string }

export class DiceLayer {
  private shown = new Map<string, Shown>();

  constructor(private scene: Phaser.Scene) {}

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
      const box = this.scene.add.container(0, 0, [shape, label]).setDepth(2000);
      this.shown.set(die.key, { die, box, shape, label, born: now, spot, drawn: '' });
    }
  }

  /** How many dice stand on the field now, for the page's own record of what it shows. */
  get count() { return this.shown.size; }

  follow(time: number, reduced: boolean) {
    for (const one of this.shown.values()) {
      const { home } = one.spot;
      const size = Math.max(28, Math.min(46, home.size * .3));
      // Beside the head, on the figure's open side, never off the top of the field.
      const x = home.x + home.size * .42, y = Math.max(size * .7, home.y - home.size * .92);
      const age = reduced ? TUMBLE_MS : time - one.born;
      const settled = age >= TUMBLE_MS;
      const progress = Math.min(1, age / TUMBLE_MS);
      const shownValue = settled ? one.die.value : 1 + Math.floor(((time / 45) + one.die.value * 7) % 20);
      // A settled die changes nothing frame to frame: its shape and its number (a text texture) are drawn again
      // only when the size or the face changes, never on every frame.
      const drawn = `${size}/${shownValue}`;
      if (drawn !== one.drawn) {
        one.drawn = drawn;
        const tone = TONES[one.die.tone];
        one.shape.clear();
        const points = Array.from({ length: 6 }, (_, index) => {
          const angle = Math.PI / 6 + index * Math.PI / 3;
          return new Phaser.Math.Vector2(Math.cos(angle) * size / 2, Math.sin(angle) * size / 2);
        });
        one.shape.fillStyle(tone.fill, 1).fillPoints(points, true).lineStyle(1.5, tone.line, .9).strokePoints(points, true, true);
        // The facets of a d20 seen face on: a triangle within the hexagon.
        one.shape.lineStyle(1, tone.line, .35).strokeTriangle(0, -size * .36, size * .32, size * .2, -size * .32, size * .2);
        one.label.setText(String(shownValue)).setFontSize(Math.round(size * (shownValue >= 10 ? .42 : .5)));
      }
      const spin = settled ? 0 : (1 - progress) * Math.PI * 3;
      const lift = settled ? 0 : Math.sin(progress * Math.PI) * size * .5;
      const scale = settled ? 1 : .6 + .4 * progress;
      one.box.setPosition(x, y - lift).setRotation(spin).setScale(scale).setAlpha(settled ? 1 : .4 + .6 * progress);
    }
  }

  clear() { for (const one of this.shown.values()) one.box.destroy(); this.shown.clear(); }
}
