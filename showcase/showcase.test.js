import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test, { after } from 'node:test';
import { GAME_ORIGIN, SITE_ORIGIN } from '../table/site-origins.js';

// Built into a folder of its own, so running the tests leaves the working tree's showcase/dist as it was.
const builder = fileURLToPath(new URL('./build.mjs', import.meta.url));
const workingDist = fileURLToPath(new URL('./dist', import.meta.url));
const stamp = () => [workingDist, join(workingDist, 'index.html')].map(path => existsSync(path) ? statSync(path).mtimeMs : null);
const before = stamp();
const root = join(mkdtempSync(join(tmpdir(), 'showcase-')), 'dist') + '/';
after(() => rmSync(join(root, '..'), { recursive: true, force: true }));
const environment = { ...process.env };
delete environment.GAME_ORIGIN;
execFileSync(process.execPath, [builder, root], { env: environment });
const read = name => readFileSync(join(root, name), 'utf8');
// The game's origin when nothing overrides it: the one the table names as its own.
const game = GAME_ORIGIN;

test('the build writes where it is told and leaves the working tree alone', () => {
  assert.ok(statSync(join(root, 'index.html')).size > 0);
  assert.deepEqual(stamp(), before);
});

// The build replaces its destination, so it refuses one that holds anything but an earlier build.
test('the build refuses a folder that is not an earlier build, and leaves it as it was', () => {
  const folder = mkdtempSync(join(tmpdir(), 'showcase-other-'));
  try {
    writeFileSync(join(folder, 'notes.txt'), 'keep me');
    const refuses = target => assert.throws(
      () => execFileSync(process.execPath, [builder, target], { env: environment, stdio: 'pipe' }), target);
    refuses(folder);
    refuses('--help');
    // A site of another kind with the same two files at its root is still not this build.
    writeFileSync(join(folder, 'index.html'), 'other site');
    writeFileSync(join(folder, '_headers'), '/*');
    refuses(folder);
    // Nor is a folder that hides a stranger beneath a name the build writes.
    const nested = mkdtempSync(join(tmpdir(), 'showcase-nested-'));
    mkdirSync(join(nested, 'assets'));
    writeFileSync(join(nested, 'assets', 'notes.txt'), 'keep me too');
    try {
      refuses(nested);
      assert.equal(readFileSync(join(nested, 'assets', 'notes.txt'), 'utf8'), 'keep me too');
    } finally {
      rmSync(nested, { recursive: true, force: true });
    }
    assert.equal(readFileSync(join(folder, 'notes.txt'), 'utf8'), 'keep me');
    assert.equal(readFileSync(join(folder, 'index.html'), 'utf8'), 'other site');
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

for (const [lang, file, other] of [['en', 'index.html', '/fr/'], ['fr', 'fr/index.html', '/']]) {
  test(`${lang} ships its complete game introduction, artwork and two-way language navigation`, () => {
    const html = read(file);
    assert.ok(html.includes(`<html lang="${lang}">`));
    assert.equal(/<a class="language"[^>]* href="([^"]+)"/.exec(html)?.[1], other);
    const hrefs = [...html.matchAll(/<a\b[^>]* href="([^"]+)"/g)].map(match => match[1]);
    assert.equal(hrefs.filter(href => href === game).length, 3);
    assert.deepEqual(hrefs.filter(href => href.startsWith('https:') && href !== game), [new URL('leaderboard', game).href]);
    const sections = [...html.matchAll(/<section\b[^>]* aria-labelledby="([^"]+)"/g)].map(match => match[1]);
    assert.deepEqual(sections.slice(0, 4), ['title', 'duel-title', 'world-title', 'mutation-title']);
    assert.match(html, /<a class="discover" href="#duel">/);
    for (const href of hrefs.filter(href => href.startsWith('#'))) assert.ok(html.includes(`id="${href.slice(1)}"`), href);
    for (const [, path] of html.matchAll(/(?:href|src)="(\/(?:assets|fonts|page-art)\/[^"]+|\/[^"/]+\.(?:css|js))"/g))
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
    for (const text of [free, advertising, inbox, tabletop, /World of Warcraft/, /Magic: The Gathering/, /Skyrim/])
      assert.match(html, text);
    assert.doesNotMatch(html, /Counter[ -]Strike/i);
    assert.match(html, file === 'index.html' ? /morning coffee.*come back that evening/ : /mon café.*revenir le soir/);
    assert.match(html, /<section id="story"/);
    assert.match(html, /<aside class="commitments"/);
    assert.equal((html.match(/class="play-note"/g) ?? []).length, 2);
  });
}

for (const [file, titles, rating, privateStats] of [
  ['index.html', ['Solo against a bot', 'Friendly PvP', 'Ranked PvP'], /public rating/, /private stats.*mutations.*spells/],
  ['fr/index.html', ['Solo contre un bot', 'PvP amical', 'PvP classé'], /cote publique/, /stats privées.*mutations.*sorts/],
]) {
  test(`${file} explains all three ways to play and links to the public ladder without sign-in`, () => {
    const html = read(file);
    const modes = html.match(/<section id="modes"[\s\S]*?<\/section>/)?.[0] ?? '';
    assert.deepEqual([...modes.matchAll(/<h3>([^<]+)<\/h3>/g)].map(match => match[1]), titles);
    assert.match(modes, rating);
    assert.match(modes, privateStats);
    assert.ok(modes.includes(`href="${new URL('leaderboard', game).href}"`));
    assert.match(html, /href="#modes"/);
  });
}

test('the published CSS has its local font and battlefield layers', () => {
  for (const css of ['branding.css', 'showcase.css'])
    for (const [, asset] of read(css).matchAll(/url\('(\/[^']+)'\)/g))
      assert.ok(statSync(join(root, asset)).isFile(), asset);
  assert.ok(statSync(join(root, 'fonts/OFL.txt')).isFile());
});

// The public rulebook draws the concept card as the game does: the table's .concept-card rule, copied whole into the
// published sheet, and the card in that rule's classes (#810).
test('the published sheet carries the table\'s concept card rule, and the rulebook draws its cards in its classes', () => {
  const identity = readFileSync(new URL('../table/identity.css', import.meta.url), 'utf8');
  const rule = identity.split('\n').filter(line => line.startsWith('.concept-card'));
  assert.ok(rule.length >= 4, 'identity.css still holds the concept card');
  assert.ok(read('showcase.css').trimEnd().endsWith(rule.join('\n')), 'showcase.css ends with the table\'s rule, line for line');
  for (const file of ['rules/index.html', 'fr/regles/index.html']) {
    const cards = read(file).match(/<aside class="rules-card[^"]*"[\s\S]*?<\/aside>/g) ?? [];
    assert.ok(cards.length > 0, file);
    for (const card of cards) {
      assert.match(card, /^<aside class="rules-card concept-card"/);
      for (const part of ['<p class="eyebrow">', '<h4 class="concept-card-title">', '<div class="concept-lines">', '<p class="concept-example">']) assert.ok(card.includes(part), `${file}: ${part}`);
    }
  }
  assert.doesNotMatch(read('showcase.css'), /rules-example|\.rules-card h4/);
});

const pages = {
  en: ['index.html', 'mutations/index.html', 'privacy/index.html', 'copyright/index.html', 'rules/index.html'],
  fr: ['fr/index.html', 'fr/mutations/index.html', 'fr/confidentialite/index.html', 'fr/droit-auteur/index.html', 'fr/regles/index.html'],
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
      const rightsRoute = '/' + documents[3].replace('index.html', '');
      assert.ok(html.includes(`href="${rightsRoute}"`), file);
      const rulesRoute = '/' + documents[4].replace('index.html', '');
      assert.ok(html.includes(`href="${rulesRoute}"`), file);
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
    const cards = new Map([...guide.matchAll(/<li id="spell-([^"]+)"[^>]*>([\s\S]*?)\n  <\/li>/g)].map(([, id, html]) => [id, html]));
    for (const spell of files('Spells')) {
      const card = cards.get(spell.id.split(':')[1]);
      assert.ok(card.includes(`</svg>${spell.energyCost} ${lang === 'fr' ? 'énergie' : 'energy'}</span>`), spell.id);
      assert.equal(card.includes('class="spell-chip crit"'), spell.criticalChance > 0, spell.id);
      if (spell.criticalChance > 0) assert.ok(card.includes(`${Math.round(spell.criticalChance * 100)}%`), spell.id);
      assert.equal(card.includes('class="caster"'), Boolean(spell.casterEffects?.length), spell.id);
    }
    const forms = new Map([...guide.matchAll(/<details[^>]+id="form-([^"]+)"[^>]*>([\s\S]*?)<\/details>/g)].map(([, id, html]) => [id, html]));
    for (const tier of files('Tiers')) {
      const form = forms.get(tier.id.split(':')[1]);
      assert.equal(form.includes('class="spell-chip initiative"'), Boolean(tier.initiativeBonus), tier.id);
      if (tier.initiativeBonus) assert.ok(form.includes(`${tier.initiativeBonus} initiative</span>`), tier.id);
    }
    assert.equal((guide.match(/class="field-art"/g) ?? []).length, 21);
    assert.equal((guide.match(/class="passive-rule"/g) ?? []).length, 3);
    assert.doesNotMatch(guide, /\/hybrids\//);
    // The one hybrid is the observation's, drawn by the picture kit: the field guide's portraits hold none.
    assert.ok(!existsSync(join(root, 'assets/evolutions/hybrids')));
    assert.match(read(documents[0]), /src="\/page-art\/specimen-warped-revenant-brute-\d+\.[0-9a-f]{8}\.webp"/);
    assert.match(guide, lang === 'fr' ? /Sur la créature qui lance le sort/ : /On the caster/);
  });

  test(`${lang} publishes account controls, retention and the privacy contact before sign-in`, () => {
    assert.match(policy, /mailto:contact@downfallinstinct\.com/);
    for (const value of ['downfall_session', '30', 'Google', 'Cloudflare', 'Azure', 'Settings → Account'])
      assert.ok(policy.includes(value), value);
    assert.doesNotMatch(policy, /<script|<iframe/);
  });

  test(`${lang} makes the permission contact and separate font licence available without scripts`, () => {
    const notice = read(documents[3]);
    for (const value of ['© 2026', 'mailto:contact@downfallinstinct.com', 'href="/fonts/OFL.txt"', 'Marcellus', 'Phaser'])
      assert.ok(notice.includes(value), value);
    assert.doesNotMatch(notice, /<script|<iframe/);
  });
}

// The public book (docs/tabletop/rulebook-plan.md R7): the table's rulebook, the same file, drawn as static HTML.
const plain = html => html.replace(/<[^>]+>/g, '').replaceAll('&lt;', '<').replaceAll('&quot;', '"').replaceAll('&amp;', '&');
const spanText = spans => spans.map(span => span.text).join('');
const blockTexts = block => block.type === 'paragraph' ? [spanText(block.spans)]
  : block.type === 'list' ? block.items.map(spanText)
    : [...block.header, ...block.rows.flat()].map(spanText);

for (const [lang, file, bookFile] of [['en', 'rules/index.html', 'rules.json'], ['fr', 'fr/regles/index.html', 'rules.fr.json']]) {
  const book = JSON.parse(readFileSync(new URL(`../table/${bookFile}`, import.meta.url), 'utf8'));

  test(`${lang} publishes every part, section, block and card of the table's book, in its order`, () => {
    const html = read(file);
    assert.equal(book.language, lang);
    assert.match(html, new RegExp(`<html lang="${lang}">`));
    assert.doesNotMatch(html, /<script|<iframe/);
    const order = [...html.matchAll(/<section class="rules-(?:part|section)" id="([^"]+)"/g)].map(m => m[1]);
    assert.deepEqual(order, book.parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)]));
    // Each part's opening and each section, read in its own slice of the page, so a dropped block cannot pass on
    // the same words elsewhere; and every line in its order.
    const slice = id => {
      const at = html.indexOf(` id="${id}"`);
      const next = html.slice(at + 1).search(/<section class="rules-(?:part|section)" id="/);
      return plain(next === -1 ? html.slice(at) : html.slice(at, at + 1 + next));
    };
    const inOrder = (where, lines) => {
      let from = 0;
      for (const line of lines) {
        const found = where.indexOf(line, from);
        assert.ok(found >= 0, line);
        from = found + line.length;
      }
    };
    for (const part of book.parts) {
      inOrder(slice(part.id), [`${part.number} ${part.title}`, ...part.blocks.flatMap(blockTexts)]);
      for (const section of part.sections) {
        const card = section.card ? [section.card.title, ...section.card.lines, section.card.example] : [];
        inOrder(slice(section.id), [`${section.number} ${section.title}`, ...section.blocks.flatMap(blockTexts), ...card]);
      }
    }
  });

  test(`${lang} writes no id of its own beyond those its anchors are kept from`, async () => {
    const { PAGE_IDS } = await import('./rules.mjs');
    const html = read(file);
    const ofTheBook = new Set([...book.parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)]),
      ...Object.keys(book.moved), ...book.cards.map(entry => entry.id)]);
    const own = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]).filter(id => !ofTheBook.has(id));
    assert.deepEqual([...new Set(own)].sort(), [...PAGE_IDS].sort());
  });

  test(`${lang} lists the book's contents, its changes latest first, and lands a card's link on its section`, () => {
    const html = read(file), text = plain(html);
    const contents = html.match(/<nav id="rules-contents"[\s\S]*?<\/nav>/)?.[0] ?? '';
    const linked = [...contents.matchAll(/href="#([^"]+)"/g)].map(m => m[1]);
    for (const part of book.parts) for (const id of [part.id, ...part.sections.map(section => section.id)]) assert.ok(linked.includes(id), id);
    const dates = [...html.matchAll(/<li><time datetime="(\d{4}-\d{2}-\d{2})">/g)].map(m => m[1]);
    assert.equal(dates.length, book.changes.length);
    assert.deepEqual(dates, [...dates].sort().reverse());
    for (const change of book.changes) assert.ok(text.includes(change.text), change.text);
    for (const card of book.cards) {
      const at = html.indexOf(`id="${card.id}"`), section = html.indexOf(`<section class="rules-section" id="${card.section}"`);
      assert.ok(at >= section && section >= 0, card.id);
      const next = html.indexOf('<section class="rules-section"', section + 1);
      assert.ok(next === -1 || at < next, card.id);
    }
    assert.ok(html.includes(`href="${new URL(`rules?lang=${lang}`, game).href}"`), 'the table\'s own page, where the numbers are played, in the language read here');
  });
}

test('the public book lands a moved id on its section and anchors a card only where the book and the page have no such id', async () => {
  const { rulesPage } = await import('./rules.mjs');
  const { english } = await import('./copy.mjs');
  const span = text => ({ text });
  const book = {
    language: 'en', drawnFrom: {}, moved: { 'old-tempo': 'older-tempo', 'older-tempo': 'tempo', 'gone-loop': 'gone-loop-2', 'gone-loop-2': 'gone-loop', 'what-changed': 'tempo' },
    cards: [{ id: 'tempo', section: 'tempo' }, { id: 'speed', section: 'tempo' }],
    changes: [{ date: '2026-11-01', text: 'First <change> & "quoted".', adrs: [] }],
    parts: [{ id: 'the-round', number: 3, title: 'The round', blocks: [], sections: [
      { id: 'tempo', number: '3.5', title: 'Tempo', sources: [], blocks: [{ type: 'paragraph', spans: [span('See '), { text: 'turn order', href: '#tempo' }, { text: ' and ', strong: true }, { text: 'this', href: 'javascript:alert(1)' }]}],
        card: { id: 'tempo', title: 'Tempo', lines: ['One line.'], example: 'An example.' } },
    ]}],
  };
  const html = rulesPage(english, book, game);
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
  assert.equal(new Set(ids).size, ids.length);
  for (const id of ['old-tempo', 'older-tempo', 'speed']) assert.ok(ids.includes(id), id);
  assert.ok(!ids.includes('gone-loop'), 'a redirect that lands nowhere is left out');
  const section = html.slice(html.indexOf('<section class="rules-section" id="tempo"'));
  assert.ok(section.includes('id="old-tempo"') && section.includes('id="speed"'));
  assert.ok(html.includes('First &lt;change> &amp; &quot;quoted&quot;.'));
  assert.ok(html.includes('<a href="#tempo">turn order</a>'));
  assert.ok(html.includes('<strong> and </strong>'));
  assert.doesNotMatch(html, /javascript:/);
});

const repository = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');

test('the game sign-in and account privacy panel link to the public policy on the apex', () => {
  assert.ok(repository('table/lobby.html').includes(`href="${new URL('privacy/', SITE_ORIGIN).href}"`));
  assert.match(repository('table/navigation.js'), /href="\$\{new URL\('privacy\/', SITE_ORIGIN\)\.href\}"/);
  assert.match(repository('table/navigation.js'), /href="\$\{SITE_ORIGIN\}">Game website/);
});

test('the game lobby and repository point to their copyright notices', () => {
  assert.ok(repository('table/lobby.html').includes(`href="${new URL('copyright/', SITE_ORIGIN).href}"`));
  assert.match(repository('README.md'), /\]\(COPYRIGHT\.md\)/);
  assert.match(repository('COPYRIGHT.md'), /table\/fonts\/OFL\.txt/);
});

// One place names the two origins (#612). The apex exists only on Cloudflare Pages, so a developer's table
// links to the published one on purpose; what must not happen is two sites disagreeing about each other.
test('the table and the showcase name the same two origins, from one module', () => {
  assert.equal(new URL(SITE_ORIGIN).href, SITE_ORIGIN);
  assert.equal(new URL(GAME_ORIGIN).href, GAME_ORIGIN);
  assert.equal(new URL(GAME_ORIGIN).hostname, `play.${new URL(SITE_ORIGIN).hostname}`);
  for (const file of ['index.html', 'fr/mutations/index.html'])
    assert.ok(read(file).includes(`<link rel="canonical" href="${new URL(file.replace('index.html', ''), SITE_ORIGIN).href}">`), file);
  assert.match(repository('table/navigation.js'), /import \{ SITE_ORIGIN \} from '\.\/site-origins\.js';/);
  const named = /https:\/\/(?:[a-z0-9-]+\.)*downfallinstinct\.com[^"'`\s<)]*/g;
  const sources = readdirSync(new URL('../table/', import.meta.url))
    .filter(name => /\.(?:html|js)$/.test(name) && !name.endsWith('.test.js') && name !== 'site-origins.js');
  for (const name of sources)
    for (const [url] of repository(`table/${name}`).matchAll(named))
      assert.ok(url.startsWith(SITE_ORIGIN) || url.startsWith(GAME_ORIGIN), `${name} names ${url}`);
  for (const name of readdirSync(new URL('./', import.meta.url)).filter(name => /\.m?js$/.test(name) && !name.endsWith('.test.js')))
    assert.doesNotMatch(repository(`showcase/${name}`), /https:\/\/(?:play\.)?downfallinstinct\.com/, name);
});

// The policy promises what infra/main.bicep configures (#615): a change to either fails here, not in public.
test('the privacy policy promises the retention periods main.bicep configures', () => {
  const bicep = repository('infra/main.bicep');
  const block = name => bicep.slice(bicep.indexOf(`resource ${name} `)).split(/\n}\n/)[0];
  const logs = /retentionInDays: (\d+)/.exec(block('logs'))?.[1];
  // A policy turned off keeps nothing, whatever its days say.
  const blobs = /deleteRetentionPolicy: \{\s*enabled: true\s+days: (\d+)/.exec(block('blobs'))?.[1];
  const containers = /containerDeleteRetentionPolicy: \{\s*enabled: true\s+days: (\d+)/.exec(block('blobs'))?.[1];
  assert.ok(logs && blobs, 'main.bicep names the log and soft-delete retention');
  assert.equal(containers, blobs, 'deleted blobs and deleted containers are kept the same time');
  const policy = { en: read('privacy/index.html'), fr: read('fr/confidentialite/index.html') };
  assert.ok(policy.en.includes(`console logs is ${logs} days`), 'en logs');
  assert.ok(policy.en.includes(`recoverable in Azure storage for ${blobs} days`), 'en blobs');
  assert.ok(policy.fr.includes(`journaux de console de l’hébergement du jeu sont conservés ${logs} jours`), 'fr logs');
  assert.ok(policy.fr.includes(`récupérables pendant ${blobs} jours`), 'fr blobs');
});

test('the apex ships a strict content policy that every page keeps to', () => {
  const headers = read('_headers');
  for (const value of ["script-src 'self'", "style-src 'self'", "frame-ancestors 'none'", 'X-Content-Type-Options: nosniff'])
    assert.ok(headers.includes(value), value);
  const pages = readdirSync(root, { recursive: true }).filter(name => String(name).endsWith('.html'));
  assert.ok(pages.length >= 9);
  for (const name of pages) {
    const html = read(String(name));
    assert.doesNotMatch(html, /<script(?![^>]*\bsrc=)[^>]*>|\sstyle="|\son[a-z]+="/, String(name));
  }
});

// UX6 (docs/art/ux-craft-plan.md, docs/art/journeys/doors.md): the first door shows the game played.
const picturesOf = html => [...html.matchAll(/<img\s[^>]*>/g)].map(([tag]) => tag);
const attribute = (tag, name) => new RegExp(`\\s${name}="([^"]*)"`).exec(tag)?.[1];

for (const [lang, file] of [['en', 'index.html'], ['fr', 'fr/index.html']]) {
  test(`${lang}: a round is seen played, each move a picture of the real battle view with its name for a screen reader`, () => {
    const duel = /<section id="duel"[\s\S]*?<\/section>/.exec(read(file))[0];
    const moves = [...duel.matchAll(/<li>([\s\S]*?)<\/li>/g)].map(([, html]) => html);
    assert.equal(moves.length, 3);
    for (const [index, move] of moves.entries()) {
      assert.match(move, /^<figure class="move-frame">/, 'the picture comes before the move\'s name');
      const [tag] = picturesOf(move);
      assert.match(attribute(tag, 'src'), new RegExp(`^/page-art/round-${['mutate', 'tempo', 'cast'][index]}-`));
      assert.equal(attribute(tag, 'loading'), 'lazy', 'below the first screen, it loads when near');
    }
    // The Cast strip is one element with one name; its frames have none.
    assert.match(moves[2], /<span class="motion-strip" data-frames="6" data-still="4" role="img" aria-label="[^"]{20,}"><img [^>]*alt=""/);
    for (const move of moves.slice(0, 2)) assert.ok(attribute(picturesOf(move)[0], 'alt').split(' ').filter(word => /\p{L}/u.test(word)).length <= 14);
  });

  test(`${lang}: every picture of the home is the kit's, sent at the size it is drawn, the ground asked for first`, () => {
    const html = read(file), tags = picturesOf(html);
    const kit = tags.filter(tag => attribute(tag, 'src').startsWith('/page-art/'));
    // The brand's rune is a drawing; every painting comes from the kit, with its sizes.
    assert.deepEqual(tags.filter(tag => !kit.includes(tag)).map(tag => attribute(tag, 'src')), ['/assets/brand-mark.svg', '/assets/brand-mark.svg']);
    for (const tag of kit) {
      assert.ok(attribute(tag, 'srcset') && attribute(tag, 'sizes'), tag);
      for (const [, path] of attribute(tag, 'srcset').matchAll(/(\/page-art\/\S+)/g)) assert.ok(existsSync(join(root, path)), path);
    }
    const first = kit.filter(tag => attribute(tag, 'fetchpriority') === 'high').map(tag => attribute(tag, 'src'));
    assert.equal(first.length, 2);
    assert.match(first[0], /^\/page-art\/showcase-ground-/, 'the ground is the largest paint, in the HTML');
    assert.match(first[1], /^\/page-art\/colossus-/);
    // No painting waits for the sheet: the stylesheet names none.
    assert.doesNotMatch(read('showcase.css'), /url\('\/assets\/[^']+\.webp'\)/);
  });

  test(`${lang}: one filled action, a breathing Colossus, and a language link named by its own letters`, () => {
    const html = read(file);
    assert.equal((html.match(/class="gold-button"/g) ?? []).length, 1);
    assert.match(html, /<img class="picture hero-beast motion-breath"/);
    assert.match(html, /<script type="module" src="\/motion\.js"><\/script>/);
    assert.ok(statSync(join(root, 'motion.js')).size > 0);
    const [, label, letters] = /<a class="language"[^>]* aria-label="([^"]+)"[^>]*>([A-Z]{2})<\/a>/.exec(html);
    assert.ok(label.startsWith(letters), `${label} starts with ${letters}`);
  });
}

test('the showcase sends the kit\'s files only, none twice, and no picture smaller than 11 px of text beside it', () => {
  const sent = readdirSync(join(root, 'page-art'));
  assert.ok(sent.length > 0);
  for (const name of sent) assert.match(name, /^[a-z0-9-]+-\d+\.[0-9a-f]{8}\.webp$/);
  assert.ok(!existsSync(join(root, 'assets/breach.webp')) && !existsSync(join(root, 'assets/shattered-sky.webp')));
  const css = read('showcase.css');
  for (const [, size] of css.matchAll(/\.specimen span \{[^}]*font-size: (\d+)px/g)) assert.ok(Number(size) >= 11, `the specimen's line at ${size} px`);
});
