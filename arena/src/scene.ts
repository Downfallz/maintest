import Phaser from 'phaser';
import { placements, strikeApproach } from '../../table/arena-layout.js';
import { CREATURE_ART, figureArt, figureGeometry } from '../../table/arena-art.js';
import { DRAW_MS, STRIKE_MS } from '../../table/arena-timing.js';
import type { SceneFrame, Placement } from './types';
import { SpellEffects, type EffectAnchor } from './spell-effects';

const playing = (state: SceneFrame) => state.stage === 'windup' || state.stage === 'impact';

interface Figure {
  image: Phaser.GameObjects.Image; shadow: Phaser.GameObjects.Ellipse; ring: Phaser.GameObjects.Ellipse;
  home: Placement; art: ReturnType<typeof figureArt>; enemy: boolean;
}

const GOLD = 0xeac88e;
const EMBER = 0xd18472;
const MOTES = 24;

// A remainder that stays in [0, span) whatever the sign of the value: `%` alone turns negative once a
// drift outgrows its offset, and a mote that goes negative leaves the screen for good.
const wrap = (value: number, span: number) => ((value % span) + span) % span;

export class ArenaScene extends Phaser.Scene {
  private figures = new Map<string, Figure>();
  private backdrop?: Phaser.GameObjects.Image;
  private motes: Phaser.GameObjects.Arc[] = [];
  private effects: Phaser.GameObjects.GameObject[] = [];
  private spellEffects = new SpellEffects(this);
  private state!: SceneFrame;
  private reduced = false;
  private rate = 1;
  private previous = '';
  private ready = false;
  private roster = '';
  onLayout: (positions: Placement[]) => void = () => {};
  onReady: () => void = () => {};
  onResizeDuringPlayback: () => void = () => {};
  onImpact: () => void = () => {};
  assetFailed = false;

  constructor(private assetRoot: string) { super('arena'); }

  preload() {
    this.load.image('arena-floor', `${this.assetRoot}eclipse-arena.webp`);
    for (const asset of CREATURE_ART) this.load.image(asset.key, `${this.assetRoot}${asset.file}`);
    this.load.on('loaderror', () => { this.assetFailed = true; });
  }

  create() {
    if (this.textures.exists('arena-floor')) this.backdrop = this.add.image(0, 0, 'arena-floor').setOrigin(.5, 0).setTint(0xb5bbc2).setDepth(-10);
    // A local vector fallback keeps the scene selectable if an image fails to load.
    const fallback = this.add.graphics();
    fallback.fillStyle(0x71867e).fillTriangle(40, 0, 0, 90, 80, 90);
    fallback.generateTexture('missing-figure', 80, 100);
    fallback.destroy();
    for (let index = 0; index < MOTES; index++) this.motes.push(this.add.circle(0, 0, index % 3 === 0 ? 2 : 1, GOLD, .24).setDepth(1000));
    this.scale.on('resize', () => {
      if (this.state && playing(this.state)) this.onResizeDuringPlayback();
      this.arrange();
    });
    this.ready = true;
    this.onReady();
  }

  present(state: SceneFrame, reduced: boolean, rate: number) {
    this.state = state;
    this.reduced = reduced;
    this.rate = rate;
    if (!this.ready) return;
    const roster = JSON.stringify(state.creatures.map(one => [one.id, one.form, one.enemy]));
    const rosterChanged = roster !== this.roster;
    if (rosterChanged) {
      this.clearMotion();
      for (const figure of this.figures.values()) { figure.image.destroy(); figure.shadow.destroy(); figure.ring.destroy(); }
      this.figures.clear();
      this.roster = roster;
      this.makeFigures();
    }
    const changed = state.stage !== this.previous;
    if (changed && (state.stage === 'decision' || state.stage === 'resolved')) this.clearMotion();
    this.arrange(rosterChanged);
    for (const creature of state.creatures) {
      const figure = this.figures.get(creature.id)!;
      const acting = creature.id === state.actor;
      const selected = creature.id === state.target || state.selected?.includes(creature.id) === true;
      const legal = state.stage === 'targeting' && state.targets.includes(creature.id);
      figure.ring.setStrokeStyle(selected ? 3 : 1.5, selected || acting ? GOLD : EMBER, selected || legal || acting ? .95 : .15);
      figure.image.setAlpha(creature.isAlive ? 1 : .20);
      figure.shadow.setAlpha(creature.isAlive ? .55 : .2);
    }
    if (changed && !reduced && state.stage === 'windup') this.windup();
    if (changed && state.stage === 'impact') {
      this.onImpact();
      if (!reduced) this.impact();
    }
    this.previous = state.stage;
  }

  private makeFigures() {
    for (const creature of this.state.creatures) {
      const enemy = creature.enemy;
      const shadow = this.add.ellipse(0, 0, 100, 20, 0x020608, .55);
      const ring = this.add.ellipse(0, 0, 110, 26).setStrokeStyle(1, GOLD, .2);
      const art = figureArt(creature.form, (key: string) => this.textures.exists(key));
      const image = this.add.image(0, 0, art.key).setFlipX(enemy);
      // Team identity remains in labels, formation and targeting; preserve the approved art palette.
      this.figures.set(creature.id, { image, shadow, ring, art, enemy, home: { x: 0, y: 0, size: 0, hitWidth: 0 } });
    }
  }

  private arrange(resetPositions = false) {
    const { width, height } = this.scale;
    if (this.backdrop) {
      const source = this.backdrop.texture.getSourceImage();
      const factor = Math.max(width / source.width, height / source.height);
      this.backdrop.setDisplaySize(source.width * factor, source.height * factor).setPosition(width / 2, -(source.height * factor - height) * .35);
    }
    if (!this.figures.size) return;
    const positions = placements(width, height, this.state.creatures.filter(one => !one.enemy).length, this.state.creatures.filter(one => one.enemy).length);
    const inFlight = playing(this.state);
    for (const [index, creature] of this.state.creatures.entries()) {
      const figure = this.figures.get(creature.id)!;
      const home = positions[index];
      figure.home = home;
      const geometry = figureGeometry(figure.art, home, figure.enemy);
      figure.image.setOrigin(geometry.originX, geometry.originY).setScale(geometry.scale).setDepth(home.y);
      // Reduced motion can enter impact directly with newly mutated historical figures.
      if (!inFlight || resetPositions) figure.image.setPosition(home.x, home.y).setAngle(0);
      figure.shadow.setPosition(home.x, home.y - 2).setDisplaySize(home.size * .75, Math.max(12, home.size * .10)).setDepth(home.y - 1);
      figure.ring.setPosition(home.x, home.y).setDisplaySize(home.size * .80, Math.max(14, home.size * .13)).setDepth(home.y - .5);
    }
    this.onLayout(positions);
  }

  private windup() {
    this.clearMotion();
    const actor = this.figures.get(this.state.actor!);
    const target = this.figures.get(this.state.target!);
    if (!actor) return;
    const cue = this.state.cue;
    if (cue) {
      this.spellEffects.windup(cue.visual, this.anchor(actor), cue.contact.flatMap(id => {
        const figure = this.figures.get(id);
        return figure ? [{ id, anchor: this.anchor(figure) }] : [];
      }), this.rate);
      if (cue.visual.delivery !== 'melee') return;
    }
    if (!target || actor === target) return;
    const approach = strikeApproach(actor.home, target.home);
    const direction = approach.dx < 0 ? -1 : 1;
    this.tweens.add({ targets: actor.image, x: actor.home.x - approach.dx * 14, y: actor.home.y - approach.dy * 14, angle: -direction * 7, duration: DRAW_MS / this.rate,
      onComplete: () => this.tweens.add({ targets: actor.image, x: approach.x, y: approach.y, angle: direction * 9, duration: STRIKE_MS / this.rate, ease: 'Cubic.In' }) });
  }

  private impact() {
    const actor = this.figures.get(this.state.actor!);
    if (actor) {
      this.tweens.killTweensOf(actor.image);
      this.tweens.add({ targets: actor.image, x: actor.home.x, y: actor.home.y, angle: 0, duration: 430 / this.rate, delay: 80 / this.rate, ease: 'Cubic.Out' });
    }
    const cue = this.state.cue;
    if (cue) { this.spellImpact(cue, actor); return; }
    const fallback = this.state.target ? [this.state.target] : [];
    const struck = fallback;
    for (const id of struck) {
      const target = this.figures.get(id);
      if (!target) continue;
      const { dx, dy } = actor ? strikeApproach(actor.home, target.home) : { dx: 0, dy: 0 };
      this.tweens.add({ targets: target.image, x: target.home.x + dx * 18, y: target.home.y + dy * 18, angle: dx * 5, yoyo: true, duration: 140 / this.rate, ease: 'Quad.Out' });
      this.burst(target, 0xffd995, true);
    }
    if (struck.length) this.cameras.main.shake(160 / this.rate, .003);
  }

  private anchor(figure: Figure): EffectAnchor {
    const height = (figure.art.bounds[3] - figure.art.bounds[1]) * figure.image.scaleY;
    return { x: figure.home.x, y: figure.home.y - height * .5,
      radius: Math.max(16, Math.min(58, figure.home.hitWidth * .3, height * .35)) };
  }

  private spellImpact(cue: NonNullable<SceneFrame['cue']>, actor?: Figure) {
    this.spellEffects.clear();
    if (cue.kind === 'fizzle') {
      if (actor) this.spellEffects.impact(cue.visual, this.anchor(actor), this.state.actor!, this.rate);
      return;
    }
    for (const id of cue.recipients) {
      const target = this.figures.get(id);
      if (!target) continue;
      const self = id === this.state.actor;
      // A blood ritual's self-cost is a wound, never a lunge, lightning bolt or summoned spirit at its caster.
      const effects = cue.effects.filter(effect => effect.creature === id);
      if (self && effects.some(effect => effect.wound)) {
        this.spellEffects.impact({ family: 'physical', motif: 'wound', delivery: 'cast' }, this.anchor(target), id, this.rate);
        if (effects.every(effect => effect.wound)) continue;
      }
      const tones = effects.filter(effect => !effect.wound).map(effect => effect.tone);
      const visual = self && cue.visual.delivery === 'melee'
        ? { family: cue.visual.family, motif: tones.includes('recovery') ? 'vital' : 'focus', delivery: 'cast' } : cue.visual;
      this.spellEffects.impact(visual, this.anchor(target), id, this.rate, actor ? this.anchor(actor) : undefined);
      if (!self && cue.visual.delivery === 'melee' && cue.struck.includes(id) && actor) {
        const { dx, dy } = strikeApproach(actor.home, target.home);
        this.tweens.add({ targets: target.image, x: target.home.x + dx * 12, y: target.home.y + dy * 12,
          angle: dx * 4, yoyo: true, duration: 140 / this.rate, ease: 'Quad.Out' });
      }
    }
    if (cue.visual.family === 'physical' && cue.contact.some(id => cue.struck.includes(id))) {
      this.cameras.main.shake(120 / this.rate, .0015);
    }
  }

  private burst(target: Figure, color: number, sparks: boolean) {
    const visibleHeight = (target.art.bounds[3] - target.art.bounds[1]) * target.image.scaleY;
    const x = target.home.x; const y = target.home.y - visibleHeight * .5;
    const ring = this.add.circle(x, y, 12).setStrokeStyle(4, color).setDepth(2000);
    this.effects.push(ring);
    this.tweens.add({ targets: ring, scale: 5, alpha: 0, duration: 360 / this.rate });
    if (!sparks) return;
    for (let i = 0; i < 14; i++) {
      const angle = i * Math.PI * 2 / 14;
      const spark = this.add.rectangle(x, y, 4, 13, i % 2 ? color : 0xcd6d43).setRotation(angle).setDepth(2001);
      this.effects.push(spark);
      this.tweens.add({ targets: spark, x: x + Math.cos(angle) * 85, y: y + Math.sin(angle) * 65, alpha: 0, duration: (280 + i * 15) / this.rate });
    }
  }

  private clearMotion() {
    this.tweens.killAll();
    this.cameras.main.resetFX();
    this.spellEffects.clear();
    for (const effect of this.effects) effect.destroy();
    this.effects = [];
    for (const figure of this.figures.values()) figure.image.setPosition(figure.home.x, figure.home.y).setAngle(0);
  }

  update(time: number) {
    if (!this.state) return;
    const idle = !this.reduced && !playing(this.state);
    let index = 0;
    for (const figure of this.figures.values()) {
      if (idle) figure.image.y = figure.home.y + Math.sin(time / 800 + index * 1.7) * 2;
      index++;
    }
    const width = Math.max(1, this.scale.width);
    const height = Math.max(1, this.scale.height);
    const drift = this.reduced ? 0 : time * .007;
    this.motes.forEach((mote, i) => {
      mote.setPosition(wrap(i * 131 + drift, width), wrap(i * 73 - drift * .7, height));
      mote.visible = !this.reduced;
    });
  }
}
