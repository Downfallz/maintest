import Phaser from 'phaser';
import { placements, strikeApproach } from '../../table/battle-layout.js';
import { CREATURE_ART, figureArt, figureGeometry } from '../../table/battle-art.js';
import { DRAW_MS, STRIKE_MS, RETURN_MS, RETURN_DELAY_MS, MOTION_SCALE, playbackRate } from '../../table/battle-timing.js';
import type { SceneFrame, Placement } from './types';
import { UpkeepMarkers } from './upkeep-markers';
import { MutationReveal } from './mutation-reveal';
import { MutationCues } from './mutation-cues';
import { CapstoneEffects } from './capstone-effects';
import { EVOLUTION_REGISTRY } from '../../table/battle-evolution-art.js';
import { SpellEffects, type EffectAnchor } from './spell-effects';

const playing = (state: SceneFrame) => state.stage === 'windup' || state.stage === 'impact';

// What a figure draws: an audited sprite (battle-art.js) or an evolution illustration (battle-evolution-art.js).
// Both carry their canvas, silhouette and ground anchor, which is all the geometry reads.
interface Art { key: string; width: number; height: number; bounds: readonly number[]; ground: readonly number[]; form?: string; id?: string }
type SceneCreature = SceneFrame['creatures'][number];

interface Figure {
  id: string;
  image: Phaser.GameObjects.Image; shadow: Phaser.GameObjects.Ellipse; ring: Phaser.GameObjects.Ellipse;
  cues: MutationCues; home: Placement; art: Art; enemy: boolean; alive: boolean;
  // The form the creature asked for: when the art drew another, the accents shaped for the asked one stay hidden.
  form: string;
  // The evolution illustration it asked for (ADR 0151), its capstone effects, and the appearance they were made for.
  wanted: string | null; effects: CapstoneEffects; look: string; cueKey: string;
}

const GOLD = 0xeac88e;
const EMBER = 0xd18472;
const MOTES = 24;

// A remainder that stays in [0, span) whatever the sign of the value: `%` alone turns negative once a
// drift outgrows its offset, and a mote that goes negative leaves the screen for good.
const wrap = (value: number, span: number) => ((value % span) + span) % span;

export class BattleScene extends Phaser.Scene {
  private figures = new Map<string, Figure>();
  private backdrop?: Phaser.GameObjects.Image;
  private motes: Phaser.GameObjects.Arc[] = [];
  private effects: Phaser.GameObjects.GameObject[] = [];
  private upkeepMarkers = new UpkeepMarkers(this);
  private mutationReveal = new MutationReveal(this);
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
  onCancel: () => void = () => {};
  // Which image a creature now shows: an evolution illustration's id, or `form:<form>` for an audited sprite.
  onArtwork: (id: string, shown: string) => void = () => {};
  assetFailed = false;
  // Evolution illustrations load when a creature on the field first asks for one, never all at boot; one that
  // fails stays failed for the page, and the figure keeps its audited sprite.
  private loadingArt = new Set<string>();
  private failedArt = new Set<string>();

  constructor(private assetRoot: string) { super('battle'); }

  preload() {
    this.load.image('battle-floor', `${this.assetRoot}eclipse-battle.webp`);
    for (const asset of CREATURE_ART) this.load.image(asset.key, `${this.assetRoot}${asset.file}`);
    this.load.on('loaderror', (file: Phaser.Loader.File) => {
      if (!String(file.key).startsWith('evolution-')) { this.assetFailed = true; return; }
      this.loadingArt.delete(String(file.key)); this.failedArt.add(String(file.key));
    });
  }

  create() {
    if (this.textures.exists('battle-floor')) this.backdrop = this.add.image(0, 0, 'battle-floor').setOrigin(.5, 0).setTint(0xb5bbc2).setDepth(-10);
    // A local vector fallback keeps the scene selectable if an image fails to load.
    const fallback = this.add.graphics();
    fallback.fillStyle(0x71867e).fillTriangle(40, 0, 0, 90, 80, 90);
    fallback.generateTexture('missing-figure', 80, 100);
    fallback.destroy();
    for (let index = 0; index < MOTES; index++) this.motes.push(this.add.circle(0, 0, index % 3 === 0 ? 2 : 1, GOLD, .24).setDepth(1000));
    this.fitCamera();
    this.scale.on('resize', () => {
      this.fitCamera();
      if (this.state && playing(this.state)) this.onResizeDuringPlayback();
      this.arrange();
    });
    this.ready = true;
    this.onReady();
  }

  present(state: SceneFrame, reduced: boolean, rate: number) {
    this.state = state;
    this.reduced = reduced;
    this.rate = playbackRate(rate);
    if (!this.ready) return;
    // The roster is who stands where; what each one looks like changes in place, so a purchase can be seen to happen.
    const roster = JSON.stringify(state.creatures.map(one => [one.id, one.enemy]));
    const rosterChanged = roster !== this.roster;
    if (rosterChanged) {
      this.clearMotion();
      for (const figure of this.figures.values()) { figure.cues.destroy(); figure.effects.destroy(); figure.image.destroy(); figure.shadow.destroy(); figure.ring.destroy(); }
      this.figures.clear();
      this.roster = roster;
      this.makeFigures();
    }
    for (const creature of state.creatures) this.dress(this.figures.get(creature.id)!, creature, !rosterChanged);
    const changed = state.stage !== this.previous;
    if (changed && (state.stage === 'decision' || state.stage === 'resolved')) this.clearMotion();
    this.arrange(rosterChanged);
    for (const creature of state.creatures) {
      const figure = this.figures.get(creature.id)!;
      const acting = creature.id === state.actor;
      const selected = creature.id === state.mutationFocus || creature.id === state.target || state.selected?.includes(creature.id) === true;
      const legal = state.stage === 'targeting' && state.targets.includes(creature.id);
      figure.ring.setStrokeStyle(selected ? 3 : 1.5, selected || acting ? GOLD : EMBER, selected || legal || acting ? .95 : .15);
      // While targets are chosen, whatever cannot be targeted steps back; the caster stays in full.
      const aside = state.stage === 'targeting' && !legal && !selected && !acting;
      figure.image.setAlpha(!creature.isAlive ? .20 : (state.mutationFocus && !selected) || aside ? .52 : 1);
      figure.alive = creature.isAlive;
      figure.shadow.setAlpha(creature.isAlive ? .55 : .2);
      this.followCues(figure);
    }
    if (changed && !reduced && state.stage === 'windup') this.windup();
    if (changed && state.stage === 'impact') {
      this.onImpact();
      if (!reduced) this.impact();
    }
    this.previous = state.stage;
  }

  revealMutations(ids: string[]) {
    for (const id of ids) {
      const figure = this.figures.get(id);
      if (figure) this.burst(figure, GOLD, false);
    }
  }

  private makeFigures() {
    for (const creature of this.state.creatures) {
      const enemy = creature.enemy;
      const shadow = this.add.ellipse(0, 0, 100, 20, 0x020608, .55);
      const ring = this.add.ellipse(0, 0, 110, 26).setStrokeStyle(1, GOLD, .2);
      const art = figureArt(creature.form, (key: string) => this.textures.exists(key));
      const image = this.add.image(0, 0, art.key).setFlipX(enemy);
      // Team identity remains in labels, formation and targeting; preserve the approved art palette.
      const cues = new MutationCues(this, creature.layers ?? [], creature.id, creature.form);
      this.figures.set(creature.id, { id: creature.id, image, shadow, ring, art, enemy, cues, form: creature.form, alive: creature.isAlive,
        wanted: null, effects: new CapstoneEffects(this, []), look: '', cueKey: '', home: { x: 0, y: 0, size: 0, hitWidth: 0 } });
    }
  }

  // A creature's appearance: its ART-5 accents, the evolution illustration it resolved to and its capstone effects
  // (ADR 0151). Only what changed is rebuilt; a change of illustration fades the old one out over the new one.
  private dress(figure: Figure, creature: SceneCreature, animate: boolean) {
    const effects = creature.evolution?.capstoneEffectIds ?? [];
    const look = JSON.stringify([creature.form, creature.layers ?? [], creature.evolution?.artworkId ?? null, effects]);
    if (look === figure.look) return;
    figure.look = look;
    const cueKey = JSON.stringify([creature.form, creature.layers ?? []]);
    if (cueKey !== figure.cueKey) {
      if (figure.cueKey) { figure.cues.destroy(); figure.cues = new MutationCues(this, creature.layers ?? [], creature.id, creature.form); }
      figure.cueKey = cueKey;
    }
    figure.form = creature.form;
    if (JSON.stringify(figure.effects.ids) !== JSON.stringify(effects)) { figure.effects.destroy(); figure.effects = new CapstoneEffects(this, effects); }
    figure.wanted = creature.evolution?.artworkId ?? null;
    this.showArt(figure, animate);
  }

  // The illustration the figure asked for once it is loaded; until then, or if it never loads, the audited sprite of
  // its form, which is always there. Geometry follows whichever image is drawn, by its own silhouette.
  private showArt(figure: Figure, animate: boolean) {
    // The ancestral Stage 0 is the audited base sprite the scene loads at boot, the same drawing: fetching its
    // re-encoded twin would add a cold download to every opening board for no change on screen.
    const asked = figure.wanted ? EVOLUTION_REGISTRY.art(figure.wanted) : null;
    const wanted = asked && asked.stage > 0 ? asked : null;
    let art: Art;
    if (wanted && this.textures.exists(wanted.key)) art = wanted;
    else {
      if (wanted && !this.failedArt.has(wanted.key)) this.request(wanted);
      art = figureArt(figure.form, (key: string) => this.textures.exists(key));
    }
    if (art.key !== figure.image.texture.key) {
      if (animate && !this.reduced && figure.image.visible) {
        const old = figure.image;
        const ghost = this.add.image(old.x, old.y, old.texture.key).setOrigin(old.originX, old.originY).setScale(old.scaleX, old.scaleY)
          .setFlipX(old.flipX).setAlpha(old.alpha).setDepth(old.depth + .0001);
        this.effects.push(ghost);
        this.tweens.add({ targets: ghost, alpha: 0, duration: 420, ease: 'Sine.Out', onComplete: () => ghost.destroy() });
      }
      figure.image.setTexture(art.key);
    }
    figure.art = art;
    this.onArtwork(figure.id, art === wanted ? wanted.id : `form:${art.form ?? figure.form}`);
  }

  private request(asset: NonNullable<ReturnType<typeof EVOLUTION_REGISTRY.art>>) {
    if (this.loadingArt.has(asset.key)) return;
    this.loadingArt.add(asset.key);
    this.load.image(asset.key, `${this.assetRoot}${asset.file}`);
    this.load.once(`filecomplete-image-${asset.key}`, () => {
      this.loadingArt.delete(asset.key);
      const waiting = [...this.figures.values()].filter(figure => figure.wanted === asset.id);
      for (const figure of waiting) this.showArt(figure, true);
      if (waiting.length) this.arrange();
    });
    if (!this.load.isLoading()) this.load.start();
  }

  // The framebuffer uses device pixels; all art, animation distances and DOM targets share CSS coordinates.
  private fitCamera() {
    const { clientWidth: width, clientHeight: height } = this.game.canvas;
    if (!width || !height) return;
    this.cameras.main.setSize(this.scale.width, this.scale.height)
      .setZoom(this.scale.width / width, this.scale.height / height).centerOn(width / 2, height / 2);
  }

  private arrange(resetPositions = false) {
    const { displayWidth: width, displayHeight: height } = this.cameras.main;
    if (this.backdrop) {
      const source = this.backdrop.texture.getSourceImage();
      const factor = Math.max(width / source.width, height / source.height);
      this.backdrop.setDisplaySize(source.width * factor, source.height * factor).setPosition(width / 2, -(source.height * factor - height) * .35);
    }
    if (!this.figures.size) {
      this.upkeepMarkers.present(this.state?.upkeep, new Map(), this.reduced);
      this.mutationReveal.present(undefined, new Map(), this.reduced);
      return;
    }
    // A docked sheet (a mutation, the tempos, a spell) shortens a phone's board, but a phone held upright keeps its
    // two rows: the opposing formations of a wide screen would stack the captions on each other.
    const portraitDraft = width < 620 && matchMedia('(orientation: portrait)').matches;
    const positions = placements(width, height, this.state.creatures.filter(one => !one.enemy).length,
      this.state.creatures.filter(one => one.enemy).length, portraitDraft ? true : undefined);
    const inFlight = playing(this.state);
    for (const [index, creature] of this.state.creatures.entries()) {
      const figure = this.figures.get(creature.id)!;
      const home = positions[index];
      figure.home = home;
      const geometry = figureGeometry(figure.art, home, figure.enemy);
      // Figures of one row share a y: a step per figure keeps each one's accents under its neighbour's image.
      figure.image.setOrigin(geometry.originX, geometry.originY).setScale(geometry.scale).setDepth(home.y + index * .001);
      // Reduced motion can enter impact directly with newly mutated historical figures.
      if (!inFlight || resetPositions) figure.image.setPosition(home.x, home.y).setAngle(0);
      figure.shadow.setPosition(home.x, home.y - 2).setDisplaySize(home.size * .75, Math.max(12, home.size * .10)).setDepth(home.y - 1);
      figure.ring.setPosition(home.x, home.y).setDisplaySize(home.size * .80, Math.max(14, home.size * .13)).setDepth(home.y - .5);
    }
    for (const figure of this.figures.values()) this.followCues(figure);
    const homes = new Map(this.state.creatures.map((one, i) => [one.id, positions[i]]));
    this.upkeepMarkers.present(this.state.upkeep, homes, this.reduced);
    this.mutationReveal.present(this.state.upkeep ? undefined : this.state.reveal, homes, this.reduced);
    this.onLayout(positions);
  }

  private followCues(figure: Figure) {
    // Geometry-specific accents are suppressed if the requested sprite was unavailable.
    figure.cues.follow(figure.image, figure.art.bounds, figure.art.ground, figure.art.form === figure.form);
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
      this.tweens.add({ targets: actor.image, x: actor.home.x, y: actor.home.y, angle: 0, duration: RETURN_MS / this.rate, delay: RETURN_DELAY_MS / this.rate, ease: 'Cubic.Out' });
    }
    const cue = this.state.cue;
    if (cue) { this.spellImpact(cue, actor); return; }
    const fallback = this.state.target ? [this.state.target] : [];
    const struck = fallback;
    for (const id of struck) {
      const target = this.figures.get(id);
      if (!target) continue;
      const { dx, dy } = actor ? strikeApproach(actor.home, target.home) : { dx: 0, dy: 0 };
      this.tweens.add({ targets: target.image, x: target.home.x + dx * 18, y: target.home.y + dy * 18, angle: dx * 5, yoyo: true, duration: 140 * MOTION_SCALE / this.rate, ease: 'Quad.Out' });
      this.burst(target, 0xffd995, true);
    }
    if (struck.length) this.cameras.main.shake(160 * MOTION_SCALE / this.rate, .003);
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
          angle: dx * 4, yoyo: true, duration: 140 * MOTION_SCALE / this.rate, ease: 'Quad.Out' });
      }
    }
    if (cue.visual.family === 'physical' && cue.contact.some(id => cue.struck.includes(id))) {
      this.cameras.main.shake(120 * MOTION_SCALE / this.rate, .0015);
    }
  }

  private burst(target: Figure, color: number, sparks: boolean) {
    const visibleHeight = (target.art.bounds[3] - target.art.bounds[1]) * target.image.scaleY;
    const x = target.home.x; const y = target.home.y - visibleHeight * .5;
    const ring = this.add.circle(x, y, 12).setStrokeStyle(4, color).setDepth(2000);
    this.effects.push(ring);
    this.tweens.add({ targets: ring, scale: 5, alpha: 0, duration: 360 * MOTION_SCALE / this.rate });
    if (!sparks) return;
    for (let i = 0; i < 14; i++) {
      const angle = i * Math.PI * 2 / 14;
      const spark = this.add.rectangle(x, y, 4, 13, i % 2 ? color : 0xcd6d43).setRotation(angle).setDepth(2001);
      this.effects.push(spark);
      this.tweens.add({ targets: spark, x: x + Math.cos(angle) * 85, y: y + Math.sin(angle) * 65, alpha: 0, duration: (280 + i * 15) * MOTION_SCALE / this.rate });
    }
  }

  private clearMotion() {
    this.onCancel();
    this.tweens.killAll();
    this.cameras.main.resetFX();
    this.spellEffects.clear();
    for (const effect of this.effects) effect.destroy();
    this.effects = [];
    for (const figure of this.figures.values()) {
      figure.image.setPosition(figure.home.x, figure.home.y).setAngle(0);
      this.followCues(figure);
    }
  }

  update(time: number) {
    if (!this.state) return;
    // Creatures stand still between actions: only a played action moves them.
    for (const figure of this.figures.values()) {
      this.followCues(figure);
      figure.effects.follow(figure.image, figure.alive && figure.image.visible, time, this.reduced);
    }
    const width = Math.max(1, this.cameras.main.displayWidth);
    const height = Math.max(1, this.cameras.main.displayHeight);
    const drift = this.reduced ? 0 : time * .007;
    this.motes.forEach((mote, i) => {
      mote.setPosition(wrap(i * 131 + drift, width), wrap(i * 73 - drift * .7, height));
      mote.visible = !this.reduced;
    });
  }
}
