import { test } from 'node:test';
import assert from 'node:assert/strict';
import { accumulate, feedLine, nextSince, outcomeText, resolutionText } from './feed.js';

const cards = new Map([['spell:throwing_star:v1', { name: 'Throwing Star' }]]);

const resolved = {
  kind: 'CombatActionResolved',
  resolution: {
    action: { actor: 4, spell: 'spell:throwing_star:v1', targets: [1] },
    fizzled: false,
    fizzleReason: null,
    droppedTargets: [],
    isCritical: false,
  },
  appliedOutcomes: [{ kind: 'DamageOutcome', amount: 3, critical: false, target: 1 }],
};

// Every other event is its kind and nothing more: those are the engine's words and the page has never heard of
// any of them.
test('an ordinary event is its round, its sub-phase and its kind', () => {
  const entry = { round: 2, subPhase: 'Cleanup', event: { kind: 'ConditionsExpired' } };

  assert.equal(feedLine(entry, cards), '2 · Cleanup · ConditionsExpired');
  assert.equal(feedLine({ event: { kind: 'RoundStarted' } }, cards), '— ·  · RoundStarted');
  assert.equal(feedLine(undefined, cards), '— ·  · ');
});

// The board's own totals cannot say whether a critical landed -- defense, caps and overkill all move the
// number one produced -- so the resolution line is where stage 4 owes the player that answer.
test('a critical is on the resolution line, because no total on the board can say', () => {
  const critical = { ...resolved, resolution: { ...resolved.resolution, isCritical: true } };

  assert.equal(resolutionText(critical, cards), '4: Throwing Star · critical · Damage 3 on 1');
});

// Said both ways. Silence would not tell a player whether the roll landed: it reads the same as a line that
// forgot to mention it, and the card they cast advertised a threshold they were watching for.
test('a resolution that was not critical says so rather than staying silent', () => {
  assert.equal(resolutionText(resolved, cards), '4: Throwing Star · no critical · Damage 3 on 1');
  assert.equal(feedLine({ round: 1, subPhase: 'ActionResolution', event: resolved }, cards),
    '1 · ActionResolution · CombatActionResolved — 4: Throwing Star · no critical · Damage 3 on 1');
});

test('a fizzle says so and says why', () => {
  const fizzled = {
    ...resolved,
    resolution: {
      ...resolved.resolution,
      fizzled: true,
      fizzleReason: { code: 'Combat.AllTargetsInvalid', message: 'Every target was invalid.' },
    },
    appliedOutcomes: [],
  };

  assert.equal(resolutionText(fizzled, cards), '4: Throwing Star · fizzled: Every target was invalid.');
});

// A fizzle never rolled, so it is the one resolution that says nothing about the die: reporting "no critical"
// there would be reporting a roll that did not happen.
test('a fizzle says nothing about the die, because no die was rolled', () => {
  const fizzled = {
    ...resolved,
    resolution: { ...resolved.resolution, fizzled: true, fizzleReason: { message: 'Nothing to hit.' } },
    appliedOutcomes: [],
  };

  assert.ok(!resolutionText(fizzled, cards).includes('critical'));
});

// A target dropped at resolution time -- dead before the cast reached it -- is why a cast did less than the
// card promised, so it is on the line rather than left to be inferred from the boards.
test('targets dropped at resolution are named with their reason', () => {
  const dropped = {
    ...resolved,
    resolution: {
      ...resolved.resolution,
      droppedTargets: [{ target: 2, error: { code: 'Combat.TargetDead', message: 'Creature 2 is dead.' } }],
    },
  };

  assert.equal(resolutionText(dropped, cards), '4: Throwing Star · no critical · Damage 3 on 1 · dropped 2 (Creature 2 is dead.)');
});

test('a global targeting failure has no creature to name', () => {
  const global = {
    ...resolved,
    resolution: { ...resolved.resolution, droppedTargets: [{ target: null, error: { message: 'Nothing was left.' } }] },
  };

  assert.equal(resolutionText(global, cards), '4: Throwing Star · no critical · Damage 3 on 1 · dropped Nothing was left.');
});

// The applied outcomes are what the board took, which can be less than the rules computed; that is the pair
// the engine keeps apart, and the feed prints the one that happened.
test('an outcome is the engine kind without its type suffix, its number and its target', () => {
  assert.equal(outcomeText({ kind: 'DamageOutcome', amount: 3, target: 1 }), 'Damage 3 on 1');
  assert.equal(outcomeText({ kind: 'HealOutcome', amount: 2, target: 4 }), 'Heal 2 on 4');
  assert.equal(outcomeText({ kind: 'ConditionOutcome', target: 5 }), 'Condition on 5');
  assert.equal(outcomeText({ kind: 'DamageOutcome', amount: 9, critical: true, target: 1 }), 'Damage 9! on 1');
  assert.equal(outcomeText({}), '');
});

test('a resolution the page has no card for still names its spell', () => {
  const unknown = { ...resolved, resolution: { ...resolved.resolution, action: { actor: 1, spell: 'spell:x:v1' } } };

  assert.equal(resolutionText(unknown, new Map()), '1: spell:x:v1 · no critical · Damage 3 on 1');
});

// The feed is the whole match's history and it only grows, so a poll that asked for all of it every 700 ms
// would serialize and download the match again each time -- quadratic over a half-hour session, for twelve
// lines on screen. The page asks for what it has not seen and keeps a bounded tail.
test('the next request asks for one past the highest entry held', () => {
  assert.equal(nextSince([{ sequence: 0 }, { sequence: 3 }, { sequence: 7 }]), 8);
  assert.equal(nextSince([{ sequence: 0 }]), 1);
  assert.equal(nextSince([]), 0);
  assert.equal(nextSince(undefined), 0);
});

// Sequence numbers have gaps by design: the entries a seat may not see are filtered out and their numbers go
// with them. So the cursor is the highest seen and never a count of what is held.
test('a gap in the sequence numbers does not move the cursor back', () => {
  assert.equal(nextSince([{ sequence: 0 }, { sequence: 5 }, { sequence: 12 }]), 13);
});

test('what arrives is appended to what was held, oldest first', () => {
  const kept = accumulate([{ sequence: 0 }, { sequence: 1 }], [{ sequence: 2 }, { sequence: 3 }], 60);

  assert.deepEqual(kept.map(entry => entry.sequence), [0, 1, 2, 3]);
});

test('an entry the page already holds is not added twice', () => {
  const kept = accumulate([{ sequence: 0 }, { sequence: 1 }], [{ sequence: 1 }, { sequence: 2 }], 60);

  assert.deepEqual(kept.map(entry => entry.sequence), [0, 1, 2]);
});

test('the kept history is bounded, and it is the newest that is kept', () => {
  const kept = accumulate([{ sequence: 0 }, { sequence: 1 }, { sequence: 2 }], [{ sequence: 3 }], 2);

  assert.deepEqual(kept.map(entry => entry.sequence), [2, 3]);
});

test('a first poll with nothing held keeps what arrives', () => {
  assert.deepEqual(accumulate(undefined, [{ sequence: 0 }], 60).map(entry => entry.sequence), [0]);
  assert.deepEqual(accumulate(undefined, undefined, 60), []);
});
