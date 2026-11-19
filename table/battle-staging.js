// A spell's staging (ADR 0196): the list of beats its card's `visual` carries, read into what the battle plays.
// The host's builder checked it (`SpellArt`); the page reads it again, because a page and a catalogue are not
// always the same deploy's. What the page does not understand it does not half-play: the motif is drawn instead.
import { IMPACT_MS, VFX_MS, WINDUP_MS } from './battle-timing.js';

// The same words as `SpellArt` (src/DownfallArena.Infrastructure/Resources/SpellArt.cs); the test reads both.
export const BEATS = Object.freeze(['motif', 'burst', 'flow', 'trail', 'gather', 'glow', 'flash', 'shake', 'pulse']);
export const ANCHORS = Object.freeze(['caster', 'target']);
export const SHAPES = Object.freeze(['dot', 'spark', 'shard', 'ring']);
export const TINTS = Object.freeze(['family', 'bone', 'ember', 'blood']);
export const BLENDS = Object.freeze(['normal', 'add', 'screen']);
export const DIRECTIONS = Object.freeze(['out', 'along', 'back', 'up']);
export const MAX_BEATS = 12;
export const MAX_PARTICLES = 24;
// The most particles alive on the field at once, whatever the beats ask for (the craft book, "A budget of pixels").
export const SCENE_PARTICLES = 150;
export const ACTION_MS = WINDUP_MS + IMPACT_MS;

// What a beat plays with when its author left a parameter out. `lasts` is at 1×; a beat near the end of its
// window is shortened to end inside it.
const DEFAULTS = Object.freeze({
  motif: { on: 'target', lasts: VFX_MS },
  burst: { on: 'target', lasts: 320, count: 12, shape: 'spark', tint: 'family', blend: 'normal', direction: 'out', reach: 200 },
  flow: { on: 'target', lasts: 600, count: 12, shape: 'dot', tint: 'family', blend: 'add', direction: 'up', spread: 40, reach: 100 },
  trail: { on: 'target', lasts: 600, count: 16, shape: 'dot', tint: 'family', blend: 'add' },
  gather: { on: 'caster', lasts: 400, count: 12, shape: 'dot', tint: 'family', blend: 'add', reach: 150 },
  glow: { on: 'target', lasts: 300, tint: 'family', blend: 'add', reach: 150, strength: .8 },
  flash: { on: 'target', lasts: 80, tint: 'bone' },
  shake: { on: 'target', lasts: 120, strength: .25 },
  pulse: { on: 'target', lasts: 300, tint: 'family', strength: .5 },
});

const WORDS = { on: ANCHORS, shape: SHAPES, tint: TINTS, blend: BLENDS, direction: DIRECTIONS };
// The reach is a share of the figure's radius, which is about a third of its height: 300 clears the body.
export const MAX_REACH = 400;
const NUMBERS = { count: [1, MAX_PARTICLES], spread: [1, 360], reach: [1, MAX_REACH] };
const given = value => value !== undefined && value !== null;

function readBeat(authored, known) {
  if (!authored || typeof authored !== 'object' || !known.includes(authored.beat) || !Object.hasOwn(DEFAULTS, authored.beat)) return null;
  const { at } = authored;
  if (!Number.isInteger(at) || at < 0 || at >= ACTION_MS) return null;
  const window = at < WINDUP_MS ? 'windup' : 'impact';
  if (authored.beat === 'motif' && window !== 'impact') return null;
  const start = window === 'windup' ? 0 : WINDUP_MS, end = window === 'windup' ? WINDUP_MS : ACTION_MS;
  const beat = { ...DEFAULTS[authored.beat], beat: authored.beat, at, window, delay: at - start };
  for (const [key, among] of Object.entries(WORDS)) {
    if (!given(authored[key])) continue;
    if (!among.includes(authored[key])) return null;
    beat[key] = authored[key];
  }
  for (const [key, [least, most]] of Object.entries(NUMBERS)) {
    if (!given(authored[key])) continue;
    if (!Number.isInteger(authored[key]) || authored[key] < least || authored[key] > most) return null;
    beat[key] = authored[key];
  }
  if (given(authored.strength)) {
    if (typeof authored.strength !== 'number' || !(authored.strength > 0 && authored.strength <= 1)) return null;
    beat.strength = authored.strength;
  }
  if (given(authored.lasts)) {
    if (!Number.isInteger(authored.lasts) || authored.lasts <= 0 || at + authored.lasts > end) return null;
    beat.lasts = authored.lasts;
  } else if (given(beat.lasts)) beat.lasts = Math.min(beat.lasts, end - at);
  // A fan all around unless its author aimed it; an aimed one is 50° wide unless told.
  if (authored.beat === 'burst' && !given(beat.spread)) beat.spread = beat.direction === 'out' ? 360 : 50;
  return beat;
}

// The beats of a visual's staging, each with its defaults, its window and its delay inside it, in the order
// authored. Null when the visual has none, or when one beat is not understood: by the page (`known` is what the
// loaded kit plays, and the motif), or at all. Null means the motif.
export function stagingBeats(visual, known = BEATS) {
  const staging = visual?.staging;
  if (!Array.isArray(staging) || staging.length === 0 || staging.length > MAX_BEATS) return null;
  const beats = staging.map(authored => readBeat(authored, known));
  if (beats.includes(null)) return null;
  for (const once of ['motif', 'flash', 'shake']) if (beats.filter(beat => beat.beat === once).length > 1) return null;
  return beats;
}
