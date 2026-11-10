// The classic table is archived (ADR 0157): /play hands every seat on to the battle view, except in a browser that
// asked for the archive by storing `classic`. The classic table's own tests are that browser.
export const archivedTable = () => { try { localStorage.setItem('downfall.presentation', 'classic'); } catch { /* storage-denial fixtures */ } };
export async function classicTable({ context }) { await context.addInitScript(archivedTable); }
