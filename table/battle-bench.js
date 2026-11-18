// The effects bench (`/battle-lab?bench`, docs/art/battle-craft-plan.md, BC2): one authored spell look played on
// chosen creatures of the laboratory's fixture. It holds no rule and reads no host: who is struck is where the
// creature stands, not what a spell does. Never import this module from the live table.
import { battleFixture } from './battle-lab-fixture.js';
import { IMPACT_MS, WINDUP_MS, playbackRate } from './battle-timing.js';

const words = id => id.split(':')[1].split('_').map(word => word[0].toUpperCase() + word.slice(1)).join(' ');

// Every entry of `data/presentation/spell-art.json`, in the order of their names.
export function benchEntries(art) {
  return Object.entries(art)
    .map(([id, { family, motif, delivery }]) => ({ id, name: words(id), family, motif, delivery }))
    .sort((one, other) => one.name.localeCompare(other.name, 'en'));
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
export function benchFrame({ entry, actor, targets, stage }) {
  const creatures = benchCreatures();
  const caster = creatures.find(one => one.id === actor);
  const known = targets.filter(id => creatures.some(one => one.id === id));
  const contact = known.filter(id => id !== actor);
  const frame = { round: 1, stage, actor, target: contact[0] ?? null, targets: [], creatures };
  if (stage !== 'windup' && stage !== 'impact') return frame;
  const struck = contact.filter(id => creatures.find(one => one.id === id).enemy !== caster?.enemy);
  return { ...frame, cue: { kind: struck.length ? 'strike' : 'pulse',
    visual: { family: entry.family, motif: entry.motif, delivery: entry.delivery },
    recipients: known, contact, struck, effects: [], changes: [], roll: null } };
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
