import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { PAGE_ART } from './page-art.js';
import { PLATE_ALT, drawPlate } from './offline.js';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

// Enough of the DOM for a plate: elements with attributes, children and listeners.
function fakeDocument() {
  const element = tag => ({
    tag, attributes: {}, children: [], className: '', listeners: {}, removed: false,
    setAttribute(name, value) { this.attributes[name] = value; },
    append(...nodes) { this.children.push(...nodes); },
    replaceChildren(...nodes) { this.children = nodes; },
    addEventListener(kind, listener) { this.listeners[kind] = listener; },
    querySelector(selector) { return this.children.find(child => child.tag === selector); },
    remove() { this.removed = true; },
  });
  return { createElement: element, element };
}

test('the offline page draws its plate into the room it holds: the Parasite whole, with its alt, asked for first', () => {
  const doc = fakeDocument(), room = doc.element('div');
  drawPlate(doc, room);
  const [figure] = room.children;
  assert.equal(figure.tag, 'figure');
  assert.equal(figure.className, 'scene scene-plate');
  const image = figure.querySelector('img');
  assert.equal(image.attributes.alt, PLATE_ALT);
  assert.equal(image.attributes.fetchpriority, 'high', 'the page\'s one picture on its first screen');
  assert.match(image.attributes.src, /^\/page-art\/parasite-offline-/);
  assert.equal(figure.children.length, 1, 'no caption: the page\'s words under it say what the screen is');
});

test('a plate the worker did not keep takes its room away: the page is its words', () => {
  const doc = fakeDocument(), room = doc.element('div');
  drawPlate(doc, room);
  room.children[0].querySelector('img').listeners.error();
  assert.ok(room.removed);
});

test('the plate is its own picture of two files, so the worker keeps only what the page draws', () => {
  assert.deepEqual(Object.keys(PAGE_ART['parasite-offline'].pages), ['table/offline']);
  assert.equal(PAGE_ART['parasite-offline'].sizes.length, 2);
});

test('the offline page holds the plate\'s room before its script, and lets it go when the script never ran', () => {
  const page = read('./offline.html');
  assert.match(page, /<main>\s*<div id="offline-plate" class="offline-plate"><\/div>\s*<h1>/);
  assert.match(page, /\.offline-plate \{[^}]*aspect-ratio: 16 \/ 9/);
  assert.match(page, /<script type="module" src="offline.js"><\/script>/);
  assert.match(page, /if \(room && !room\.firstChild\) room\.remove\(\)/);
});
