// The official rulebook (ADR 0188, docs/tabletop/rulebook-plan.md R1): `rules/` is built into `table/rules.json` by
// scripts/rules/build.mjs. Each refusal of the build has its test on a small book written here; the committed file
// is the build of the repository's book (UPDATE_INDEXES=1 rewrites it, as for ADR 0184's indexes); and its values
// are the hosted rule set's and the reference creature's. The French edition (rules/fr/, R6) is held to the English.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildRules, evaluate, idsOf, linksFrom, linksIn, readInputs, render } from '../scripts/rules/build.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const committed = join(root, 'table/rules.json');
const committedFrench = join(root, 'table/rules.fr.json');

const terminology = `| Internal (domain, code, data) | Legacy player-facing | Player-facing EN | Player-facing FR |
| --- | --- | --- | --- |
| Tier (\`tier:...\` ids) | Package | **Mutation** | **Mutation** |
| Tier \`level\` | Tier (Tier 2) | **Stage** (Stage 2) | **Stade** (Stade 2) |
| Speed (\`Speed\` choice) | Speed | **Tempo** | **Tempo** |
| \`Speed.Quick\` | Quick | **Instinct** | **Instinct** |
| Evolution | Evolution | **Evolution** | **Évolution** |
| \`StartOfRound\` | Start | **Upkeep** | **Entretien** |

| Before | After |
| --- | --- |
| Choose speed | Choose tempo |
| Your spellbook | Your spellbook |
`;

const goal = `# What you are trying to do {#the-goal}

Two players, each with {rules.teamSize} creatures.

## Winning {#winning}

A creature starts at {creature.health} Health. **Defeat** every creature of the other team.

sources: ADR 0011, game-rules#match-lifecycle

\`\`\`card
id: health
title: Health
At zero Health a creature is defeated.
> A creature starts at {creature.health} Health. A hit of 7 leaves it at {max(0, creature.health - 7)}.
\`\`\`

## A round {#a-round}

- Upkeep: every creature gains {rules.energyPerRound} Energy.
- Combat: *at its slot*, a creature casts. See [winning](#winning).

| Value | Reference |
| --- | --- |
| Critical multiplier | {rules.criticalMultiplier} |

Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.

sources: ADR 0010
`;

/** A small book that builds clean; each test changes one thing in it. */
function inputs(overrides = {}) {
  return {
    parts: [{ file: '01-the-goal.md', text: goal }],
    moved: {},
    changes: null,
    ruleSet: { path: 'docs/tabletop/playtest.rules.json', values: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 2.0 } },
    creature: { id: 'creature:main:v1', baseHealth: 30, baseDefense: 0, baseInitiative: 5 },
    spells: new Map([['spell:pummel:v1', { id: 'spell:pummel:v1', name: 'Pummel', energyCost: 1, effects: [{ kind: 'Damage', amount: 2 }] }]]),
    terminology,
    adrs: new Set(['0010', '0011']),
    gameRules: new Set(['match-lifecycle']),
    conceptIds: ['health', 'energy'],
    // The small book's capitalised words; the real book's are rules/words.json.
    words: {
      en: ['Health', 'Energy', 'Defense', 'Upkeep', 'Evolution', 'Stage', 'What', 'Two', 'Defeat', 'Combat', 'See', 'Value', 'Reference', 'Critical'],
      fr: ['Santé', 'Énergie', 'Précision', 'Stade', 'Évolution', 'Tempo', 'Battez', 'Ce', 'Combat', 'Deux', 'Entretien', 'Multiplicateur', 'Référence', 'Tous', 'Valeur', 'Voir', 'Au'],
    },
    engine: { defenseBuffCeiling: 10 },
    links: [],
    previous: null,
    ...overrides,
  };
}

const withGoal = (from, to) => inputs({ parts: [{ file: '01-the-goal.md', text: goal.replace(from, to) }] });
const errorsOf = given => buildRules(given).errors;
const refuses = (given, pattern) => {
  const errors = errorsOf(given);
  assert.ok(errors.some(error => pattern.test(error)), `expected ${pattern}, got ${JSON.stringify(errors)}`);
};

test('a clean book builds with no error, numbered from its files and its headings', () => {
  const { book, errors } = buildRules(inputs());
  assert.deepEqual(errors, []);
  assert.equal(book.parts.length, 1);
  const [part] = book.parts;
  assert.deepEqual({ id: part.id, number: part.number, title: part.title }, { id: 'the-goal', number: 1, title: 'What you are trying to do' });
  assert.deepEqual(part.sections.map(section => [section.id, section.number, section.title]), [['winning', '1.1', 'Winning'], ['a-round', '1.2', 'A round']]);
});

test('a section\'s blocks are data: paragraphs, lists and tables of spans, never HTML', () => {
  const { book } = buildRules(inputs());
  const [winning, round] = book.parts[0].sections;
  assert.deepEqual(winning.blocks[0], {
    type: 'paragraph',
    spans: [{ text: 'A creature starts at 30 Health. ' }, { text: 'Defeat', strong: true }, { text: ' every creature of the other team.' }],
  });
  assert.deepEqual(round.blocks[0].type, 'list');
  assert.deepEqual(round.blocks[0].items[1], [{ text: 'Combat: ' }, { text: 'at its slot', emphasis: true }, { text: ', a creature casts. See ' }, { text: 'winning', href: '#winning' }, { text: '.' }]);
  assert.deepEqual(round.blocks[1], { type: 'table', header: [[{ text: 'Value' }], [{ text: 'Reference' }]], rows: [[[{ text: 'Critical multiplier' }], [{ text: '2' }]]] });
  assert.ok(!JSON.stringify(book).includes('<'));
});

test('a section ends with its sources, and carries the card it summarises', () => {
  const { book } = buildRules(inputs());
  const [winning, round] = book.parts[0].sections;
  assert.deepEqual(winning.sources, ['ADR 0011', 'game-rules#match-lifecycle']);
  assert.deepEqual(round.sources, ['ADR 0010']);
  assert.deepEqual(winning.card, {
    id: 'health', title: 'Health', lines: ['At zero Health a creature is defeated.'], example: 'A creature starts at 30 Health. A hit of 7 leaves it at 23.',
  });
  assert.equal(round.card, null);
  assert.deepEqual(book.cards, [{ id: 'health', section: 'winning' }]);
});

test('the values are drawn from the rule set, the creature and the spell, so a balance pass moves the book', () => {
  const moved = buildRules(inputs({
    ruleSet: { path: 'x', values: { teamSize: 4, energyPerRound: 3, criticalMultiplier: 1.5 } },
    creature: { id: 'creature:main:v1', baseHealth: 40 },
    spells: new Map([['spell:pummel:v1', { id: 'spell:pummel:v1', name: 'Pummel', energyCost: 2, effects: [{ kind: 'Damage', amount: 5 }] }]]),
  }));
  assert.deepEqual(moved.errors, []);
  const text = JSON.stringify(moved.book);
  for (const expected of ['each with 4 creatures', 'starts at 40 Health', 'gains 3 Energy', '"1.5"', 'Pummel costs 2 and deals 5', 'leaves it at 33']) {
    assert.ok(text.includes(expected), expected);
  }
});

test('a value is a small expression: names, numbers, + - * /, parentheses, max, min and floor', () => {
  const look = name => ({ a: 30, b: 2 })[name];
  assert.equal(evaluate('a', look), 30);
  assert.equal(evaluate('max(0, a - 7) * b', look), 46);
  assert.equal(evaluate('floor(7 * 1.5)', look), 10);
  assert.equal(evaluate('min(a, b + 1) / 2', look), 1.5);
  assert.throws(() => evaluate('a +', look));
  assert.throws(() => evaluate('process.exit(1)', look));
});

test('refuses a section id used twice, or a part and a section sharing one', () => {
  refuses(withGoal('{#a-round}', '{#winning}'), /id "winning" is used twice/);
  refuses(withGoal('{#a-round}', '{#the-goal}'), /id "the-goal" is used twice/);
});

test('refuses a heading without its id, or an id that is not lowercase words and hyphens', () => {
  refuses(withGoal('## A round {#a-round}', '## A round'), /heading "A round" has no \{#id\}/);
  refuses(withGoal('{#a-round}', '{#A_Round}'), /id "A_Round"/);
});

test('refuses an id gone from the book that moved.json does not redirect, and a redirect to nowhere', () => {
  const previous = { ids: ['the-goal', 'winning', 'the-end'] };
  refuses(inputs({ previous }), /id "the-end" is gone and rules\/moved\.json does not redirect it/);
  assert.deepEqual(errorsOf(inputs({ previous, moved: { 'the-end': 'winning' } })), []);
  refuses(inputs({ moved: { 'the-end': 'nowhere' } }), /moved\.json sends "the-end" to "nowhere", which is not a section/);
  refuses(inputs({ moved: { winning: 'a-round' } }), /moved\.json redirects "winning", which is still a section/);
});

test('the earlier ids are read from a built book, its redirects included, and a conflicted file is refused', () => {
  const built = JSON.stringify({ parts: [{ id: 'the-goal', sections: [{ id: 'winning' }] }], moved: { 'the-end': 'winning' } });
  assert.deepEqual(idsOf(built, 'x'), { ids: ['the-goal', 'winning', 'the-end'] });
  assert.match(idsOf('<<<<<<< HEAD\n{}\n=======\n{}\n>>>>>>> main\n', 'table/rules.json').error, /table\/rules\.json is not JSON: settle a conflict by taking main's file and rebuilding/);
  refuses(inputs({ previous: { ids: [], errors: ['table/rules.json is not JSON: settle a conflict by taking main\'s file and rebuilding'] } }), /not JSON/);
});

test('a link to a moved id still lands, and the redirects are in the built book', () => {
  const given = inputs({ moved: { 'the-end': 'winning' }, links: [{ file: 'table/x.js', id: 'the-end' }] });
  assert.deepEqual(errorsOf(given), []);
  assert.deepEqual(buildRules(given).book.moved, { 'the-end': 'winning' });
});

test('refuses a value that does not resolve, and a spell id that is not in data/', () => {
  refuses(withGoal('{rules.teamSize}', '{rules.teamsize}'), /"rules\.teamsize" does not resolve/);
  refuses(withGoal('{creature.health}', '{creature.mana}'), /"creature\.mana" does not resolve/);
  refuses(withGoal('{spell:pummel:v1.energyCost}', '{spell:punch:v1.energyCost}'), /spell "spell:punch:v1" is not in data\//);
  refuses(withGoal('{spell:pummel:v1.damage}', '{spell:pummel:v1.heal}'), /"spell:pummel:v1\.heal" does not resolve/);
  refuses(withGoal('{rules.teamSize}', '{rules.teamSize +}'), /"rules\.teamSize \+"/);
  refuses(withGoal('{rules.teamSize}', '{rules.teamSize / 0}'), /"rules\.teamSize \/ 0" is not a finite number/);
});

test('a spell\'s effect is read by its kind, and refused when the spell has two of that kind', () => {
  const spells = new Map([['spell:guard:v1', { id: 'spell:guard:v1', effects: [{ kind: 'DefenseBuff', amount: 2 }, { kind: 'DefenseBuff', amount: 1, durationRounds: 3 }] }],
    ['spell:latch:v1', { id: 'spell:latch:v1', name: 'Latch', effects: [{ kind: 'Bleed', amountPerRound: 2, durationRounds: 3 }] }]]);
  const book = text => inputs({ spells, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', text) }] });
  assert.deepEqual(errorsOf(book('Latch bleeds {spell:latch:v1.bleed} for {spell:latch:v1.bleed.durationRounds|round|rounds}.')), []);
  assert.ok(JSON.stringify(buildRules(book('Latch bleeds {spell:latch:v1.bleed} for {spell:latch:v1.bleed.durationRounds|round|rounds}.')).book).includes('Latch bleeds 2 for 3 rounds.'));
  refuses(book('Guard gives {spell:guard:v1.defenseBuff}.'), /"spell:guard:v1\.defenseBuff" names one of two DefenseBuff effects/);
});

const pummelLine = 'Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.';
const spellBook = (spells, text, overrides = {}) => inputs({ spells, parts: [{ file: '01-the-goal.md', text: goal.replace(pummelLine, text) }], ...overrides });
const builtText = given => JSON.stringify(buildRules(given).book);
const bloodPrice = ['spell:hateful_sacrifice:v1', { id: 'spell:hateful_sacrifice:v1', name: 'Blood Price', targeting: { origin: 'Enemy', scope: 'SingleTarget', maxTargets: 1 },
  effects: [{ kind: 'Damage', amount: 11 }], casterEffects: [{ kind: 'Damage', amount: 3 }] }];
const wraithguard = ['spell:revenant_guards:v1', { id: 'spell:revenant_guards:v1', name: 'Wraithguard', targeting: { origin: 'Ally', scope: 'Multi', maxTargets: 3 },
  effects: [{ kind: 'DefenseBuff', amount: 3, permanent: true }, { kind: 'DefenseBuff', amount: 5, durationRounds: 2 }, { kind: 'Stun', durationRounds: 1 }] }];

test('the caster\'s own line is drawn from casterEffects, by its kind, as the targets\' line is', () => {
  const spells = new Map([bloodPrice]);
  const clean = spellBook(spells, 'Blood Price deals {spell:hateful_sacrifice:v1.damage}, and {spell:hateful_sacrifice:v1.caster.damage} to its caster.');
  assert.deepEqual(errorsOf(clean), []);
  assert.ok(builtText(clean).includes('Blood Price deals 11, and 3 to its caster.'));
  refuses(spellBook(spells, '{spell:hateful_sacrifice:v1.caster.heal}'), /"spell:hateful_sacrifice:v1\.caster\.heal" does not resolve/);
});

test('a spell\'s targets, its origin, the count of an effect kind and one effect of several by its order are drawn', () => {
  const spells = new Map([wraithguard]);
  const clean = spellBook(spells, 'Wraithguard ({spell:revenant_guards:v1.origin}, up to {spell:revenant_guards:v1.targets}) gives {spell:revenant_guards:v1.defenseBuff.count} buffs: {spell:revenant_guards:v1.defenseBuff1} and {spell:revenant_guards:v1.defenseBuff2} for {spell:revenant_guards:v1.defenseBuff2.durationRounds|round|rounds}.');
  assert.deepEqual(errorsOf(clean), []);
  assert.ok(builtText(clean).includes('Wraithguard (Ally, up to 3) gives 2 buffs: 3 and 5 for 2 rounds.'));
  refuses(spellBook(spells, '{spell:revenant_guards:v1.defenseBuff3}'), /"spell:revenant_guards:v1\.defenseBuff3" does not resolve/);
});

test('a fact the book states is checked when it is built, and draws nothing a player reads', () => {
  const spells = new Map([wraithguard, bloodPrice]);
  const clean = spellBook(spells, 'Wraithguard{?spell:revenant_guards:v1.origin = \'Ally\'}{?spell:revenant_guards:v1.targets > 1} shields several allies; its first buff is permanent{?spell:revenant_guards:v1.defenseBuff1.permanent}, and it stuns{?spell:revenant_guards:v1.stun}.');
  assert.deepEqual(errorsOf(clean), []);
  assert.ok(builtText(clean).includes('Wraithguard shields several allies; its first buff is permanent, and it stuns.'));
  refuses(spellBook(spells, 'Blood Price stuns{?spell:hateful_sacrifice:v1.stun}.'), /"spell:hateful_sacrifice:v1\.stun" does not resolve/);
  refuses(spellBook(spells, 'Blood Price{?spell:hateful_sacrifice:v1.origin = \'Ally\'} heals.'), /the fact "spell:hateful_sacrifice:v1\.origin = 'Ally'" is false/);
  refuses(spellBook(spells, 'Wraithguard{?spell:revenant_guards:v1.targets = 1}.'), /the fact "spell:revenant_guards:v1\.targets = 1" is false/);
  refuses(spellBook(spells, 'Its second buff{?spell:revenant_guards:v1.defenseBuff2.permanent} lasts.'), /"spell:revenant_guards:v1\.defenseBuff2\.permanent" does not resolve/);
  refuses(spellBook(spells, 'It stuns for {spell:revenant_guards:v1.stun}.'), /"spell:revenant_guards:v1\.stun" is a fact, not a value: write \{\?/);
  refuses(spellBook(spells, '{?spell:revenant_guards:v1.targets}'), /"spell:revenant_guards:v1\.targets" is not a fact/);
});

test('the engine\'s Defense ceiling is drawn by its name, and the book says it was', () => {
  const clean = withGoal('Two players', 'Defense buffs count for {engine.defenseBuffCeiling} at most. Two players');
  assert.deepEqual(errorsOf(clean), []);
  assert.ok(builtText(inputs({ ...clean, engine: { defenseBuffCeiling: 12 } })).includes('count for 12 at most'));
  refuses(withGoal('Two players', '{engine.defenseCap} Two players'), /"engine\.defenseCap" does not resolve/);
  refuses(inputs({ engine: {}, engineMissing: ['src/Creature.cs declares no const int DefenseBuffCeiling: engine.defenseBuffCeiling has no source'] }), /^src\/Creature\.cs declares no const int DefenseBuffCeiling: engine\.defenseBuffCeiling has no source/);
});

test('a count is written with its noun, singular for one, so a value of 1 never reads "1 rounds"', () => {
  const at = interval => inputs({ ruleSet: { path: 'x', values: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 2, evolutionInterval: interval } },
    parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', 'Every {rules.evolutionInterval|round|rounds}, two players') }] });
  assert.ok(builtText(at(2)).includes('Every 2 rounds, two players'));
  assert.ok(builtText(at(1)).includes('Every 1 round, two players'));
  refuses(withGoal('Two players', 'For {rules.teamSize} rounds, two players'), /"\{rules\.teamSize\} rounds": a count takes its noun: \{rules\.teamSize\|round\|rounds\}/);
  refuses(withGoal('Two players', 'At the next {rules.teamSize} Upkeeps, two players'), /a count takes its noun: \{rules\.teamSize\|Upkeep\|Upkeeps\}/);
  for (const written of ['{rules.teamSize} **rounds**', '{rules.teamSize} more rounds', '{rules.teamSize} additional rounds', '{rules.teamSize} consecutive rounds', '{rules.teamSize}{?rules.teamSize > 1} rounds', '{rules.teamSize} *more* rounds']) {
    refuses(withGoal('Two players', `For ${written}, two players`), /a count takes its noun: \{rules\.teamSize\|round\|rounds\}/);
  }
  refuses(withGoal('Two players', 'Every {rules.teamSize|round} two players'), /"rules\.teamSize\|round": a count names its singular and its plural/);
  assert.deepEqual(errorsOf(withGoal('| {rules.criticalMultiplier} |', '| {rules.energyPerRound|round|rounds} |')), [], 'a count in a table cell keeps its bars');
});

test('a capitalised name inside a sentence is a spell\'s or a mutation\'s name in data/, or a word the book may capitalise', () => {
  const spells = new Map([['spell:pummel:v1', { id: 'spell:pummel:v1', name: 'Pummel', energyCost: 1, effects: [{ kind: 'Damage', amount: 2 }] }], bloodPrice]);
  const mutations = new Map([['tier:titan:v1', { id: 'tier:titan:v1', name: 'Titan', level: 4, initiativeBonus: 0 }]]);
  const book = text => inputs({ spells, mutations, parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', text) }] });
  assert.deepEqual(errorsOf(book('A Titan casts Blood Price and Pummel at Upkeep for Energy. Every creature. Two players')), []);
  assert.deepEqual(errorsOf(book('A creature casts Blood Price{?spell:hateful_sacrifice:v1.origin = \'Enemy\'} on an enemy. Two players')), [], 'a fact against a name');
  refuses(book('A Titan casts Fireball. Two players'), /rules\/01-the-goal\.md:\d+: "Fireball" is not the name of a spell or a mutation in data\/, nor a word of rules\/words\.json/);
  refuses(book('A creature casts Blood Prize. Two players'), /"Prize" is not the name/);
  refuses(book('A spell. Fireball deals damage. Two players'), /"Fireball" is not the name/);
  assert.deepEqual(errorsOf(book('A creature. Every creature. Two players')), [], 'a first word the book writes in lowercase elsewhere');
  const opening = text => inputs({ words: { en: ['Health', 'Energy', 'Upkeep', 'What', 'Two', 'Defeat', 'Combat', 'See', 'Value', 'Reference', 'Critical'], firstWords: { en: ['Whereas'] } },
    parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', text) }] });
  assert.deepEqual(errorsOf(opening('Whereas a creature waits. Two players')), [], 'a first word of firstWords opens a sentence');
  refuses(opening('A creature, Whereas it waits. Two players'), /"Whereas" is not the name/);
  for (const opening of ['A spell: Fireball deals damage.', 'A spell; Fireball deals damage.', 'A spell — Fireball deals damage.', 'A spell, "Fireball", deals damage.', 'A spell, «Fireball», deals damage.']) {
    refuses(book(`${opening} Two players`), /"Fireball" is not the name/);
  }
  const renamed = new Map([['spell:wait:v1', { id: 'spell:wait:v1', name: 'Rest', effects: [] }]]);
  refuses(inputs({ spells: renamed, terminology: `${terminology}\n| Spells | Spell | **Strike** and **Focus** | **Strike** et **Focus** |\n`,
    parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', 'A creature casts Focus. Two players').replace(pummelLine, 'Two.') }] }), /"Focus" is not the name/);
  refuses(inputs({ spells: new Map([['spell:pummel:v1', { id: 'spell:pummel:v1', name: 'Thump', energyCost: 1, effects: [{ kind: 'Damage', amount: 2 }] }]]),
    parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', 'A creature casts Pummel. Two players') }] }), /"Pummel" is not the name/);
  refuses(withFrench('Deux joueurs', 'Une créature lance Fireball. Deux joueurs'), /rules\/fr\/01-the-goal\.md:\d+: "Fireball" is not the name/);
  assert.deepEqual(errorsOf(withFrench('Deux joueurs', 'Au Stade III, l’Énergie. Deux joueurs')), [], 'the French player\'s words, an elided article and a Stage numeral');
});

test('a mutation\'s values are drawn from its file: its name, stage, initiative bonus and passive', () => {
  const mutations = new Map([['tier:titan:v1', { id: 'tier:titan:v1', name: 'Titan', level: 4, initiativeBonus: 0, passive: { sunder: 10 } }],
    ['tier:brute:v1', { id: 'tier:brute:v1', name: 'Brute', level: 1, initiativeBonus: 1 }]]);
  const book = text => inputs({ mutations, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', text) }] });
  const clean = book('{tier:brute:v1.name} adds {tier:brute:v1.initiativeBonus}; {tier:titan:v1.name}, Stage {tier:titan:v1.level}, ignores {tier:titan:v1.sunder} Defense.');
  assert.deepEqual(errorsOf(clean), []);
  const built = buildRules(clean).book;
  assert.ok(JSON.stringify(built).includes('Brute adds 1; Titan, Stage 4, ignores 10 Defense.'));
  assert.deepEqual(built.drawnFrom.mutations, ['tier:brute:v1', 'tier:titan:v1']);
  refuses(book('{tier:brute:v1.sunder}'), /"tier:brute:v1\.sunder" does not resolve/);
  refuses(book('{tier:apex:v1.name}'), /mutation "tier:apex:v1" is not in data\//);
});

// #805: the spells a mutation teaches and the reference creature knows are lists; a fact states what one holds.
test('a fact states a spell a mutation teaches or the reference creature knows, read from data/', () => {
  const spells = new Map(['pummel', 'guard', 'heavy_strike', 'wait', 'shock'].map(name => [`spell:${name}:v1`, { id: `spell:${name}:v1`, name, energyCost: 1, effects: [] }]));
  const mutations = new Map([['tier:brute:v1', { id: 'tier:brute:v1', name: 'Brute', level: 1, initiativeBonus: 1, spells: ['spell:pummel:v1', 'spell:guard:v1'] }]]);
  const creature = { id: 'creature:main:v1', baseHealth: 30, baseDefense: 0, baseInitiative: 5, startingSpellIds: ['spell:heavy_strike', 'spell:wait:v1'] };
  const aliases = { 'spell:heavy_strike': 'spell:heavy_strike:v1' };
  const book = text => inputs({ spells, mutations, creature, aliases, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', text) }] });
  const clean = book("Brute{?tier:brute:v1.teaches = 'spell:pummel:v1'}{?tier:brute:v1.teaches = 'spell:guard:v1'}{?tier:brute:v1.teaches != 'spell:shock:v1'}; every creature knows{?creature.knows = 'spell:heavy_strike:v1'}{?creature.knows = 'spell:wait:v1'} two.");
  assert.deepEqual(errorsOf(clean), []);
  const built = buildRules(clean).book;
  assert.deepEqual(built.drawnFrom.mutations, ['tier:brute:v1']);
  assert.deepEqual(built.drawnFrom.spells, ['spell:guard:v1', 'spell:heavy_strike:v1', 'spell:pummel:v1', 'spell:shock:v1', 'spell:wait:v1']);
  refuses(book("Brute{?tier:brute:v1.teaches = 'spell:shock:v1'}."), /the fact "tier:brute:v1\.teaches = 'spell:shock:v1'" is false/);
  refuses(book("Every creature knows{?creature.knows = 'spell:pummel:v1'}."), /the fact "creature\.knows = 'spell:pummel:v1'" is false/);
  refuses(book("Brute{?tier:brute:v1.teaches = 'spell:fireball:v1'}."), /spell "spell:fireball:v1" is not in data\//);
  refuses(book('Brute teaches {tier:brute:v1.teaches}.'), /"tier:brute:v1\.teaches" is a list of spells: state one it holds with \{\?tier:brute:v1\.teaches = 'spell:\.\.\.'\}/);
  refuses(book('Brute{?tier:brute:v1.teaches > 1}.'), /is not a value/);
  // A sentence that names the whole list states its count too, so a spell added to it is caught.
  assert.deepEqual(errorsOf(book('Brute teaches two{?tier:brute:v1.teaches.count = 2}; a creature knows two{?creature.knows.count = 2}.')), []);
  refuses(book('Brute teaches three{?tier:brute:v1.teaches.count = 3}.'), /the fact "tier:brute:v1\.teaches\.count = 3" is false/);
  assert.ok(JSON.stringify(buildRules(book('A creature knows {creature.knows.count|spell|spells}.')).book).includes('A creature knows 2 spells.'));
  const aliased = new Map([['tier:brute:v1', { ...mutations.get('tier:brute:v1'), spells: ['spell:heavy_strike', 'spell:guard:v1'] }]]);
  assert.deepEqual(errorsOf(inputs({ spells, mutations: aliased, creature, aliases, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', "Brute{?tier:brute:v1.teaches = 'spell:heavy_strike:v1'}.") }] })), [], 'a mutation\'s spells resolve through the aliases too');
  refuses(inputs({ spells, creature: { ...creature, startingSpellIds: ['spell:strike'] }, parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', "Every creature knows{?creature.knows = 'spell:wait:v1'}. Two players") }] }),
    /starting spell "spell:strike" is neither a versioned spell id nor an alias of data\/aliases\.json/);
});

// #879: the mutations a mutation needs are a list too, read from its prerequisites.
test('a fact states a mutation another one needs, read from its prerequisites in data/', () => {
  const mutations = new Map([
    ['tier:brute:v1', { id: 'tier:brute:v1', name: 'Brute', level: 1, initiativeBonus: 1, prerequisites: [] }],
    ['tier:marauder:v1', { id: 'tier:marauder:v1', name: 'Oppressor', level: 2, initiativeBonus: 2, prerequisites: ['tier:brute'] }],
    ['tier:titan:v1', { id: 'tier:titan:v1', name: 'Titan', level: 4, initiativeBonus: 0, prerequisites: [], anyOf: ['tier:marauder:v1'] }],
  ]);
  const aliases = { 'tier:brute': 'tier:brute:v1' };
  const book = text => inputs({ mutations, aliases, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', text) }] });
  const clean = book("Oppressor needs Brute{?tier:marauder:v1.needs = 'tier:brute:v1'}{?tier:marauder:v1.needs.count = 1}; Brute needs nothing{?tier:brute:v1.needs.count = 0}.");
  assert.deepEqual(errorsOf(clean), []);
  assert.deepEqual(buildRules(clean).book.drawnFrom.mutations, ['tier:brute:v1', 'tier:marauder:v1']);
  // A capstone needs any one of its anyOf, not its empty prerequisites: "Titan needs nothing" would be false.
  refuses(book('Titan needs nothing{?tier:titan:v1.needs.count = 0}.'), /"tier:titan:v1\.needs": Titan needs any one of its anyOf; \.needs reads prerequisites only/);
  refuses(book("Oppressor needs Titan{?tier:marauder:v1.needs = 'tier:titan:v1'}."), /the fact "tier:marauder:v1\.needs = 'tier:titan:v1'" is false/);
  refuses(book("Oppressor{?tier:marauder:v1.needs != 'tier:apex:v1'}."), /mutation "tier:apex:v1" is not in data\//);
  refuses(book('Oppressor needs {tier:marauder:v1.needs}.'), /"tier:marauder:v1\.needs" is a list of mutations: state one it holds with \{\?tier:marauder:v1\.needs = 'tier:\.\.\.'\}/);
  refuses(inputs({ mutations: new Map([['tier:marauder:v1', { ...mutations.get('tier:marauder:v1'), prerequisites: ['tier:brut'] }]]), parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', "Oppressor{?tier:marauder:v1.needs.count = 1}. Two players") }] }),
    /prerequisite of tier:marauder:v1 "tier:brut" is neither a versioned mutation id nor an alias of data\/aliases\.json/);
});

// #877: a paragraph or a list item joins its lines; a value that fails names its own line, not the first.
test('a value that fails names the line that holds it, inside a paragraph or a list item', () => {
  const lines = goal.replace('Two players, each with {rules.teamSize} creatures.', "Two players,\neach with {rules.teamSize} creatures{?rules.teamSize = 4}.\nThen {?rules.teamSize = 5}more.")
    .replace('- Upkeep: every creature gains {rules.energyPerRound} Energy.', '- Upkeep: every creature\n  gains {rules.energyPerRound} Energy{?rules.energyPerRound = 9}.\n- Command{?rules.teamSize = 6}: they plan.');
  const errors = errorsOf(inputs({ parts: [{ file: '01-the-goal.md', text: lines }], words: { ...inputs().words, en: [...inputs().words.en, 'Then', 'Command'] } }));
  const at = fact => errors.find(error => error.includes(fact))?.split(': ')[0];
  assert.equal(at('rules.teamSize = 4'), 'rules/01-the-goal.md:4');
  assert.equal(at('rules.teamSize = 5'), 'rules/01-the-goal.md:5');
  assert.equal(at('rules.energyPerRound = 9'), 'rules/01-the-goal.md:23');
  assert.equal(at('rules.teamSize = 6'), 'rules/01-the-goal.md:24');
});

// #884: an unknown capitalised word, a retired word and a count without its noun name their own line too.
test('a word the book refuses names the line that holds it, inside a paragraph or a list item', () => {
  const lines = goal.replace('Two players, each with {rules.teamSize} creatures.', 'Two players,\neach with {rules.teamSize} **creatures** and a [Fireball](#winning).\nThey buy a Package for {rules.teamSize} rounds.')
    .replace('- Upkeep: every creature gains {rules.energyPerRound} Energy.', '- Upkeep: every creature\n  gains {rules.energyPerRound} Energy and a Firebolt.');
  const errors = errorsOf(inputs({ parts: [{ file: '01-the-goal.md', text: lines }] }));
  const at = word => errors.filter(error => error.includes(word)).map(error => error.split(': ')[0]);
  assert.deepEqual(at('"Fireball"'), ['rules/01-the-goal.md:4']);
  assert.deepEqual(at('"Package": the player'), ['rules/01-the-goal.md:5']);
  assert.deepEqual(at('a count takes its noun'), ['rules/01-the-goal.md:5']);
  assert.deepEqual(at('"Firebolt"'), ['rules/01-the-goal.md:23']);
});

test('refuses an engine word where the player\'s word exists, from the terminology page', () => {
  refuses(withGoal('Two players', 'Two players buy a Package'), /"Package": the player's word is Mutation/);
  refuses(withGoal('Upkeep: every', 'StartOfRound: every'), /"StartOfRound": the player's word is Upkeep/);
  refuses(withGoal('*at its slot*', '*at its Quick slot*'), /"Quick": the player's word is Instinct/);
  refuses(withGoal('## A round {#a-round}', '## A Speed {#a-round}'), /"Speed": the player's word is Tempo/);
  refuses(withGoal('title: Health', 'title: Tier'), /"Tier": the player's word is Stage/);
  refuses(withGoal('Two players', 'Choose speed for each creature. Two players'), /"Choose speed": the player's word is Choose tempo/);
  assert.deepEqual(errorsOf(withGoal('Two players', 'Two quick players at Evolution, at the start')), [], 'an everyday word, the player\'s own word and an ordinary Start are not refused');
});

test('refuses a sources line naming an ADR or a heading that does not exist, and a section without sources', () => {
  refuses(withGoal('sources: ADR 0010', 'sources: ADR 0999'), /ADR 0999 does not exist/);
  refuses(withGoal('game-rules#match-lifecycle', 'game-rules#nowhere'), /game-rules#nowhere is not a heading of docs\/domain\/game-rules\.md/);
  refuses(withGoal('sources: ADR 0010', 'sources: the engine'), /"the engine" is not an ADR or a game-rules heading/);
  refuses(withGoal('\nsources: ADR 0010\n', '\n'), /section "a-round" has no sources line/);
  refuses(withGoal('sources: ADR 0010\n', 'sources: ADR 0010\n\nOne more line.\n'), /section "a-round": nothing follows its sources line but its card/);
  refuses(withGoal('sources: ADR 0011, game-rules#match-lifecycle\n', ''), /section "winning": its card follows its sources line/);
});

test('refuses a link to the rules, anywhere in the pages, to an id that is not a section', () => {
  refuses(inputs({ links: [{ file: 'battle/src/tutorial.ts', id: 'tempo' }] }), /battle\/src\/tutorial\.ts links to \/rules#tempo, which is not a section/);
  assert.deepEqual(errorsOf(inputs({ links: [{ file: 'table/x.js', id: 'a-round' }, { file: 'table/x.js', id: 'the-goal' }] })), []);
  refuses(withGoal('[winning](#winning)', '[winning](#losing)'), /link to #losing, which is not a section/);
});

test('refuses a concept card whose id is not one ConceptCards.Ids lists, a card used twice, and a card out of shape', () => {
  refuses(withGoal('id: health', 'id: stamina'), /card "stamina" is not in ConceptCards\.Ids/);
  const twice = goal.replace('sources: ADR 0010', 'sources: ADR 0010\n\n```card\nid: health\ntitle: Health\nOne line.\n> One example.\n```');
  refuses(inputs({ parts: [{ file: '01-the-goal.md', text: twice }] }), /card "health" is in two sections/);
  refuses(withGoal('> A creature starts', 'A creature starts'), /card "health" has no example/);
  refuses(withGoal('At zero Health a creature is defeated.', 'One.\nTwo.\nThree.\nFour.'), /card "health" has 4 lines/);
  refuses(withGoal('At zero Health a creature is defeated.', 'At zero Health a creature is **defeated**.'), /a card is plain text, with no markup/);
  assert.deepEqual(errorsOf(withGoal('{max(0, creature.health - 7)}', '{floor(creature.health * 2)}')), []);
});

test('refuses Markdown outside the subset: code, HTML, deeper headings, quotes, images and links out of the book', () => {
  refuses(withGoal('Two players', 'Two `players`'), /code/);
  refuses(withGoal('Two players', 'Two <b>players</b>'), /HTML/);
  refuses(withGoal('## A round {#a-round}', '### A round {#a-round}'), /heading of level 3/);
  refuses(withGoal('Two players', '> Two players'), /quote/);
  refuses(withGoal('See [winning](#winning)', 'See ![x](y.png)'), /image/);
  refuses(withGoal('(#winning)', '(../docs/x.md)'), /link to "\.\.\/docs\/x\.md"/);
  refuses(withGoal('{rules.teamSize}', '{ oops'), /brace/);
});

test('refuses Markdown that would pass for a paragraph: other list markers, breaks, setext and unspaced headings', () => {
  for (const line of ['* one', '+ one', '---', '##Title', '***']) refuses(withGoal('Two players', `${line}\n\nTwo players`), /is outside the subset/);
  refuses(withGoal('Two players, each', 'Title\n=====\n\nTwo players, each'), /"=====" is outside the subset/);
  refuses(withGoal('[winning](#winning)', '[winning](#winning "title")'), /markup the subset does not read/);
  refuses(withGoal('**Defeat**', '**Defeat'), /markup the subset does not read/);
  refuses(withGoal('title: Health', 'title: **Health**'), /a card is plain text/);
});

test('a wrapped line that starts with a number other than 1 stays in its paragraph', () => {
  const { book, errors } = buildRules(withGoal('Two players, each with', 'Two players, each\n2. with'));
  assert.deepEqual(errors, []);
  assert.deepEqual(book.parts[0].blocks.map(block => block.type), ['paragraph']);
});

test('the changes are dated lines, each with its ADR when it has one', () => {
  const given = inputs({ changes: '# What changed\n\n- 2026-11-20: Tempo is chosen at the slot (ADR 0112).\n- 2026-11-21: A typo.\n' });
  assert.deepEqual(buildRules({ ...given, adrs: new Set(['0010', '0011', '0112']) }).book.changes, [
    { date: '2026-11-20', text: 'Tempo is chosen at the slot (ADR 0112).', adrs: ['0112'] },
    { date: '2026-11-21', text: 'A typo.', adrs: [] },
  ]);
  refuses(given, /changes\.md: ADR 0112 does not exist/);
  refuses(inputs({ changes: '- yesterday: something\n' }), /changes\.md: "- yesterday: something" is not "- YYYY-MM-DD: what changed"/);
});

test('the book says what it was drawn from', () => {
  const { book } = buildRules(inputs());
  assert.deepEqual(book.drawnFrom, { ruleSet: 'docs/tabletop/playtest.rules.json', rules: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 2 }, engine: { defenseBuffCeiling: 10 }, creature: 'creature:main:v1', spells: ['spell:pummel:v1'], mutations: [] });
});

const but = `# Ce que vous cherchez à faire {#the-goal}

Deux joueurs, chacun avec {rules.teamSize} créatures.

## Gagner {#winning}

Une créature commence à {creature.health} Santé. **Battez** toutes les créatures de l’autre équipe.

sources: ADR 0011, game-rules#match-lifecycle

\`\`\`card
id: health
title: Santé
À zéro Santé, une créature est vaincue.
> Une créature commence à {creature.health} Santé. Un coup de 7 la laisse à {max(0, creature.health - 7)}.
\`\`\`

## Un round {#a-round}

- Entretien : chaque créature gagne {rules.energyPerRound} Énergie.
- Combat : *à son tour*, une créature lance un sort. Voir [gagner](#winning).

| Valeur | Référence |
| --- | --- |
| Multiplicateur critique | {rules.criticalMultiplier} |

Pummel coûte {spell:pummel:v1.energyCost} et inflige {spell:pummel:v1.damage}.

sources: ADR 0010
`;

/** The small book with its French edition; each French test changes one thing in the French. */
const withFrench = (from = '', to = '', overrides = {}) => inputs({ french: { parts: [{ file: '01-the-goal.md', text: but.replace(from, to) }], changes: null }, ...overrides });

test('the French edition builds beside the English: the same ids, numbers, sources and cards, in French', () => {
  const { book, french, errors } = buildRules(withFrench());
  assert.deepEqual(errors, []);
  assert.equal(book.language, 'en');
  assert.equal(french.language, 'fr');
  assert.deepEqual(french.parts[0].sections.map(section => [section.id, section.number, section.title]), [['winning', '1.1', 'Gagner'], ['a-round', '1.2', 'Un round']]);
  assert.deepEqual(french.parts[0].sections[0].card, { id: 'health', title: 'Santé', lines: ['À zéro Santé, une créature est vaincue.'], example: 'Une créature commence à 30 Santé. Un coup de 7 la laisse à 23.' });
  assert.deepEqual(french.cards, book.cards);
  assert.deepEqual(french.drawnFrom, book.drawnFrom);
  assert.equal(buildRules(inputs()).french, null, 'no rules/fr, no French book');
});

test('the French edition writes a decimal the French way', () => {
  const { french } = buildRules(withFrench('', '', { ruleSet: { path: 'x', values: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 1.5 } } }));
  assert.ok(JSON.stringify(french).includes('"1,5"'));
});

test('refuses a French part or section missing, added, renamed or out of the English order', () => {
  refuses(inputs({ french: { parts: [], changes: null } }), /rules\/fr\/01-the-goal\.md is missing; the French edition has every part/);
  refuses(inputs({ french: { parts: [{ file: '01-the-goal.md', text: but }, { file: '02-more.md', text: '# Plus {#more}\n' }], changes: null } }), /rules\/fr\/02-more\.md has no English part/);
  refuses(withFrench('{#the-goal}', '{#le-but}'), /the part's id is "le-but"; the English part's is "the-goal"/);
  refuses(withFrench('{#a-round}', '{#un-round}'), /section "a-round" is missing/);
  refuses(withFrench('{#a-round}', '{#un-round}'), /section "un-round" is not in the English edition/);
  const swapped = but.replace(/## Gagner[\s\S]*?(?=## Un round)/, '') + but.match(/## Gagner[\s\S]*?(?=## Un round)/)[0];
  refuses(inputs({ french: { parts: [{ file: '01-the-goal.md', text: swapped }], changes: null } }), /the sections are not in the English order/);
});

test('refuses a French section citing other sources or carrying another card than the English', () => {
  refuses(withFrench('sources: ADR 0010', 'sources: ADR 0011'), /section "a-round" cites ADR 0011; the English cites ADR 0010/);
  refuses(withFrench('```card\nid: health\ntitle: Santé\nÀ zéro Santé, une créature est vaincue.\n> Une créature commence à {creature.health} Santé. Un coup de 7 la laisse à {max(0, creature.health - 7)}.\n```\n', ''), /section "winning" carries card none; the English carries health/);
});

test('refuses in French an English player\'s word the French replaced, and the engine\'s words as in English', () => {
  refuses(withFrench('- Entretien :', '- Upkeep :'), /rules\/fr\/01-the-goal\.md:\d+: "Upkeep": the player's word is Entretien/);
  refuses(withFrench('Un round', 'Un Stage'), /"Stage": the player's word is Stade/);
  refuses(withFrench('Deux joueurs', 'Deux joueurs prennent un Tier'), /"Tier": the player's word is Stade/);
  assert.deepEqual(errorsOf(withFrench('Deux joueurs', 'Deux joueurs en Évolution, en Précision, avec un Tempo')), []);
});

test('refuses a French section that types a number the English draws, or drops a value', () => {
  refuses(withFrench('{creature.health} Santé. **Battez**', '30 Santé. **Battez**'), /section "winning" draws creature\.health, max\(0,creature\.health-7\); the English draws creature\.health, creature\.health, max/);
  assert.deepEqual(errorsOf(withFrench('chaque créature gagne {rules.energyPerRound} Énergie', 'l’Énergie de chaque créature augmente de {rules.energyPerRound}')), [], 'a value may move in the sentence');
});

test('a French count takes the French noun, singular for zero and one, and the values compare without their nouns', () => {
  const given = interval => withFrench('Deux joueurs', 'Tous les {rules.evolutionInterval|tour|tours}, deux joueurs', {
    ruleSet: { path: 'x', values: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 2, evolutionInterval: interval } },
    parts: [{ file: '01-the-goal.md', text: goal.replace('Two players', 'Every {rules.evolutionInterval|round|rounds}, two players') }] });
  assert.deepEqual(errorsOf(given(2)), []);
  assert.ok(JSON.stringify(buildRules(given(2)).french).includes('Tous les 2 tours, deux joueurs'));
  assert.ok(JSON.stringify(buildRules(given(1)).french).includes('Tous les 1 tour, deux joueurs'));
  refuses(withFrench('Deux joueurs', 'Pendant {rules.teamSize} tours, deux joueurs'), /a count takes its noun: \{rules\.teamSize\|tour\|tours\}/);
  refuses(withFrench('Deux joueurs', 'Pendant {rules.teamSize} autres tours, deux joueurs'), /a count takes its noun: \{rules\.teamSize\|tour\|tours\}/);
});

test('refuses an engine word in French only as written, for "tiers" and "stage" are French words', () => {
  assert.deepEqual(errorsOf(withFrench('Deux joueurs', 'Un tiers des joueurs, en stage,')), []);
  refuses(withFrench('Deux joueurs', 'Un Tier des joueurs'), /"Tier": the player's word is Stade/);
});

test('refuses a French link to an id that is not a section, and the French values as the English ones', () => {
  refuses(withFrench('(#winning)', '(#gagner)'), /rules\/fr\/01-the-goal\.md:\d+: a link to #gagner, which is not a section/);
  refuses(withFrench('{rules.teamSize}', '{rules.taille}'), /rules\/fr\/01-the-goal\.md:\d+: "rules\.taille" does not resolve/);
});

test('the French changes are the English lines translated, one for one, with their dates and ADRs', () => {
  const english = '# What changed\n\n- 2026-11-20: Tempo is chosen at the slot (ADR 0010).\n- 2026-11-21: A typo.\n';
  const given = changes => withFrench('', '', { changes: english, french: { parts: [{ file: '01-the-goal.md', text: but }], changes } });
  const { french, errors } = buildRules(given('# Ce qui a changé\n\n- 2026-11-20: Le tempo se choisit au tour (ADR 0010).\n- 2026-11-21: Une coquille.\n'));
  assert.deepEqual(errors, []);
  assert.deepEqual(french.changes[0], { date: '2026-11-20', text: 'Le tempo se choisit au tour (ADR 0010).', adrs: ['0010'] });
  refuses(given('- 2026-11-20: Le tempo se choisit au tour (ADR 0010).\n'), /rules\/fr\/changes\.md has 1 lines; rules\/changes\.md has 2/);
  refuses(given('- 2026-11-20: Le tempo (ADR 0011).\n- 2026-11-21: Une coquille.\n'), /rules\/fr\/changes\.md, line 1: 2026-11-20 \(0011\); the English line is 2026-11-20 \(0010\)/);
  refuses(given('- hier: Une coquille.\n- 2026-11-21: Une coquille.\n'), /rules\/fr\/changes\.md: "- hier: Une coquille\." is not/);
});

test('the repository\'s book builds with no error', () => {
  assert.deepEqual(buildRules(readInputs(root)).errors, []);
});

test('table/rules.json is the build of rules/ (UPDATE_INDEXES=1 rewrites it)', () => {
  const { book, errors } = buildRules(readInputs(root));
  assert.deepEqual(errors, []);
  const built = render(book);
  if (process.env.UPDATE_INDEXES === '1') writeFileSync(committed, built);
  assert.equal(readFileSync(committed, 'utf8'), built, 'run: node scripts/rules/build.mjs');
});

test('table/rules.fr.json is the build of rules/fr/ (UPDATE_INDEXES=1 rewrites it)', () => {
  const { french, errors } = buildRules(readInputs(root));
  assert.deepEqual(errors, []);
  assert.ok(french, 'rules/fr/ holds the French edition');
  const built = render(french);
  if (process.env.UPDATE_INDEXES === '1') writeFileSync(committedFrench, built);
  assert.equal(readFileSync(committedFrench, 'utf8'), built, 'run: node scripts/rules/build.mjs');
});

test('the repository\'s book draws its values from the hosted rule set and the reference creature', () => {
  const book = JSON.parse(readFileSync(committed, 'utf8'));
  const rules = JSON.parse(readFileSync(join(root, 'docs/tabletop/playtest.rules.json'), 'utf8'));
  const creature = JSON.parse(readFileSync(join(root, 'data/Creatures/main.v1.json'), 'utf8'));
  assert.deepEqual(book.drawnFrom.rules, rules);
  assert.equal(book.drawnFrom.ruleSet, 'docs/tabletop/playtest.rules.json');
  assert.equal(book.drawnFrom.creature, creature.id);
  const health = book.parts[0].sections.find(section => section.card?.id === 'health');
  assert.equal(health.card.example, `A creature starts at ${creature.baseHealth} Health. A hit of 7 leaves it at ${Math.max(0, creature.baseHealth - Math.max(0, 7 - creature.baseDefense))}.`);
});

test('a link to the rules is /rules#<id>, wherever it is written; the pages\' tests are not read', () => {
  assert.deepEqual(linksIn('<a href="/rules#tempo">Tempo</a>, `/rules#a-round`, "rules#nothing", /rules, /docs/rules#x'), ['tempo', 'a-round']);
  assert.deepEqual(linksIn('href="https://table.example/rules#winning" and /rules/#a-round'), ['winning', 'a-round']);
  assert.deepEqual(['table/x.js', 'battle/src/tutorial.ts', 'scenarios/tutorials/a.json', 'table/x.test.js', 'battle/src/x.spec.ts', 'table/rules.json', 'table/rules.fr.json', 'table/logo.png'].filter(linksFrom),
    ['table/x.js', 'battle/src/tutorial.ts', 'scenarios/tutorials/a.json']);
  const { conceptIds, engine } = readInputs(root);
  assert.ok(conceptIds.includes('health') && conceptIds.includes('tempo'));
  const ceiling = /DefenseBuffCeiling = (\d+);/.exec(readFileSync(join(root, 'src/DownfallArena.Domain/Matches/Creatures/Creature.cs'), 'utf8'))[1];
  assert.deepEqual(engine, { defenseBuffCeiling: Number(ceiling) }, 'the engine\'s values are read from the domain\'s source');
});
