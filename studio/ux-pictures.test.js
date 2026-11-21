// The picture kit's script and its files (ADR 0198, docs/art/page-pictures.md): what a page draws is declared in
// docs/art/page-pictures.json, derived once into table/page-art/ and table/page-art.js by
// scripts/ux/pictures.mjs, and held under the budget of docs/art/page-budget.json. Nothing is encoded here: the
// committed files are read, so the gate needs no image library.
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { PAGE_ART } from '../table/page-art.js';
import { asked, budgetOf, check, fileName, moduleText, origin, paintingSize, phoneSize, widthsOf, webpSize } from '../scripts/ux/pictures.mjs';

const root = new URL('../', import.meta.url);
const read = path => readFileSync(new URL(path, root));
const json = path => JSON.parse(read(path).toString('utf8'));
const authored = json('docs/art/page-pictures.json'), budget = json('docs/art/page-budget.json');

test('a picture is sent at the size it is drawn, once and twice, and never past its painting', () => {
  assert.deepEqual(widthsOf([308], 1536), [308, 616]);
  assert.deepEqual(widthsOf([533, 1440], 1672), [533, 1066, 1440, 1672], 'twice 1440 stops at the painting');
  assert.deepEqual(widthsOf([308, 620], 1536), [308, 620, 1240], 'a size within a tenth of a larger one is not kept twice');
  assert.deepEqual(widthsOf([2000], 1536), [1536]);
});

test('a WebP file says its width and its height, in each of its three forms', () => {
  const header = (tag, body) => Buffer.concat([Buffer.from('RIFF\0\0\0\0WEBP' + tag), Buffer.from([body.length, 0, 0, 0]), Buffer.from(body)]);
  assert.deepEqual(webpSize(header('VP8 ', [0, 0, 0, 0x9d, 0x01, 0x2a, 0x34, 0x01, 0xcd, 0x00])), { width: 308, height: 205 });
  assert.deepEqual(webpSize(header('VP8L', [0x2f, 0x33, 0x01, 0x33, 0x00])), { width: 308, height: 205 });
  assert.deepEqual(webpSize(header('VP8X', [0, 0, 0, 0, 0x33, 0x01, 0x00, 0xcc, 0x00, 0x00])), { width: 308, height: 205 });
  assert.throws(() => webpSize(Buffer.from('not a picture')), /not a WebP file/);
});

test('a painting the owner hands over as a PNG says its size too, so it can be derived and held', () => {
  const png = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13]), Buffer.from('IHDR'), Buffer.from([0, 0, 6, 0, 0, 0, 4, 0])]);
  assert.deepEqual(paintingSize(png), { width: 1536, height: 1024 });
  assert.deepEqual(paintingSize(read('table/battle-art/eclipse-battle.webp')), { width: 1672, height: 941 });
  const one = authored.pictures[0], drawn = { ...authored, pictures: [{ ...one, source: 'painting.png' }, ...authored.pictures.slice(1)] };
  const problems = check({ authored: drawn, art: PAGE_ART, read: path => (path === 'painting.png' ? png : read(path)), list: () => readdirSync(new URL('table/page-art', root)) });
  assert.doesNotMatch(problems.join('\n'), /neither a PNG nor a WebP/);
});

test('a file is named by its picture, its width and its own bytes, so a day-long cache never shows the old one', () => {
  assert.match(fileName('colossus', 308, Buffer.from('a')), /^colossus-308\.[0-9a-f]{8}\.webp$/);
  assert.notEqual(fileName('colossus', 308, Buffer.from('a')), fileName('colossus', 308, Buffer.from('b')));
});

test('a phone takes the smallest file that fills what it draws at its density', () => {
  const sizes = [{ width: 308, bytes: 10 }, { width: 616, bytes: 30 }, { width: 1240, bytes: 90 }];
  assert.equal(phoneSize(sizes, 308, 2).width, 616);
  assert.equal(phoneSize(sizes, 308, 1).width, 308);
  assert.equal(phoneSize(sizes, 900, 2).width, 1240, 'the largest when none fills it');
  const strip = [{ width: 1848, bytes: 10 }, { width: 3696, bytes: 30 }];
  assert.equal(phoneSize(strip, 308, 2, 6).width, 3696, 'a strip is as many pictures as it has frames');
});

test('a page counts the pictures asked for before any scroll, then all of them', () => {
  const art = {
    ground: { drawn: [400], frames: 1, pages: { home: 'largest' }, sizes: [{ width: 400, bytes: 20 }, { width: 800, bytes: 50 }] },
    beast: { drawn: [100], frames: 1, pages: { home: 'first', ladder: 'lazy' }, sizes: [{ width: 100, bytes: 5 }, { width: 200, bytes: 9 }] },
    round: { drawn: [100], frames: 1, pages: { home: 'lazy' }, sizes: [{ width: 200, bytes: 7 }] },
  };
  assert.deepEqual(budgetOf(art, 'home', 2), { first: 59, all: 66, largest: 1 });
  assert.deepEqual(budgetOf(art, 'ladder', 2), { first: 0, all: 9, largest: 0 });
});

test('a page that draws a picture at more than one width on the phone counts each of its files once', () => {
  const sizes = [{ width: 24, bytes: 2 }, { width: 40, bytes: 3 }, { width: 48, bytes: 5 }, { width: 80, bytes: 8 }];
  const art = {
    portrait: { drawn: [40, 24], frames: 1, pages: { battle: 'lazy', guide: 'lazy' }, phone: { battle: [40, 24] }, sizes },
    twice: { drawn: [40, 44], frames: 1, pages: { battle: 'lazy' }, phone: { battle: [40, 44] }, sizes },
  };
  assert.deepEqual(budgetOf({ portrait: art.portrait }, 'battle', 2), { first: 0, all: 13, largest: 0 }, 'the 80 and the 48 px files');
  assert.deepEqual(budgetOf({ portrait: art.portrait }, 'guide', 2), { first: 0, all: 8, largest: 0 }, 'a page not named draws the first width only');
  assert.deepEqual(budgetOf({ twice: art.twice }, 'battle', 2), { first: 0, all: 8, largest: 0 }, 'two widths that take one file count it once');
});

test('the committed pictures are the ones the declaration asks for: every file, its size, its bytes, its painting', () => {
  assert.deepEqual(check({ authored, art: PAGE_ART, read, list: () => readdirSync(new URL('table/page-art', root)) }), []);
});

test('a painting that changed, a file that went or a stranger in the folder is said', () => {
  const list = () => readdirSync(new URL('table/page-art', root));
  const stale = check({ authored, art: PAGE_ART, read: path => (path === authored.pictures[0].source ? Buffer.from('repainted') : read(path)), list });
  assert.match(stale.join('\n'), /eclipse-ground: its painting or its declaration changed/);
  const one = PAGE_ART[authored.pictures[0].name].sizes[0].file;
  assert.match(check({ authored, art: PAGE_ART, read, list: () => list().filter(file => file !== one) }).join('\n'), new RegExp(`${one.replaceAll('.', '\\.')} is missing`));
  assert.match(check({ authored, art: PAGE_ART, read, list: () => [...list(), 'stray.webp'] }).join('\n'), /stray\.webp is no picture of the declaration/);
  const name = authored.pictures[0].name, short = { ...PAGE_ART, [name]: { ...PAGE_ART[name], sizes: PAGE_ART[name].sizes.slice(1) } };
  assert.match(check({ authored, art: short, read, list: () => list().filter(file => file !== one) }).join('\n'), /eclipse-ground: it is sent at 533, 1066, 1440, 1672 px, and the module names 1066, 1440, 1672/);
  const fewer = { ...authored, pictures: authored.pictures.slice(1) };
  assert.match(check({ authored: fewer, art: PAGE_ART, read, list }).join('\n'), /eclipse-ground is derived but not declared/);
});

test('a focus is carried to the module, and held there, without deriving a file again', () => {
  const list = () => readdirSync(new URL('table/page-art', root)), one = authored.pictures[0];
  const focused = { ...authored, pictures: [{ ...one, focus: '30% 60%' }, ...authored.pictures.slice(1)] };
  assert.equal(origin(focused.pictures[0], [read(one.source)], authored.encoding), PAGE_ART[one.name].from, 'where CSS cuts is no part of the files');
  assert.match(check({ authored: focused, art: PAGE_ART, read, list }).join('\n'), new RegExp(`${one.name}: the module does not say what the declaration says`));
  const art = { ...PAGE_ART, [one.name]: { ...PAGE_ART[one.name], focus: '30% 60%' } };
  assert.deepEqual(check({ authored: focused, art, read, list }), []);
  assert.match(moduleText(art), new RegExp(`'${one.name}': \\{\\n    from: '[0-9a-f]+', frames: 1, drawn: \\[[\\d,]+\\], pages: \\{[^}]+\\}, focus: '30% 60%',\\n`));
});

test('the module the pages import is what the script writes from its entries', () => {
  assert.equal(read('table/page-art.js').toString('utf8'), moduleText(PAGE_ART));
  assert.equal(origin({ name: 'a', source: 'x', drawn: [1] }, [Buffer.from('p')], authored.encoding).length, 16);
});

test('every page of the declaration has a budget, and its pictures hold under it', () => {
  const pages = new Set(authored.pictures.flatMap(picture => Object.keys(picture.pages)));
  assert.deepEqual([...pages].sort(), Object.keys(budget.pages).sort(), 'a page with pictures has a budget, and a budget has a page');
  assert.equal(budget.largestPaintMs, 2500, 'ADR 0197');
  assert.equal(budget.shift, 0.1, 'ADR 0197');
  for (const [page, limit] of Object.entries(budget.pages)) {
    const measured = budgetOf(PAGE_ART, page, budget.density);
    assert.ok(measured.first <= limit.first, `${page}: ${measured.first} bytes before any scroll, over ${limit.first}`);
    assert.ok(measured.all <= limit.all, `${page}: ${measured.all} bytes of pictures, over ${limit.all}`);
    assert.ok(limit.first <= measured.first * 1.5 + 4096, `${page}: a budget half again over what is loaded holds nothing`);
    assert.ok(measured.largest <= 2, `${page}: one or two pictures are asked for first, or the hint means nothing`);
  }
});

test('the battle page may load the 320 KiB the windows\' brief measured, and none of it before any scroll', () => {
  // docs/art/journeys/windows.md: 322,218 B with every portrait at 24 and 40 px and every form at 64 px.
  assert.deepEqual(budget.pages['table/battle'], { first: 0, all: 327680 });
  const measured = budgetOf(PAGE_ART, 'table/battle', budget.density);
  assert.equal(measured.first, 0);
  // Without phone the page would count each portrait at 40 px alone and each form at the statistics' 144: phone
  // adds the 47 portraits' 48 px files, and counts the 21 forms at their 128 px files instead of the 288.
  const bare = Object.fromEntries(Object.entries(PAGE_ART).map(([name, { phone, ...entry }]) => [name, entry]));
  const fileOf = (prefix, width) => Object.entries(PAGE_ART).filter(([name]) => name.startsWith(prefix))
    .reduce((total, [, entry]) => total + entry.sizes.find(size => size.width === width).bytes, 0);
  assert.equal(measured.all - budgetOf(bare, 'table/battle', budget.density).all, fileOf('spell-', 48) + fileOf('mutation-', 128) - fileOf('mutation-', 288));
});

test('a declaration is refused when a role is unknown, a name is no file name, or a strip has one frame', () => {
  const list = () => readdirSync(new URL('table/page-art', root));
  const bad = change => check({ authored: { ...authored, pictures: [{ ...authored.pictures[0], ...change }, ...authored.pictures.slice(1)] }, art: PAGE_ART, read, list }).join('\n');
  assert.match(bad({ pages: { 'showcase/home': 'soon' } }), /largest, first or lazy/);
  assert.match(bad({ name: 'Eclipse Ground' }), /lower case, digits and hyphens/);
  assert.match(bad({ source: undefined, frames: ['table/battle-art/eclipse-battle.webp'] }), /a strip has at least two frames/);
  assert.match(bad({ drawn: [] }), /the width it is drawn at/);
  assert.match(bad({ phone: { 'table/sign-in': [533, 99] } }), /eclipse-ground: on table\/sign-in it is drawn at 99 px, a width its drawn does not list/);
  assert.match(bad({ phone: { 'table/sign-in': [] } }), /on table\/sign-in it is drawn at one width or more/);
  assert.match(bad({ phone: null }), /phone gives each page the widths it draws/);
  assert.match(bad({ phone: [533] }), /phone gives each page the widths it draws/);
  assert.match(bad({ phone: { 'showcase/home': [533] } }), /eclipse-ground: phone names showcase\/home, a page that does not draw it/);
  assert.match(check({ authored: { ...authored, pictures: [...authored.pictures, authored.pictures[0]] }, art: PAGE_ART, read, list }).join('\n'), /eclipse-ground is declared twice/);
  for (const focus of ['33% 60%', null, {}, []]) assert.match(bad({ focus }), /eclipse-ground: a focus is two percentages/, JSON.stringify(focus));
  assert.match(bad({ source: undefined, frames: ['a.webp', 'b.webp'], focus: '50% 50%' }), /eclipse-ground: a strip is never cut, and takes no focus/);
  assert.throws(() => webpSize(Buffer.concat([Buffer.from('RIFF\0\0\0\0WEBPVP8X'), Buffer.alloc(10)])), /not a WebP file/, 'a header cut short');
});

test('a brief\'s request is listed with its screen, its size, what holds its place and its prompt', () => {
  const brief = ['# The two doors', '', '## The sign-in panel (`sign-in-panel`)', '', '### Pictures', '',
    '| Picture | Drawn at | Answers | From |', '| --- | --- | --- | --- |',
    '| The ground. | 358 by 300 px | Where? | `table/battle-art/eclipse-battle.webp` |',
    '| Two teams facing each other. | 358 by 300 px | Who invites me? | asked of the owner; `table/battle-art/eclipse-battle.webp` holds its place |',
    '', 'Some words.', '', '```text', 'Two teams of three creatures.', 'Keep the centre readable.', '```', '', '### States'].join('\n');
  assert.deepEqual(asked(brief, 'docs/art/journeys/doors.md'), [{
    brief: 'docs/art/journeys/doors.md', screen: 'The sign-in panel', shows: 'Two teams facing each other.', drawn: '358 by 300 px',
    holds: 'table/battle-art/eclipse-battle.webp', prompt: 'Two teams of three creatures.\nKeep the centre readable.',
  }]);
  assert.deepEqual(asked('# A\n\n## B\n\n### Pictures\n\nNone.\n', 'x.md'), []);
});

test('every request of the briefs in the repository can be listed, and the doors\' duel is answered by the owner\'s painting', () => {
  const folder = new URL('docs/art/journeys/', root);
  const requests = readdirSync(folder).filter(name => name.endsWith('.md'))
    .flatMap(name => asked(readFileSync(new URL(name, folder), 'utf8'), `docs/art/journeys/${name}`));
  // docs/art/scene-paintings-plan.md, S2: the one request the doors had open is the owner's file, drawn on the sign-in.
  assert.equal(requests.filter(one => one.brief.endsWith('doors.md')).length, 0);
  assert.match(read('docs/art/journeys/doors.md').toString('utf8'), /\| The duel, for the invited friend:[^\n]*\| `docs\/art\/sources\/pages\/duel\.webp` \|/);
  assert.deepEqual(authored.pictures.find(one => one.name === 'duel')?.pages, { 'table/sign-in': 'largest' });
  for (const one of requests) assert.ok(one.prompt.length > 40 && existsSync(new URL(one.holds, root)), `${one.shows}: a prompt, and a painting that holds its place`);
});

test('every scene in the folder is in its register, at its size, and names only creatures and spells the content holds', () => {
  // docs/art/scene-paintings-plan.md, S1: the scenes are a register, each with its page in the plan.
  const folder = 'docs/art/sources/pages/scenes/';
  const files = readdirSync(new URL(folder, root)).filter(name => name.endsWith('.webp')).sort();
  const rows = read(`${folder}README.md`).toString('utf8').split('\n')
    .map(line => line.match(/^\| `([a-z0-9-]+\.webp)` \| (\d+) by (\d+) \|(.*)\|$/)).filter(Boolean);
  assert.deepEqual(rows.map(row => row[1]).sort(), files, 'a scene in the folder is a row of the register, and a row is a file');
  const walk = url => readdirSync(url, { withFileTypes: true })
    .flatMap(entry => entry.isDirectory() ? walk(new URL(`${entry.name}/`, url)) : entry.name.endsWith('.json') ? [new URL(entry.name, url)] : []);
  const ids = new Set(['Creatures', 'Tiers', 'Spells'].flatMap(kind => walk(new URL(`data/${kind}/`, root))).flatMap(url => [...readFileSync(url, 'utf8').matchAll(/"id":\s*"([^"]+)"/g)].map(match => match[1])));
  for (const [, file, width, height, rest] of rows) {
    assert.deepEqual(webpSize(read(folder + file)), { width: Number(width), height: Number(height) }, `${file}: the size the register gives`);
    const named = [...rest.split('|')[1].matchAll(/`([^`]+)`/g)].map(match => match[1]);
    for (const id of named) assert.match(id, /^(creature|tier|spell):[a-z0-9_]+:v\d+$/, `${file}: ${id} is no content id`);
    assert.ok(named.length > 0, `${file}: the register names what it shows by content id`);
    for (const id of named) assert.ok(ids.has(id), `${file}: ${id} is no id of data/`);
  }
  assert.match(read('docs/art/sources/pages/README.md').toString('utf8'), /`scenes\/`/);
});

test('the script and the kit are where a session looks for them', () => {
  const commands = read('docs/reference/commands.md').toString('utf8');
  assert.match(commands, /^### ux\/pictures\.mjs$/m);
  assert.match(read('scripts/README.md').toString('utf8'), /ux\/pictures\.mjs/);
  assert.match(read('docs/README.md').toString('utf8'), /art\/page-pictures\.md/);
});
