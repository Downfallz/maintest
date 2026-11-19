// The ladder (docs/art/storyboards/README.md, "The ladder"; BC6 of docs/art/battle-craft-plan.md): the higher the
// Stage of the mutation that teaches a spell, the more spectacular its staging. The owner's rule is held on what
// the spell art carries: each staged spell sits inside the band of its Stage, every spell of a family whose
// stage of the plan is built has a staging the page reads, and each has its storyboard.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { benchSpells } from '../battle/bench-spells.mjs';
import { FLIGHT } from '../table/battle-motion.js';
import { SCENE_PARTICLES, stagingBeats } from '../table/battle-staging.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const read = path => readFileSync(join(root, path), 'utf8');
const art = JSON.parse(read('data/presentation/spell-art.json'));
const spells = await benchSpells(join(root, 'data'));

// The families whose stage of the plan is built, each with the file of its storyboards: all of them since BC8.
const BUILT = {
  physical: 'physical.md', biological: 'biological.md', elemental: 'elemental.md', astral: 'astral.md',
  necromantic: 'necromantic.md', neutral: 'base-actions.md',
};
// A base action's storyboard is with the base actions, whatever its family.
const BASE_FILE = 'base-actions.md';

// The bands, as the README's table writes them. `particles` is what one target is asked for, all beats together.
export const LADDER = Object.freeze({
  0: { name: 'a base action', beats: [1, 3], particles: 8, windup: [0, 0], glows: [0, 0], pulses: 0, shake: [0, 0], gone: 1540 },
  1: { name: 'Stage I', beats: [4, 5], particles: 24, windup: [0, 0], glows: [0, 0], pulses: 0, shake: [0, 0], gone: 1700 },
  2: { name: 'Stage II', beats: [6, 8], particles: 60, windup: [0, 1], glows: [1, 8], pulses: 0, shake: [0, .15], gone: 2000 },
  3: { name: 'Stage III', beats: [9, 12], particles: 120, windup: [1, 12], glows: [2, 12], pulses: 1, shake: [0, .35], gone: 2400 },
});

/** The rung a spell stands on: 0 for a spell a creature starts with, otherwise the lowest Stage that teaches it. */
export function rungOf(spell) {
  if (!spell || spell.starting || spell.mutations.length === 0) return 0;
  return Math.min(...spell.mutations.map(one => one.level));
}

const PARTICLE_BEATS = ['burst', 'flow', 'gather', 'trail'];

/** What a staging spends, in the ladder's own measures. */
export function spectacle(beats) {
  const of = kind => beats.filter(beat => beat.beat === kind);
  const onTarget = beats.filter(beat => PARTICLE_BEATS.includes(beat.beat) && (beat.on === 'target' || beat.beat === 'trail'));
  return {
    beats: beats.length,
    particles: onTarget.reduce((sum, beat) => sum + beat.count, 0),
    windup: beats.filter(beat => beat.window === 'windup').length,
    glows: of('glow').length,
    pulses: of('pulse').length,
    shake: of('shake')[0]?.strength ?? 0,
    gone: Math.max(...beats.map(beat => beat.at + (beat.lasts ?? 0))),
  };
}

/** What is wrong with a staging on its rung, as sentences. */
export function ladderProblems(beats, rung) {
  const band = LADDER[rung], spent = spectacle(beats), problems = [];
  const within = (value, [least, most]) => value >= least && value <= most;
  if (!within(spent.beats, band.beats)) problems.push(`${spent.beats} beats, where ${band.name} holds ${band.beats.join(' to ')}`);
  if (spent.particles > band.particles) problems.push(`${spent.particles} particles on one target, more than ${band.particles}`);
  if (!within(spent.windup, band.windup)) problems.push(`${spent.windup} beats in the windup, where ${band.name} holds ${band.windup.join(' to ')}`);
  if (!within(spent.glows, band.glows)) problems.push(`${spent.glows} glows, where ${band.name} holds ${band.glows.join(' to ')}`);
  if (spent.pulses > band.pulses) problems.push(`${spent.pulses} pulses, more than ${band.pulses}`);
  if (!within(spent.shake, band.shake)) problems.push(`a shake of ${spent.shake}, where ${band.name} holds ${band.shake.join(' to ')}`);
  if (spent.gone > band.gone) problems.push(`its last beat ends at ${spent.gone} ms, after ${band.gone}`);
  return problems;
}

const staged = Object.entries(art).filter(([, visual]) => visual.staging);
const built = Object.entries(art).filter(([, visual]) => Object.hasOwn(BUILT, visual.family));

test('every spell of a family whose stage is built has a staging the page reads', () => {
  assert.equal(built.length, Object.keys(art).length);
  for (const [id, visual] of built) {
    assert.ok(visual.staging, `${id} has no staging`);
    assert.ok(stagingBeats(visual), `${id}: the page cannot read its staging, and would draw the motif`);
  }
});

test('a staging keeps its motif: a spell is read as its family first', () => {
  for (const [id, visual] of staged) {
    assert.equal(stagingBeats(visual).filter(beat => beat.beat === 'motif').length, 1, `${id} places its motif once`);
  }
});

test('each staged spell sits inside the band of its Stage', () => {
  for (const [id, visual] of staged) {
    assert.ok(spells[id], `${id} is a spell of the content`);
    assert.deepEqual(ladderProblems(stagingBeats(visual), rungOf(spells[id])), [], `${spells[id].name} (${id})`);
  }
});

test('within a family the spectacle climbs with the Stage: no spell asks for fewer beats than one of a lower Stage', () => {
  for (const family of Object.keys(BUILT)) {
    const rungs = new Map();
    for (const [id, visual] of staged.filter(([, one]) => one.family === family)) {
      const rung = rungOf(spells[id]), count = stagingBeats(visual).length;
      rungs.set(rung, { least: Math.min(count, rungs.get(rung)?.least ?? Infinity), most: Math.max(count, rungs.get(rung)?.most ?? 0) });
    }
    const climb = [...rungs.entries()].sort(([one], [other]) => one - other);
    for (let at = 1; at < climb.length; at++) {
      assert.ok(climb[at][1].least > climb[at - 1][1].most, `${family}: ${LADDER[climb[at][0]].name} starts above ${LADDER[climb[at - 1][0]].name}`);
    }
  }
});

test('a spell that strikes several figures leaves each its share of the field\'s particles', () => {
  for (const [id, visual] of staged) {
    const targets = JSON.parse(read(spellFile(id))).targeting?.maxTargets ?? 1;
    const beats = stagingBeats(visual);
    // What may be alive at one moment: the beats on a target that overlap, on every target, and those on the caster.
    for (const moment of beats.map(beat => beat.at)) {
      const alive = beats.filter(beat => PARTICLE_BEATS.includes(beat.beat) && beat.at <= moment && moment < beat.at + beat.lasts);
      const asked = alive.reduce((sum, beat) => sum + beat.count * (beat.on === 'caster' && beat.beat !== 'trail' ? 1 : targets), 0);
      assert.ok(asked <= SCENE_PARTICLES, `${id} asks for ${asked} particles at ${moment} ms on ${targets} targets, more than ${SCENE_PARTICLES}`);
    }
  }
});

test('a halo on the outline is one figure\'s: a pulse is on the caster, or on a spell with one target', () => {
  for (const [id, visual] of staged) {
    const targets = JSON.parse(read(spellFile(id))).targeting?.maxTargets ?? 1;
    for (const beat of stagingBeats(visual).filter(one => one.beat === 'pulse')) {
      assert.ok(beat.on === 'caster' || targets === 1, `${id} pulses ${targets} figures`);
    }
  }
});

test('a trail behind a projectile lasts as long as its flight: it leaves with the quill and lands with it', () => {
  for (const [id, visual] of staged.filter(([, one]) => one.delivery === 'projectile')) {
    for (const beat of stagingBeats(visual).filter(one => one.beat === 'trail')) {
      assert.deepEqual([beat.at, beat.lasts], [FLIGHT.at, FLIGHT.lasts], `${id}: its trail is the flight's`);
    }
  }
});

test('each staged spell has its storyboard, in its family\'s file or with the base actions', () => {
  const boards = file => read(`docs/art/storyboards/${file}`).split('\n').filter(line => line.startsWith('## '));
  for (const [id, visual] of built) {
    const file = rungOf(spells[id]) === 0 ? BASE_FILE : BUILT[visual.family];
    assert.ok(boards(file).some(title => title.includes(`\`${id}\``)), `${id} has no storyboard in ${file}`);
  }
});

test('the bands are the ones the storyboards\' README writes', () => {
  const readme = read('docs/art/storyboards/README.md');
  const row = label => readme.split('\n').find(line => line.startsWith(`| ${label}`))?.split('|').slice(2, 6).map(cell => cell.trim());
  assert.deepEqual(row('Beats in the staging'), Object.values(LADDER).map(band => band.beats.join(' to ')));
  assert.deepEqual(row('Particles asked for on one target'), Object.values(LADDER).map(band => `up to ${band.particles}`));
  assert.deepEqual(row('The last beat is gone by'), Object.values(LADDER).map(band => `${band.gone} ms`));
});

/** The file that authors a spell, found by its id. */
function spellFile(id) {
  spellFile.found ??= new Map(spellPaths().map(path => [JSON.parse(read(path)).id, path]));
  return spellFile.found.get(id);
}

function spellPaths() {
  return readdirSync(join(root, 'data/Spells'), { withFileTypes: true, recursive: true })
    .filter(entry => entry.isFile() && entry.name.endsWith('.json'))
    .map(entry => join(entry.parentPath, entry.name).slice(root.length + 1));
}
