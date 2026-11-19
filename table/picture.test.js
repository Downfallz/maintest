// The picture kit's form (ADR 0198): an `<img>` with its width, its height and its sizes, the largest paint asked
// for first, a strip as one named element. And what holds every page to it: a picture a page names has its
// dimensions, and one of the kit's is a file the declaration derived.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { PAGE_ART } from './page-art.js';
import { PAGE_ART_BASE, STRIP_FRAMES, pictureAttributes, pictureElement, pictureHtml, stripElement, stripHtml } from './picture.js';

const root = new URL('../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');

const ART = {
  beast: { frames: 1, drawn: [308, 620], pages: { home: 'largest', ladder: 'lazy', door: 'first' }, sizes: [
    { file: 'beast-308.aa.webp', width: 308, height: 205, bytes: 1 }, { file: 'beast-620.bb.webp', width: 620, height: 413, bytes: 2 }] },
  cast: { frames: 6, drawn: [358], pages: { home: 'lazy' }, sizes: [
    { file: 'cast-2148.cc.webp', width: 2148, height: 201, bytes: 3 }, { file: 'cast-4296.dd.webp', width: 4296, height: 402, bytes: 4 }] },
};

function fakeDocument() {
  const element = tag => ({ tag, attributes: {}, children: [], className: '', setAttribute(name, value) { this.attributes[name] = value; }, append(...nodes) { this.children.push(...nodes); } });
  return { createElement: element };
}

test('a picture is an image with its width, its height and the sizes it is sent at', () => {
  assert.equal(pictureHtml('beast', { page: 'door', art: ART }),
    '<img src="/page-art/beast-308.aa.webp" srcset="/page-art/beast-308.aa.webp 308w, /page-art/beast-620.bb.webp 620w" sizes="308px" width="308" height="205" alt="" decoding="async">');
});

test('the largest paint is asked for first and never lazy; a picture below the first screen loads when near', () => {
  const largest = pictureAttributes('beast', { page: 'home', art: ART }), lazy = pictureAttributes('beast', { page: 'ladder', art: ART });
  assert.equal(largest.fetchpriority, 'high');
  assert.equal(largest.loading, undefined);
  assert.equal(lazy.loading, 'lazy');
  assert.equal(lazy.fetchpriority, undefined);
});

test('a page says its own width, its words for the picture and its folder', () => {
  const html = pictureHtml('beast', { page: 'home', art: ART, alt: 'A Colossus, "armoured" & low', sizes: '(min-width: 900px) 620px, 79vw', base: '/assets/pages/', class: 'hero-beast' });
  assert.match(html, /^<img class="hero-beast" src="\/assets\/pages\/beast-308\.aa\.webp"/);
  assert.match(html, /sizes="\(min-width: 900px\) 620px, 79vw"/);
  assert.match(html, /alt="A Colossus, &quot;armoured&quot; &amp; low"/);
});

test('a picture nobody declared, or not for this page, is a fault and not a broken image', () => {
  assert.throws(() => pictureHtml('ghost', { page: 'home', art: ART }), /ghost is not a picture/);
  assert.throws(() => pictureHtml('beast', { page: 'stats', art: ART }), /beast is not declared for stats/);
});

test('a page that builds itself gets the same picture as an element', () => {
  const image = pictureElement(fakeDocument(), 'beast', { page: 'home', art: ART, class: 'beast' });
  assert.equal(image.tag, 'img');
  assert.equal(image.className, 'beast');
  assert.deepEqual(image.attributes, Object.fromEntries(Object.entries(pictureAttributes('beast', { page: 'home', art: ART })).map(([name, value]) => [name, String(value)])));
});

test('a strip is one element with one name, its frames in one file as wide as all of them', () => {
  const html = stripHtml('cast', { page: 'home', art: ART, alt: 'A creature casts, and the blow lands.' });
  assert.match(html, /^<span class="motion-strip" data-frames="6" role="img" aria-label="A creature casts, and the blow lands\."><img /);
  assert.match(html, /src="\/page-art\/cast-2148\.cc\.webp"/);
  assert.match(html, /sizes="2148px" width="2148" height="201" alt="" decoding="async" loading="lazy"><\/span>$/);
  assert.match(stripHtml('cast', { page: 'home', art: ART }), /^<span class="motion-strip" data-frames="6" aria-hidden="true">/);
  const element = stripElement(fakeDocument(), 'cast', { page: 'home', art: ART, alt: 'A cast.' });
  assert.equal(element.attributes['data-frames'], '6');
  assert.equal(element.children[0].attributes.alt, '');
  assert.throws(() => stripHtml('beast', { page: 'home', art: ART }), /not a strip/);
});

test('the sheet steps a strip of every count the form allows, and nothing in the form needs an inline style', () => {
  const sheet = read('table/identity.css');
  for (let frames = 2; frames <= STRIP_FRAMES; frames++) assert.ok(sheet.includes(`.motion-strip[data-frames="${frames}"] { --frames: ${frames}; }`), `${frames} frames`);
  // The showcase's policy refuses a style attribute (showcase/_headers).
  assert.doesNotMatch(stripHtml('cast', { page: 'home', art: ART }) + pictureHtml('beast', { page: 'home', art: ART }), /style=/);
});

test('every picture of the kit is declared with the files a page is given', () => {
  for (const [name, entry] of Object.entries(PAGE_ART)) for (const page of Object.keys(entry.pages)) {
    const attributes = pictureAttributes(name, { page });
    assert.ok(attributes.src.startsWith(PAGE_ART_BASE) && attributes.width > 0 && attributes.height > 0, `${name} on ${page}`);
  }
});

// What a page names: the pages of the table as they are served, and the showcase's as its build writes them.
const sources = [
  ...readdirSync(new URL('table/', root)).filter(name => /\.(html|js)$/.test(name) && !name.endsWith('.test.js')).map(name => `table/${name}`),
  ...readdirSync(new URL('showcase/', root)).filter(name => /\.(html|mjs|js)$/.test(name) && !name.endsWith('.test.js')).map(name => `showcase/${name}`),
];

test('every picture a page names has its width and its height, so its room is held', () => {
  // picture.js writes its own from the derived module, which the tests above hold.
  const images = sources.filter(path => path !== 'table/picture.js').flatMap(path => [...read(path).matchAll(/<img\s[^>]*>/g)].map(([tag]) => ({ path, tag })));
  assert.ok(images.length > 0);
  for (const { path, tag } of images) assert.ok(/\swidth="[^"]+"/.test(tag) && /\sheight="[^"]+"/.test(tag), `${path}: ${tag}`);
});

test('a page reaches a derived picture through the kit, never by its file name', () => {
  for (const path of sources.filter(path => !['table/page-art.js', 'table/picture.js'].includes(path)))
    assert.doesNotMatch(read(path), /page-art\/[\w-]+\.[0-9a-f]{8}\.webp/, `${path} names a derived file: its name changes when it is derived again`);
});
