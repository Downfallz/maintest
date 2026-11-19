import type Phaser from 'phaser';
import { PUNCH, TRAUMA, shakeOf, traumaAfter, wander } from '../../table/battle-motion.js';

// The view's answer to a hit (the craft book, "Shake from trauma" and "Punch zoom"): one value, trauma, that a
// loss of health adds to and that falls at a steady rate, and a short punch of the zoom on a defeat or a critical
// hit. Both are laid over the camera as `fitCamera` placed it and never written into it: a resize, a skip or the
// end of the tremor puts the camera back exactly. Nothing is drawn from `Math.random`: two films of one hit agree.
export class ViewShake {
  private trauma = 0;
  private punch = 0;
  private clock = 0;
  private laid = false;
  private fitted = { zoomX: 1, zoomY: 1, scrollX: 0, scrollY: 0, side: 0 };

  constructor(private readonly scene: Phaser.Scene) {}

  /** The camera was fitted to the canvas: what the tremor is laid over, and the size its reach is a share of. */
  fit() {
    const camera = this.scene.cameras.main;
    this.fitted = { zoomX: camera.zoomX, zoomY: camera.zoomY, scrollX: camera.scrollX, scrollY: camera.scrollY,
      side: Math.min(camera.width / camera.zoomX, camera.height / camera.zoomY) };
    this.lay();
  }

  /** A hit adds to the trauma, up to its ceiling. */
  add(amount: number) {
    if (amount <= 0) return;
    if (!this.moving) this.clock = 0;
    this.trauma = Math.min(TRAUMA.ceiling, this.trauma + amount);
  }

  /** The zoom punches in once, and comes back by itself. */
  punchIn() {
    if (!this.moving) this.clock = 0;
    this.punch = 1;
  }

  get moving() { return this.trauma > 0 || this.punch > 0; }

  /** What the view does now, for the bench and the browser specs: the trauma, the offset in pixels, the zoom over its fitted one. */
  get state() {
    const camera = this.scene.cameras.main;
    const round = (value: number) => Math.round(value * 1000) / 1000;
    return { trauma: round(this.trauma), x: round(camera.scrollX - this.fitted.scrollX), y: round(camera.scrollY - this.fitted.scrollY),
      zoom: round(camera.zoomX / this.fitted.zoomX) };
  }

  /** One step of the action's clock, already divided by the playback rate's inverse: the tremor falls, the punch eases out. */
  update(delta: number) {
    if (!this.moving) return;
    this.clock += delta;
    this.trauma = traumaAfter(this.trauma, delta);
    this.punch = Math.max(0, this.punch - delta / PUNCH.lasts);
    this.lay();
  }

  /** The camera as it was fitted, for whoever measures the field while the view moves. The tremor goes on. */
  rest() {
    if (!this.laid) return;
    this.laid = false;
    this.scene.cameras.main.setZoom(this.fitted.zoomX, this.fitted.zoomY).setScroll(this.fitted.scrollX, this.fitted.scrollY);
  }

  /** The cancel path: no trauma, no punch, the camera as it was fitted. */
  clear() {
    this.trauma = 0;
    this.punch = 0;
    this.rest();
  }

  private lay() {
    if (!this.moving) { this.rest(); return; }
    const reach = shakeOf(this.trauma) * TRAUMA.reach * this.fitted.side;
    const { x, y } = wander(this.clock);
    // Quad.Out on the way back: fast at first, then gently to the fitted zoom.
    const zoom = 1 + PUNCH.amount * this.punch * this.punch;
    this.laid = true;
    this.scene.cameras.main.setZoom(this.fitted.zoomX * zoom, this.fitted.zoomY * zoom)
      .setScroll(this.fitted.scrollX + x * reach, this.fitted.scrollY + y * reach);
  }
}
