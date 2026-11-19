#!/usr/bin/env node
// The measures of the journeys as one page a person reads (docs/art/ux-craft-plan.md, UX2): the files
// `journey.mjs` wrote in a folder, laid out a journey a heading and a width a table, with the paints when
// `paint.json` is beside them. docs/art/journeys/baseline.md is this page, as the journeys stood before any pass.
//
//   node scripts/ux/journey.mjs --all --paint --out <folder>
//   node scripts/ux/baseline.mjs <folder> --commit <sha> --date <day> > docs/art/journeys/baseline.md

import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { JOURNEYS } from './journeys.mjs';
import { parseMeasures } from './measures.mjs';

const COLUMNS = ['Screen', 'Height', 'Words', 'Filled actions', 'Pictures', 'Smallest text', 'Words under 11 px', 'Targets under 44 px', 'under 24 px', 'Overlaps', 'Animating', 'Shift', 'axe'];

function row(screen, axe) {
  const { pictures, text, targets } = screen;
  const found = Object.entries(screen.axe ?? {}).map(([rule, nodes]) => `${rule} (${nodes})`).join(', ');
  const drawn = [pictures.img ? `${pictures.img} img` : null, pictures.background ? `${pictures.background} background` : null, pictures.canvas ? 'the canvas' : null].filter(Boolean).join(', ');
  return [
    `${screen.screen}${screen.staged ? ' (staged)' : ''}`, `${screen.height} px${screen.window ? ', a window' : ''}`, screen.words, screen.filled,
    `${drawn || 'none'}${pictures.oversized ? `; ${pictures.oversized} sent over twice their size` : ''}`, text.smallest === null ? 'none' : `${text.smallest} px`, text.under,
    `${targets.under} of ${targets.all}`, targets.tiny, screen.overlaps, screen.animating, screen.shift.toFixed(2), axe ? found || 'nothing' : 'not run',
  ];
}

const table = (header, rows) => [`| ${header.join(' | ')} |`, `| ${header.map(() => '---').join(' | ')} |`, ...rows.map(cells => `| ${cells.join(' | ')} |`)].join('\n');
const seconds = spread => (spread ? `${(spread.median / 1000).toFixed(1)} s (${(spread.fastest / 1000).toFixed(1)} to ${(spread.slowest / 1000).toFixed(1)})` : 'not painted');

/** The page, from the measures of each journey and width ({ header, screens }) and the paints, if any. */
export function baselineMarkdown(walks, paint, { commit, date }) {
  if (!walks.length) throw new Error('No measures to lay out.');
  const parts = [`# The journeys' baseline

The five journeys of [the pages' craft plan](../ux-craft-plan.md) as they stood before any pass, walked by
\`scripts/ux/journey.mjs\` on \`main\` at \`${commit}\`, ${date}: the built showcase and a real host with developer
accounts, in Chromium. Written by \`scripts/ux/baseline.mjs\` from the files of that walk; not edited by hand. A
later stage walks its journey again and reads its measures against these.

A screen is measured on the window a player is in: the modal window when one is open, the whole page
otherwise. Words are the words shown. A filled action is a control whose background is the brand's gold. A
target is a control; it is counted under 44 px when either side is. An overlap is two controls sharing more
than 2 px each way. Animating is what still runs once the screen is at rest. The shift is the layout's, summed
since the page loaded. axe ran the rules tagged WCAG 2.2 AA; a number is the elements a rule fails on. A staged
screen was opened by the walk itself, where a walk cannot press what a player presses.`];
  for (const journey of JOURNEYS) {
    const mine = walks.filter(walk => walk.header.journey === journey.id);
    if (!mine.length) continue;
    parts.push(`## ${journey.title}`);
    for (const walk of mine) {
      parts.push(`At ${walk.header.viewport.replace('x', ' by ')}:`, table(COLUMNS, walk.screens.map(screen => row(screen, walk.header.axe))));
      if (walk.header.blocked?.length) parts.push(`Out of a player's reach at this size, and chosen by the walk's script so the rest is filmed:\n\n${walk.header.blocked.map(line => `- ${line.replaceAll('|', '\\|')}`).join('\n')}`);
    }
  }
  if (paint) {
    const { profile, pages } = paint;
    parts.push('## The first paints, on a slow phone',
      `Each page loaded ${Object.values(pages)[0].runs} times with nothing cached, at ${profile.viewport.replace('x', ' by ')}, the network held to ${(profile.download * 8 / 1024 / 1024).toFixed(1)} Mbit/s down with ${profile.latency} ms of latency and the processor ${profile.cpu} times slower. The median, with the fastest and the slowest run. A paint depends on the machine that measures it: these compare with one another and with a later walk on the same machine, not with a phone.`,
      table(['Page', 'First paint', 'Largest paint', 'The largest is', 'Layout shift', 'Pictures loaded'],
        Object.entries(pages).map(([name, page]) => [name, seconds(page.firstPaintMs), seconds(page.largestPaintMs), page.largestIs, page.shift ? `${page.shift.median.toFixed(2)} (${page.shift.fastest.toFixed(2)} to ${page.shift.slowest.toFixed(2)})` : 'not read', `${Math.round(page.pictureBytes / 1024)} KB`])));
  }
  return `${parts.join('\n\n')}\n`;
}

/** Read the measures and the paints `journey.mjs` wrote in a folder, in the journeys' order, the narrow width first. */
export function readWalks(folder) {
  const files = readdirSync(folder).filter(name => name.endsWith('.measures.jsonl')).sort();
  const walks = files.map(name => parseMeasures(readFileSync(join(folder, name), 'utf8')));
  const paint = existsSync(join(folder, 'paint.json')) ? JSON.parse(readFileSync(join(folder, 'paint.json'), 'utf8')) : null;
  return { walks, paint };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const [folder, ...rest] = process.argv.slice(2);
    const named = flag => (rest.includes(flag) ? rest[rest.indexOf(flag) + 1] : null);
    if (!folder || !named('--commit') || !named('--date')) throw new Error('Usage: node scripts/ux/baseline.mjs <folder> --commit <sha> --date <day>');
    const { walks, paint } = readWalks(resolve(folder));
    process.stdout.write(baselineMarkdown(walks, paint, { commit: named('--commit'), date: named('--date') }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
