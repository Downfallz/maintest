import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync, statSync } from 'node:fs';
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
    const sections = [...html.matchAll(/<(?:article|figure) (?:class="moment" )?id="([^"]+)"/g)].map(match => match[1]);
    assert.deepEqual(sections, ['brute', 'predator', 'warped', 'round-1', 'round-6', 'round-12']);
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
