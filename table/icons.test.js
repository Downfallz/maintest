// The pages' icon set (ADR 0198, docs/art/branding.md): one drawing a meaning, in the identity's line, taking the
// colour of the text beside it, and silent for a screen reader, since the control it sits in has a name.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { ICONS, iconElement, iconHtml } from './icons.js';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

// What the pages write today as a glyph or an emoji, and UX11 and UX12 replace.
const MEANINGS = ['close', 'check', 'menu', 'previous', 'next', 'open', 'settings', 'reaction', 'sound', 'reset', 'history',
  'bell', 'leave', 'critical', 'health', 'energy', 'defense', 'initiative', 'stun', 'damage'];

test('the set draws each meaning the pages write as a glyph today', () => {
  assert.deepEqual(Object.keys(ICONS).sort(), [...MEANINGS].sort());
});

test('an icon is drawn on a grid of 24 with no colour and no size of its own', () => {
  for (const [name, path] of Object.entries(ICONS)) {
    assert.match(path, /^[MmLlHhVvAaZz0-9 .,-]+$/, `${name}: lines and arcs only`);
    const numbers = path.replace(/[Aa][^MmLlHhVvZz]*/g, '').match(/-?\d*\.?\d+/g).map(Number);
    assert.ok(numbers.every(number => number >= -24 && number <= 24), `${name} stays on the grid`);
    assert.ok(path.length < 240, `${name}: a few hundred bytes at most`);
  }
});

test('an icon is hidden from a screen reader and cannot take the focus', () => {
  const html = iconHtml('close');
  assert.equal(html, `<svg class="icon" viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false"><path d="${ICONS.close}"/></svg>`);
  assert.throws(() => iconHtml('unicorn'), /unicorn is not an icon of the set/);
});

test('a page that builds itself gets the icon as an element in the SVG namespace', () => {
  const made = [];
  const doc = { createElementNS(namespace, tag) { const node = { namespace, tag, attributes: {}, children: [], setAttribute(name, value) { this.attributes[name] = value; }, append(child) { this.children.push(child); } }; made.push(node); return node; } };
  const icon = iconElement(doc, 'settings');
  assert.equal(icon.tag, 'svg');
  assert.ok(made.every(node => node.namespace === 'http://www.w3.org/2000/svg'));
  assert.equal(icon.attributes['aria-hidden'], 'true');
  assert.equal(icon.attributes.class, 'icon');
  assert.equal(icon.children[0].attributes.d, ICONS.settings);
});

test('the sheet draws an icon as a line in the colour of the text, at the size of the text', () => {
  const rule = /\.icon \{([^}]*)\}/.exec(read('identity.css'))?.[1] ?? '';
  for (const part of ['fill: none', 'stroke: currentColor', 'stroke-linecap: square', 'stroke-linejoin: miter', 'width: 1.25em', 'height: 1.25em'])
    assert.ok(rule.includes(part), part);
});
