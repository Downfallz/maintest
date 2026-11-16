import { existsSync, readdirSync } from 'node:fs';
import { cp, mkdir, mkdtemp, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { EVOLUTION_REGISTRY } from '../table/battle-evolution-art.js';
import { resolveEvolution } from '../table/battle-evolution.js';
import { loadCatalogue } from './catalogue.mjs';
import { english, french } from './copy.mjs';
import { copyrightPage } from './copyright.mjs';
import { guidePage } from './guide.mjs';
import { routes } from './layout.mjs';
import { specimenPath } from './lore.mjs';
import { page } from './page.mjs';
import { privacyPage } from './privacy.mjs';
import { GAME_ORIGIN } from '../table/site-origins.js';

const source = new URL('../', import.meta.url);
// The folder to write, `showcase/dist` by default; the tests name a temporary one so the working tree stays as it is.
// The build replaces that folder, so it takes only one that is absent or holds nothing but what it writes.
const argument = process.argv[2];
if (argument?.startsWith('-')) throw new Error(`Usage: node showcase/build.mjs [folder]; ${argument} is not a folder.`);
const destination = resolve(argument || fileURLToPath(new URL('./dist', import.meta.url)));
const application = new URL(process.env.GAME_ORIGIN || GAME_ORIGIN);
if (application.protocol !== 'https:' || application.href !== application.origin + '/' || application.username || application.password)
  throw new Error('GAME_ORIGIN must be an HTTPS origin without credentials, path, query or fragment.');

const catalogue = await loadCatalogue();
const packages = new Map(catalogue.nodes.map(t => [t.id, t]));
const owned = [];
const specimen = [EVOLUTION_REGISTRY.base()];
for (const shortId of specimenPath) {
  const id = `tier:${shortId}:v1`, node = packages.get(id);
  if (!node || !node.prerequisites.every(p => owned.includes(p)) || (node.anyOf.length && !node.anyOf.some(p => owned.includes(p))))
    throw new Error(`Unreachable observation step ${id}`);
  owned.push(id);
  const visual = resolveEvolution({ acquiredTiers: owned }, packages, { log: { order: () => owned } });
  specimen.push(EVOLUTION_REGISTRY.art(visual.artworkId));
}
// Publish only the ordinary forms and this observation's artwork. The remaining hybrids are discovered in play.
const portraits = new Set([...specimen.map(art => art.file),
  ...catalogue.nodes.filter(t => t.visual.artwork).map(t => EVOLUTION_REGISTRY.art(t.visual.artwork).file)]);
const stage = await mkdtemp(destination + '-');
try {
  const assets = [
    ['docs/art/branding.css', 'branding.css'],
    ...['showcase.css', 'gallery.js', 'guide.js', '404.html', '_headers'].map(file => [`showcase/${file}`, file]),
    ...['brand-mark.svg', 'icon.svg'].map(file => [`table/${file}`, `assets/${file}`]),
    ...['marcellus-latin-400.woff2', 'OFL.txt'].map(file => [`table/fonts/${file}`, `fonts/${file}`]),
    ['table/battle-art/eclipse-battle.webp', 'assets/eclipse-battle.webp'],
    ...['breach', 'broken-floor', 'shattered-sky'].map(id => [`table/battle-art/horizon/${id}.webp`, `assets/${id}.webp`]),
    ...[...portraits].map(file => [`table/battle-art/${file}`, `assets/${file}`]),
  ];
  const documents = [english, french].flatMap(c => {
    const path = routes[c.lang];
    return [[path.home, page(c, specimen, application.href)], [path.guide, guidePage(c, catalogue, application.href)],
      [path.privacy, privacyPage(c, application.href)], [path.copyright, copyrightPage(c, application.href)]]
      .map(([route, html]) => [route.slice(1) + 'index.html', html]);
  });
  // Every file already there must be one this build writes, at any depth: the folder is removed whole below.
  const written = new Set([...assets.map(([, to]) => to), ...documents.map(([to]) => to)].map(to => to.split(sep).join('/')));
  const strangers = existsSync(destination)
    ? readdirSync(destination, { recursive: true, withFileTypes: true })
      .filter(entry => !entry.isDirectory())
      .map(entry => relative(destination, join(entry.parentPath, entry.name)).split(sep).join('/'))
      .filter(path => !written.has(path))
    : [];
  if (strangers.length)
    throw new Error(`${destination} holds ${strangers.join(', ')}, which this build does not write; it would replace the folder.`);
  const directories = new Set([...assets.map(([, to]) => dirname(to)), ...documents.map(([to]) => dirname(to))]);
  await Promise.all([...directories].map(dir => mkdir(join(stage, dir), { recursive: true })));
  await Promise.all(assets.map(([from, to]) => cp(new URL(from, source), join(stage, to))));
  await Promise.all(documents.map(([to, content]) => writeFile(join(stage, to), content)));
  await rm(destination, { recursive: true, force: true });
  await rename(stage, destination);
} finally {
  await rm(stage, { recursive: true, force: true });
}
