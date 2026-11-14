import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../showcase/dist/', import.meta.url));
const read = name => readFileSync(join(root, name), 'utf8');
const game = 'https://play.downfallinstinct.com/';
execFileSync(process.execPath, [fileURLToPath(new URL('../showcase/build.mjs', import.meta.url))], { env: { ...process.env, GAME_ORIGIN: game } });

for (const [lang, file, other] of [['en', 'index.html', '/fr/'], ['fr', 'fr/index.html', '/']]) {
  test(`${lang} ships its complete game introduction, artwork and two-way language navigation`, () => {
    const html = read(file);
    assert.ok(html.includes(`<html lang="${lang}">`));
    assert.equal(/<a class="language"[^>]* href="([^"]+)"/.exec(html)?.[1], other);
    const hrefs = [...html.matchAll(/<a\b[^>]* href="([^"]+)"/g)].map(match => match[1]);
    assert.deepEqual(hrefs.filter(href => href.startsWith('https:')), [game, game, game]);
    for (const href of hrefs.filter(href => href.startsWith('#'))) assert.ok(html.includes(`id="${href.slice(1)}"`), href);
    for (const [, path] of html.matchAll(/(?:href|src)="(\/(?:assets|fonts)\/[^"]+|\/[^"/]+\.(?:css|js))"/g))
      assert.ok(statSync(join(root, path)).size > 0, path);
    const panels = [...html.matchAll(/<figure[^>]+id="([^"]+)"[^>]* data-panel>/g)].map(match => match[1]);
    assert.deepEqual(panels, ['round-1', 'round-6', 'round-12', 'specimen-ancestral', 'specimen-warped',
      'specimen-necrotic', 'specimen-revenant', 'specimen-hybrid']);
    assert.doesNotMatch(html, /<iframe|\/api\/|accounts\.google/);
  });
}

for (const [file, free, advertising, inbox, tabletop] of [
  ['index.html', /No purchases, microtransactions or pay-to-win advantages/, /No ads or advertising trackers/, /No newsletters, promotional emails or email tracking/, /Maybe one day you’ll be able to take Downfall Instinct out of a box/],
  ['fr/index.html', /Aucun achat, aucune microtransaction et aucun avantage payant/, /Aucune pub ni aucun traceur publicitaire/, /Aucune infolettre, aucun courriel promotionnel ni suivi des courriels/, /Peut-être qu’un jour, tu pourras sortir Downfall Instinct de sa boîte/],
]) {
  test(`${file} includes the creator's influences and the game's commitments without scripting`, () => {
    const html = read(file);
    for (const text of [free, advertising, inbox, tabletop, /Counter-Strike/, /World of Warcraft/, /Magic: The Gathering/, /Skyrim/])
      assert.match(html, text);
    assert.match(html, file === 'index.html' ? /morning coffee.*come back that evening/ : /mon café.*revenir le soir/);
    assert.match(html, /<section id="story"/);
    assert.match(html, /<aside class="commitments"/);
    assert.equal((html.match(/class="play-note"/g) ?? []).length, 2);
  });
}

test('the published CSS has its local font and battlefield layers', () => {
  for (const css of ['branding.css', 'showcase.css'])
    for (const [, asset] of read(css).matchAll(/url\('(\/[^']+)'\)/g))
      assert.ok(statSync(join(root, asset)).isFile(), asset);
  assert.ok(statSync(join(root, 'fonts/OFL.txt')).isFile());
});

const pages = {
  en: ['index.html', 'mutations/index.html', 'privacy/index.html'],
  fr: ['fr/index.html', 'fr/mutations/index.html', 'fr/confidentialite/index.html'],
};

for (const [lang, documents] of Object.entries(pages)) {
  const guide = read(documents[1]), policy = read(documents[2]);
  test(`${lang} has working routes, fragments, local assets and policy links on every page`, () => {
    for (const [index, file] of documents.entries()) {
      const html = read(file);
      const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
      assert.equal(new Set(ids).size, ids.length, file);
      const policyRoute = '/' + documents[2].replace('index.html', '');
      assert.ok(html.includes(`href="${policyRoute}"`), file);
      const otherFile = pages[lang === 'en' ? 'fr' : 'en'][index];
      assert.equal(/<a class="language"[^>]* href="([^"]+)"/.exec(html)?.[1], '/' + otherFile.replace('index.html', ''));
      for (const [, href] of html.matchAll(/\b(?:href|src)="([^"]+)"/g)) {
        if (!href.startsWith('/') && !href.startsWith('#')) continue;
        const [path, fragment] = href.split('#');
        const dest = path.endsWith('/') ? path + 'index.html' : path || file;
        assert.ok(statSync(join(root, dest)).size > 0, href);
        if (fragment) assert.ok(read(dest).includes(`id="${fragment}"`), href);
      }
    }
  });

  test(`${lang} renders every playable mutation and spell from the game without revealing a second hybrid`, () => {
    const data = fileURLToPath(new URL('../data/', import.meta.url));
    const files = folder => readdirSync(join(data, folder), { recursive: true })
      .filter(file => file.endsWith('.json')).map(file => JSON.parse(readFileSync(join(data, folder, file), 'utf8')))
      .filter(item => item.enabled !== false);
    const actualIds = pattern => [...guide.matchAll(pattern)].map(m => m[1]).sort();
    const expected = folder => files(folder).map(item => item.id.split(':')[1]).sort();
    assert.deepEqual(actualIds(/<details[^>]+id="form-([^"]+)"/g), expected('Tiers'));
    assert.deepEqual(actualIds(/<li id="spell-([^"]+)"/g), expected('Spells'));
    assert.equal((guide.match(/class="field-art"/g) ?? []).length, 21);
    assert.equal((guide.match(/class="passive-rule"/g) ?? []).length, 3);
    assert.doesNotMatch(guide, /\/hybrids\//);
    assert.deepEqual(readdirSync(join(root, 'assets/evolutions/hybrids')), ['warped-revenant-brute.webp']);
    assert.match(read(documents[0]), /src="\/assets\/evolutions\/hybrids\/warped-revenant-brute\.webp"/);
    assert.match(guide, lang === 'fr' ? /Sur la créature qui lance le sort/ : /On the caster/);
  });

  test(`${lang} publishes account controls, retention and the privacy contact before sign-in`, () => {
    assert.match(policy, /mailto:contact@downfallinstinct\.com/);
    for (const value of ['downfall_session', '30', 'Google', 'Cloudflare', 'Azure', 'Settings → Account'])
      assert.ok(policy.includes(value), value);
    assert.doesNotMatch(policy, /<script|<iframe/);
  });
}

test('both the game sign-in and account privacy panel link to the public policy', () => {
  for (const file of ['../table/lobby.html', '../table/navigation.js'])
    assert.match(readFileSync(new URL(file, import.meta.url), 'utf8'), /href="https:\/\/downfallinstinct\.com\/privacy\/"/);
});
