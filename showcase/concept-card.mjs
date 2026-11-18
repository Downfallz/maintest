// The concept card's parts (its eyebrow, title, lines and example) are one rule, `.concept-card` in the table's
// identity.css, which the battle and the table's rulebook share (#699). The public site cannot load that sheet, so
// its build copies those lines into the showcase.css it publishes, and its test holds the copy to them (#810).
export const CONCEPT_CARD_SOURCE = 'table/identity.css';

/** The lines of the stylesheet whose rule starts with `.concept-card`, in their order, as one block. */
export function conceptCardRules(css) {
  const lines = css.split('\n').filter(line => line.startsWith('.concept-card'));
  if (!lines.length) throw new Error(`${CONCEPT_CARD_SOURCE} has no .concept-card rule left to copy.`);
  return lines.join('\n') + '\n';
}

/** The published showcase.css: the site's own sheet, then the concept card's rule as the table writes it. */
export const publishedShowcaseCss = (showcase, identity) =>
  `${showcase.trimEnd()}\n\n/* Copied by showcase/build.mjs from ${CONCEPT_CARD_SOURCE}: the concept card as the game draws it (#810). */\n${conceptCardRules(identity)}`;
