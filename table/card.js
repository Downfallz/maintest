// A card, as the page draws it.
//
// Every word comes from the card the host served: what a spell does, who it hits, how often it crits and what
// it costs are all computed by the catalogue projection, in the same words the printed deck uses
// (docs/tabletop/components.md §2.1). That is what lets a tuning pass be a rebuild and a restart rather than a
// change to this page -- and it only holds while nothing here defaults. A field a card does not carry is a line
// the card does not have, never a blank to fill in.

export function cardTitle(card) {
  return card?.name ?? '';
}

// The energy a cast costs, as the head prints it. Zero is a cost and is printed: "free" is a rule the page
// would be inventing.
export function cardCost(card) {
  return Number.isInteger(card?.cost) ? String(card.cost) : '';
}

// The class the spell belongs to. A depth into the talent tree used to print beside it; it does not any more,
// because a package's prerequisites are what a creature climbs and the tree gates nothing (ADR 0056).
export function cardHead(card) {
  return card?.creatureClass ?? '';
}

// The body of the card, in the order the printed one reads: who it hits, what it does to them, what it does to
// the caster, and how often it crits.
//
// What it takes to get the spell is not on it. A spell is acquired by buying the package that teaches it, so
// the requirement and the initiative it buys belong to that package's card and are printed there once, whole.
// Repeating a per-spell gate and a per-spell unlock bonus here would print two rules the engine stopped
// applying, which a table reads as the rules (ADR 0056).
export function cardLines(card) {
  return [
    card?.targeting,
    ...(card?.effects ?? []),
    ...(card?.casterEffects ?? []),
    criticalLine(card),
  ].filter(Boolean);
}

// The chance, and the face of the die when the chance is a twentieth. The threshold is the host's: whether a
// chance can be rolled on a d20 is a property of the content, not of the screen.
export function criticalLine(card) {
  if (!card?.critical) return '';
  return card.criticalThreshold ? `Crit ${card.critical} · d20 ${card.criticalThreshold}+` : `Crit ${card.critical}`;
}

// The catalogue as a lookup, so a spell id on a decision finds its card. A spell the catalogue does not carry
// has no card, and the caller prints the id it was given rather than inventing one.
export function cardsById(catalogue) {
  return new Map((catalogue?.cards ?? []).map(card => [card.id, card]));
}

// The catalogue, through whichever seat this table actually accepts. A page can be holding a token from an
// earlier table -- kept in this browser, and ordered first -- and asking with that one alone would leave every
// spell on the screen as its raw id for the whole session. Any seat will do: the catalogue is the same for
// both, and nothing in it is hidden.
export async function loadCatalogue(seats) {
  for (const seat of seats ?? []) {
    const answer = await seat.transport.catalogue();
    if (answer.ok) {
      return answer.body;
    }
  }

  return null;
}

export async function loadCards(seats) {
  return cardsById(await loadCatalogue(seats));
}
