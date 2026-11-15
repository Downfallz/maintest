// Builds the Downfall Instinct design system's files from the repository (docs/art/design-system/README.md): the
// palette, the font and the radii from docs/art/branding.css, the usage notes from tokens.notes.json, the shared
// parts from table/identity.css, and the brand book and component cards as written. A session publishes the result
// to the design system's page; nothing here talks to the network.
//
//   node scripts/design-system/build.mjs [out]    writes <out>/project/ (default artifacts/design-system)

import { execFileSync } from 'node:child_process';
import { cpSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const source = join(root, 'docs', 'art', 'design-system');
const read = (path) => readFileSync(join(root, path), 'utf8');

/** The custom properties declared in branding.css's :root, in order, as [name, value]. */
export function readBranding(css = read('docs/art/branding.css')) {
  const rootBlock = css.slice(css.indexOf(':root {'));
  const declarations = [...rootBlock.matchAll(/^\s*--([a-z0-9-]+):\s*(.+?);\s*$/gm)].map((m) => [m[1], m[2]]);
  const face = /@font-face\s*{([^}]*)}/.exec(css)?.[1] ?? '';
  const font = {
    family: /font-family:\s*"([^"]+)"/.exec(face)?.[1],
    file: basename(/url\('([^']+)'\)/.exec(face)?.[1] ?? ''),
    weight: /font-weight:\s*([^;]+);/.exec(face)?.[1].trim(),
    style: /font-style:\s*([^;]+);/.exec(face)?.[1].trim(),
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

/** tokens.json: the palette from branding.css with the notes' usage, then type, radii, cuts, sizes and shadow. */
export function buildTokens({ branding = readBranding(), notes = readNotes(), pageCss = read('table/table.css'), ref } = {}) {
  const css = new Map(branding.declarations);
  const problems = [];
  const theme = notes.theme.id;
  const colours = [];
  for (const [name, value] of branding.declarations) {
    if (!isColour(value)) continue;
    const usage = notes.colors[name];
    if (!usage) problems.push(`--${name} has no usage note in tokens.notes.json`);
    colours.push({ name, value: { [theme]: tokenValue(value) }, usage });
  }
  for (const name of Object.keys(notes.colors)) {
    if (!css.has(name) || !isColour(css.get(name))) problems.push(`${name} has a usage note but no colour in branding.css`);
  }
  for (const { name, value, source: where, usage } of notes.pageColors) {
    if (!pageCss.toLowerCase().includes(value.toLowerCase())) problems.push(`${name} (${value}) is no longer in ${where}`);
    colours.push({ name, value: { [theme]: value }, usage: `${usage} Written out in ${where}.` });
  }
  const families = structuredClone(notes.families);
  for (const family of Object.values(families)) {
    for (const token of family.tokens) {
      if (token.value !== undefined) continue;
      if (!css.has(token.name)) problems.push(`${token.name} takes its value from branding.css, which does not declare it`);
      token.value = css.get(token.name);
    }
  }
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
      ref,
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
  const textures = branding.declarations.filter(([name]) => name.startsWith('tex-') || name === 'cut');
  return [
    '/* Downfall Instinct - generated by scripts/design-system/build.mjs from docs/art/branding.css, docs/art/design-system/frame.css and table/identity.css. */',
    `:root {\n${textures.map(([name, value]) => `  --${name}: ${value};`).join('\n')}\n}`,
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
    .map((path) => relative(source, path).split('\\').join('/'))
    .filter((path) => path !== 'tokens.notes.json' && path !== 'frame.css')
    .sort();
}

export function build(out = join(root, 'artifacts', 'design-system')) {
  const ref = `main@${execFileSync('git', ['-C', root, 'rev-parse', '--short', 'HEAD'], { encoding: 'utf8' }).trim()}`;
  const branding = readBranding();
  const project = join(out, 'project');
  rmSync(project, { recursive: true, force: true });
  for (const path of writtenFiles()) {
    mkdirSync(dirname(join(project, path)), { recursive: true });
    cpSync(join(source, path), join(project, path));
  }
  writeFileSync(join(project, 'tokens.json'), `${JSON.stringify(buildTokens({ branding, ref }), null, 1)}\n`);
  mkdirSync(join(project, 'components'), { recursive: true });
  writeFileSync(join(project, 'components', 'bundle.css'), buildBundleCss(branding));
  mkdirSync(join(project, 'fonts'), { recursive: true });
  cpSync(join(root, 'table', 'fonts', branding.font.file), join(project, 'fonts', branding.font.file));
  cpSync(join(root, 'table', 'fonts', 'OFL.txt'), join(project, 'fonts', 'OFL.txt'));
  return project;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const project = build(process.argv[2] ? join(process.cwd(), process.argv[2]) : undefined);
  console.log(`Wrote ${relative(process.cwd(), project)}. Pictures to upload: ${Object.values(ASSETS).flat().join(', ')}`);
}
