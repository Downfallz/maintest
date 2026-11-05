import { readFile, writeFile } from 'node:fs/promises';

// Inline the canonical tokens: file:// viewers, generated standalone reports, the two
// HTTP hosts and GitHub Pages all keep working without a new asset route or CSS import.
const root = new URL('../', import.meta.url);
const start = '/* BEGIN GENERATED BRANDING — run node scripts/sync-branding.mjs */';
const end = '/* END GENERATED BRANDING */';
const palette = (await readFile(new URL('docs/art/branding.css', root), 'utf8')).trimEnd();
const block = `${start}\n${palette}\n${end}\n`;
for (const path of ['table/table.css', 'viewer/viewer.css', 'table/arena-lab.css']) {
  const file = new URL(path, root), current = await readFile(file, 'utf8');
  const body = current.startsWith(start) ? current.slice(current.indexOf(end) + end.length).replace(/^\n/, '') : current;
  const expected = block + body;
  if (process.argv.includes('--check')) {
    if (expected !== current) { console.error(`${path}: run node scripts/sync-branding.mjs`); process.exitCode = 1; }
  } else if (expected !== current) await writeFile(file, expected);
}
