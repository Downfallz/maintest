import Phaser from 'phaser';
import type { Placement, SceneFrame, UpkeepMarker } from './types';

const toneColors: Record<string, string> = { energy: '#b9e2ff', recovery: '#ace0bf', harm: '#ffb9a8', condition: '#eac88e' };

// One anchored plaque per recipient. Keep objects across polls and recompute only positions
// on resize. Historical receipts lay out identities alone; they never borrow a later board.
export class UpkeepMarkers {
  private labels = new Map<string, Phaser.GameObjects.Container>();
  private key = '';

  constructor(private scene: Phaser.Scene) {}

  private plaque(marker: UpkeepMarker, recorded: boolean) {
    const lines = recorded ? [{ text: marker.name, tone: 'name' }, ...marker.lines] : marker.lines;
    const text = lines.map(line => this.scene.add.text(0, 0, line.text, {
      fontFamily: line.tone === 'name' ? 'Georgia, serif' : 'system-ui, sans-serif',
      fontSize: line.tone === 'name' ? '15px' : '13px',
      fontStyle: line.tone === 'name' ? 'normal' : 'bold',
      color: toneColors[line.tone] ?? '#ece9dc',
    }).setOrigin(.5, 0).setResolution(Math.min(2, window.devicePixelRatio || 1)));
    const width = Math.max(...text.map(line => line.width)) + 16;
    const height = text.reduce((sum, line) => sum + line.height, 0) + 8;
    let y = -height / 2 + 4;
    for (const line of text) { line.y = y; y += line.height; }
    const plate = this.scene.add.graphics();
    plate.fillStyle(0x101e24, .95).fillRoundedRect(-width / 2, -height / 2, width, height, 5);
    plate.lineStyle(1, recorded ? 0xb79868 : 0x7294a2, .85).strokeRoundedRect(-width / 2, -height / 2, width, height, 5);
    return this.scene.add.container(0, 0, [plate, ...text]).setSize(width, height).setDepth(2100);
  }

  present(cue: SceneFrame['upkeep'], homes: Map<string, Placement>, reduced: boolean) {
    const key = JSON.stringify([cue, reduced]);
    if (key !== this.key) {
      for (const label of this.labels.values()) {
        this.scene.tweens.killTweensOf(label);
        label.destroy();
      }
      this.labels.clear();
      this.key = key;
      if (cue) for (const marker of cue.markers) {
        if (!cue.recorded && !homes.has(marker.id)) continue;
        const label = this.plaque(marker, cue.recorded);
        if (!reduced) this.scene.tweens.add({ targets: label, alpha: { from: .35, to: 1 }, duration: 220 });
        this.labels.set(marker.id, label);
      }
    }
    if (!cue) return;
    const { displayWidth: width, displayHeight: height } = this.scene.cameras.main;
    for (const side of ['Opponent', 'Your team']) {
      const row = cue.markers.filter(marker => marker.side === side);
      row.forEach((marker, i) => {
        const label = this.labels.get(marker.id);
        if (!label) return;
        const home = homes.get(marker.id);
        const x = home?.x ?? width * (i + .5) / row.length;
        const y = home ? home.y - home.size * .76 : height * (side === 'Opponent' ? .28 : .74);
        const available = home?.hitWidth ?? width / row.length;
        label.setScale(Math.max(.1, Math.min(1, (available - 4) / label.width, height * .45 / label.height)));
        label.setPosition(x, Phaser.Math.Clamp(y, label.displayHeight / 2, height - label.displayHeight / 2));
      });
    }
  }
}
