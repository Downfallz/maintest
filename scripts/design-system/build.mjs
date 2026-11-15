// Builds the Downfall Instinct design system's files from the repository (docs/art/design-system/README.md): the
// palette, the font and the radii from docs/art/branding.css, the usage notes from tokens.notes.json, the shared
// parts from table/identity.css, and the brand book and component cards as written. A session publishes the result
// to the design system's page; nothing here talks to the network.
//
//   node scripts/design-system/build.mjs    writes artifacts/design-system/project/ (ignored)

import { cpSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const source = join(root, 'docs', 'art', 'design-system');
const read = (path) => readFileSync(join(root, path), 'utf8');

/** One `name: value;` declaration, or null. Plain string work: no regular expression to backtrack. */
function declaration(line) {
  const text = line.trim();
  const colon = text.indexOf(':');
  if (colon < 0 || !text.endsWith(';')) return null;
  return [text.slice(0, colon).trim(), text.slice(colon + 1, -1).trim()];
}

/** The custom properties declared in branding.css's :root, in order, as [name, value], and its @font-face. */
export function readBranding(css = read('docs/art/branding.css')) {
  const lines = css.split('\n');
  const declarations = lines
    .slice(lines.findIndex((line) => line.startsWith(':root {')))
    .map(declaration)
    .filter((pair) => pair && pair[0].startsWith('--'))
    .map(([name, value]) => [name.slice(2), value]);
  const faceStart = lines.findIndex((line) => line.startsWith('@font-face'));
  const face = new Map();
  for (const line of lines.slice(faceStart + 1)) {
    if (line.startsWith('}')) break;
    const pair = declaration(line);
    if (pair) face.set(pair[0], pair[1]);
  }
  const src = face.get('src') ?? '';
  const url = src.slice(src.indexOf("url('") + 5, src.indexOf("')"));
  const font = {
    family: (face.get('font-family') ?? '').replaceAll('"', ''),
    file: basename(url),
    weight: face.get('font-weight'),
    style: face.get('font-style'),
  };
  return { declarations, font };
}

const isColour = (value) => /^#[0-9a-f]{3,8}$/i.test(value) || /^rgba?\(/.test(value) || /^var\(--[a-z0-9-]+\)$/.test(value);

/** A branding.css value as the design system's tokens read it: hex as is, var() as an alias, rgb() spelled out. */
export function tokenValue(value) {
  const alias = /^var\(--([a-z0-9-]+)\)$/.exec(value);
  if (alias) return `{${alias[1]}}`;
  const rgb = /^rgb\((\d+) (\d+) (\d+) \/ (\d+)%\)$/.exec(value);
  if (rgb) return `rgba(${rgb[1]}, ${rgb[2]}, ${rgb[3]}, ${Number(rgb[4]) / 100})`;
  return value.toLowerCase();
}

/** The palette's colours, each with its note; a colour with no note, or a note with no colour, is a problem. */
function paletteColours(branding, notes, problems) {
  const css = new Map(branding.declarations);
  const theme = notes.theme.id;
  const colours = branding.declarations
    .filter(([, value]) => isColour(value))
    .map(([name, value]) => {
      if (!notes.colors[name]) problems.push(`--${name} has no usage note in tokens.notes.json`);
      return { name, value: { [theme]: tokenValue(value) }, usage: notes.colors[name] };
    });
  const unknown = Object.keys(notes.colors).filter((name) => !isColour(css.get(name) ?? ''));
  for (const name of unknown) problems.push(`${name} has a usage note but no colour in branding.css`);
  return colours;
}

/** The shades a page sheet writes out, each still checked against that sheet. */
function pageColours(notes, pageCss, problems) {
  const sheet = pageCss.toLowerCase();
  return notes.pageColors.map(({ name, value, source: where, usage }) => {
    if (!sheet.includes(value.toLowerCase())) problems.push(`${name} (${value}) is no longer in ${where}`);
    return { name, value: { [notes.theme.id]: value }, usage: `${usage} Written out in ${where}.` };
  });
}

/** Radii, cuts, sizes and shadow; a token with no value of its own takes branding.css's. */
function otherFamilies(branding, notes, problems) {
  const css = new Map(branding.declarations);
  const families = structuredClone(notes.families);
  const inherited = Object.values(families).flatMap((family) => family.tokens.filter((token) => token.value === undefined));
  for (const token of inherited) {
    if (!css.has(token.name)) problems.push(`${token.name} takes its value from branding.css, which does not declare it`);
    token.value = css.get(token.name);
  }
  return families;
}

/** tokens.json: the palette from branding.css with the notes' usage, then type, radii, cuts, sizes and shadow. */
export function buildTokens({ branding = readBranding(), notes = readNotes(), pageCss = read('table/table.css') } = {}) {
  const css = new Map(branding.declarations);
  const problems = [];
  const colours = [...paletteColours(branding, notes, problems), ...pageColours(notes, pageCss, problems)];
  const families = otherFamilies(branding, notes, problems);
  if (problems.length) throw new Error(`The design system is out of step:\n- ${problems.join('\n- ')}`);
  return {
    name: notes.name,
    version: 1,
    color: { themes: [notes.theme], tokens: colours },
    type: {
      fonts: [{ ...branding.font, file: `fonts/${branding.font.file}` }],
      families: { display: css.get('font-display'), text: css.get('font-text') },
      groups: notes.type.groups,
    },
    ...families,
    meta: {
      source: 'github',
      repo: 'Downfallz/maintest',
      paths: {
        tokens: ['docs/art/branding.css', 'docs/art/design-system/tokens.notes.json'],
        fonts: [`table/fonts/${branding.font.file}`],
        assets: Object.values(ASSETS).flat(),
        docs: ['docs/art/branding.md', 'docs/art/design-system/', 'table/identity.css'],
      },
    },
  };
}

/** The pictures the design system shows, by group. They are uploaded to the page, not sent as files. */
export const ASSETS = {
  Logos: ['table/brand-mark.svg', 'table/icon.svg', 'table/icon-512.png', 'table/icon-maskable-512.png', 'table/notification-badge.svg'],
  Captures: ['docs/art/images/branding/lobby-desktop.jpg', 'docs/art/images/branding/lobby-phone.jpg'],
};

/** bundle.css: the textures (token values cannot hold a url()), the previews' frame, then identity.css whole. */
export function buildBundleCss(branding = readBranding()) {
  const textures = branding.declarations
    .filter(([name]) => name.startsWith('tex-') || name === 'cut')
    .map(([name, value]) => `  --${name}: ${value};`);
  return [
    '/* Downfall Instinct - generated by scripts/design-system/build.mjs from docs/art/branding.css, docs/art/design-system/frame.css and table/identity.css. */',
    `:root {\n${textures.join('\n')}\n}`,
    read('docs/art/design-system/frame.css').trimEnd(),
    read('table/identity.css').trimEnd(),
    '',
  ].join('\n\n');
}

function readNotes() {
  return JSON.parse(read('docs/art/design-system/tokens.notes.json'));
}

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

/** The written files, as system paths: README.md, assets/<Group>/README.md, components/<Comp>/... */
export function writtenFiles() {
  return walk(source)
    .map((path) => relative(source, path).replaceAll('\\', '/'))
    .filter((path) => path !== 'tokens.notes.json' && path !== 'frame.css')
    .sort();
}

/** Writes the design system's files under artifacts/design-system/project/, always there, and returns that path. */
export function build() {
  const branding = readBranding();
  const project = join(root, 'artifacts', 'design-system', 'project');
  rmSync(project, { recursive: true, force: true });
  for (const path of writtenFiles()) {
    mkdirSync(dirname(join(project, path)), { recursive: true });
    cpSync(join(source, path), join(project, path));
  }
  writeFileSync(join(project, 'tokens.json'), `${JSON.stringify(buildTokens({ branding }), null, 1)}\n`);
  mkdirSync(join(project, 'components'), { recursive: true });
  writeFileSync(join(project, 'components', 'bundle.css'), buildBundleCss(branding));
  mkdirSync(join(project, 'fonts'), { recursive: true });
  for (const file of [branding.font.file, 'OFL.txt']) cpSync(join(root, 'table', 'fonts', file), join(project, 'fonts', file));
  return project;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const project = build();
  console.log(`Wrote ${relative(process.cwd(), project)}. Pictures to upload: ${Object.values(ASSETS).flat().join(', ')}`);
}
