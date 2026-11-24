#!/usr/bin/env node
// The journeys against their baseline (docs/art/ux-craft-plan.md, UX14): the files `journey.mjs` wrote in a folder,
// read screen by screen against docs/art/journeys/baseline.md, a journey a heading and a width a table, each measure
// written `then → now` where it moved. With the paints (`paint.json`) and the walks under less motion (`--less-motion`)
// beside them, it reads those too. docs/art/journeys/acceptance.md is this page, as UX14 found the journeys.
//
//   node scripts/ux/journey.mjs --all --paint --out <folder> && node scripts/ux/journey.mjs --all --less-motion --out <folder>
//   node scripts/ux/acceptance.mjs <folder> --commit <sha> --date <day> > docs/art/journeys/acceptance.md

import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { JOURNEYS } from './journeys.mjs';
import { readWalks } from './baseline.mjs';
import { lessMotionFaults } from './motion.mjs';

const root = resolve(import.meta.dirname, '../..');

/** The measures read against the baseline, by the baseline's column names. */
export const COMPARED = ['Words', 'Filled actions', 'Smallest text', 'Words under 11 px', 'Targets under 44 px', 'Overlaps', 'Animating', 'axe'];

/** A screen's line as the baseline writes those columns. */
export function cells(screen, axe) {
  const found = Object.entries(screen.axe ?? {}).map(([rule, nodes]) => `${rule} (${nodes})`).join(', ');
  return {
    Words: String(screen.words),
    'Filled actions': String(screen.filled),
    'Smallest text': screen.text.smallest === null ? 'none' : `${screen.text.smallest} px`,
    'Words under 11 px': String(screen.text.under),
    'Targets under 44 px': `${screen.targets.under} of ${screen.targets.all}`,
    Overlaps: String(screen.overlaps),
    Animating: String(screen.animating),
    axe: axe ? found || 'nothing' : 'not run',
  };
}

/**
 * The baseline's tables: `{ [journey id]: { [width]: Map(screen → { column: text }) } }`, read from its Markdown.
 * Refuses a page with no table of a journey: an empty reading would compare nothing and say all is new.
 */
export function parseBaseline(text) {
  const byTitle = new Map(JOURNEYS.map(journey => [journey.title, journey.id]));
  const tables = {};
  let journey = null, width = null, header = null;
  for (const line of text.split('\n')) {
    const heading = /^## (.+)$/.exec(line);
    if (heading) { journey = byTitle.get(heading[1]) ?? null; width = null; header = null; continue; }
    const at = /^At (\d+) by \d+:$/.exec(line);
    if (at && journey) { width = Number(at[1]); header = null; continue; }
    if (!line.startsWith('|')) { if (line.trim()) header = null; continue; }
    if (!journey || !width) continue;
    const row = line.slice(1, -1).split('|').map(cell => cell.trim());
    if (!header) { header = row; continue; }
    if (row.every(cell => /^-+$/.test(cell))) continue;
    const measured = Object.fromEntries(header.map((name, index) => [name, row[index]]));
    ((tables[journey] ??= {})[width] ??= new Map()).set(measured.Screen.replace(/ \(staged\)$/, ''), measured);
  }
  if (!Object.keys(tables).length) throw new Error('The baseline holds no table of a journey.');
  return tables;
}

const table = (header, rows) => [`| ${header.join(' | ')} |`, `| ${header.map(() => '---').join(' | ')} |`, ...rows.map(row => `| ${row.join(' | ')} |`)].join('\n');
const moved = (then, now) => (then === undefined ? now : then === now ? now : `${then} → ${now}`);
const width = walk => Number(walk.header.viewport.split('x')[0]);
const seconds = spread => (spread ? `${(spread.median / 1000).toFixed(1)} s` : 'not painted');

/** What the screens of some walks hold, summed: the lines of the answer in short. */
export function tally(walks) {
  const screens = walks.flatMap(walk => walk.screens.map(screen => ({ ...screen, axeRun: walk.header.axe })));
  return {
    screens: screens.length,
    overFilled: screens.filter(screen => screen.filled > 1).length,
    wordsUnder: screens.reduce((sum, screen) => sum + screen.text.under, 0),
    overlaps: screens.filter(screen => screen.overlaps > 0).length,
    axe: screens.filter(screen => Object.keys(screen.axe ?? {}).length).length,
    axeRun: screens.every(screen => screen.axeRun),
    animating: screens.filter(screen => screen.animating > 0).length,
  };
}

/** The page: each journey's screens then and now, the paints then and now, and what less motion found. */
export function acceptanceMarkdown({ walks, paint, baseline, baselinePaints = null, commit, date }) {
  const full = walks.filter(walk => !walk.header.lessMotion), less = walks.filter(walk => walk.header.lessMotion);
  if (!full.length) throw new Error('No walk to read against the baseline: the folder holds no measures under full motion.');
  const now = tally(full);
  const parts = [`# The journeys at acceptance

The journeys of [the pages' craft plan](../ux-craft-plan.md) as UX14 found them, walked by \`scripts/ux/journey.mjs\` on
\`main\` at \`${commit}\`, ${date}, and read against [the baseline](baseline.md), screen by screen. Written by
\`scripts/ux/acceptance.mjs\` from the files of that walk; not edited by hand. The measures are the baseline's, defined
on its page. A cell reads \`then → now\` where the measure moved since the baseline, and holds one value where it did
not. A screen the baseline does not hold is marked *new*. [The record](../ux-acceptance.md) reads this page.

${now.screens} screens walked. ${now.overFilled} with more than one filled action. ${now.wordsUnder} words under 11 px in all.
${now.overlaps} with controls that overlap. ${now.axeRun ? `${now.axe} with a finding of axe.` : 'axe was not run.'} ${now.animating} still animating at rest.`];
  for (const journey of JOURNEYS) {
    const mine = full.filter(walk => walk.header.journey === journey.id);
    if (!mine.length) continue;
    parts.push(`## ${journey.title}`);
    for (const walk of mine) {
      const then = baseline[journey.id]?.[width(walk)] ?? new Map();
      const rows = walk.screens.map(screen => {
        const was = then.get(screen.screen), is = cells(screen, walk.header.axe);
        return [`${screen.screen}${was ? '' : ' *new*'}`, ...COMPARED.map(column => moved(was?.[column], is[column]))];
      });
      parts.push(`At ${walk.header.viewport.replace('x', ' by ')}:`, table(['Screen', ...COMPARED], rows));
      const gone = [...then.keys()].filter(name => !walk.screens.some(screen => screen.screen === name));
      if (gone.length) parts.push(`In the baseline and not walked now: ${gone.join(', ')}.`);
      if (walk.header.blocked?.length) parts.push(`Out of a player's reach at this size:\n\n${walk.header.blocked.map(line => `- ${line.replaceAll('|', '\\|')}`).join('\n')}`);
    }
  }
  if (paint) {
    const { profile, pages } = paint;
    parts.push('## The first paints, on a slow phone',
      `Each page loaded ${Object.values(pages)[0].runs} times with nothing cached, at ${profile.viewport.replace('x', ' by ')}, on the baseline's slow phone. The median; the baseline's where it painted the page. A paint is this machine's on this day.`,
      table(['Page', 'Largest paint', 'The largest is', 'Layout shift', 'Pictures loaded'],
        Object.entries(pages).map(([name, page]) => {
          const was = baselinePaints?.get(name) ?? baselinePaints?.get(BASELINE_PAINT_NAMES[name]);
          return [name, moved(was?.['Largest paint']?.split(' (')[0], seconds(page.largestPaintMs)), page.largestIs,
            moved(was?.['Layout shift']?.split(' (')[0], page.shift ? page.shift.median.toFixed(2) : 'not read'),
            moved(was?.['Pictures loaded'], `${Math.round(page.pictureBytes / 1024)} KB`)];
        })));
  }
  if (less.length) {
    const faults = less.flatMap(walk => lessMotionFaults(walk.screens).map(line => `${walk.header.journey} at ${width(walk)} px, ${line}`));
    const screens = less.reduce((sum, walk) => sum + walk.screens.length, 0);
    parts.push('## Less motion',
      `Every journey walked again with the system's wish for less motion (\`--less-motion\`): ${screens} screens. On the way to each, the walk keeps every transition, keyframe animation or \`animate\` call that moves a thing across the screen (\`scripts/ux/motion.mjs\`); at rest, what still animates.`,
      faults.length ? faults.map(line => `- ${line}`).join('\n') : 'Nothing travelled, and nothing animated at rest.');
  }
  return `${parts.join('\n\n')}\n`;
}

/** The baseline's names of the pages it painted, before the budget file named them (#962). */
export const BASELINE_PAINT_NAMES = { 'showcase/home': 'showcase-home', 'table/sign-in': 'sign-in-panel', 'table/home': 'home-new' };

/** The baseline's paints, by page, as its last table writes them. */
export function parseBaselinePaints(text) {
  const at = text.indexOf('## The first paints');
  if (at < 0) return null;
  const rows = text.slice(at).split('\n').filter(line => line.startsWith('|')).map(line => line.slice(1, -1).split('|').map(cell => cell.trim()));
  const [header, , ...body] = rows;
  return new Map(body.map(row => [row[0], Object.fromEntries(header.map((name, index) => [name, row[index]]))]));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const [folder, ...rest] = process.argv.slice(2);
    const named = flag => {
      const value = rest.includes(flag) ? rest[rest.indexOf(flag) + 1] : null;
      return value && !value.startsWith('--') ? value : null;
    };
    if (!folder || !named('--commit') || !named('--date')) throw new Error('Usage: node scripts/ux/acceptance.mjs <folder> --commit <sha> --date <day>');
    const { walks, paint } = readWalks(resolve(folder));
    const text = readFileSync(join(root, 'docs/art/journeys/baseline.md'), 'utf8');
    process.stdout.write(acceptanceMarkdown({ walks, paint, baseline: parseBaseline(text), baselinePaints: parseBaselinePaints(text), commit: named('--commit'), date: named('--date') }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
