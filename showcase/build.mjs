import { cp, mkdir, mkdtemp, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { english, french, paths } from './copy.mjs';
import { page } from './page.mjs';

const source = new URL('../', import.meta.url);
const destination = fileURLToPath(new URL('./dist', import.meta.url));
const application = new URL(process.env.GAME_ORIGIN || 'https://play.downfallinstinct.com');
if (application.protocol !== 'https:' || application.href !== application.origin + '/' || application.username || application.password)
  throw new Error('GAME_ORIGIN must be an HTTPS origin without credentials, path, query or fragment.');

const json = async path => JSON.parse(await readFile(new URL(path, source), 'utf8'));
const art = await json('data/presentation/mutation-art.json');
const families = await Promise.all(paths.map(async path => Promise.all(path.map(async id => {
  const tier = await json(`data/Tiers/${id}.v1.json`);
  return { name: tier.name, ...art[tier.id] };
}))));

// Stage the complete output before replacing the last successful build.
const stage = await mkdtemp(destination + '-');
try {
  const copy = (from, to) => cp(new URL(from, source), join(stage, to));
  for (const dir of ['assets', 'fonts', 'fr']) await mkdir(join(stage, dir));
  const assets = [
    ['docs/art/branding.css', 'branding.css'],
    ...['showcase.css', 'gallery.js', '404.html'].map(file => [`showcase/${file}`, file]),
    ...['brand-mark.svg', 'icon.svg'].map(file => [`table/${file}`, `assets/${file}`]),
    ...['marcellus-latin-400.woff2', 'OFL.txt'].map(file => [`table/fonts/${file}`, `fonts/${file}`]),
    ['table/battle-art/eclipse-battle.webp', 'assets/eclipse-battle.webp'],
    ...['breach', 'broken-floor', 'shattered-sky'].map(id => [`table/battle-art/horizon/${id}.webp`, `assets/${id}.webp`]),
    ...families.flat().map(({ artwork }) => [`table/battle-art/evolutions/${artwork}.webp`, `assets/${artwork}.webp`]),
  ];
  await Promise.all(assets.map(([from, to]) => copy(from, to)));
  for (const [language, target] of [[english, 'index.html'], [french, 'fr/index.html']])
    await writeFile(join(stage, target), page(language, families, application.href));
  await rm(destination, { recursive: true, force: true });
  await rename(stage, destination);
} finally {
  await rm(stage, { recursive: true, force: true });
}
