// The official rulebook (ADR 0188, docs/tabletop/rulebook-plan.md R1): `rules/` is built into `table/rules.json` by
// scripts/rules/build.mjs. Each refusal of the build has its test on a small book written here; the committed file
// is the build of the repository's book (UPDATE_INDEXES=1 rewrites it, as for ADR 0184's indexes); and its values
// are the hosted rule set's and the reference creature's.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildRules, evaluate, idsOf, linksFrom, linksIn, readInputs, render } from '../scripts/rules/build.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const committed = join(root, 'table/rules.json');

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
    spells: new Map([['spell:pummel:v1', { id: 'spell:pummel:v1', energyCost: 2, effects: [{ kind: 'Damage', amount: 5 }] }]]),
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
    ['spell:latch:v1', { id: 'spell:latch:v1', effects: [{ kind: 'Bleed', amountPerRound: 2, durationRounds: 3 }] }]]);
  const book = text => inputs({ spells, parts: [{ file: '01-the-goal.md', text: goal.replace('Pummel costs {spell:pummel:v1.energyCost} and deals {spell:pummel:v1.damage}.', text) }] });
  assert.deepEqual(errorsOf(book('Latch bleeds {spell:latch:v1.bleed} for {spell:latch:v1.bleed.durationRounds} rounds.')), []);
  assert.ok(JSON.stringify(buildRules(book('Latch bleeds {spell:latch:v1.bleed} for {spell:latch:v1.bleed.durationRounds} rounds.')).book).includes('Latch bleeds 2 for 3 rounds.'));
  refuses(book('Guard gives {spell:guard:v1.defenseBuff}.'), /"spell:guard:v1\.defenseBuff" names one of two DefenseBuff effects/);
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
  assert.deepEqual(book.drawnFrom, { ruleSet: 'docs/tabletop/playtest.rules.json', rules: { teamSize: 3, energyPerRound: 2, criticalMultiplier: 2 }, creature: 'creature:main:v1', spells: ['spell:pummel:v1'], mutations: [] });
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
  assert.deepEqual(['table/x.js', 'battle/src/tutorial.ts', 'scenarios/tutorials/a.json', 'table/x.test.js', 'battle/src/x.spec.ts', 'table/rules.json', 'table/logo.png'].filter(linksFrom),
    ['table/x.js', 'battle/src/tutorial.ts', 'scenarios/tutorials/a.json']);
  const { conceptIds } = readInputs(root);
  assert.ok(conceptIds.includes('health') && conceptIds.includes('tempo'));
});
