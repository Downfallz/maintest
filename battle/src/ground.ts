import Phaser from 'phaser';
import { DEPTH, EMBERS, EMBER_COUNT, FOG, LIGHT, PAINTING, REST_TINT, coronaScale, depthPlace, depthShift, emberAt, emberGlow, fogDrift, lightAt, mix } from '../../table/battle-ground.js';

/** How the view stands off its fitted place: its offset in pixels and its zoom over the fitted one. */
export interface ViewOffset { x: number; y: number; zoom: number }
/** What takes the ground's tint beside the painting: the horizon's plates and its arch. */
export interface Tinted { tint(painting: number, arch: number): void; lean(sky: (x: number, y: number) => { x: number; y: number; scale: number }): void; readonly sky: boolean }

const FOG_KEY = 'ground-fog', EMBER_KEY = 'ground-ember', EMBER_FRAME = 16, CORONA_KEY = 'ground-corona', CORONA_FRAME = 128;
const STILL: ViewOffset = { x: 0, y: 0, zoom: 1 };
const hex = (colour: number) => `0x${colour.toString(16).padStart(6, '0')}`;
const round = (value: number) => Math.round(value * 1000) / 1000;

// The ground's life (docs/art/storyboards/ground.md, BC9): a band of fog on the horizon and embers rising from the
// floor, both read from the scene's ambient clock and nothing else, so a hidden tab, which stops that clock, stops
// them, and two films of the field at rest agree; the depth of the far layers when the view moves; and the
// eclipse's corona answering a blow. The numbers are in `table/battle-ground.js`.
export class BattleGround {
  private readonly fog: Phaser.GameObjects.TileSprite;
  private readonly embers: Phaser.GameObjects.Image[] = [];
  private readonly light: Phaser.GameObjects.Image;
  private field = { width: 1, height: 1, top: 0, scale: 1 };
  private fogHome = { x: 0, y: 0, tile: 1 };
  private lightHome = { x: 0, y: 0, radius: 1 };
  private view: ViewOffset = STILL;
  private drift = 0;
  private shown = 0;
  private glow = 1;
  private corona = 1;
  private strength = 0;
  private colour: number | null = null;
  private tint: number = REST_TINT.painting;

  constructor(private readonly scene: Phaser.Scene, private readonly backdrop: Phaser.GameObjects.Image, private readonly horizon: Tinted) {
    this.textures();
    this.fog = scene.add.tileSprite(0, 0, 8, 8, FOG_KEY).setName('ground-fog').setOrigin(.5, 0).setTint(FOG.tint).setAlpha(FOG.alpha).setDepth(-8.9);
    for (let index = 0; index < EMBER_COUNT; index++) {
      this.embers.push(scene.add.image(0, 0, EMBER_KEY).setName('ground-ember').setTint(EMBERS.tint).setBlendMode(Phaser.BlendModes.ADD).setDepth(-8.8).setVisible(false));
    }
    // Over the sky's plates, under the arch, the faults and every figure: it lights the sky and never covers a body.
    this.light = scene.add.image(0, 0, CORONA_KEY).setName('ground-light').setBlendMode(Phaser.BlendModes.ADD).setDepth(-9.45).setAlpha(0).setVisible(false);
  }

  // Both textures are drawn once, in white: a tint colours them. The fog tiles across: a puff that crosses an edge
  // is drawn again on the other side.
  private textures() {
    const textures = this.scene.textures;
    if (!textures.exists(FOG_KEY)) {
      const { width, height } = FOG.tile;
      const canvas = textures.createCanvas(FOG_KEY, width, height)!;
      const context = canvas.getContext();
      for (const [x, y, across, down, alpha] of FOG.puffs as readonly number[][]) {
        for (const at of [x - width, x, x + width]) {
          context.save();
          context.translate(at, y);
          context.scale(1, down / across);
          const soft = context.createRadialGradient(0, 0, 0, 0, 0, across);
          soft.addColorStop(0, `rgba(255,255,255,${alpha})`); soft.addColorStop(1, 'rgba(255,255,255,0)');
          context.fillStyle = soft;
          context.fillRect(-across, -across, across * 2, across * 2);
          context.restore();
        }
      }
      canvas.refresh();
    }
    if (!textures.exists(CORONA_KEY)) {
      // A soft ring: its frame's half is the ring's outer edge, and the disc inside it is left clear.
      const canvas = textures.createCanvas(CORONA_KEY, CORONA_FRAME, CORONA_FRAME)!;
      const context = canvas.getContext(), half = CORONA_FRAME / 2, { inner, peak, outer } = LIGHT.ring;
      const soft = context.createRadialGradient(half, half, half * inner / outer, half, half, half);
      soft.addColorStop(0, 'rgba(255,255,255,0)'); soft.addColorStop((peak - inner) / (outer - inner), 'rgba(255,255,255,1)'); soft.addColorStop(1, 'rgba(255,255,255,0)');
      context.fillStyle = soft;
      context.fillRect(0, 0, CORONA_FRAME, CORONA_FRAME);
      canvas.refresh();
    }
    if (!textures.exists(EMBER_KEY)) {
      const canvas = textures.createCanvas(EMBER_KEY, EMBER_FRAME, EMBER_FRAME)!;
      const context = canvas.getContext(), half = EMBER_FRAME / 2;
      const soft = context.createRadialGradient(half, half, 0, half, half, half);
      soft.addColorStop(0, 'rgba(255,255,255,1)'); soft.addColorStop(.5, 'rgba(255,255,255,.55)'); soft.addColorStop(1, 'rgba(255,255,255,0)');
      context.fillStyle = soft;
      context.fillRect(0, 0, EMBER_FRAME, EMBER_FRAME);
      canvas.refresh();
    }
  }

  /** The field was measured: the fog lies on the painting's horizon and the light on its eclipse, wherever the painting was laid. */
  layout(width: number, height: number) {
    const scale = this.backdrop.scaleY, left = this.backdrop.x - this.backdrop.displayWidth / 2, top = this.backdrop.y;
    this.field = { width, height, top, scale };
    const [from, to] = FOG.rows;
    const band = Math.max(8, (to - from) * scale);
    this.fogHome = { x: width / 2, y: top + from * scale, tile: band / FOG.tile.height };
    // A little wider than the field: a tremor of the view never shows the band's end.
    this.fog.setSize(width * 1.1 + 40, band).setTileScale(this.fogHome.tile);
    this.lightHome = { x: left + PAINTING.eclipse.x * this.backdrop.scaleX, y: top + PAINTING.eclipse.y * scale, radius: PAINTING.eclipse.radius * scale * LIGHT.ring.outer };
    this.lay();
  }

  /** One step of the ambient clock: the air moves by it, and the far layers lean by how the view stands. */
  update(clock: number, reduced: boolean, round: number | null, view: ViewOffset) {
    this.view = reduced ? STILL : view;
    this.drift = fogDrift(clock, reduced);
    this.glow = emberGlow(round);
    this.corona = coronaScale(round);
    this.shown = 0;
    const centre = this.centre;
    for (const [index, image] of this.embers.entries()) {
      if (reduced) { image.setVisible(false); continue; }
      const ember = emberAt(index, clock, this.field);
      const placed = depthPlace(ember, ember.far ? DEPTH.far : DEPTH.near, this.view, centre);
      image.setVisible(true).setPosition(placed.x, placed.y).setDisplaySize(ember.size, ember.size).setAlpha(ember.alpha * this.glow);
      this.shown++;
    }
    this.lay();
  }

  private get centre() { return { x: this.field.width / 2, y: this.field.height / 2 }; }

  // The fog and the sky's plates where the view's offset and zoom leave them; the light keeps to the sky it lights.
  private lay() {
    const centre = this.centre;
    const fog = depthPlace(this.fogHome, DEPTH.fog, this.view, centre);
    this.fog.setPosition(fog.x, fog.y).setScale(fog.scale);
    this.fog.tilePositionX = -this.drift / this.fogHome.tile;
    this.horizon.lean((x, y) => depthPlace({ x, y }, DEPTH.sky, this.view, centre));
    const sky = depthPlace(this.lightHome, this.horizon.sky ? DEPTH.sky : DEPTH.painting, this.view, centre);
    this.light.setPosition(sky.x, sky.y).setDisplaySize(this.lightHome.radius * 2 * sky.scale * this.corona, this.lightHome.radius * 2 * sky.scale * this.corona);
  }

  /**
   * The eclipse answers a blow: one rise and one fall of its light and of the ground's tint, in the family's
   * colour, by the strength the confirmed loss gives. The tweens are the scene's, so the cancel path's `killAll`
   * stops them and `clear` puts the ground back.
   */
  answer(colour: number | null, strength: number, rate: number) {
    this.clear();
    if (colour === null || strength <= 0) return;
    this.colour = colour;
    // One counter for the rise and the fall: two chained ones lose a step between them at 2×.
    const lasts = LIGHT.rise + LIGHT.fall;
    this.scene.tweens.addCounter({ from: 0, to: lasts, duration: lasts / rate, ease: 'Linear',
      onUpdate: tween => this.shine(strength * lightAt(tween.getValue() ?? 0)), onComplete: () => this.clear() });
  }

  private shine(strength: number) {
    this.strength = strength;
    const colour = this.colour ?? REST_TINT.painting;
    this.tint = mix(REST_TINT.painting, colour, strength * LIGHT.tint);
    this.backdrop.setTint(this.tint);
    this.horizon.tint(this.tint, mix(REST_TINT.arch, colour, strength * LIGHT.tint));
    this.light.setVisible(strength > 0).setAlpha(strength * LIGHT.alpha).setTint(colour);
  }

  /** The cancel path: no light, every tint at its rest. The air goes on: it is the clock's. */
  clear() {
    this.shine(0);
    this.colour = null;
  }

  /** How the ground stands now, for the bench, its film and the browser specs. */
  get state() {
    const shift = (factor: number) => { const { x, y, scale } = depthShift(factor, this.view); return { x: round(x), y: round(y), scale: round(scale) }; };
    return {
      fog: { drift: round(this.drift), alpha: FOG.alpha, visible: this.fog.visible },
      embers: { shown: this.shown, alpha: round(this.glow) },
      layers: { painting: shift(DEPTH.painting), floor: shift(DEPTH.floor), arch: shift(DEPTH.arch), ...(this.horizon.sky ? { sky: shift(DEPTH.sky) } : {}),
        fog: shift(DEPTH.fog), far: shift(DEPTH.far), near: shift(DEPTH.near) },
      light: { strength: round(this.strength), colour: this.colour === null ? null : hex(this.colour), tint: hex(this.tint), shown: this.light.visible },
    };
  }
}
