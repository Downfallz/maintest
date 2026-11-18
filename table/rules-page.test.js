// The rulebook's page (/rules, docs/tabletop/rulebook-plan.md R3): its contents are built from table/rules.json,
// a deep link lands on a section by its id, a redirect or a card, and the filter keeps the sections whose title
// or text holds every word typed. The page reads the book; it never writes a rule of its own. It reads it in English
// or in French (R6): the language stored on the device, else the browser's, and a switch between them.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { contentsOf, targetOf, matching, normalized, spanText, changeText, latestFirst, languageOf, askedLanguage, isKeptCopy, bookFile, WORDS } from './rules.js';

const book = JSON.parse(readFileSync(new URL('./rules.json', import.meta.url), 'utf8'));
const french = JSON.parse(readFileSync(new URL('./rules.fr.json', import.meta.url), 'utf8'));
const page = readFileSync(new URL('./rules.html', import.meta.url), 'utf8');

// A small book in the built file's shape, so a test reads what it asserts.
const tiny = {
  parts: [
    {
      id: 'the-goal', number: 1, title: 'What you are trying to do',
      blocks: [{ type: 'paragraph', spans: [{ text: 'Two players.' }] }],
      sections: [
        { id: 'winning', number: '1.1', title: 'Winning', blocks: [{ type: 'paragraph', spans: [{ text: 'At zero Health a creature is ' }, { text: 'defeated', strong: true }, { text: '.' }] }], sources: [], card: { id: 'health', title: 'Health', lines: ['At zero Health it falls.'], example: 'A hit of 7 leaves 23.' } },
      ],
    },
    {
      id: 'the-round', number: 2, title: 'The round, step by step',
      blocks: [],
      sections: [
        { id: 'tempo', number: '2.1', title: 'Tempo', blocks: [{ type: 'list', ordered: false, items: [[{ text: 'Instinct', strong: true }, { text: ' acts first.' }], [{ text: 'Precision keeps its critical roll.' }]] }], sources: [] },
        { id: 'conditions', number: '2.2', title: 'The conditions', blocks: [{ type: 'table', header: [[{ text: 'Condition' }]], rows: [[[{ text: 'Bleed ignores Defense' }]]] }], sources: [] },
      ],
    },
  ],
  cards: [{ id: 'health', section: 'winning' }],
  moved: { speed: 'tempo', 'old-speed': 'speed' },
  changes: [{ date: '2026-11-16', text: 'The official rulebook.', adrs: ['0188'] }],
};

test('the contents list every part of the built book in order, each with its sections and their numbers', () => {
  const contents = contentsOf(book);

  assert.deepEqual(contents.map(part => part.id), book.parts.map(part => part.id));
  assert.equal(contents.length, book.parts.length);
  for (const [index, part] of contents.entries()) {
    assert.equal(part.number, book.parts[index].number);
    assert.equal(part.title, book.parts[index].title);
    assert.deepEqual(part.sections.map(section => [section.id, section.number, section.title]),
      book.parts[index].sections.map(section => [section.id, section.number, section.title]));
  }
  assert.ok(contents.flatMap(part => part.sections).some(section => section.id === 'tempo'), 'the book has its tempo section');
});

test('a deep link to a section lands on that section, in its part', () => {
  assert.deepEqual(targetOf(tiny, '#tempo'), { id: 'tempo', part: 'the-round' });
  assert.deepEqual(targetOf(book, '#tempo'), { id: 'tempo', part: book.parts.find(part => part.sections.some(section => section.id === 'tempo')).id });
});

test('a deep link to a part lands on the part', () => {
  assert.deepEqual(targetOf(tiny, '#the-round'), { id: 'the-round', part: 'the-round' });
});

test('a deep link to an id the book moved lands where moved.json sends it, through every hop', () => {
  assert.deepEqual(targetOf(tiny, '#speed'), { id: 'tempo', part: 'the-round', movedFrom: 'speed' });
  assert.deepEqual(targetOf(tiny, '#old-speed'), { id: 'tempo', part: 'the-round', movedFrom: 'old-speed' });
});

test('a redirect that loops lands nowhere rather than spinning', () => {
  assert.equal(targetOf({ ...tiny, moved: { a: 'b', b: 'a' } }, '#a'), null);
});

test('a deep link to a concept card lands on the section the card summarises', () => {
  assert.deepEqual(targetOf(tiny, '#health'), { id: 'winning', part: 'the-goal', card: 'health' });
  // In the built book a card may share its section's id (tempo); either way the link lands on that section.
  for (const card of book.cards) assert.equal(targetOf(book, `#${card.id}`)?.id, card.section, card.id);
});

test('an empty hash or an id the book does not hold lands nowhere', () => {
  for (const hash of ['', '#', '#no-such-rule', '#what-changed']) assert.equal(targetOf(tiny, hash), null, hash);
});

test('an encoded hash is read as the id it spells', () => {
  assert.deepEqual(targetOf(tiny, '#%74empo'), { id: 'tempo', part: 'the-round' });
  assert.equal(targetOf(tiny, '#%E0%A4%A'), null, 'a malformed escape lands nowhere instead of throwing');
});

test('an empty filter keeps everything', () => {
  assert.equal(matching(tiny, ''), null);
  assert.equal(matching(tiny, '   '), null);
});

test('the filter keeps a section whose title holds the words, and the part it sits in', () => {
  const kept = matching(tiny, 'tempo');

  assert.deepEqual([...kept.sections], ['tempo']);
  assert.deepEqual([...kept.parts], ['the-round']);
});

test('the filter reads a section\'s text, its lists, its tables and its card, without case or accents', () => {
  assert.deepEqual([...matching(tiny, 'PRECISION').sections], ['tempo'], 'a list item');
  assert.deepEqual([...matching(tiny, 'bleed').sections], ['conditions'], 'a table cell');
  assert.deepEqual([...matching(tiny, 'hit of 7').sections], ['winning'], 'the card\'s example');
  assert.deepEqual([...matching(tiny, 'défeated').sections], ['winning'], 'an accent typed is an accent ignored');
});

test('the filter keeps a section only when it holds every word typed', () => {
  assert.deepEqual([...matching(tiny, 'instinct first').sections], ['tempo']);
  assert.deepEqual([...matching(tiny, 'instinct bleed').sections], []);
});

test('a part whose own title matches is kept with all its sections', () => {
  const kept = matching(tiny, 'step by step');

  assert.deepEqual([...kept.parts], ['the-round']);
  assert.deepEqual([...kept.sections].sort(), ['conditions', 'tempo']);
});

test('the filter finds Sunder in the built book, for "what does Sunder do"', () => {
  const kept = matching(book, 'sunder');

  assert.ok(kept.sections.has('sunder'));
  assert.ok(kept.sections.size < book.parts.flatMap(part => part.sections).length, 'and not every section');
});

test('a word is read without case, accents or extra spaces', () => {
  assert.equal(normalized('  Évolution  CRITIQUE '), 'evolution critique');
});

test('spans read as their text, whatever their marks', () => {
  assert.equal(spanText([{ text: 'A ' }, { text: 'hit', strong: true }, { text: ', see ' }, { text: 'Sunder', href: '#sunder' }]), 'A hit, see Sunder');
});

test('a change reads as its date in words and its text', () => {
  assert.equal(changeText(tiny.changes[0]), '16 November 2026 · The official rulebook.');
});

test('the changes read latest first, whatever order changes.md lists them in', () => {
  const changes = [{ date: '2026-11-16', text: 'b' }, { date: '2026-12-02', text: 'c' }, { date: '2026-11-01', text: 'a' }];

  assert.deepEqual(latestFirst(changes).map(change => change.text), ['c', 'b', 'a']);
  assert.deepEqual(changes.map(change => change.text), ['b', 'c', 'a'], 'the book itself is left as it is');
});

test('no id the book holds is an id the page itself uses, so a deep link cannot land on the page\'s own parts', () => {
  // The menu's ids too: navigation.js writes its header and dialogs into every page that has one.
  const menu = readFileSync(new URL('./navigation.js', import.meta.url), 'utf8');
  const pageIds = new Set([...`${page}${menu}`.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]));
  pageIds.add('what-changed');
  const bookIds = [...book.parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)]), ...Object.keys(book.moved), ...book.cards.map(card => card.id)];

  for (const id of bookIds) assert.ok(!pageIds.has(id), `${id} is both a rule and an element of rules.html`);
});

test('the page loads its own script and style, the navigation, and identity.css last', () => {
  assert.match(page, /<script type="module" src="rules\.js"><\/script>/);
  assert.match(page, /<script type="module" src="navigation\.js"><\/script>/);
  assert.match(page, /<link rel="stylesheet" href="rules\.css" \/>/);
  const sheets = [...page.matchAll(/<link rel="stylesheet" href="([^"]+)"/g)].map(match => match[1]);
  assert.equal(sheets.at(-1), 'identity.css');
  assert.match(page, /data-navigation="rules"/);
});

test('the page reads the book by a relative path, so it works at /rules and under /table/', () => {
  const script = readFileSync(new URL('./rules.js', import.meta.url), 'utf8');

  assert.match(script, /'rules\.json'/);
  assert.match(script, /'rules\.fr\.json'/);
  assert.doesNotMatch(script, /'\/rules(\.fr)?\.json'/);
});

test('the language is the one stored on the device, else the browser\'s first English or French, else English', () => {
  assert.equal(languageOf({ stored: 'fr', languages: ['en-CA'] }), 'fr');
  assert.equal(languageOf({ stored: 'en', languages: ['fr-CA'] }), 'en');
  assert.equal(languageOf({ stored: null, languages: ['fr-CA', 'en-US'] }), 'fr');
  assert.equal(languageOf({ stored: null, languages: ['de-DE', 'FR'] }), 'fr', 'the first language the book is written in');
  assert.equal(languageOf({ stored: 'de', languages: ['es-ES'] }), 'en', 'neither: English');
  assert.equal(languageOf({}), 'en');
});

test('a language asked in the address wins over the stored one and the browser\'s: a link from the French public book opens French', () => {
  assert.equal(languageOf({ asked: 'fr', stored: 'en', languages: ['en-US'] }), 'fr');
  assert.equal(languageOf({ asked: 'en', stored: 'fr', languages: ['fr-CA'] }), 'en');
  assert.equal(languageOf({ asked: 'de', stored: 'fr', languages: ['en-US'] }), 'fr', 'a language the book is not written in is not asked');
  assert.equal(askedLanguage('?lang=fr'), 'fr');
  assert.equal(askedLanguage('?from=battle&lang=FR'), 'fr');
  assert.equal(askedLanguage(''), null);
});

test('a book the service worker gave from its kept copy is told apart from the network\'s', () => {
  assert.equal(isKeptCopy(new Headers({ 'X-Rulebook-Copy': 'kept' })), true);
  assert.equal(isKeptCopy(new Headers()), false);
});

test('the page says when the book is the copy this device kept, in each language, and is quiet until then', () => {
  assert.match(page, /<output id="rules-kept"[^>]*data-words="offline"[^>]*hidden/);
  assert.match(WORDS.en.offline, /^Offline: /);
  assert.match(WORDS.fr.offline, /^Hors ligne : /);
});

test('each language reads its own book, by a relative path', () => {
  assert.equal(bookFile('en'), 'rules.json');
  assert.equal(bookFile('fr'), 'rules.fr.json');
  assert.equal(bookFile('de'), 'rules.json');
});

test('the French book is the English one in French: the same parts, sections, numbers and cards', () => {
  assert.equal(french.language, 'fr');
  assert.deepEqual(contentsOf(french).map(part => [part.id, part.number, part.sections.map(section => [section.id, section.number])]),
    contentsOf(book).map(part => [part.id, part.number, part.sections.map(section => [section.id, section.number])]));
  assert.deepEqual(french.cards, book.cards);
  for (const card of french.cards) assert.equal(targetOf(french, `#${card.id}`)?.id, card.section, card.id);
});

test('the page says the same things in English and in French', () => {
  assert.deepEqual(Object.keys(WORDS.fr).sort(), Object.keys(WORDS.en).sort());
  for (const [key, english] of Object.entries(WORDS.en)) assert.equal(typeof WORDS.fr[key], typeof english, key);
});

test('a change reads in French with its date in French', () => {
  assert.equal(changeText({ date: '2026-11-16', text: 'Le livre officiel.' }, 'fr'), '16 novembre 2026 · Le livre officiel.');
  assert.equal(changeText({ date: '2026-02-01', text: 'x' }, 'fr'), '1er février 2026 · x');
});

test('every word of the page that the French translates has its place in rules.html', () => {
  const keys = [...page.matchAll(/data-words="([^"]+)"/g)].map(match => match[1]);
  assert.ok(keys.length >= 6);
  for (const key of keys) assert.equal(typeof WORDS.en[key], 'string', key);
  assert.match(page, /data-language="en"[^>]*lang="en"|lang="en"[^>]*data-language="en"/);
  assert.match(page, /data-language="fr"[^>]*lang="fr"|lang="fr"[^>]*data-language="fr"/);
});
