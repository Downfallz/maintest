// The pages' motion kit (ADR 0197, docs/art/ux-craft.md#motion): the tokens beside the palette's, the five uses
// and ambience in identity.css, and the small module that brings the account's setting for less motion to a
// page. Held here as text and as pure functions; studio/browser/motion-kit.spec.js holds what a browser plays.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { SETTINGS_KEY, abortedTransition, ambienceToPlay, lessMotion, mountMotion, numberChanged, reach, savedLess } from './motion.js';
import { mountGameSettings, saveLocalSettings } from './game-preferences.js';

const root = new URL('../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');

const DURATIONS = ['--motion-instant', '--motion-fast', '--motion-normal', '--motion-slow', '--motion-breath', '--motion-strip'];
const CURVES = ['--motion-enter', '--motion-exit', '--motion-standard'];
// What may move (docs/art/ux-craft.md#transform-and-opacity-only). `display` and `overlay` do not move: they are
// what lets a window leave through its fade.
const TRAVEL = ['transform', 'translate', 'scale', 'rotate'];
const MAY_CHANGE = ['opacity', ...TRAVEL, 'display', 'overlay', 'filter'];
const FULL_MOTION = '@media (prefers-reduced-motion: no-preference)';
const NOT_LESS = ':root:not([data-motion="less"])';

const between = (text, start, end) => {
  const from = text.indexOf(start), to = text.indexOf(end);
  assert.ok(from >= 0 && to > from, `${start} ... ${end} is missing`);
  return text.slice(from + start.length, to);
};
const kitOf = path => between(read(path), '/* BEGIN MOTION KIT', '/* END MOTION KIT */');

/** Every declaration of a sheet, with the at-rules and the selector it sits under. No strings hold a brace here. */
export function declarations(css) {
  const out = [], stack = [];
  let text = '';
  for (const char of css.replace(/\/\*[\s\S]*?\*\//g, '')) {
    if (char === '{') { stack.push(text.trim()); text = ''; }
    else if (char === '}' || char === ';') {
      const colon = text.indexOf(':');
      if (text.trim() && colon > 0) out.push({ under: [...stack], property: text.slice(0, colon).trim(), value: text.slice(colon + 1).trim() });
      text = '';
      if (char === '}') stack.pop();
    } else text += char;
  }
  return out;
}

test('the reader of a sheet gives each declaration the rules it sits under', () => {
  const found = declarations('@media (x) { a:hover { scale: .9; opacity: 0 } } /* c { d: e } */ b { translate: 0 8px; }');
  assert.deepEqual(found, [
    { under: ['@media (x)', 'a:hover'], property: 'scale', value: '.9' },
    { under: ['@media (x)', 'a:hover'], property: 'opacity', value: '0' },
    { under: ['b'], property: 'translate', value: '0 8px' },
  ]);
});

test('the motion tokens are declared once, beside the palette, so both doors read them', () => {
  const palette = read('docs/art/branding.css');
  for (const token of [...DURATIONS, ...CURVES]) assert.match(palette, new RegExp(`\\s${token}: [^;]+;`), `${token} in docs/art/branding.css`);
  for (const path of ['table/table.css', 'table/identity.css', 'table/battle.css', 'table/lobby.css', 'showcase/showcase.css']) {
    const own = read(path).split('/* END GENERATED BRANDING */').at(-1);
    assert.doesNotMatch(own, /--motion-[a-z]+\s*:/, `${path} declares a motion token of its own: the set is in docs/art/branding.css`);
  }
});

test('a duration fits the vocabulary: a press about 100 ms, a window 200 to 300, an exit shorter than its entry', () => {
  const ms = Object.fromEntries(DURATIONS.map(token => {
    const [, number, unit] = new RegExp(`${token}: ([\\d.]+)(ms|s);`).exec(read('docs/art/branding.css'));
    return [token, Number(number) * (unit === 's' ? 1000 : 1)];
  }));
  assert.ok(ms['--motion-instant'] < ms['--motion-fast'] && ms['--motion-fast'] <= 150);
  assert.ok(ms['--motion-normal'] >= 200 && ms['--motion-normal'] <= 300);
  assert.ok(ms['--motion-fast'] < ms['--motion-normal'], 'a window leaves faster than it enters');
  assert.ok(ms['--motion-slow'] < 500, 'at 500 ms an animation drags');
  assert.ok(ms['--motion-breath'] >= 3000, 'a breath is slow');
});

for (const sheet of ['table/identity.css', 'showcase/showcase.css']) {
  test(`${sheet}: the kit moves transform and opacity only, by the tokens`, () => {
    const kit = declarations(kitOf(sheet));
    assert.ok(kit.length > 0);
    for (const { under, property, value } of kit) {
      const where = `${under.join(' { ')} { ${property}: ${value}`;
      if (property === 'transition' || property === 'transition-property') {
        const moved = value.split(',').map(part => part.trim().split(/\s+/)[0]);
        for (const name of moved) assert.ok(MAY_CHANGE.includes(name), `${where}: ${name} is neither transform nor opacity`);
      }
      if (/^(transition|animation)/.test(property)) assert.doesNotMatch(value, /\d(ms|s)\b/, `${where}: a duration is a token, never a number`);
      if (under.some(rule => rule.startsWith('@keyframes'))) assert.ok(MAY_CHANGE.includes(property), `${where}: a keyframe moves transform or opacity`);
    }
  });

  test(`${sheet}: what travels is written for full motion only, so its absence is the default`, () => {
    for (const { under, property, value } of declarations(kitOf(sheet))) {
      const travels = TRAVEL.includes(property) || ((property === 'transition' || property === 'transition-property') && TRAVEL.some(name => new RegExp(`(^|,\\s*)${name}\\b`).test(value)));
      const breathes = property.startsWith('animation') && /breath/.test(value);
      const keyframe = under.some(rule => rule.startsWith('@keyframes') && !rule.endsWith('-fade'));
      if (!travels && !breathes && !keyframe) continue;
      const where = `${under.join(' { ')} { ${property}: ${value}`;
      assert.ok(under.includes(FULL_MOTION), `${where}: outside ${FULL_MOTION}`);
      if (sheet.startsWith('table/') && !keyframe) assert.ok(under.some(rule => rule.includes(NOT_LESS)), `${where}: the account's setting does not reach it (${NOT_LESS})`);
    }
  });

  test(`${sheet}: a page changes with a transition under full motion only`, () => {
    const page = declarations(kitOf(sheet)).filter(one => one.under.includes('@view-transition'));
    assert.deepEqual(page.map(one => `${one.property}: ${one.value}`), ['navigation: auto']);
    assert.ok(page[0].under.includes(FULL_MOTION));
  });
}

test('a window enters from a starting style and leaves through its fade, where the browser can', () => {
  const kit = declarations(kitOf('table/identity.css'));
  const windows = kit.filter(one => one.under.some(rule => /dialog/.test(rule)));
  assert.ok(windows.length > 0);
  // A browser without the features opens and closes a window at once, as before: nothing of the form is outside.
  for (const one of windows) assert.ok(one.under.includes('@supports (transition-behavior: allow-discrete)'), `${one.under.join(' { ')} { ${one.property}: outside @supports`);
  assert.ok(windows.some(one => one.under.includes('@starting-style') && one.property === 'opacity' && one.value === '0'), 'the window starts transparent');
  assert.ok(windows.some(one => one.under.some(rule => rule.includes('::backdrop')) && one.under.includes('@starting-style')), 'the backdrop has a starting style of its own');
  assert.ok(windows.some(one => one.property === 'transition' && /display [^,]*allow-discrete/.test(one.value) && /overlay [^,]*allow-discrete/.test(one.value)), 'display and overlay wait for the fade');
});

test('the classic sheet no longer opens a window by its own animation, and its net leaves the kit its fade', () => {
  const sheet = read('table/table.css').split('/* END GENERATED BRANDING */').at(-1);
  assert.doesNotMatch(sheet, /dialog\[open\][^{]*\{\s*animation:/, 'two entrances would play on one window');
  const net = declarations(sheet).filter(one => one.property === 'transition-duration' && one.value.includes('!important'));
  assert.ok(net.length >= 2, 'the net answers the system and the account');
  for (const one of net) assert.match(one.under.at(-1), /:not\(dialog, \.motion-arrive, \.motion-number\)/, 'the net cuts what the kit fades');
  assert.ok(net.some(one => one.under.includes('@media (prefers-reduced-motion: reduce)')));
  assert.ok(net.some(one => one.under.at(-1).startsWith(':root[data-motion="less"]')));
});

test('every page that loads the identity loads the kit\'s module', () => {
  const pages = readdirSync(new URL('table/', root)).filter(name => name.endsWith('.html'));
  const identity = pages.filter(name => read(`table/${name}`).includes('href="identity.css"'));
  assert.ok(identity.includes('lobby.html') && identity.includes('battle.html'));
  for (const name of identity) assert.match(read(`table/${name}`), /<script type="module" src="motion\.js"><\/script>/, `table/${name}`);
});

// --- The module.

const storageOf = (items = {}) => ({ getItem: key => items[key] ?? null, setItem(key, value) { items[key] = value; } });

test('the module reads the setting where the game settings keep it', () => {
  const storage = storageOf();
  assert.equal(savedLess(storage), false);
  saveLocalSettings(storage, { reducedMotion: true });
  assert.notEqual(storage.getItem(SETTINGS_KEY), null, 'the key is the game settings\' own');
  assert.equal(savedLess(storage), true);
  saveLocalSettings(storage, { reducedMotion: false });
  assert.equal(savedLess(storage), false);
  assert.equal(savedLess(storageOf({ [SETTINGS_KEY]: '{not json' })), false);
  assert.equal(savedLess({ getItem() { throw new Error('blocked'); } }), false);
  assert.equal(savedLess(undefined), false);
});

test('a game\'s own setting wins over the account\'s on its page; elsewhere the account\'s is the page\'s', () => {
  assert.equal(lessMotion({ saved: true }), true);
  assert.equal(lessMotion({ saved: false }), false);
  assert.equal(lessMotion({ saved: true, game: 'false' }), false);
  assert.equal(lessMotion({ saved: false, game: 'true' }), true);
  assert.equal(lessMotion({ saved: true, game: undefined }), true);
});

const elementOf = (attributes = {}) => ({
  attributes: { ...attributes }, dataset: {}, listeners: {},
  setAttribute(name, value) { this.attributes[name] = value; },
  removeAttribute(name) { delete this.attributes[name]; },
  getAttribute(name) { return this.attributes[name] ?? null; },
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); },
});

test('less motion is one attribute on the root, set and taken away', () => {
  const html = elementOf();
  reach(html, true);
  assert.equal(html.attributes['data-motion'], 'less');
  reach(html, false);
  assert.equal('data-motion' in html.attributes, false);
});

test('one ambience plays a screen: the first in view, and none in a hidden tab', () => {
  const [a, b, c] = [{}, {}, {}];
  assert.equal(ambienceToPlay([a, b, c], new Set([b, c]), false), b);
  assert.equal(ambienceToPlay([a, b, c], new Set(), false), null);
  assert.equal(ambienceToPlay([a, b, c], new Set([a]), true), null);
});

test('a number that changes plays its beat again each time', () => {
  const calls = [];
  const el = { classList: { remove: name => calls.push(`-${name}`), add: name => calls.push(`+${name}`) }, get offsetWidth() { calls.push('reflow'); return 0; } };
  numberChanged(el);
  assert.deepEqual(calls, ['-motion-changed', 'reflow', '+motion-changed']);
  assert.doesNotThrow(() => numberChanged(null));
});

function windowOf({ saved = false, battle = null } = {}) {
  const html = elementOf(), listeners = {};
  const storage = storageOf();
  saveLocalSettings(storage, { reducedMotion: saved });
  const observed = [];
  const win = {
    localStorage: storage,
    document: { documentElement: html, hidden: false, getElementById: id => (id === 'battle' ? battle : null), querySelectorAll: () => [], addEventListener() {} },
    addEventListener(type, listener) { (listeners[type] ??= []).push(listener); },
    MutationObserver: class { constructor(callback) { this.callback = callback; } observe(target, options) { observed.push({ target, options, callback: this.callback }); } },
  };
  return { win, html, storage, listeners, observed };
}

test('a page opens with the account\'s setting, and follows it when another tab or the settings panel changes it', () => {
  const { win, html, storage, listeners } = windowOf({ saved: true });
  mountMotion(win);
  assert.equal(html.attributes['data-motion'], 'less');
  saveLocalSettings(storage, { reducedMotion: false });
  for (const listener of listeners.storage) listener({ key: SETTINGS_KEY });
  assert.equal('data-motion' in html.attributes, false);
  saveLocalSettings(storage, { reducedMotion: true });
  for (const listener of listeners['downfall:settings']) listener({});
  assert.equal(html.attributes['data-motion'], 'less');
});

test('the battle\'s page follows the game\'s own setting, as the match settings change it', () => {
  const battle = elementOf();
  battle.dataset.reducedMotion = 'false';
  const { win, html, observed } = windowOf({ saved: true, battle });
  mountMotion(win);
  assert.equal('data-motion' in html.attributes, false, 'the game said full motion');
  assert.deepEqual(observed.map(one => one.options), [{ attributes: true, attributeFilter: ['data-reduced-motion'] }]);
  battle.dataset.reducedMotion = 'true';
  observed[0].callback();
  assert.equal(html.attributes['data-motion'], 'less');
});

test('under less motion a page is left at once: the old page skips the transition the account cannot gate in CSS', () => {
  const { win, listeners } = windowOf({ saved: true });
  mountMotion(win);
  let skipped = 0;
  for (const listener of listeners.pageswap) listener({ viewTransition: { skipTransition() { skipped++; } } });
  for (const listener of listeners.pageswap) listener({ viewTransition: null });
  assert.equal(skipped, 1);
  const full = windowOf({ saved: false });
  mountMotion(full.win);
  for (const listener of full.listeners.pageswap) listener({ viewTransition: { skipTransition() { skipped++; } } });
  assert.equal(skipped, 1);
});

test('a page\'s transition the browser gave up on is answered, and no other rejection is', () => {
  const aborted = Object.assign(new Error('Transition was aborted because of invalid state. Viewport size changed'), { name: 'InvalidStateError' });
  assert.equal(abortedTransition(aborted), true);
  assert.equal(abortedTransition(Object.assign(new Error('The object is in an invalid state.'), { name: 'InvalidStateError' })), false);
  assert.equal(abortedTransition(new Error('Transition was aborted, said a host')), false);
  assert.equal(abortedTransition('Transition was aborted'), false);
  assert.equal(abortedTransition(undefined), false);

  const { win, listeners } = windowOf();
  mountMotion(win);
  const answered = [];
  for (const reason of [aborted, new Error('The host did not answer')]) {
    for (const listener of listeners.unhandledrejection) listener({ reason, preventDefault: () => answered.push(reason.message) });
  }
  assert.deepEqual(answered, [aborted.message]);
});

test('the kit\'s module comes before a page\'s own scripts, so its answer is given before theirs', () => {
  for (const name of readdirSync(new URL('table/', root)).filter(name => name.endsWith('.html'))) {
    const page = read(`table/${name}`), kit = page.indexOf('src="motion.js"');
    if (kit < 0) continue;
    const others = [...page.matchAll(/<script[^>]*\ssrc="([^"]+)"/g)].filter(match => match[1] !== 'motion.js').map(match => match.index);
    for (const at of others) assert.ok(at > kit, `table/${name}: a script before motion.js`);
  }
});

test('the settings panel tells the page at once when less motion is ticked, before the host answers, and again once the account is read', async () => {
  const made = {};
  const el = id => (made[id] ??= { id, listeners: {}, value: 'step', checked: false, hidden: false, textContent: '', addEventListener(type, listener) { this.listeners[type] = listener; } });
  const told = [];
  let answerSave;
  const storage = storageOf();
  const doc = { getElementById: el, querySelectorAll: () => [], defaultView: { dispatchEvent: event => told.push(`${event.type}: ${savedLess(storage) ? 'less' : 'full'}`) } };
  const request = (path, body) => (body ? new Promise(resolve => { answerSave = resolve; }) : Promise.resolve({ saved: false }));
  const panel = mountGameSettings(doc, storage, { request });
  el('game-settings-speed').value = '1';

  el('game-settings-motion').checked = true;
  el('game-settings').listeners.change();
  assert.deepEqual(told, ['downfall:settings: less'], 'the page follows before the host has answered');
  answerSave?.({});

  panel.setAccount();
  await new Promise(resolve => setTimeout(resolve, 0));
  assert.equal(told.at(-1), 'downfall:settings: less');
  assert.ok(told.length >= 2, 'the account once read is told too');
});

test('a keyframe written outside full motion moves opacity alone, whatever its name', () => {
  for (const sheet of ['table/identity.css', 'showcase/showcase.css']) {
    for (const { under, property } of declarations(kitOf(sheet))) {
      if (!under.some(rule => rule.startsWith('@keyframes')) || under.includes(FULL_MOTION)) continue;
      assert.equal(property, 'opacity', `${sheet}: ${under.join(' { ')} { ${property}`);
    }
  }
});

test('a press never outranks a control\'s own look: a fallen creature\'s portrait stays grey under the finger', () => {
  const press = declarations(kitOf('table/identity.css')).filter(one => one.property === 'filter');
  assert.equal(press.length, 1);
  assert.match(press[0].under.at(-1), /:where\([^()]*\):where\(.*:active\)$/, 'the press has no specificity');
});
