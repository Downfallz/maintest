// The picture kit's form (ADR 0198): an `<img>` with its width, its height and its sizes, the largest paint asked
// for first, a strip as one named element. And what holds every page to it: a picture a page names has its
// dimensions, and one of the kit's is a file the declaration derived.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { PAGE_ART } from './page-art.js';
import { FOCUS_STEP, PAGE_ART_BASE, SCENE_FORMS, STRIP_FRAMES, focusAttributes, pictureAttributes, pictureElement, pictureHtml, sceneElement, sceneHtml, stripElement, stripHtml } from './picture.js';

const root = new URL('../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');

const ART = {
  beast: { frames: 1, drawn: [308, 620], pages: { home: 'largest', ladder: 'lazy', door: 'first' }, sizes: [
    { file: 'beast-308.aa.webp', width: 308, height: 205, bytes: 1 }, { file: 'beast-620.bb.webp', width: 620, height: 413, bytes: 2 }] },
  scene: { frames: 1, drawn: [533], focus: '30% 60%', pages: { home: 'lazy' }, sizes: [
    { file: 'scene-533.ee.webp', width: 533, height: 300, bytes: 5 }] },
  cast: { frames: 6, drawn: [358], pages: { home: 'lazy' }, sizes: [
    { file: 'cast-2148.cc.webp', width: 2148, height: 201, bytes: 3 }, { file: 'cast-4296.dd.webp', width: 4296, height: 402, bytes: 4 }] },
};

function fakeDocument() {
  const element = tag => ({ tag, attributes: {}, children: [], className: '', setAttribute(name, value) { this.attributes[name] = value; }, append(...nodes) { this.children.push(...nodes); } });
  return { createElement: element, createTextNode: text => ({ text }) };
}

test('a picture is an image with its width, its height and the sizes it is sent at', () => {
  assert.equal(pictureHtml('beast', { page: 'door', art: ART }),
    '<img class="picture" src="/page-art/beast-308.aa.webp" srcset="/page-art/beast-308.aa.webp 308w, /page-art/beast-620.bb.webp 620w" sizes="308px" width="308" height="205" alt="" decoding="async">');
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
  assert.match(html, /^<img class="picture hero-beast" src="\/assets\/pages\/beast-308\.aa\.webp"/);
  assert.match(html, /sizes="\(min-width: 900px\) 620px, 79vw"/);
  assert.match(html, /alt="A Colossus, &quot;armoured&quot; &amp; low"/);
});

test('a picture nobody declared, or not for this page, is a fault and not a broken image', () => {
  assert.throws(() => pictureHtml('ghost', { page: 'home', art: ART }), /ghost is not a picture/);
  assert.throws(() => pictureHtml('beast', { page: 'stats', art: ART }), /beast is not declared for stats/);
});

test('a painting cut by its box keeps its subject in view: its focus is where the cut centres, and no file changes', () => {
  assert.match(pictureHtml('scene', { page: 'home', art: ART }), /^<img class="picture" src="\/page-art\/scene-533\.ee\.webp" .* data-focus-x="30" data-focus-y="60">$/);
  assert.doesNotMatch(pictureHtml('beast', { page: 'home', art: ART }), /data-focus/, 'without a focus, the centre, and nothing written');
  // One scene in two boxes: the page that places it names where this one cuts.
  assert.deepEqual(focusAttributes('0% 100%'), { 'data-focus-x': '0', 'data-focus-y': '100' });
  const image = pictureElement(fakeDocument(), 'scene', { page: 'home', art: ART, focus: '75% 25%' });
  assert.equal(image.attributes['data-focus-x'], '75');
  assert.equal(image.attributes['data-focus-y'], '25');
  for (const focus of ['05% 50%', '50% 005%', null, '30%', '30% 60', '33% 60%', '-5% 50%', '105% 50%', 'left top', 30]) assert.throws(() => focusAttributes(focus), /a focus is two percentages/, String(focus));
  assert.throws(() => pictureHtml('scene', { page: 'home', art: ART, focus: null }), /a focus is two percentages/, 'a null focus is refused, not the declaration\'s');
  assert.throws(() => stripHtml('cast', { page: 'home', art: ART, focus: '50% 50%' }), /a strip is never cut/);
});

test('the sheet places the cut at every focus the form allows, and nothing in the form needs an inline style', () => {
  const sheet = read('table/identity.css');
  for (let at = 0; at <= 100; at += FOCUS_STEP) for (const axis of ['x', 'y']) assert.ok(sheet.includes(`.picture[data-focus-${axis}="${at}"] { --focus-${axis}: ${at}%; }`), `${axis} ${at}`);
  assert.match(sheet, /:where\(\.picture\[data-focus-x\], \.picture\[data-focus-y\]\) \{ object-position: var\(--focus-x, 50%\) var\(--focus-y, 50%\); \}/);
  // The showcase's policy refuses a style attribute (showcase/_headers).
  assert.doesNotMatch(pictureHtml('scene', { page: 'home', art: ART, focus: '10% 90%' }), /style=/);
});

test('a page that builds itself gets the same picture as an element', () => {
  const image = pictureElement(fakeDocument(), 'beast', { page: 'home', art: ART, class: 'beast' });
  assert.equal(image.tag, 'img');
  assert.equal(image.className, 'picture beast');
  assert.deepEqual(image.attributes, Object.fromEntries(Object.entries(pictureAttributes('beast', { page: 'home', art: ART })).map(([name, value]) => [name, String(value)])));
});

test('a strip is one element with one name, its frames in one file as wide as all of them', () => {
  const html = stripHtml('cast', { page: 'home', art: ART, alt: 'A creature casts, and the blow lands.' });
  assert.match(html, /^<span class="motion-strip" data-frames="6" role="img" aria-label="A creature casts, and the blow lands\."><img class="picture" /);
  assert.match(html, /src="\/page-art\/cast-2148\.cc\.webp"/);
  assert.match(html, /sizes="2148px" width="2148" height="201" alt="" decoding="async" loading="lazy"><\/span>$/);
  assert.match(stripHtml('cast', { page: 'home', art: ART }), /^<span class="motion-strip" data-frames="6" aria-hidden="true">/);
  const element = stripElement(fakeDocument(), 'cast', { page: 'home', art: ART, alt: 'A cast.' });
  assert.equal(element.attributes['data-frames'], '6');
  assert.equal(element.children[0].attributes.alt, '');
  assert.throws(() => stripHtml('beast', { page: 'home', art: ART }), /not a strip/);
});

test('a strip names the frame it rests on under less motion, the first unless told (ADR 0199)', () => {
  assert.match(stripHtml('cast', { page: 'home', art: ART, still: 4 }), /^<span class="motion-strip" data-frames="6" data-still="4" aria-hidden="true">/);
  assert.doesNotMatch(stripHtml('cast', { page: 'home', art: ART, still: 1 }), /data-still/, 'the first frame is the default and is not written');
  assert.equal(stripElement(fakeDocument(), 'cast', { page: 'home', art: ART, still: 6 }).attributes['data-still'], '6');
  for (const still of [0, 7, 2.5]) assert.throws(() => stripHtml('cast', { page: 'home', art: ART, still }), /rests on a frame from 1 to 6/);
  const sheet = readFileSync(new URL('identity.css', import.meta.url), 'utf8');
  for (let frame = 2; frame <= STRIP_FRAMES; frame++) assert.ok(sheet.includes(`.motion-strip[data-still="${frame}"] { --still: ${frame - 1}; }`), `frame ${frame}`);
  // Under the system's wish and under the account's: a still pose, set outside full motion.
  assert.match(sheet, /@media \(prefers-reduced-motion: reduce\) \{\s*\.motion-strip\[data-still\] > img \{ margin-left: calc\(var\(--still\) \* -100%\); \}/);
  assert.match(sheet, /:root\[data-motion="less"\] \.motion-strip\[data-still\] > img \{ margin-left: calc\(var\(--still\) \* -100%\); \}/);
});

test('the sheet steps a strip of every count the form allows, and nothing in the form needs an inline style', () => {
  const sheet = read('table/identity.css');
  for (let frames = 2; frames <= STRIP_FRAMES; frames++) assert.ok(sheet.includes(`.motion-strip[data-frames="${frames}"] { --frames: ${frames}; }`), `${frames} frames`);
  // The showcase's policy refuses a style attribute (showcase/_headers).
  assert.doesNotMatch(stripHtml('cast', { page: 'home', art: ART }) + pictureHtml('beast', { page: 'home', art: ART }), /style=/);
});

test('a scene takes one of three forms: a head, a ground or a plate', () => {
  assert.deepEqual(SCENE_FORMS, ['head', 'ground', 'plate']);
  assert.throws(() => sceneHtml('scene', { page: 'home', art: ART, form: 'banner' }), /a scene is a head, a ground or a plate, not banner/);
  assert.throws(() => sceneHtml('scene', { page: 'home', art: ART }), /a scene is a head, a ground or a plate, not undefined/);
  assert.throws(() => sceneHtml('cast', { page: 'home', art: ART, form: 'head' }), /a strip is never a scene/);
});

test('a head is a band behind its words, its painting a decoration, its scrim on the side the words sit', () => {
  const html = sceneHtml('scene', { page: 'home', art: ART, form: 'head', words: '<p class="eyebrow">Friends</p><h2>Invite</h2>' });
  assert.match(html, /^<div class="scene scene-head" data-side="start"><img class="picture" src="\/page-art\/scene-533\.ee\.webp" [^>]*alt="" [^>]*data-focus-x="30" data-focus-y="60"><div class="scene-words"><p class="eyebrow">Friends<\/p><h2>Invite<\/h2><\/div><\/div>$/);
  assert.match(sceneHtml('scene', { page: 'home', art: ART, form: 'head', side: 'end' }), /^<div class="scene scene-head" data-side="end">/);
  assert.throws(() => sceneHtml('scene', { page: 'home', art: ART, form: 'head', side: 'left' }), /a head's words sit at its start or its end, not left/);
  assert.throws(() => sceneHtml('scene', { page: 'home', art: ART, form: 'ground', side: 'end' }), /only a head has a side/);
  assert.throws(() => sceneHtml('scene', { page: 'home', art: ART, form: 'head', alt: 'A pool.' }), /a head's or a ground's painting is a decoration/);
});

test('a ground is a card\'s painting behind its words, and goes under less data: the card keeps its slate', () => {
  assert.match(sceneHtml('beast', { page: 'ladder', art: ART, form: 'ground', words: '<p>Waiting</p>', saveData: false }),
    /^<div class="scene scene-ground"><img class="picture" [^>]*loading="lazy"><div class="scene-words"><p>Waiting<\/p><\/div><\/div>$/);
  assert.equal(sceneHtml('beast', { page: 'ladder', art: ART, form: 'ground', words: '<p>Waiting</p>', saveData: true }),
    '<div class="scene scene-ground" data-saved=""><div class="scene-words"><p>Waiting</p></div></div>');
  // A head and a plate say something: they stay under less data.
  assert.match(sceneHtml('scene', { page: 'home', art: ART, form: 'head', saveData: true }), /<img /);
});

test('a plate is a figure, its painting named and its words under it', () => {
  assert.match(sceneHtml('scene', { page: 'home', art: ART, form: 'plate', alt: 'A claw on an arch.', words: 'Nothing reaches us here.' }),
    /^<figure class="scene scene-plate"><img class="picture" [^>]*alt="A claw on an arch\." [^>]*><figcaption class="scene-words">Nothing reaches us here\.<\/figcaption><\/figure>$/);
  assert.match(sceneHtml('scene', { page: 'home', art: ART, form: 'plate', alt: 'A claw.' }), /^<figure class="scene scene-plate"><img [^>]*><\/figure>$/);
});

test('a page that builds itself gets the same scene as an element, its words appended where they sit', () => {
  const doc = fakeDocument(), title = { tag: 'h2' };
  const head = sceneElement(doc, 'scene', { page: 'home', art: ART, form: 'head', side: 'end', words: [title, 'Friends'] });
  assert.equal(head.tag, 'div');
  assert.equal(head.className, 'scene scene-head');
  assert.equal(head.attributes['data-side'], 'end');
  const [image, words] = head.children;
  assert.equal(image.tag, 'img');
  assert.equal(image.attributes.alt, '');
  assert.equal(words.className, 'scene-words');
  assert.deepEqual(words.children, [title, { text: 'Friends' }]);
  const ground = sceneElement(doc, 'beast', { page: 'ladder', art: ART, form: 'ground', saveData: true });
  assert.equal(ground.attributes['data-saved'], '');
  assert.deepEqual(ground.children.map(child => child.className), ['scene-words']);
  const plate = sceneElement(doc, 'scene', { page: 'home', art: ART, form: 'plate', alt: 'A claw.' });
  assert.equal(plate.tag, 'figure');
  assert.equal(plate.children.length, 1, 'a plate without words has no caption');
});

test('the sheet holds the three forms: a head no taller than 120 px, 64 on a short screen, a scrim to soot under words', () => {
  const sheet = read('table/identity.css');
  assert.match(sheet, /\.scene-head \{[^}]*aspect-ratio: 3 \/ 1;[^}]*max-height: 120px;/);
  assert.match(sheet, /@media \(max-height: 600px\) \{\s*\.scene-head \{ max-height: 64px; \}/);
  assert.match(sheet, /\.scene-head::before \{[^}]*linear-gradient\(to right, var\(--soot\)/);
  assert.match(sheet, /\.scene-head\[data-side="end"\]::before \{[^}]*linear-gradient\(to left, var\(--soot\)/);
  assert.match(sheet, /\.scene-ground::before \{[^}]*var\(--soot\)/);
  assert.match(sheet, /\.scene-ground\[data-saved\]::before \{ content: none; \}/);
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
