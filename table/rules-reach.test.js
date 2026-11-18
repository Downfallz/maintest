// Which sections of the official rulebook a change reaches (scripts/rules/reach.mjs, read by the /rules skill and
// the rules-keeper): by the ADRs and game-rules headings a section cites, by the values it draws, and by its own
// lines. What no section draws or cites is listed for a reading by hand.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readDiff, reach, report, sectionsOf } from '../scripts/rules/reach.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

const PARTS = [
  {
    file: '01-the-goal.md',
    text: [
      '# What you are trying to do {#the-goal}',
      '',
      'The opening of the part.',
      '',
      '## Winning {#winning}',
      '',
      "A creature starts at {creature.health} Health, and its Defense buffs count for {engine.defenseBuffCeiling} at most. It knows Focus{?creature.knows = 'spell:wait:v1'}.",
      '',
      'sources: ADR 0011, game-rules#match-lifecycle',
    ].join('\n'),
  },
  {
    file: '02-the-round.md',
    text: [
      '# The round {#the-round}',
      '',
      '## Energy gain {#energy-gain}',
      '',
      'Every living creature gains {rules.energyPerRound} Energy, and Archmage gives {tier:archmage:v1.upkeepEnergy}.',
      '',
      'sources: ADR 0114, game-rules#round-sequence',
      '',
      '## Damage {#damage}',
      '',
      'Heavy Strike hits for {spell:heavy_strike:v1.damage}; a critical hit is {rules.criticalMultiplier} times that.',
      '',
      'sources: ADR 0010',
      '',
      '```card',
      'id: damage',
      'title: Damage',
      'A hit takes Health.',
      '> {spell:heavy_strike:v1.damage}',
      '```',
    ].join('\n'),
  },
];

const GAME_RULES = ['# Game rules', '', '## Match lifecycle', '', 'A match ends.', '', '## Round sequence', '', 'Upkeep first.'].join(
  '\n',
);

const CONTENT = new Map([
  ['data/Spells/brawler/heavy_strike.v1.json', { id: 'spell:heavy_strike:v1', name: 'Heavy Strike' }],
  ['data/Tiers/archmage.v1.json', { id: 'tier:archmage:v1', name: 'Archmage' }],
  ['data/Tiers/titan.v1.json', { id: 'tier:titan:v1', name: 'Titan' }],
]);

const given = { sections: sectionsOf(PARTS), gameRules: GAME_RULES, content: path => CONTENT.get(path) ?? null };

/** A one-hunk diff of a file: the lines it removes and adds, at a line of the new file. */
function diffOf(path, { at = 1, removed = [], added = [], context = [] } = {}) {
  return [
    `diff --git a/${path} b/${path}`,
    `--- a/${path}`,
    `+++ b/${path}`,
    `@@ -${at},${removed.length + context.length} +${at},${added.length + context.length} @@`,
    ...context.map(line => ` ${line}`),
    ...removed.map(line => `-${line}`),
    ...added.map(line => `+${line}`),
  ].join('\n');
}

const reached = diff => reach(readDiff(diff), given);
const ids = diff => reached(diff).sections.map(section => section.id);

test('the book is read as its parts and sections, numbered in the order of the files, each with its lines', () => {
  const sections = sectionsOf(PARTS);
  assert.deepEqual(
    sections.map(({ id, number, title, from, to }) => ({ id, number, title, from, to })),
    [
      { id: 'the-goal', number: '1', title: 'What you are trying to do', from: 1, to: 4 },
      { id: 'winning', number: '1.1', title: 'Winning', from: 5, to: 9 },
      { id: 'the-round', number: '2', title: 'The round', from: 1, to: 2 },
      { id: 'energy-gain', number: '2.1', title: 'Energy gain', from: 3, to: 8 },
      { id: 'damage', number: '2.2', title: 'Damage', from: 9, to: 20 },
    ],
  );
  assert.deepEqual(sections[3].sources, ['ADR 0114', 'game-rules#round-sequence']);
});

test('a unified diff gives each path, the lines it adds and removes, and where in the new file it changed', () => {
  const [change] = readDiff(diffOf('rules/02-the-round.md', { at: 4, context: ['', 'kept'], removed: ['old'], added: ['new', 'newer'] }));
  assert.equal(change.path, 'rules/02-the-round.md');
  assert.deepEqual(change.removed, ['old']);
  assert.deepEqual(change.added, ['new', 'newer']);
  assert.deepEqual([...change.lines], [6, 7]);
  assert.deepEqual([...change.removedAt], [6]);
});

test('a rule removed from the end of a section reaches that section, not the one whose heading follows', () => {
  // As git writes it: the last paragraph of Match lifecycle and its blank line go, just above Round sequence.
  const rules = [
    'diff --git a/docs/domain/game-rules.md b/docs/domain/game-rules.md',
    '--- a/docs/domain/game-rules.md',
    '+++ b/docs/domain/game-rules.md',
    '@@ -5,5 +5,3 @@',
    ' A match ends.',
    ' ',
    '-A match also ends at the round cap.',
    '-',
    ' ## Round sequence',
  ].join('\n');
  assert.deepEqual(ids(rules), ['winning']);
  // The same in the book: the end of Energy gain removed, just above the Damage heading.
  const book = [
    'diff --git a/rules/02-the-round.md b/rules/02-the-round.md',
    '--- a/rules/02-the-round.md',
    '+++ b/rules/02-the-round.md',
    '@@ -7,5 +7,3 @@',
    ' sources: ADR 0114, game-rules#round-sequence',
    ' ',
    '-A line.',
    '-',
    ' ## Damage {#damage}',
  ].join('\n');
  assert.deepEqual(ids(book), ['energy-gain']);
});

test('input that is not a unified diff is refused, never read as nothing reached', () => {
  const script = join(root, 'scripts/rules/reach.mjs');
  for (const input of ['', 'rules/03-the-round.md\ndata/Tiers/titan.v1.json\n']) {
    assert.throws(
      () => execFileSync('node', [script], { cwd: root, input, encoding: 'utf8', stdio: 'pipe' }),
      error => error.status === 1 && /not a unified diff/.test(error.stderr),
      JSON.stringify(input),
    );
  }
});

test('an ADR reaches the sections that cite it, and no other', () => {
  const { sections } = reached(diffOf('docs/adr/0114-a-shared-package-is-held-by-the-whole-side.md', { added: ['More.'] }));
  assert.deepEqual(sections.map(section => section.id), ['energy-gain']);
  assert.deepEqual(sections[0].why, ['cites ADR 0114']);
});

test('a spell reaches the sections that draw its values, cards included, or name it', () => {
  assert.deepEqual(ids(diffOf('data/Spells/brawler/heavy_strike.v1.json', { removed: ['"amount": 6'], added: ['"amount": 7'] })), [
    'damage',
  ]);
  assert.deepEqual(reached(diffOf('data/Spells/brawler/heavy_strike.v1.json', { added: ['x'] })).sections[0].why, [
    'draws spell:heavy_strike:v1',
    'names Heavy Strike',
  ]);
});

test('a mutation reaches the sections that draw it or name it, and a mutation the book never names reaches nothing', () => {
  assert.deepEqual(ids(diffOf('data/Tiers/archmage.v1.json', { added: ['"upkeepEnergy": 2'] })), ['energy-gain']);
  const titan = reached(diffOf('data/Tiers/titan.v1.json', { added: ['"sunder": 10'] }));
  assert.deepEqual(titan.sections, []);
  assert.deepEqual(titan.unmapped, ['data/Tiers/titan.v1.json']);
});

test('the hosted rule set reaches the sections that draw the fields it changes', () => {
  const diff = diffOf('docs/tabletop/playtest.rules.json', { removed: ['  "criticalMultiplier": 2,'], added: ['  "criticalMultiplier": 3,'] });
  assert.deepEqual(ids(diff), ['damage']);
  assert.deepEqual(reached(diff).sections[0].why, ['draws rules.criticalMultiplier']);
});

test('the reference creature reaches the sections that draw the stat it changes', () => {
  const diff = diffOf('data/Creatures/main.v1.json', { removed: ['  "baseHealth": 30,'], added: ['  "baseHealth": 32,'] });
  assert.deepEqual(ids(diff), ['winning']);
  assert.deepEqual(ids(diffOf('data/Creatures/main.v1.json', { removed: ['  "baseSpeed": 3,'], added: ['  "baseSpeed": 4,'] })), []);
});

test('the engine\'s constant reaches the sections that draw it, and another line of its file reaches none by it', () => {
  const file = 'src/DownfallArena.Domain/Matches/Creatures/Creature.cs';
  const diff = diffOf(file, { removed: ['    public const int DefenseBuffCeiling = 10;'], added: ['    public const int DefenseBuffCeiling = 12;'] });
  assert.deepEqual(ids(diff), ['winning']);
  assert.deepEqual(reached(diff).sections[0].why, ['draws engine.defenseBuffCeiling']);
  assert.deepEqual(ids(diffOf(file, { removed: ['    public bool IsDead => Health.IsZero;'], added: ['    public bool IsDead => Health.IsEmpty;'] })), []);
});

test('a creature change that moves no stat reaches no section by its stats, and is read by hand', () => {
  const diff = diffOf('data/Creatures/main.v1.json', { removed: ['  "talentTreeId": "a",'], added: ['  "talentTreeId": "b",'] });
  assert.deepEqual(reached(diff), { sections: [], unmapped: ['data/Creatures/main.v1.json'] });
});

// #805: the creature's starting spells are drawn as creature.knows, resolved through data/aliases.json.
test('the creature\'s starting spells, or an alias they resolve through, reach the sections that state what it knows', () => {
  const starting = reached(diffOf('data/Creatures/main.v1.json', { removed: ['  "startingSpellIds": ["spell:heavy_strike", "spell:wait"],'], added: ['  "startingSpellIds": ["spell:heavy_strike"],'] }));
  assert.deepEqual(starting.sections.map(section => [section.id, section.why]), [['winning', ['draws creature.knows']]]);
  const alias = reached(diffOf('data/aliases.json', { removed: ['  "spell:wait": "spell:wait:v1",'], added: ['  "spell:wait": "spell:wait:v2",'] }));
  assert.deepEqual(alias.sections.map(section => [section.id, section.why]), [['winning', ['draws creature.knows']]]);
  assert.deepEqual(alias.unmapped, []);
});

test('a renamed spell reaches the sections that still name it by its old name', () => {
  const diff = diffOf('data/Spells/brawler/heavy_strike.v1.json', { removed: ['  "name": "Heavy Strike",'], added: ['  "name": "Crushing Blow",'] });
  const renamed = new Map([['data/Spells/brawler/heavy_strike.v1.json', { id: 'spell:heavy_strike:v1', name: 'Crushing Blow' }]]);
  const { sections } = reach(readDiff(diff), { ...given, content: path => renamed.get(path) ?? null });
  assert.deepEqual(sections[0].why, ['draws spell:heavy_strike:v1', 'names Heavy Strike']);
});

test('game-rules reaches the sections that cite the heading it changes, or the heading it renames', () => {
  assert.deepEqual(ids(diffOf('docs/domain/game-rules.md', { at: 9, removed: ['Upkeep first.'], added: ['Upkeep comes first.'] })), [
    'energy-gain',
  ]);
  assert.deepEqual(ids(diffOf('docs/domain/game-rules.md', { at: 3, removed: ['## Life of a match'], added: ['## Match lifecycle'] })), [
    'winning',
  ]);
});

test('a change to the book reaches the sections whose lines it changes, the opening of a part included', () => {
  assert.deepEqual(ids(diffOf('rules/02-the-round.md', { at: 11, removed: ['Heavy Strike hits.'], added: ['Heavy Strike hits hard.'] })), [
    'damage',
  ]);
  assert.deepEqual(ids(diffOf('rules/01-the-goal.md', { at: 3, removed: ['The opening.'], added: ['The opening of the part.'] })), [
    'the-goal',
  ]);
  assert.deepEqual(reached(diffOf('rules/01-the-goal.md', { at: 3, added: ['x'] })).sections[0].why, ['changed in the book']);
});

test('what no section draws or cites is listed for a reading by hand, and the reaching sections come in the book\'s order', () => {
  const diff = [
    diffOf('src/DownfallArena.Domain/Matches/Match.cs', { added: ['x'] }),
    diffOf('table/terms.js', { added: ['x'] }),
    diffOf('docs/adr/0010-the-round.md', { added: ['x'] }),
    diffOf('docs/adr/0114-a-shared-package-is-held-by-the-whole-side.md', { added: ['x'] }),
    diffOf('scripts/verify.sh', { added: ['x'] }),
  ].join('\n');
  const { sections, unmapped } = reached(diff);
  assert.deepEqual(sections.map(section => section.id), ['energy-gain', 'damage']);
  assert.deepEqual(unmapped, ['src/DownfallArena.Domain/Matches/Match.cs', 'table/terms.js']);
});

test('the French edition is reached beside the English, by its sources and its own lines, and marked [fr]', () => {
  const french = [{ file: 'fr/01-the-goal.md', text: PARTS[0].text }, { file: 'fr/02-the-round.md', text: PARTS[1].text.replace('# The round', '# Le round').replace('## Energy gain', '## Gain d’Énergie') }];
  const both = { ...given, sections: [...given.sections, ...sectionsOf(french)] };
  const byAdr = reach(readDiff(diffOf('docs/adr/0114-a-shared-package-is-held-by-the-whole-side.md', { added: ['x'] })), both);
  assert.deepEqual(byAdr.sections.map(section => [section.id, section.file]), [['energy-gain', '02-the-round.md'], ['energy-gain', 'fr/02-the-round.md']]);
  assert.match(report(byAdr), /^2\.1 energy-gain \[fr\] \(Gain d’Énergie\): cites ADR 0114$/m);
  const own = reach(readDiff(diffOf('rules/fr/02-the-round.md', { at: 5, added: ['Chaque créature vivante gagne.'] })), both);
  assert.deepEqual(own.sections.map(section => [section.id, section.file, section.why]), [['energy-gain', 'fr/02-the-round.md', ['changed in the book']]]);
  assert.deepEqual(own.unmapped, []);
});

test('on the real book, an excerpt of a past balance pass reaches the section that teaches the shared Archmage', () => {
  // Commit 0532e592 (#371) shared Archmage with the whole side (ADR 0114) and raised Titan's sunder.
  const diff = [
    diffOf('docs/adr/0114-a-shared-package-is-held-by-the-whole-side.md', { added: ['# 0114. A shared package is held by the whole side'] }),
    diffOf('data/Tiers/archmage.v1.json', { added: ['  "sharedWithTeam": true,'] }),
    diffOf('data/Tiers/titan.v1.json', { removed: ['    "sunder": 2'], added: ['    "sunder": 10'] }),
  ].join('\n');
  const output = execFileSync('node', [join(root, 'scripts/rules/reach.mjs')], { cwd: root, input: diff, encoding: 'utf8' });
  assert.match(output, /^3\.1 energy-gain \(Energy gain\): cites ADR 0114, draws tier:archmage:v1, names Archmage$/m);
  assert.match(output, /draws tier:titan:v1/);
});
