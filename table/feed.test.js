import { test } from 'node:test';
import assert from 'node:assert/strict';
import { feedLine, outcomeText, resolutionText } from './feed.js';

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

test('a resolution that was not critical does not say it was', () => {
  assert.equal(resolutionText(resolved, cards), '4: Throwing Star · Damage 3 on 1');
  assert.equal(feedLine({ round: 1, subPhase: 'ActionResolution', event: resolved }, cards),
    '1 · ActionResolution · CombatActionResolved — 4: Throwing Star · Damage 3 on 1');
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

  assert.equal(resolutionText(dropped, cards), '4: Throwing Star · Damage 3 on 1 · dropped 2 (Creature 2 is dead.)');
});

test('a global targeting failure has no creature to name', () => {
  const global = {
    ...resolved,
    resolution: { ...resolved.resolution, droppedTargets: [{ target: null, error: { message: 'Nothing was left.' } }] },
  };

  assert.equal(resolutionText(global, cards), '4: Throwing Star · Damage 3 on 1 · dropped Nothing was left.');
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

  assert.equal(resolutionText(unknown, new Map()), '1: spell:x:v1 · Damage 3 on 1');
});
