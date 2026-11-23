import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { firstTutorial, mountShowAgain, mountTutorials, opensByItself, seriesProgress, tutorialCards } from './lobby-tutorials.js';

const first = { id: 'sc-repo-tutorial-your-first-combat', order: 1, of: 4, title: 'Your first combat', goal: 'Hit the wounded enemy.', link: '/battle?scenario=sc-repo-tutorial-your-first-combat', state: 'new' };
const second = { id: 'sc-repo-tutorial-who-acts-first', order: 2, of: 4, title: 'Who acts first', goal: '', link: '/battle?scenario=sc-repo-tutorial-who-acts-first', state: 'done' };

function memory() {
  const kept = new Map();
  return { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, String(value)) };
}

// The few elements the section and the settings row are drawn with.
function page() {
  const made = tag => ({ tag, children: [], dataset: {}, hidden: false, disabled: false, textContent: '', className: '', listeners: {},
    attributes: {}, setAttribute(name, value) { this.attributes[name] = value; },
    append(...more) { this.children.push(...more); }, replaceChildren(...more) { this.children = more; },
    addEventListener(kind, heard) { this.listeners[kind] = heard; }, scrollIntoView() { this.scrolled = true; } });
  const ids = Object.fromEntries(['learn-to-play', 'tutorial-cards', 'tutorial-progress', 'tutorial-again-row', 'tutorial-again', 'tutorial-again-problem'].map(id => [id, made('div')]));
  ids['learn-to-play'].hidden = true; ids['tutorial-again-row'].hidden = true;
  return { ids, getElementById: id => ids[id] ?? null, createElement: made };
}
const answering = body => async () => ({ ok: true, json: async () => body });
const texts = node => [node.textContent, ...node.children.flatMap(texts)].filter(Boolean);

test('a tutorial is a card with its painting, its title, its goal, its state and its link', () => {
  assert.deepEqual(tutorialCards({ tutorials: [first, second] }), [
    { id: first.id, title: 'Your first combat', place: 'Tutorial 1 of 4', goal: 'Hit the wounded enemy.', done: false, skipped: false, next: true, tag: 'New', action: 'Play', link: first.link, picture: 'lesson-base' },
    { id: second.id, title: 'Who acts first', place: 'Tutorial 2 of 4', goal: '', done: true, skipped: false, next: false, tag: 'Done', action: 'Play again', link: second.link, picture: 'lesson-brute' },
  ]);
});

test('a skipped tutorial says so, and one with no link is left out', () => {
  const cards = tutorialCards({ tutorials: [{ ...first, state: 'skipped' }, { ...second, link: undefined }] });

  assert.deepEqual(cards.map(one => [one.id, one.tag]), [[first.id, 'Skipped']]);
});

test('the next tutorial is the first one neither done nor skipped, in the series\' order: the section\'s one filled action (UX8, #987)', () => {
  const third = { ...first, id: 'c', order: 3, title: 'Grow by mutating', state: 'new' };
  assert.deepEqual(tutorialCards({ tutorials: [third, { ...first, state: 'done' }, second] }).map(one => [one.id, one.next]), [['c', true], [first.id, false], [second.id, false]]);
  assert.deepEqual(tutorialCards({ tutorials: [{ ...first, state: 'done' }, second] }).map(one => one.next), [false, false]);
  assert.deepEqual(tutorialCards({ tutorials: [third, { ...first, state: 'skipped' }, second] }).map(one => [one.id, one.next]), [['c', true], [first.id, false], [second.id, false]]);
  assert.deepEqual(tutorialCards({ tutorials: [{ ...first, state: 'skipped' }, second] }).map(one => one.next), [false, false]);
});

test('the progress counts the tutorials the host says done, in words', () => {
  assert.deepEqual(seriesProgress(tutorialCards({ tutorials: [first, second] })), { done: 1, of: 2, label: '1 of 2 done' });
  assert.deepEqual(seriesProgress([]), { done: 0, of: 0, label: '' });
});

test('no tutorial, or no answer, is no card and no first tutorial', () => {
  assert.deepEqual(tutorialCards({ tutorials: [] }), []);
  assert.deepEqual(tutorialCards(undefined), []);
  assert.equal(firstTutorial(undefined), null);
});

test('the first tutorial is the one of order 1, wherever it is listed', () => {
  assert.equal(firstTutorial({ tutorials: [second, first] }), first.link);
});

test('the tutorial the host says opens by itself opens once a browser session', () => {
  const session = memory();

  assert.equal(opensByItself({ opens: first.link }, session), first.link);
  assert.equal(opensByItself({ opens: first.link }, session), null);
});

test('nothing opens by itself when the host names none, names another page, or the browser keeps no session', () => {
  assert.equal(opensByItself({ opens: null }, memory()), null);
  assert.equal(opensByItself({ opens: 'https://elsewhere.example/' }, memory()), null);
  assert.equal(opensByItself({ opens: first.link }, null), null);
  assert.equal(opensByItself({ opens: first.link }, { getItem() { throw new Error('blocked'); } }), null);
});

test('a new account is taken to the first tutorial, and the section is not drawn under it', async () => {
  const doc = page(), went = [];
  const tutorials = mountTutorials(doc, { request: answering({ opens: first.link, tutorials: [first] }), session: memory(), go: link => went.push(link), hash: () => '', plain: () => true });

  await tutorials.setAccount({ id: 'acct_1' });

  assert.deepEqual(went, [first.link]);
  assert.equal(doc.ids['learn-to-play'].hidden, true);
});

test('an address that asks for something is answered first: the tutorial does not take the page, and still opens at the next visit', async () => {
  const doc = page(), went = [], session = memory();
  const asked = mountTutorials(doc, { request: answering({ opens: first.link, tutorials: [first] }), session, go: link => went.push(link), hash: () => '', plain: () => false });

  await asked.setAccount({ id: 'acct_1' });

  assert.deepEqual(went, []);
  assert.equal(doc.ids['learn-to-play'].hidden, false);
  assert.equal(opensByItself({ opens: first.link }, session), first.link);
});

test('an account that skipped it finds the series under Learn to play, each to play', async () => {
  const doc = page(), went = [];
  const tutorials = mountTutorials(doc, { request: answering({ opens: null, tutorials: [{ ...first, state: 'skipped' }, second] }), session: memory(), go: link => went.push(link), hash: () => '#learn-to-play' });

  await tutorials.setAccount({ id: 'acct_1' });

  assert.deepEqual(went, []);
  assert.equal(doc.ids['learn-to-play'].hidden, false);
  assert.equal(doc.ids['learn-to-play'].scrolled, true);
  const [one, two] = doc.ids['tutorial-cards'].children;
  assert.deepEqual(texts(one), ['Your first combat', 'Skipped', 'Hit the wounded enemy.', 'Play']);
  assert.deepEqual(texts(two), ['Who acts first', 'Done', 'Play again']);
  // Each card carries its painting, decoration beside its title, at the size it is drawn, loaded when near.
  const picture = one.children.find(child => child.tag === 'img');
  assert.equal(picture.attributes.alt, '');
  assert.equal(picture.attributes.loading, 'lazy');
  assert.match(picture.attributes.src, /^\/page-art\/lesson-base-/);
  // Every card's action is an outline (UX9): the next one new is marked, and filled once, in the featured space. A
  // skipped lesson is not the next one (#987).
  const action = node => node.children.find(child => child.className?.includes('card-ways')).children[0];
  assert.doesNotMatch(action(one).className, /primary/);
  assert.doesNotMatch(action(two).className, /primary/);
  assert.equal(one.dataset.next, undefined);
  assert.equal(two.dataset.next, undefined);
  assert.equal(doc.ids['tutorial-progress'].textContent, '1 of 2 done');
});

test('signed out, or on a host that answers no series, there is no section', async () => {
  const doc = page();
  const tutorials = mountTutorials(doc, { request: async () => ({ ok: false, json: async () => ({}) }), session: memory(), go() {}, hash: () => '' });

  await tutorials.setAccount({ id: 'acct_1' });
  const refused = doc.ids['learn-to-play'].hidden;
  doc.ids['learn-to-play'].hidden = false;
  await tutorials.setAccount(null);

  assert.deepEqual([refused, doc.ids['learn-to-play'].hidden], [true, true]);
});

test('Show the first tutorial again is a signed-in player\'s, and opens tutorial 1 whatever its state', async () => {
  const doc = page(), went = [];
  const again = mountShowAgain(doc, { request: answering({ opens: null, tutorials: [second, { ...first, state: 'done' }] }), go: link => went.push(link) });

  again.setAccount({ id: 'acct_1' });
  await doc.ids['tutorial-again'].listeners.click();

  assert.equal(doc.ids['tutorial-again-row'].hidden, false);
  assert.deepEqual(went, [first.link]);
  again.setAccount(null);
  assert.equal(doc.ids['tutorial-again-row'].hidden, true);
});

test('Show the first tutorial again says so when the host has no tutorial', async () => {
  const doc = page(), went = [];
  mountShowAgain(doc, { request: answering({ tutorials: [] }), go: link => went.push(link) });

  await doc.ids['tutorial-again'].listeners.click();

  assert.deepEqual(went, []);
  assert.equal(doc.ids['tutorial-again-problem'].hidden, false);
  assert.equal(doc.ids['tutorial-again-problem'].textContent, 'No tutorial is available here right now.');
});

// #669: Learn to play is an entry of the main menu's bar, beside Rules, for a signed-in account; the account menu no
// longer holds it. Every page draws its header from navigation.js, so its source says where the entry stands.
test('Learn to play stands in the main menu beside Rules, for a signed-in account, and not in the account menu', () => {
  const source = readFileSync(new URL('./navigation.js', import.meta.url), 'utf8');
  const bar = source.match(/<nav id="home-navigation"[\s\S]*?<\/nav>/)[0];
  const accountMenu = source.match(/<div class="account-menu">[\s\S]*?<\/div><\/details>/)[0];
  assert.match(bar, /<a id="rules-link" href="\/rules">Rules<\/a>\s*<a id="learn-link" href="\/#learn-to-play" hidden>Learn to play<\/a>/);
  assert.doesNotMatch(accountMenu, /Learn to play/);
  assert.match(source, /el\('learn-link'\)\.hidden = !account;/);
});

test('the lobby is told the cards each read draws, and nothing for nobody signed in, so its featured space can offer the next', async () => {
  const doc = page(), told = [];
  const tutorials = mountTutorials(doc, { request: answering({ opens: null, tutorials: [first, second] }), session: memory(), go() {}, hash: () => '', listed: cards => told.push(cards?.map(card => card.id) ?? null) });

  await tutorials.setAccount({ id: 'acct_1' });
  await tutorials.setAccount(null);

  assert.deepEqual(told, [[first.id, second.id], null]);
});
