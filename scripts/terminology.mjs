// The one reader of docs/domain/player-terminology.md's tables, shared by every scanner of the player's words:
// the pages' terminology test (table/terminology.test.js) and the rulebook's build (scripts/rules/build.mjs).
// Two parsers of the same tables drifted once already; a scanner reads both tables here, the Before column included.

/** The rows of the page's tables whose header's first cell passes `isHeader`, as their trimmed cells. */
export function tableRows(text, isHeader) {
  const rows = [];
  let inTable = false;
  for (const line of text.split('\n')) {
    const cells = line.startsWith('|') ? line.split('|').slice(1, -1).map(cell => cell.trim()) : null;
    if (!cells) inTable = false;
    else if (isHeader(cells[0])) inTable = true;
    else if (inTable && !/^-+$/.test(cells[0])) rows.push(cells);
  }
  return rows;
}

/** The rows of the tables whose header starts with `Internal`: [internal, legacy, en, fr] as written. */
export const termRows = text => tableRows(text, first => first.startsWith('Internal'));

/** The "Before | After" table: the sentences the page rewrote, as { before, after }. */
export const rewrites = text => tableRows(text, first => first === 'Before').map(([before, after]) => ({ before, after }));
