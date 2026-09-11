// The balance reading, run in Node rather than a browser (ADR 0024). What is worth testing here is what the
// page would say about a spell: which disagreements between the knobs file and the content it names, where a
// value sits inside its own band, and what it does when the host publishes no knobs at all.
//
// The disagreements are `check-knobs`' own (`learning/src/downfall_learning/knobs.py`, `validate` and
// `_knob_problems`), so these tests are also where the two readings are held to the same list.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  aliasOfSpell, constraintsOf, entryFor, knobReading, objectiveOf, readBalance, readPointer, readings,
  summarise, survey,
} from './balance.js';

/** A spell as the editor holds it: Pummel, which is the entry the strip was designed against. */
const pummel = () => ({
  id: 'spell:pummel:v1',
  name: 'Pummel',
  initiative: 1,
  energyCost: 1,
  criticalChance: 0.667,
  effects: [{ kind: 'Damage', amount: 2 }],
});

const knobsFile = (spells, rest = {}) => ({ version: 'knobs:v1', spells, ...rest });

const withKnobs = knobs => knobsFile({
  'spell:pummel': { name: 'Pummel', class: 'Brawler', intent: 'The cheap swing.', keep: ['Its crit is its point'], knobs },
});

const of = (balance, alias, document) => summarise(entryFor(balance, alias), document);

const codes = summary => [...summary.problems, ...summary.knobs.flatMap(knob => knob.problems)].map(problem => problem.code);

test('a host that publishes no knobs gets a line to show, not a broken sheet', () => {
  const answer = readBalance({ spells: [] });

  assert.equal(answer.ok, false);
  assert.match(answer.why, /does not publish the balance knobs/);
  assert.match(answer.why, /data\/balance\/knobs\.json/);
});

test('knobs that are not an object at all are left unread rather than guessed at', () => {
  const answer = readBalance({ balance: 'knobs:v1' });

  assert.equal(answer.ok, false);
  assert.match(answer.why, /not shaped like data\/balance\/knobs\.json/);
});

test('a knobs version this page does not know is read as none of it, and names the version', () => {
  const answer = readBalance({ balance: { version: 'knobs:v2', spells: {} } });

  assert.equal(answer.ok, false);
  assert.match(answer.why, /'knobs:v2'/);
  assert.match(answer.why, /knobs:v1/);
});

test('an entry is found by the unversioned alias, never by the versioned id', () => {
  const aliases = { 'spell:pummel': 'spell:pummel:v2' };

  // The alias map is the authority: cutting a v2 repoints it, and the entry follows the spell.
  assert.equal(aliasOfSpell('spell:pummel:v2', aliases), 'spell:pummel');
  assert.equal(aliasOfSpell('spell:pummel:v1', aliases), null);
  assert.equal(aliasOfSpell('spell:pummel:v1', { 'spell:old_name': 'spell:pummel:v1' }), 'spell:old_name');
  assert.equal(aliasOfSpell('', {}), null);
});

test('a value sitting mid band reports where it sits and how many moves are left each way', () => {
  const [crit] = readings(entryFor(withKnobs([{ path: '/criticalChance', min: 0.4, max: 0.8, step: 0.05 }]), 'spell:pummel'), pummel());

  assert.equal(crit.value, 0.667);
  assert.equal(crit.at, 'inside');
  assert.equal(crit.tone, 'ok');
  assert.ok(Math.abs(crit.position - 0.6675) < 1e-9);
  // Moves are a step off the value the content carries, not off a grid, so this counts from 0.667.
  assert.deepEqual(crit.room, { down: 5, up: 2 });
});

test('a value pinned at its own maximum reads differently from one sitting mid band', () => {
  const summary = of(withKnobs([{ path: '/energyCost', min: 0, max: 1, step: 1 }]), 'spell:pummel', pummel());
  const [cost] = summary.knobs;

  assert.equal(cost.at, 'max');
  assert.equal(cost.tone, 'edge');
  assert.equal(cost.position, 1);
  assert.deepEqual(cost.room, { down: 1, up: 0 });
  assert.equal(summary.headline, '1 knob, 1 at a band edge');
  assert.deepEqual(codes(summary), []);
  // Bounds are drawn around what the spell is, so a value authored at one of its own is the ordinary case:
  // 33 of the 36 entries shipped have one, and the spell itself is not flagged for it.
  assert.equal(summary.tone, 'ok');
});

test('a value at a band edge is inside it: a float is not read as an eleventh decimal outside', () => {
  const document = { ...pummel(), criticalChance: 0.1 + 0.2 };
  const [crit] = readings(entryFor(withKnobs([{ path: '/criticalChance', min: 0.1, max: 0.3, step: 0.05 }]), 'spell:pummel'), document);

  assert.equal(crit.at, 'max');
  assert.deepEqual(crit.problems, []);
});

test('the value the content carries today outside its own band is named, with both numbers', () => {
  const document = { ...pummel(), effects: [{ kind: 'Damage', amount: 9 }] };
  const summary = of(withKnobs([{ path: '/effects/0/amount', min: 1, max: 3, step: 1 }]), 'spell:pummel', document);
  const [amount] = summary.knobs;

  // Editing a spell's damage in the studio is exactly what causes this, so it says what it carries and what
  // the band allows rather than only that something is wrong.
  assert.equal(amount.tone, 'bad');
  assert.equal(amount.at, 'above');
  assert.equal(amount.position, 1);
  assert.deepEqual(codes(summary), ['outside']);
  assert.match(amount.problems[0].message, /carries 9, outside \[1, 3]/);
  assert.equal(summary.headline, '1 knob, 1 the content disagrees with');
});

test('a pointer addressing nothing in the spell as it stands is named', () => {
  const summary = of(withKnobs([{ path: '/effects/1/amount', min: 1, max: 3, step: 1 }]), 'spell:pummel', pummel());

  // Removing the effect a knob points at is how this happens, and the spell still saves; the knob stops working.
  assert.deepEqual(codes(summary), ['missing']);
  assert.match(summary.knobs[0].problems[0].message, /'\/effects\/1\/amount' addresses nothing/);
  assert.equal(summary.knobs[0].value, null);
  assert.equal(summary.knobs[0].position, null);
});

test('a pointer addressing something that is not a number says what it found instead', () => {
  const summary = of(withKnobs([{ path: '/targeting', min: 1, max: 3, step: 1 }]), 'spell:pummel', { ...pummel(), targeting: { origin: 'Enemy' } });

  assert.deepEqual(codes(summary), ['notANumber']);
  assert.match(summary.knobs[0].problems[0].message, /which is not a number/);
});

test('a pointer walks objects and array indices to the number it addresses', () => {
  assert.deepEqual(readPointer({ effects: [{ amount: 2 }] }, '/effects/0/amount'), { ok: true, value: 2 });
});

test('a pointer past the end of an array addresses nothing', () => {
  assert.equal(readPointer({ effects: [] }, '/effects/0/amount').reason, 'missing');
});

test('a pointer whose token is not a digit addresses nothing in an array', () => {
  assert.equal(readPointer({ effects: [{ amount: 2 }] }, '/effects/first/amount').reason, 'missing');
});

test('a pointer that lands on something other than a number says which it found', () => {
  assert.equal(readPointer({ enabled: true }, '/enabled').reason, 'notANumber');
});

test('a pointer that does not start with a slash is refused as not a pointer', () => {
  assert.equal(readPointer({}, 'energyCost').reason, 'missing');
});

test('a critical chance knob on a spell that deals no damage can move nothing, and says so', () => {
  const heal = { id: 'spell:rejuvenate:v1', criticalChance: 0.3, effects: [{ kind: 'Heal', amount: 4 }] };
  const summary = of(withKnobs([{ path: '/criticalChance', min: 0.2, max: 0.5, step: 0.05 }]), 'spell:pummel', heal);

  assert.deepEqual(codes(summary), ['inert']);
  assert.match(summary.knobs[0].problems[0].message, /applies to damage only/);
});

test('an entry with no intent is named: nothing says what its numbers are for', () => {
  const balance = knobsFile({ 'spell:pummel': { name: 'Pummel', intent: '   ', knobs: [] } });
  const summary = of(balance, 'spell:pummel', pummel());

  assert.deepEqual(codes(summary), ['noIntent']);
  assert.equal(summary.tone, 'bad');
  assert.equal(summary.headline, 'no intent · nothing may move');
});

test('a duplicate pointer, bounds the wrong way round and a step of zero are each named', () => {
  const summary = of(withKnobs([
    { path: '/energyCost', min: 0, max: 2, step: 1 },
    { path: '/energyCost', min: 0, max: 2, step: 1 },
    { path: '/initiative', min: 3, max: 1, step: 1 },
    { path: '/effects/0/amount', min: 1, max: 3, step: 0 },
  ]), 'spell:pummel', pummel());

  // The first listing of a pointer is the one that counts, the way `_knob_problems` reads it.
  assert.deepEqual(summary.knobs[0].problems, []);
  assert.deepEqual(codes(summary), ['duplicate', 'bounds', 'outside', 'step']);
  assert.equal(summary.knobs[2].room, null);
  assert.equal(summary.knobs[3].room, null);
});

test('a knob missing its pointer or its numbers is refused as a knob rather than drawn as a band', () => {
  const summary = of(withKnobs([{ path: '/energyCost', min: 0, max: 'two', step: 1 }, { min: 0, max: 1, step: 1 }]), 'spell:pummel', pummel());

  assert.deepEqual(codes(summary), ['malformed', 'malformed']);
  assert.equal(summary.knobs[0].position, null);
});

test('a knob reading is a pure function of the knob and the document it is read against', () => {
  const knob = { path: '/energyCost', minimum: 0, maximum: 2, step: 1 };
  const document = pummel();

  assert.equal(knobReading(knob, document).value, 1);
  document.energyCost = 2;
  // Step 4 edits the number and reads again; nothing is cached in between.
  assert.equal(knobReading(knob, document).at, 'max');
});

test('a spell in enabled content with no entry at all is named by the roll-up', () => {
  const spells = [
    { id: 'spell:pummel:v1', name: 'Pummel', path: 'Spells/pummel.v1.json', enabled: true, document: pummel() },
    { id: 'spell:guard:v1', name: 'Guard', path: 'Spells/guard.v1.json', enabled: true, document: { effects: [] } },
    { id: 'spell:old:v1', name: 'Old', path: 'Spells/old.v1.json', enabled: false, document: { effects: [] } },
  ];
  const aliases = { 'spell:pummel': 'spell:pummel:v1', 'spell:guard': 'spell:guard:v1', 'spell:old': 'spell:old:v1' };
  const rolled = survey(withKnobs([{ path: '/energyCost', min: 0, max: 2, step: 1 }]), spells, aliases);

  // `check-knobs` fails on this one: enabled content with nothing saying what it is for.
  assert.deepEqual(rolled.uncovered.map(spell => spell.id), ['spell:guard:v1']);
  // A spell that is off keeps its entry on purpose, and is owed none: it left the build, so nothing tunes it.
  assert.deepEqual(rolled.unresolved, []);
  assert.equal(rolled.enabled, 2);
  assert.equal(rolled.covered, 1);
  assert.equal(rolled.entries, 1);
  assert.equal(rolled.knobs, 1);
  assert.equal(rolled.resting, 0);
});

test('an entry for a spell that is off is counted apart from the coverage of the build', () => {
  const balance = knobsFile({
    'spell:pummel': { name: 'Pummel', intent: 'The cheap swing.', knobs: [] },
    'spell:old': { name: 'Old', intent: 'What it was for, while it waits for a rule.', knobs: [] },
  });
  const spells = [
    { id: 'spell:pummel:v1', name: 'Pummel', path: 'a.json', enabled: true, document: pummel() },
    { id: 'spell:old:v1', name: 'Old', path: 'b.json', enabled: false, document: { effects: [] } },
  ];
  const rolled = survey(balance, spells, { 'spell:pummel': 'spell:pummel:v1', 'spell:old': 'spell:old:v1' });

  // Most of this catalogue is off, and the entry is the only thing left saying what such a spell was for.
  assert.equal(rolled.covered, 1);
  assert.equal(rolled.enabled, 1);
  assert.equal(rolled.resting, 1);
  assert.deepEqual(rolled.unresolved, []);
});

test('an entry for a spell no alias resolves to is named too', () => {
  const balance = knobsFile({ 'spell:cut': { name: 'Cut', intent: 'Gone.', knobs: [] } });

  assert.deepEqual(survey(balance, [], {}).unresolved, ['spell:cut']);
});

test('the roll-up carries every disagreement, with the pointer that caused it', () => {
  const spells = [{ id: 'spell:pummel:v1', name: 'Pummel', path: 'Spells/pummel.v1.json', enabled: true, document: { ...pummel(), energyCost: 7 } }];
  const rolled = survey(withKnobs([{ path: '/energyCost', min: 0, max: 2, step: 1 }]), spells, { 'spell:pummel': 'spell:pummel:v1' });

  assert.equal(rolled.flagged.length, 1);
  assert.equal(rolled.flagged[0].tone, 'bad');
  assert.equal(rolled.flagged[0].problems[0].path, '/energyCost');
});

test('a spell whose file does not parse is left alone rather than read through', () => {
  const spells = [{ id: 'spell:pummel:v1', name: 'Pummel', path: 'Spells/pummel.v1.json', enabled: true, problem: 'Unexpected token', document: { half: 'written' } }];
  const rolled = survey(withKnobs([{ path: '/energyCost', min: 0, max: 2, step: 1 }]), spells, { 'spell:pummel': 'spell:pummel:v1' });

  assert.deepEqual(rolled.flagged, []);
  assert.deepEqual(rolled.uncovered, []);
});

test('a target reading an evaluation the objective does not declare is a term missing from every score', () => {
  const objective = objectiveOf({
    objective: {
      seeds: 'benchmarks/benchmark-seeds.json',
      evaluations: { mirror: { p1: 'greedy', p2: 'greedy', reads: 'The content.' } },
      targets: [
        { metric: 'drawRate', on: 'mirror', max: 0.05, scale: 0.05, weight: 1, why: 'The round cap arriving first.' },
        { metric: 'winRateA', on: 'skill', min: 0.65, scale: 0.1, weight: 2, why: 'The skill gap.' },
      ],
    },
  });

  assert.deepEqual(objective.evaluations.map(evaluation => evaluation.name), ['mirror']);
  assert.deepEqual(objective.targets.map(target => target.declared), [true, false]);
  assert.deepEqual(objective.targets.map(target => target.key), ['mirror.drawRate', 'skill.winRateA']);
  assert.deepEqual(objective.targets.map(target => target.band), ['at most 0.05', 'at least 0.65']);
});

test('an objective the file does not carry reads as empty, not as a throw', () => {
  const objective = objectiveOf({});

  assert.deepEqual(objective.targets, []);
  assert.deepEqual(objective.evaluations, []);
});

test('constraints the file does not carry read as empty, not as a throw', () => {
  assert.deepEqual(constraintsOf(null), []);
});

test('an alias the file has no entry for reads as no entry', () => {
  assert.equal(entryFor({ spells: {} }, 'spell:pummel'), null);
});

test('targets that are not a list read as no targets', () => {
  assert.equal(objectiveOf({ objective: { targets: 'none' } }).targets.length, 0);
});

test('a constraint reads as what it refuses, why, and whether it is switched on', () => {
  const [dominance, kit] = constraintsOf({
    constraints: {
      noNewStrictDominance: { enabled: true, what: 'No new pair.', why: 'It takes a decision out of a hand.' },
      startingKitOffersAChoice: { enabled: false, spells: ['spell:wait', 'spell:basic_attack'], what: 'None better.', why: 'Journal entry ci-9.' },
    },
  });

  assert.deepEqual(dominance, { name: 'noNewStrictDominance', enabled: true, what: 'No new pair.', why: 'It takes a decision out of a hand.', spells: [] });
  assert.equal(kit.enabled, false);
  assert.deepEqual(kit.spells, ['spell:wait', 'spell:basic_attack']);
});

test('the knobs file this repository ships is the shape the page reads', () => {
  const shipped = JSON.parse(readFileSync(new URL('../data/balance/knobs.json', import.meta.url), 'utf8'));
  const answer = readBalance({ balance: shipped });

  assert.equal(answer.ok, true);
  const entry = entryFor(answer.balance, 'spell:pummel');
  assert.equal(entry.name, 'Pummel');
  assert.ok(entry.intent.length > 0);
  assert.ok(entry.knobs.some(knob => knob.path === '/criticalChance'));
  assert.ok(objectiveOf(answer.balance).targets.every(target => target.declared));
  assert.ok(constraintsOf(answer.balance).length >= 3);
});

// ---------- what the review found the page and `validate` disagreeing about ----------

test('a spell that is off is counted but never judged, the way validate never reads it', () => {
  const balance = { version: 'knobs:v1', spells: { 'spell:old': { name: 'Old', intent: '', knobs: [{ path: '/energyCost', min: 1, max: 3, step: 1 }] } } };
  const rolled = survey(balance, [{ id: 'spell:old:v1', name: 'Old', path: 'Spells/old.v1.json', enabled: false, document: { energyCost: 9 } }], { 'spell:old': 'spell:old:v1' });

  assert.deepEqual(rolled.flagged, []);
  assert.equal(rolled.resting, 1);
  assert.equal(rolled.covered, 0);
});

test('the same spell turned back on is judged again', () => {
  const balance = { version: 'knobs:v1', spells: { 'spell:old': { name: 'Old', intent: '', knobs: [{ path: '/energyCost', min: 1, max: 3, step: 1 }] } } };
  const rolled = survey(balance, [{ id: 'spell:old:v1', name: 'Old', path: 'Spells/old.v1.json', document: { energyCost: 9 } }], { 'spell:old': 'spell:old:v1' });

  assert.equal(rolled.flagged.length, 1);
  assert.deepEqual(rolled.flagged[0].problems.map(problem => problem.code), ['noIntent', 'outside']);
});

test('a second alias on the same spell is known, not reported as naming nothing', () => {
  const balance = { version: 'knobs:v1', spells: { 'spell:pummel': { intent: 'The all-in.', knobs: [] }, 'spell:punch': { intent: 'The same spell, other name.', knobs: [] } } };
  const rolled = survey(balance, [{ id: 'spell:pummel:v1', document: {} }], { 'spell:pummel': 'spell:pummel:v1', 'spell:punch': 'spell:pummel:v1' });

  assert.deepEqual(rolled.unresolved, []);
});

test('an entry for a spell nothing points at is still reported as naming nothing', () => {
  const balance = { version: 'knobs:v1', spells: { 'spell:cut': { intent: 'Gone.', knobs: [] } } };
  const rolled = survey(balance, [], {});

  assert.deepEqual(rolled.unresolved, ['spell:cut']);
});

test('a constraint naming a spell nothing resolves to is reported as checking nothing', () => {
  const balance = {
    version: 'knobs:v1',
    spells: {},
    constraints: { startingKitOffersAChoice: { enabled: true, spells: ['spell:wait', 'spell:ghost'] } },
  };
  const rolled = survey(balance, [{ id: 'spell:wait:v1', document: {} }], { 'spell:wait': 'spell:wait:v1' });

  assert.equal(rolled.constraintProblems.length, 1);
  assert.equal(rolled.constraintProblems[0].alias, 'spell:ghost');
  assert.equal(rolled.constraintProblems[0].constraint, 'startingKitOffersAChoice');
});

test('a critical chance on a permanent damage effect is inert, because check-knobs groups it apart', () => {
  const knob = { path: '/criticalChance', minimum: 0.4, maximum: 0.8, step: 0.05 };
  const permanent = knobReading(knob, { criticalChance: 0.5, effects: [{ kind: 'Damage', amount: 4, permanent: true }] });
  const ordinary = knobReading(knob, { criticalChance: 0.5, effects: [{ kind: 'Damage', amount: 4 }] });

  assert.deepEqual(permanent.problems.map(problem => problem.code), ['inert']);
  assert.deepEqual(ordinary.problems, []);
});

test('a pointer is split and not unescaped, which is what _tokens does', () => {
  const found = readPointer({ 'a~1b': 3 }, '/a~1b');

  assert.deepEqual(found, { ok: true, value: 3 });
});

test('an unknown version is named before the shape is, so a newer file is not called malformed', () => {
  const answer = readBalance({ balance: { version: 'knobs:v2', spells: 'anything at all' } });

  assert.equal(answer.ok, false);
  assert.match(answer.why, /version 'knobs:v2'/);
});

test('a file of the right version with no spells at all covers nothing rather than being refused', () => {
  const answer = readBalance({ balance: { version: 'knobs:v1' } });

  assert.equal(answer.ok, true);
  assert.equal(entryFor(answer.balance, 'spell:pummel'), null);
});

test('a spells that is present and not a map is refused', () => {
  const answer = readBalance({ balance: { version: 'knobs:v1', spells: ['spell:pummel'] } });

  assert.equal(answer.ok, false);
  assert.match(answer.why, /not a map of alias to entry/);
});

test('a reading refuses a knob that is nothing at all rather than throwing', () => {
  const reading = knobReading({}, {});

  assert.equal(reading.tone, 'bad');
  assert.deepEqual(reading.problems.map(problem => problem.code), ['malformed']);
});

test('a summary of no entry at all says so rather than throwing', () => {
  const summary = summarise(null, {});

  assert.equal(summary.tone, 'bad');
  assert.deepEqual(summary.problems.map(problem => problem.code), ['noIntent']);
});

test('an alias is looked up without an alias map rather than throwing', () => {
  assert.equal(aliasOfSpell('spell:pummel:v1', null), null);
});

test('a spell whose file did not parse is left out of the reading entirely', () => {
  const balance = { version: 'knobs:v1', spells: { 'spell:broken': { intent: 'Something.', knobs: [{ path: '/energyCost', min: 1, max: 3, step: 1 }] } } };
  const rolled = survey(balance, [{ id: 'spell:broken:v1', problem: 'energyCost is required', document: {} }], { 'spell:broken': 'spell:broken:v1' });

  assert.deepEqual(rolled.flagged, []);
  assert.deepEqual(rolled.uncovered, []);
});
