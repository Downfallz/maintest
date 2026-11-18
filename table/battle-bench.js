// The effects bench (`/battle-lab?bench`, docs/art/battle-craft-plan.md, BC2): one authored spell look played on
// chosen creatures of the laboratory's fixture. It holds no rule and reads no host: who is struck is where the
// creature stands, not what a spell does. Never import this module from the live table.
import { battleFixture } from './battle-lab-fixture.js';
import { IMPACT_MS, WINDUP_MS, playbackRate } from './battle-timing.js';
import { spellVisual } from './battle-playback.js';
import { stageLabel } from './terms.js';

const words = id => id.split(':')[1].split('_').map(word => word[0].toUpperCase() + word.slice(1)).join(' ');

// Every entry of `data/presentation/spell-art.json`, in the order of their names. `spells` is what the content
// says of each id (`battle/bench-spells.mjs`, read when the bundle is built): the name a player reads, the
// mutations that teach it and whether a creature starts with it. An id it lacks is named by its slug, and what
// casts it is left unsaid. An entry with a staging carries it (ADR 0196).
const castBy = spell => spell?.mutations?.length ? spell.mutations.map(one => `${one.name}, ${stageLabel(one.level)}`).join(' / ')
  : spell?.starting ? 'starting spell' : '';
export function benchEntries(art, spells = {}) {
  return Object.entries(art)
    .map(([id, { family, motif, delivery, staging }]) => ({ id, name: spells[id]?.name ?? words(id), castBy: castBy(spells[id]),
      family, motif, delivery, ...(staging ? { staging } : {}) }))
    .sort((one, other) => one.name.localeCompare(other.name, 'en'));
}

// How the bench lists an entry, and how a film strip is titled: the spell, what casts it, its look, then what
// the film asked of it.
export function benchTitle(entry) {
  return [entry.name, entry.castBy, entry.family, entry.motif, entry.delivery].filter(Boolean).join(' · ');
}
export function filmTitle(entry, { speed = 1, outcome = 'played', wound = false } = {}) {
  const asked = [outcome === 'fizzled' ? 'fizzled' : '', wound && outcome !== 'fizzled' ? 'the caster pays in blood' : ''];
  return [benchTitle(entry), ...asked.filter(Boolean), `${speed}×`].join(' · ');
}

// What the bench can ask of a play, each a fixture's choice and no rule: the spell played or fizzled.
export const BENCH_OUTCOMES = Object.freeze(['played', 'fizzled']);

// One sample staging a primitive of the effects kit, so a primitive can be seen and filmed before a spell uses
// it. They are the bench's own, not content: no spell is drawn this way until its stage of the plan stages it.
const sample = (beat, look, staging) => ({ id: `kit:${beat}`, name: `Kit: ${beat}`, family: look.family, motif: look.motif, delivery: look.delivery, staging });
export function benchSamples() {
  const physical = { family: 'physical', motif: 'impact', delivery: 'melee' };
  const elemental = { family: 'elemental', motif: 'fire', delivery: 'cast' };
  const necromantic = { family: 'necromantic', motif: 'spirits', delivery: 'cast' };
  const biological = { family: 'biological', motif: 'quill', delivery: 'projectile' };
  const astral = { family: 'astral', motif: 'vital', delivery: 'cast' };
  return [
    sample('burst', physical, [{ beat: 'motif', at: 840, lasts: 700 }, { beat: 'burst', at: 840, count: 8, shape: 'shard', tint: 'bone', direction: 'along', spread: 50 }]),
    sample('flow', elemental, [{ beat: 'motif', at: 840 }, { beat: 'flow', at: 900, lasts: 900, shape: 'spark', tint: 'ember' }]),
    sample('trail', biological, [{ beat: 'trail', at: 240, lasts: 600 }, { beat: 'motif', at: 840 }]),
    sample('gather', necromantic, [{ beat: 'gather', at: 100, lasts: 700 }, { beat: 'motif', at: 840 }]),
    sample('glow', astral, [{ beat: 'glow', at: 840, lasts: 500 }, { beat: 'motif', at: 840 }]),
    sample('flash', physical, [{ beat: 'flash', at: 840 }, { beat: 'motif', at: 840, lasts: 120 }]),
    sample('shake', physical, [{ beat: 'shake', at: 900, lasts: 240, strength: .5 }, { beat: 'motif', at: 840, lasts: 700 }]),
    sample('pulse', astral, [{ beat: 'pulse', at: 840, lasts: 600, strength: 1 }, { beat: 'motif', at: 840 }]),
    // More than the field may hold: seven streams of 24 start together on each target (#882). The kit gives
    // each only the room the streams before it left, so the field never holds more than its budget.
    sample('budget', elemental, [{ beat: 'motif', at: 840 },
      ...Array.from({ length: 7 }, () => ({ beat: 'flow', at: 900, lasts: 900, count: 24, shape: 'spark', tint: 'ember' }))]),
  ];
}

// The six creatures of the laboratory's fixture, yours first, each with the side it stands on.
export function benchCreatures() {
  const board = battleFixture();
  const stand = enemy => one => ({ id: one.id, name: one.name, form: one.form, isAlive: true, enemy });
  return [...board.allies.map(stand(false)), ...board.enemies.map(stand(true))];
}

// The first creature across the field: what a choice of caster starts from.
export function benchDefaultTargets(actor) {
  const creatures = benchCreatures();
  const caster = creatures.find(one => one.id === actor);
  const across = creatures.find(one => one.enemy !== caster?.enemy);
  return caster && across ? [across.id] : [];
}

// The length of one play at a speed, and the stage the scene shows once so much of it has elapsed.
export function benchDuration(speed = 1) {
  return (WINDUP_MS + IMPACT_MS) / playbackRate(speed);
}
export function benchStage(elapsed, speed = 1) {
  const rate = playbackRate(speed);
  if (elapsed < WINDUP_MS / rate) return 'windup';
  return elapsed < (WINDUP_MS + IMPACT_MS) / rate ? 'impact' : 'resolved';
}

// The frame the scene presents. The first round keeps the ground whole, so no scenery plate loads mid-film.
// A creature across the field from the caster is struck; one on the caster's side, or the caster, receives.
// A fizzle is drawn as the live page draws one (`actionCue`): the fizzle's mark on the caster, and no outcome.
// A caster who pays in blood receives a wound, as a blood cost's harm to its caster is drawn.
export function benchFrame({ entry, actor, targets, stage, outcome = 'played', wound = false }) {
  const creatures = benchCreatures();
  const caster = creatures.find(one => one.id === actor);
  const known = targets.filter(id => creatures.some(one => one.id === id));
  const contact = known.filter(id => id !== actor);
  const frame = { round: 1, stage, actor, target: contact[0] ?? null, targets: [], creatures };
  if (stage !== 'windup' && stage !== 'impact') return frame;
  if (outcome === 'fizzled') {
    return { ...frame, cue: { kind: 'fizzle', visual: spellVisual({ status: 'Fizzled' }), recipients: [], contact: [], struck: [], effects: [], changes: [], roll: null } };
  }
  const struck = contact.filter(id => creatures.find(one => one.id === id).enemy !== caster?.enemy);
  const paid = Boolean(wound && caster);
  const recipients = paid && !known.includes(actor) ? [...known, actor] : known;
  const effects = paid ? [{ creature: actor, text: 'Blood cost', tone: 'harm', wound: true }] : [];
  return { ...frame, cue: { kind: struck.length || paid ? 'strike' : 'pulse',
    visual: { family: entry.family, motif: entry.motif, delivery: entry.delivery, ...(entry.staging ? { staging: entry.staging } : {}) },
    recipients, contact, struck, effects, changes: [], roll: null } };
}

// Phaser's particles and its camera shake draw from `Math.random`: the bench hands them this instead, seeded
// anew at each play, so two films of one effect agree (mulberry32).
export function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let mixed = state;
    mixed = Math.imul(mixed ^ (mixed >>> 15), mixed | 1);
    mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4294967296;
  };
}

// A film's clock moves in ticks of this length, whatever the interval between two frames: a tween that starts
// when another ends starts one tick later, as on a screen, and not one frame of the sheet later.
export const FILM_TICK_MS = 20;
export const FILM_FRAMES = 12;

// The interval between two frames that spreads a sheet over one whole play, as a whole number of ticks.
export function filmInterval(speed = 1, frames = FILM_FRAMES) {
  return Math.ceil(benchDuration(speed) / (frames - 1) / FILM_TICK_MS) * FILM_TICK_MS;
}
