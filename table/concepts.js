// The concept cards (ADR 0187, docs/tabletop/onboarding-plan.md): what one rule word means, in three lines at
// most, with one example on the reference creature and values (Health 30, Defense 0, Base initiative 5, Energy
// per round 2, critical multiplier 2), and the rulebook section it summarises. The rulebook
// (docs/tabletop/rulebook.md) stays the source: a card is its summary in the player's words. A lesson step opens
// the cards it names by these ids, which the host holds a lesson to (`ConceptCards.Ids`).

/** @typedef {{ id: string, title: string, lines: readonly string[], example: string, rulebook: { section: string, anchor: string, label: string } }} ConceptCard */

// The cards as they are written: one block a card, blocks apart by a blank line. A block's first line is its id,
// its title, the rulebook section, that section's anchor and its heading; then its lines; then its example, after >.
const WRITTEN = `
health | Health | Part 1 | part-1-what-you-are-trying-to-do | Part 1: What you are trying to do
At zero Health a creature is defeated, for the rest of the match.
A team with no creature left loses, at once, even in the middle of a round.
> A creature starts at 30 Health. A hit of 7 leaves it at 23.

energy | Energy | 5.1 | 51-energy-gain | 5.1 Energy gain
Every living creature gains Energy at Upkeep, when a round begins.
A spell costs Energy. It is spent when the spell resolves, never before.
> A creature at 1 Energy gains 2 at Upkeep and has 3: enough for a spell that costs 3.

defense | Defense | 5.8 | 58-resolving-an-action | 5.8 Resolving an action
Defense lowers the damage of every hit a creature takes, never below zero.
Only damage over time, applied at Upkeep, ignores it.
> A hit of 7 on a creature with 2 Defense deals 5. The same hit on a creature with none deals 7.

initiative | Initiative | 5.5 | 55-turn-order-resolution | 5.5 Turn order resolution
Initiative orders the creatures inside their tempo: the highest acts first.
A creature starts with its base initiative. Each mutation adds its bonus, for the rest of the match.
A tie between the two teams is settled by a d20 roll-off.
> Two Precision creatures at 7 and 5 initiative: the one at 7 acts first.

tempo | Tempo | 5.4 | 54-tempo | 5.4 Tempo
Each round, every creature that is not stunned takes a tempo: Instinct or Precision.
Instinct acts before every Precision creature, and cannot crit this round.
Precision acts after every Instinct creature, and keeps the critical roll.
> An Instinct creature at 5 initiative acts before a Precision creature at 9.

critical | Critical | 6.7 | 67-the-critical-roll | 6.7 The critical roll
A Precision creature rolls a d20 when its spell resolves, against the number the spell prints.
On a critical, the spell's damage and healing on its targets are multiplied.
An Instinct creature never rolls.
> A spell prints d20: 11+. A roll of 14 is a critical: its 7 damage becomes 14.

intent | Intent | 5.6 | 56-intent-selection | 5.6 Intent selection
An intent is the spell a creature will cast, declared hidden before the turns begin.
It is revealed on the creature's turn. You then choose its targets, never another spell.
> You declare a spell that costs 3 on a creature with 3 Energy. Its Energy is not spent until its turn.

fizzle | Fizzle | 6.1 | 61-the-fizzle-and-every-cause-of-it | 6.1 The Fizzle, and every cause of it
A fizzle is a cast that does nothing, and costs nothing.
It happens when the caster is defeated or stunned before its turn, can no longer pay for its spell, or has no legal target.
> A creature declared a spell that costs 3, then was drained to 0 Energy before its turn: the spell fizzles.

conditions | Conditions | 7.1 | 71-the-nine-conditions-and-their-timing | 7.1 The nine Conditions and their timing
A condition is a lasting effect on a creature, with an amount and a number of rounds.
It counts down in the Aftermath of each round, and ends at zero.
A stunned creature sits out every round the stun lasts. After that, it cannot be stunned again for one round.
> A condition of 2 damage over time deals 2 at each Upkeep until it ends, whatever the Defense.

mutation | Mutation | 5.3 | 53-evolution | 5.3 Evolution
A mutation gives a creature new spells and an initiative bonus, for the rest of the match.
The last mutation of a family, its capstone, gives a passive in place of spells.
On a mutation round, the team shares its picks, and a creature takes at most one mutation.
> A creature at 5 base initiative takes a mutation with a bonus of 1: it knows new spells and acts at 6.
`;

/** @returns {ConceptCard} */
function read(block) {
  const [first, ...rest] = block.split('\n');
  const [id, title, section, anchor, heading] = first.split(' | ');
  const example = rest.find(line => line.startsWith('> ')) ?? '';
  return Object.freeze({
    id, title, lines: Object.freeze(rest.filter(line => line !== example)), example: example.slice(2),
    rulebook: Object.freeze({ section, anchor, label: `Rulebook, ${heading}` }),
  });
}

export const CONCEPTS = Object.freeze(WRITTEN.trim().split('\n\n').map(read));

const byId = new Map(CONCEPTS.map(one => [one.id, one]));

/**
 * The card of a concept, or none for an id that is not one.
 * @returns {ConceptCard | null}
 */
export function conceptCard(id) {
  return byId.get(id) ?? null;
}

/**
 * The cards a lesson step names, in its order, once each; an id that is no concept is left out.
 * @returns {ConceptCard[]}
 */
export function conceptCards(ids) {
  return [...new Set(Array.isArray(ids) ? ids : [])].map(conceptCard).filter(one => one !== null);
}
