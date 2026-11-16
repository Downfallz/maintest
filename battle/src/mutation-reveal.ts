import Phaser from 'phaser';
import type { MutationRevealMarker, Placement, SceneFrame } from './types';

const INK = '#ece9dc';
const GOLD = '#eac88e';
const resolution = () => Math.min(2, window.devicePixelRatio || 1);
const hex = (colour: string) => {
  const parsed = Phaser.Display.Color.ValueToColor(colour);
  return Phaser.Display.Color.GetColor(parsed.red, parsed.green, parsed.blue);
};

// A public reveal written on the battlefield: one crest above each creature that mutated, in its family's
// colour, naming the mutation, the stage it reached and what it bought. The receipt and its cards say the
// rest; the crest is the at-a-glance reading a phone has room for.
export class MutationReveal {
  private crests = new Map<string, Phaser.GameObjects.Container>();
  private key = '';

  constructor(private scene: Phaser.Scene) {}

  private crest(marker: MutationRevealMarker) {
    const accent = hex(marker.colour);
    const name = this.scene.add.text(0, 0, marker.mutation, { fontFamily: 'Georgia, serif', fontSize: '16px', color: marker.colour })
      .setOrigin(0, .5).setResolution(resolution());
    const stage = this.scene.add.text(0, 0, marker.stage == null ? '' : `STAGE ${marker.stage}`, {
      fontFamily: 'system-ui, sans-serif', fontSize: '10px', fontStyle: 'bold', color: '#aebbb4' }).setOrigin(0, .5).setResolution(resolution());
    const gain = marker.gains.length ? marker.gains.slice(0, 2).map(one => `+ ${one}`).join('  ') + (marker.gains.length > 2 ? '  …' : '') : marker.fallback;
    const detail = this.scene.add.text(0, 0, gain, { fontFamily: 'system-ui, sans-serif', fontSize: '12px', fontStyle: 'bold', color: marker.gains.length ? GOLD : INK })
      .setOrigin(0, .5).setResolution(resolution());
    const pad = 10, bar = 4, gap = 6;
    const top = Math.max(name.width + (stage.width ? gap + stage.width : 0), detail.width);
    const width = bar + pad * 2 + top, height = name.height + detail.height + 10;
    const left = -width / 2 + bar + pad;
    name.setPosition(left, -height / 2 + 5 + name.height / 2);
    stage.setPosition(left + name.width + gap, name.y + 1);
    detail.setPosition(left, name.y + name.height / 2 + detail.height / 2 + 1);
    const plate = this.scene.add.graphics();
    plate.fillStyle(0x0d1a1f, .94).fillRoundedRect(-width / 2, -height / 2, width, height, 7);
    plate.lineStyle(1, accent, .9).strokeRoundedRect(-width / 2, -height / 2, width, height, 7);
    plate.fillStyle(accent, 1).fillRoundedRect(-width / 2, -height / 2, bar + 3, height, { tl: 7, bl: 7, tr: 0, br: 0 });
    // A small pointer ties the crest to the creature under it.
    plate.fillStyle(0x0d1a1f, .94).fillTriangle(-6, height / 2 - .5, 6, height / 2 - .5, 0, height / 2 + 6);
    plate.lineStyle(1, accent, .9).lineBetween(-6, height / 2, 0, height / 2 + 6).lineBetween(6, height / 2, 0, height / 2 + 6);
    return this.scene.add.container(0, 0, [plate, name, stage, detail]).setSize(width, height + 6).setDepth(2150);
  }

  present(cue: SceneFrame['reveal'], homes: Map<string, Placement>, reduced: boolean) {
    const key = JSON.stringify([cue?.round, cue?.markers]);  // the inset moves crests, it never rebuilds them
    const fresh: Phaser.GameObjects.Container[] = [];
    if (key !== this.key) {
      for (const crest of this.crests.values()) { this.scene.tweens.killTweensOf(crest); crest.destroy(); }
      this.crests.clear();
      this.key = key;
      for (const marker of cue?.markers ?? []) {
        if (!homes.has(marker.id)) continue;
        const crest = this.crest(marker);
        this.crests.set(marker.id, crest); fresh.push(crest);
      }
    }
    const { displayWidth: width, displayHeight: height } = this.scene.cameras.main;
    for (const [id, crest] of this.crests) {
      const home = homes.get(id);
      if (!home) { crest.setVisible(false); continue; }
      // As wide as the creature's own target at most, so neighbours never overlap; never past the stage edge.
      const scale = Math.max(.1, Math.min(1, (Math.max(home.hitWidth, home.size) + 12) / crest.width, height * .3 / crest.height));
      this.scene.tweens.killTweensOf(crest);
      crest.setScale(scale).setAlpha(1);
      const half = crest.width * scale / 2, rise = crest.height * scale / 2;
      crest.setVisible(true).setPosition(Phaser.Math.Clamp(home.x, half + 4, width - half - 4),
        Phaser.Math.Clamp(home.y - home.size * .98 - rise, rise + 2 + (cue?.inset ?? 0), height - rise));
      if (!reduced && fresh.includes(crest)) {
        this.scene.tweens.add({ targets: crest, alpha: { from: 0, to: 1 }, scale: { from: scale * .82, to: scale }, duration: 260, ease: 'Back.Out' });
      }
    }
  }
}
