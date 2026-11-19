// The real-engine journeys' seat collector (studio/browser/seat-views.js), run in Node with a stub page (#921).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { installSeatViews } from './browser/seat-views.js';

// A page whose fetch answers when the test says: `answer(index, body)` resolves the index-th read asked.
function stubPage() {
  const pending = [];
  const scope = {
    location: { href: 'http://127.0.0.1/battle' },
    fetch: (path) => new Promise((resolve) => pending.push({ path, resolve })),
  };
  const answer = (index, body, { ok = true } = {}) => {
    const { path, resolve } = pending[index];
    const response = { ok, url: new URL(path, scope.location.href).href, clone: () => ({ json: async () => body }) };
    resolve(response);
    return response;
  };
  return { scope, answer };
}
const view = (waitingAsked, feed = []) => ({ board: { roundNumber: 1 }, waitingAsked, feed });

test('a seat read asked earlier that finishes later never replaces the newer view', async () => {
  const { scope, answer } = stubPage();
  installSeatViews(scope);
  const first = scope.fetch('/api/seat/player1'), second = scope.fetch('/api/seat/player1?since=4');
  answer(1, view(2)); await second;
  answer(0, view(1)); await first;
  assert.equal(scope.seatViews.latest.waitingAsked, 2);
});

test('the page gets each response only once the collector holds its body', async () => {
  const { scope, answer } = stubPage();
  installSeatViews(scope);
  const read = scope.fetch('/api/seat/player1');
  const response = answer(0, view(3));
  assert.equal(await read, response);
  assert.equal(scope.seatViews.latest.waitingAsked, 3);
});

test('only the named seats, answered with a board, count; a drawn asking counts unless skipped', async () => {
  const { scope, answer } = stubPage();
  installSeatViews(scope, { skipShown: true });
  const reads = ['/api/seat/player2', '/api/seat/player1/events', '/api/seat/player1?shown=5', '/api/seat/player1', '/api/session']
    .map((path) => scope.fetch(path));
  answer(0, view(10)); answer(1, view(11)); answer(2, view(12)); answer(3, view(13), { ok: false }); answer(4, view(14));
  await Promise.all(reads);
  assert.equal(scope.seatViews.latest, null);

  const shown = stubPage();
  installSeatViews(shown.scope, { seats: ['player1', 'player2'] });
  const drawn = shown.scope.fetch('/api/seat/player2?shown=5');
  shown.answer(0, view(5)); await drawn;
  assert.equal(shown.scope.seatViews.latest.waitingAsked, 5);
});

test('every resolved combat action the feeds carry is kept by its sequence', async () => {
  const { scope, answer } = stubPage();
  installSeatViews(scope);
  const reads = [scope.fetch('/api/seat/player1'), scope.fetch('/api/seat/player1?since=8')];
  answer(1, view(2, [{ sequence: 9, event: { kind: 'CombatActionResolved' } }]));
  answer(0, view(1, [{ sequence: 7, event: { kind: 'CombatActionResolved' } }, { sequence: 8, event: { kind: 'RoundEnded' } }]));
  await Promise.all(reads);
  assert.deepEqual(Object.keys(scope.seatViews.actions), ['7', '9']);
});

// The trap in studio/AGENTS.md, held: no browser spec or fixture reads a body in a response handler, which Playwright
// does not await, whether the handler is async or chains on the body's promise. Handlers that read the url, ok() or
// status() stay. A handler is read up to the line that closes it.
function browserFiles(folder) {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => entry.isDirectory()
    ? (entry.name === 'node_modules' ? [] : browserFiles(join(folder, entry.name)))
    : entry.name.endsWith('.js') ? [join(folder, entry.name)] : []);
}
const readsBodyInHandler = (source) =>
  [...source.matchAll(/\.on\(\s*['"`]response['"`]\s*,([\s\S]*?)\n?\s*\}?\);/g)].some(([, handler]) => /^\s*async\b|\.(json|text|body)\(/.test(handler));

test('the trap reader sees a body read in a response handler, however it is written', () => {
  assert.equal(readsBodyInHandler("page.on('response', async response => {\n  latest = await response.json();\n});"), true);
  assert.equal(readsBodyInHandler('page.on("response", async r => r.json());'), true);
  assert.equal(readsBodyInHandler("page.on('response', r => r.json().then(b => { x = b; }));"), true);
  assert.equal(readsBodyInHandler("page.on('response', response => { if (!response.ok()) failed.push(response.url()); });"), false);
});

test('no browser spec reads the host bodies in a response handler', () => {
  const folder = join(dirname(fileURLToPath(import.meta.url)), 'browser');
  assert.deepEqual(browserFiles(folder).filter((file) => readsBodyInHandler(readFileSync(file, 'utf8'))), []);
});
