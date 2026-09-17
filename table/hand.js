// The hand, and the backs beside it.
//
// A Creature's known Spells are public in the engine -- both teams' snapshots carry `knownSpells` -- and what
// is hidden is which one it has face down (components.md §3.7). So the hand is every Spell this seat's
// Creatures know, drawn as cards, with the castable ones marked from the Intent options: a player has to be
// able to read a card they cannot cast, because "why is this one greyed out" is the question the board answers
// and a rule the page must never answer itself (playtest-app.md §3.1).

// One row per allied Creature: what it knows, which of those it could cast right now, and whether it has
// already declared. `intent` is the Intent section of the options, absent outside intent selection -- and when
// it is absent nothing is castable, which is the truth rather than a fallback.
export function handRows(allies, intent, intents) {
  const castable = new Map((intent?.creatures ?? []).map(one => [one.creature, new Set(one.castableSpells ?? [])]));
  const declared = declaredBy(intents);
  return (allies ?? []).map(creature => ({
    creature: creature?.id,
    declared: declared.has(creature?.id),
    spells: (creature?.knownSpells ?? []).map(spell => ({
      spell,
      castable: castable.get(creature?.id)?.has(spell) === true,
    })),
  }));
}

// The Creatures that have something face down, from the seat's own intents.
export function declaredBy(intents) {
  return new Set((intents ?? []).map(intent => intent?.actor));
}

// This seat's own backs, which it may read: the Creature and the card it put there.
export function backText(intent, cards) {
  const spell = cards?.get?.(intent?.spell)?.name ?? intent?.spell ?? '';
  return `${intent?.actor ?? ''}: ${spell}`;
}
