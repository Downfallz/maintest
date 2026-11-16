// The concept cards (ADR 0187, docs/tabletop/onboarding-plan.md): what one rule word means, in three lines at
// most, with one example. Each is written once, in its section of the official rulebook (`rules/`, ADR 0188), and
// read here from the built book, `rules.json` (docs/tabletop/rulebook-plan.md R4): its words and the numbers its
// example draws move with the book, and none is written in this file. A card links to its section, by the
// section's id. A lesson step opens the cards it names by their ids, which the host holds a lesson to
// (`ConceptCards.Ids`) and holds to the book (`RulebookConceptCardsTests`).

/** @typedef {{ section: string, href: string, label: string }} RulesLink */
/** @typedef {{ id: string, title: string, lines: readonly string[], example: string, rules: RulesLink }} ConceptCard */
/** @typedef {{ all: readonly ConceptCard[], card: (id: unknown) => ConceptCard | null, cards: (ids: unknown) => ConceptCard[] }} Concepts */

/** The book, beside the page that reads it: `/rules.json` on the table. */
export const RULES_BOOK = 'rules.json';

const ID = /^[a-z][a-z0-9-]*$/;
const text = value => typeof value === 'string' && value.trim() !== '';

/** A section's card as the page shows it, or null for one the book does not carry whole. */
function cardOf(section) {
  const card = section?.card;
  if (!card || !ID.test(card.id ?? '') || !ID.test(section.id ?? '') || !text(card.title) || !text(card.example)) return null;
  if (!Array.isArray(card.lines) || card.lines.length === 0 || !card.lines.every(text)) return null;
  return Object.freeze({
    id: card.id, title: card.title, lines: Object.freeze([...card.lines]), example: card.example,
    rules: Object.freeze({ section: section.id, href: `/rules#${section.id}`, label: `Rules, ${[section.number, section.title].filter(text).join(' ')}` }),
  });
}

/**
 * The cards of a built book, in its order, each once; none for a book that cannot be read.
 * @returns {Concepts}
 */
export function conceptsOf(book) {
  const parts = Array.isArray(book?.parts) ? book.parts : [];
  const sections = parts.flatMap(part => Array.isArray(part?.sections) ? part.sections : []);
  const byId = new Map();
  for (const card of sections.map(cardOf)) if (card && !byId.has(card.id)) byId.set(card.id, card);
  const all = Object.freeze([...byId.values()]);
  const card = id => byId.get(id) ?? null;
  return Object.freeze({
    all,
    card,
    // The cards a lesson step names, in its order, once each; an id that is no card is left out.
    cards: ids => [...new Set(Array.isArray(ids) ? ids : [])].map(card).filter(one => one !== null),
  });
}

/** No card: before the book is read, or when it cannot be. A lesson still shows, without its cards. */
export const NO_CONCEPTS = conceptsOf(null);

/**
 * The cards of the book `read` returns; none when it cannot be read.
 * @param {() => Promise<unknown>} read
 * @returns {Promise<Concepts>}
 */
export async function loadConcepts(read) {
  try {
    const concepts = conceptsOf(await read());
    return concepts.all.length ? concepts : NO_CONCEPTS;
  } catch {
    return NO_CONCEPTS;
  }
}
