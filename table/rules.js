// The official rulebook's page (/rules, ADR 0188, docs/tabletop/rulebook-plan.md R3): the book built from rules/
// into rules.json, drawn as it is, with its contents, a deep link per section, a filter and what changed. The page
// writes no rule: every word and number is the book's, and the book draws its numbers from the table's rule set.
// The book is read in English or in French (R6): rules.json or rules.fr.json, the same book in two languages.

export const LANGUAGES = ['en', 'fr'];
// The language chosen on this page, kept on the device: an account holds no language yet.
const STORED_LANGUAGE = 'downfall.language';

/**
 * The language the page opens in: the one the address asks for (a link from the public book, `?lang=fr`), else the
 * one stored on the device, else the browser's first English or French.
 */
export function languageOf({ asked, stored, languages } = {}) {
  if (LANGUAGES.includes(asked)) return asked;
  if (LANGUAGES.includes(stored)) return stored;
  for (const tag of languages ?? []) {
    const base = String(tag ?? '').toLowerCase().split('-')[0];
    if (LANGUAGES.includes(base)) return base;
  }
  return 'en';
}

/** The language an address asks for (`?lang=`), lower case, or null. */
export function askedLanguage(search) {
  return new URLSearchParams(search ?? '').get('lang')?.toLowerCase() ?? null;
}

// The service worker marks the book it gives from its kept copy (table/sw.js, #698).
const KEPT_COPY = 'X-Rulebook-Copy';

/** Whether an answer's headers say it is the copy this device kept rather than the network's. */
export function isKeptCopy(headers) {
  return headers?.get(KEPT_COPY) === 'kept';
}

/** The book a language reads, by a relative path, so the page works at /rules and under /table/. */
export function bookFile(language) {
  return language === 'fr' ? 'rules.fr.json' : 'rules.json';
}

// A count and its noun: English says "0 sections", French "0 section".
const count = (number, one, many) => `${number} ${number === 1 ? one : many}`;
const compte = (number, one, many) => `${number} ${number <= 1 ? one : many}`;

/** The page's own words, in each language; the rules themselves are the book's. */
export const WORDS = {
  en: {
    documentTitle: 'Downfall Instinct — the rules',
    eyebrow: 'The official rulebook',
    heading: 'The rules.',
    detail: 'How a match is played at this table, part by part. Every number is the one this table uses.',
    languages: 'Language',
    filterLabel: 'Find a rule',
    placeholder: 'sunder, stun, tempo…',
    loading: 'Reading the rulebook…',
    contents: 'Contents',
    whatChanged: 'What changed',
    introduction: 'Introduction',
    conceptCard: 'Concept card',
    cardLabel: title => `Concept card: ${title}`,
    linkTo: (number, title) => `Link to ${number} ${title}`,
    total: (parts, sections) => `${parts} parts, ${count(sections, 'section', 'sections')}`,
    kept: (sections, total) => `${count(sections, 'section', 'sections')} of ${total} ${sections === 1 ? 'holds' : 'hold'} those words`,
    noMatch: 'No rule holds those words. Try another word.',
    problem: 'Could not read the rulebook. Check your connection and reload the page.',
    offline: 'Offline: this is the rulebook as this device last read it. The table may play a newer one now.',
    drawn: date => 'Every number in this book is drawn from the rule set and the content this table plays' + (date ? `. Last changed ${date}.` : '.'),
    date: (year, month, day) => `${day} ${['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'][month - 1]} ${year}`,
  },
  fr: {
    documentTitle: 'Downfall Instinct — les règles',
    eyebrow: 'Le livre de règles officiel',
    heading: 'Les règles.',
    detail: 'Comment se joue une partie à cette table, chapitre par chapitre. Chaque nombre est celui qu’utilise cette table.',
    languages: 'Langue',
    filterLabel: 'Trouver une règle',
    placeholder: 'brèche, stun, tempo…',
    loading: 'Lecture du livre de règles…',
    contents: 'Sommaire',
    whatChanged: 'Ce qui a changé',
    introduction: 'Introduction',
    conceptCard: 'Carte concept',
    cardLabel: title => `Carte concept : ${title}`,
    linkTo: (number, title) => `Lien vers ${number} ${title}`,
    total: (parts, sections) => `${compte(parts, 'chapitre', 'chapitres')}, ${compte(sections, 'section', 'sections')}`,
    kept: (sections, total) => `${compte(sections, 'section', 'sections')} sur ${total} ${sections > 1 ? 'contiennent' : 'contient'} ces mots`,
    noMatch: 'Aucune règle ne contient ces mots. Essayez un autre mot.',
    problem: 'Impossible de lire le livre de règles. Vérifiez votre connexion et rechargez la page.',
    offline: 'Hors ligne : voici le livre de règles tel que cet appareil l’a lu la dernière fois. La table en joue peut-être un plus récent.',
    drawn: date => 'Chaque nombre de ce livre vient des règles et du contenu de cette table' + (date ? `. Dernière modification le ${date}.` : '.'),
    date: (year, month, day) => `${day === 1 ? '1er' : day} ${['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre'][month - 1]} ${year}`,
  },
};

const wordsOf = language => WORDS[language] ?? WORDS.en;

/** A text as the filter compares it: lower case, without accents, its spaces collapsed. */
export function normalized(text) {
  return String(text).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/\s+/g, ' ').trim();
}

/** Spans read as plain text, whatever marks they carry. */
export function spanText(spans) {
  return (spans ?? []).map(span => span.text).join('');
}

function blockText(block) {
  if (block.type === 'paragraph') return spanText(block.spans);
  if (block.type === 'list') return block.items.map(spanText).join(' ');
  if (block.type === 'table') return [block.header, ...block.rows].flatMap(row => row.map(spanText)).join(' ');
  return '';
}

function cardText(card) {
  return card ? [card.title, ...card.lines, card.example].join(' ') : '';
}

/** The contents: every part in order, with its sections' ids, numbers and titles. */
export function contentsOf(book) {
  return book.parts.map(part => ({
    id: part.id,
    number: part.number,
    title: part.title,
    sections: part.sections.map(section => ({ id: section.id, number: section.number, title: section.title })),
  }));
}

function decoded(hash) {
  try {
    return decodeURIComponent(String(hash ?? '').replace(/^#/, ''));
  } catch {
    return '';
  }
}

/**
 * Where a deep link lands: a section or a part by its id, an id the book moved by its redirects (moved.json), a
 * concept card on the section it summarises. Null for anything else, the page's own anchors included.
 */
export function targetOf(book, hash) {
  const asked = decoded(hash);
  if (!asked) return null;
  const at = id => {
    for (const part of book.parts) {
      if (part.id === id) return { id, part: part.id };
      if (part.sections.some(section => section.id === id)) return { id, part: part.id };
    }
    return null;
  };
  const found = at(asked);
  if (found) return found;
  let id = asked;
  const seen = new Set([id]);
  while (Object.hasOwn(book.moved ?? {}, id)) {
    id = book.moved[id];
    if (seen.has(id)) return null;
    seen.add(id);
    const landed = at(id);
    if (landed) return { ...landed, movedFrom: asked };
  }
  const card = (book.cards ?? []).find(entry => entry.id === asked);
  const section = card && at(card.section);
  return section ? { ...section, card: card.id } : null;
}

/**
 * What the filter keeps: the sections whose title, text or card hold every word typed, and the parts they sit in.
 * A part whose own title or opening holds them keeps all its sections. Null when nothing is typed: everything shows.
 */
export function matching(book, query) {
  const words = normalized(query).split(' ').filter(Boolean);
  if (words.length === 0) return null;
  const holds = text => { const read = normalized(text); return words.every(word => read.includes(word)); };
  const sections = new Set();
  const parts = new Set();
  for (const part of book.parts) {
    const whole = holds([part.title, ...part.blocks.map(blockText)].join(' '));
    for (const section of part.sections) {
      if (whole || holds([section.title, ...section.blocks.map(blockText), cardText(section.card)].join(' '))) {
        sections.add(section.id);
        parts.add(part.id);
      }
    }
    if (whole) parts.add(part.id);
  }
  return { sections, parts };
}

/** The changes, the latest first, whatever order the book lists them in; the book itself is left as it is. */
export function latestFirst(changes) {
  return [...(changes ?? [])].sort((a, b) => b.date.localeCompare(a.date));
}

/** A change's date in words, in the book's language. */
function dateText(change, language) {
  const [year, month, day] = change.date.split('-').map(Number);
  return wordsOf(language).date(year, month, day);
}

/** A change as the page lists it: its date in words, then what changed. */
export function changeText(change, language = 'en') {
  return `${dateText(change, language)} · ${change.text}`;
}

// --- drawing --------------------------------------------------------------------------------------------------------

function node(doc, tag, className, text) {
  const element = doc.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}

function inline(doc, spans) {
  return (spans ?? []).map(span => {
    let element;
    // Only the links the build lets through (a section, or a page on https), whatever the file says.
    if (span.href?.startsWith('#') || span.href?.startsWith('https://')) {
      element = node(doc, 'a', null, span.text);
      element.href = span.href;
      if (!span.href.startsWith('#')) { element.target = '_blank'; element.rel = 'noopener'; }
    } else {
      element = doc.createTextNode(span.text);
    }
    if (span.emphasis) { const wrap = node(doc, 'em'); wrap.append(element); element = wrap; }
    if (span.strong) { const wrap = node(doc, 'strong'); wrap.append(element); element = wrap; }
    return element;
  });
}

function drawBlock(doc, block) {
  if (block.type === 'paragraph') {
    const paragraph = node(doc, 'p');
    paragraph.append(...inline(doc, block.spans));
    return paragraph;
  }
  if (block.type === 'list') {
    const list = node(doc, block.ordered ? 'ol' : 'ul');
    for (const item of block.items) { const entry = node(doc, 'li'); entry.append(...inline(doc, item)); list.append(entry); }
    return list;
  }
  const scroll = node(doc, 'div', 'rules-table');
  const table = node(doc, 'table');
  const head = node(doc, 'thead');
  const headRow = node(doc, 'tr');
  for (const cell of block.header) { const th = node(doc, 'th'); th.scope = 'col'; th.append(...inline(doc, cell)); headRow.append(th); }
  head.append(headRow);
  const body = node(doc, 'tbody');
  for (const row of block.rows) {
    const line = node(doc, 'tr');
    for (const cell of row) { const td = node(doc, 'td'); td.append(...inline(doc, cell)); line.append(td); }
    body.append(line);
  }
  table.append(head, body);
  scroll.append(table);
  return scroll;
}

/**
 * A table wider than the screen scrolls sideways: it is then a stop of the keyboard, named by its section, so a reader
 * without a pointer can scroll it (#1059, axe's scrollable-region-focusable). One that fits is no stop.
 */
export function reachScrolls(doc) {
  for (const scroll of doc.querySelectorAll('#rules-book .rules-table')) {
    if (scroll.scrollWidth > scroll.clientWidth) {
      scroll.tabIndex = 0;
      scroll.setAttribute('role', 'region');
      const heading = scroll.closest('[data-section]')?.querySelector('h3') ?? scroll.closest('[data-part]')?.querySelector('h2');
      if (heading) scroll.setAttribute('aria-label', heading.textContent.trim());
    } else {
      scroll.removeAttribute('tabindex');
      scroll.removeAttribute('role');
      scroll.removeAttribute('aria-label');
    }
  }
}

// A concept card, as the battle draws it in a tutorial: its title, its lines and its example.
function drawCard(doc, card, words) {
  const aside = node(doc, 'aside', 'rules-card concept-card');
  aside.setAttribute('aria-label', words.cardLabel(card.title));
  const lines = node(doc, 'div', 'concept-lines');
  lines.append(...card.lines.map(line => node(doc, 'p', null, line)));
  aside.append(node(doc, 'p', 'eyebrow', words.conceptCard), node(doc, 'h4', 'concept-card-title', card.title), lines, node(doc, 'p', 'concept-example', card.example));
  return aside;
}

function heading(doc, tag, number, title) {
  const element = node(doc, tag);
  element.tabIndex = -1;
  element.append(node(doc, 'span', 'rules-number', String(number)), doc.createTextNode(` ${title}`));
  return element;
}

// The section's own address, beside its heading rather than in it, so the heading reads as its title alone.
function anchorTo(doc, id, number, title, words) {
  const anchor = node(doc, 'a', 'rules-anchor', '#');
  anchor.href = `#${id}`;
  anchor.setAttribute('aria-label', words.linkTo(number, title));
  return anchor;
}

function backToContents(doc, words) {
  const link = node(doc, 'a', 'rules-to-contents', words.contents);
  link.href = '#rules-contents';
  return link;
}

/** The book, every part and section with its blocks and card, into the page's article. */
export function drawBook(doc, book) {
  const words = wordsOf(book.language);
  const article = doc.getElementById('rules-book');
  const parts = book.parts.map(part => {
    const element = node(doc, 'section', 'rules-part');
    element.id = part.id;
    element.dataset.part = part.id;
    const partHead = node(doc, 'div', 'rules-part-head');
    partHead.append(heading(doc, 'h2', part.number, part.title), anchorTo(doc, part.id, part.number, part.title, words));
    element.append(partHead, ...part.blocks.map(block => drawBlock(doc, block)));
    for (const section of part.sections) {
      const entry = node(doc, 'section', 'rules-section');
      entry.id = section.id;
      entry.dataset.section = section.id;
      const head = node(doc, 'div', 'rules-section-head');
      const links = node(doc, 'div', 'rules-section-links');
      links.append(anchorTo(doc, section.id, section.number, section.title, words), backToContents(doc, words));
      head.append(heading(doc, 'h3', section.number, section.title), links);
      entry.append(head, ...section.blocks.map(block => drawBlock(doc, block)));
      if (section.card) entry.append(drawCard(doc, section.card, words));
      element.append(entry);
    }
    return element;
  });
  article.replaceChildren(...parts);

  const contents = doc.getElementById('rules-contents-list');
  contents.replaceChildren(...contentsOf(book).map(part => {
    const item = node(doc, 'li');
    item.dataset.part = part.id;
    const details = node(doc, 'details');
    const summary = node(doc, 'summary');
    summary.append(node(doc, 'span', 'rules-number', String(part.number)), doc.createTextNode(` ${part.title}`));
    const list = node(doc, 'ol', 'rules-contents-sections');
    const opening = node(doc, 'li');
    const openingLink = node(doc, 'a', null, words.introduction);
    openingLink.href = `#${part.id}`;
    opening.append(openingLink);
    list.append(opening, ...part.sections.map(section => {
      const entry = node(doc, 'li');
      entry.dataset.section = section.id;
      const link = node(doc, 'a');
      link.href = `#${section.id}`;
      link.append(node(doc, 'span', 'rules-number', section.number), doc.createTextNode(` ${section.title}`));
      entry.append(link);
      return entry;
    }));
    details.append(summary, list);
    item.append(details);
    return item;
  }));

  const changes = doc.getElementById('rules-changes-list');
  const ordered = latestFirst(book.changes);
  changes.replaceChildren(...ordered.map(change => node(doc, 'li', null, changeText(change, book.language))));
  const latest = ordered[0];
  doc.getElementById('rules-drawn').textContent = words.drawn(latest ? dateText(latest, book.language) : null);
}

/** The page's own words in a language: the document's language and title, and every element marked data-words. */
export function translatePage(doc, language) {
  const words = wordsOf(language);
  doc.documentElement.lang = LANGUAGES.includes(language) ? language : 'en';
  doc.title = words.documentTitle;
  for (const element of doc.querySelectorAll('[data-words]')) element.textContent = words[element.dataset.words];
  doc.getElementById('rules-filter').placeholder = words.placeholder;
  for (const button of doc.querySelectorAll('[data-language]')) button.setAttribute('aria-pressed', String(button.dataset.language === language));
}

/** Shows what the filter keeps, in the book and in the contents, and says how much that is. */
export function applyFilter(doc, book, query) {
  const words = wordsOf(book.language);
  const kept = matching(book, query);
  const total = book.parts.reduce((sum, part) => sum + part.sections.length, 0);
  for (const element of doc.querySelectorAll('#rules-book [data-section], #rules-contents-list [data-section]')) {
    element.hidden = Boolean(kept) && !kept.sections.has(element.dataset.section);
  }
  for (const element of doc.querySelectorAll('#rules-book [data-part], #rules-contents-list [data-part]')) {
    element.hidden = Boolean(kept) && !kept.parts.has(element.dataset.part);
    const details = element.querySelector(':scope > details');
    if (details && kept) details.open = kept.parts.has(element.dataset.part);
  }
  doc.getElementById('rules-changes').hidden = Boolean(kept);
  // A table the filter hid measured no width: what it shows again is measured again, whoever cleared it.
  reachScrolls(doc);
  const status = doc.getElementById('rules-status');
  if (!kept) status.textContent = words.total(book.parts.length, total);
  else if (kept.sections.size === 0 && kept.parts.size === 0) status.textContent = words.noMatch;
  else status.textContent = words.kept(kept.sections.size, total);
  return kept;
}

/** Opens the deep link's section: the filter cleared if it hides it, its part open in the contents, at the top. */
export function land(doc, book, hash, { win = globalThis, focus = true } = {}) {
  // What changed is the page's own, hidden while a filter reads the book: reaching it shows the whole book again.
  if (decoded(hash) === 'what-changed') {
    const filter = doc.getElementById('rules-filter');
    if (filter.value) { filter.value = ''; applyFilter(doc, book, ''); }
    doc.getElementById('what-changed').scrollIntoView({ block: 'start' });
    return null;
  }
  const target = targetOf(book, hash);
  if (!target) return null;
  const filter = doc.getElementById('rules-filter');
  const element = doc.getElementById(target.id);
  if (!element) return null;
  if (element.hidden || element.closest('[hidden]')) { filter.value = ''; applyFilter(doc, book, ''); }
  for (const link of doc.querySelectorAll('#rules-contents-list a[aria-current]')) link.removeAttribute('aria-current');
  const entry = doc.querySelector(`#rules-contents-list [data-part="${target.part}"] > details`);
  if (entry) entry.open = true;
  const current = doc.querySelector(`#rules-contents-list a[href="#${target.id}"]`);
  if (current) current.setAttribute('aria-current', 'location');
  // A moved id is shown as the id it moved to, so the address a player copies is the one that lasts.
  if (target.id !== decoded(hash) && win.history?.replaceState) win.history.replaceState(win.history.state, '', `#${target.id}`);
  element.scrollIntoView({ block: 'start' });
  if (focus) element.querySelector('h2, h3')?.focus({ preventScroll: true });
  return target;
}

/**
 * Draws the book and listens to the filter and the address; `show` draws another book in its place (the other
 * language), keeping what is typed in the filter and the section in the address.
 */
export function mountRules(doc, book, win = globalThis) {
  const state = { book };
  const filter = doc.getElementById('rules-filter');
  filter.addEventListener('input', () => applyFilter(doc, state.book, filter.value));
  win.addEventListener('resize', () => reachScrolls(doc));
  // The display face can widen a table once it has loaded.
  doc.fonts?.ready.then(() => reachScrolls(doc));
  doc.getElementById('rules-filter-form').addEventListener('submit', event => {
    event.preventDefault();
    const first = doc.querySelector('#rules-book [data-section]:not([hidden])');
    if (!first) return;
    // The address already there fires no hashchange: the landing is then the page's own.
    if (decoded(win.location.hash) === first.id) land(doc, state.book, win.location.hash, { win });
    else win.location.hash = first.id;
  });
  win.addEventListener('hashchange', () => land(doc, state.book, win.location.hash, { win }));
  const show = shown => {
    state.book = shown;
    translatePage(doc, shown.language);
    drawBook(doc, shown);
    applyFilter(doc, shown, filter.value);
    doc.getElementById('rules-problem').hidden = true;
    doc.getElementById('rules-loading').hidden = true;
    doc.body.dataset.rules = 'ready';
    doc.body.dataset.language = shown.language ?? 'en';
    // The book arrives after the browser looked for the anchor, so the landing is the page's own.
    if (win.location.hash) land(doc, shown, win.location.hash, { win, focus: false });
  };
  show(book);
  return { show, get book() { return state.book; } };
}

function stored(win) {
  try {
    return win.localStorage?.getItem(STORED_LANGUAGE) ?? null;
  } catch {
    return null;
  }
}

function keep(win, language) {
  try {
    win.localStorage?.setItem(STORED_LANGUAGE, language);
  } catch {
    // A browser that keeps nothing still shows the language chosen, until the page is left.
  }
}

// A book and whether it is the device's kept copy.
async function bookIn(language) {
  const response = await fetch(bookFile(language), { credentials: 'same-origin' });
  if (!response.ok) throw new Error(`The host answered ${response.status}.`);
  return { book: await response.json(), kept: isKeptCopy(response.headers) };
}

// A choice made on the page, once its book is read, is the reader's from then on: the language a link asked for
// leaves the address, so a reload reads the one chosen.
function forgetAsked(win) {
  try {
    const url = new URL(win.location.href);
    if (!url.searchParams.has('lang')) return;
    url.searchParams.delete('lang');
    win.history?.replaceState(win.history.state, '', `${url.pathname}${url.search}${url.hash}`);
  } catch {
    // An address that cannot be rewritten keeps its language until the page is left.
  }
}

function trouble(doc, language) {
  doc.getElementById('rules-loading').hidden = true;
  const problem = doc.getElementById('rules-problem');
  problem.textContent = wordsOf(language).problem;
  problem.hidden = false;
}

async function load(doc, win = globalThis) {
  const language = languageOf({ asked: askedLanguage(win.location?.search), stored: stored(win), languages: win.navigator?.languages ?? [win.navigator?.language] });
  translatePage(doc, language);
  let rules = null;
  // Each request for a book takes a ticket; only the latest one's answer is drawn, so two quick taps end on the
  // second, and the first load never covers a book the reader chose meanwhile.
  let latest = 0;
  const show = ({ book, kept }) => {
    if (rules) rules.show(book);
    else rules = mountRules(doc, book, win);
    // A page kept from before the notice has none: the book is still drawn.
    const notice = doc.getElementById('rules-kept');
    if (notice) notice.hidden = !kept;
  };
  for (const button of doc.querySelectorAll('[data-language]')) {
    button.addEventListener('click', async () => {
      const chosen = button.dataset.language;
      const ticket = ++latest;
      if (rules?.book.language === chosen) {
        keep(win, chosen);
        forgetAsked(win);
        translatePage(doc, chosen);
        doc.getElementById('rules-problem').hidden = true;
        return;
      }
      try {
        const read = await bookIn(chosen);
        if (ticket !== latest) return;
        // Kept only once its book is read: a choice that cannot be read must not be the next visit's.
        keep(win, chosen);
        forgetAsked(win);
        show(read);
      } catch {
        if (ticket !== latest) return;
        const shown = rules?.book.language ?? language;
        translatePage(doc, shown);
        trouble(doc, shown);
      }
    });
  }
  const ticket = ++latest;
  // The language's book, else the other one: a book the worker kept is better than none.
  for (const each of [language, ...LANGUAGES.filter(one => one !== language)]) {
    try {
      const read = await bookIn(each);
      if (ticket === latest || !rules) show(read);
      return;
    } catch {
      // The next language's book.
    }
  }
  if (!rules) trouble(doc, language);
}

if (typeof document !== 'undefined') {
  // The claws on the arch: the rules hold the game up (docs/art/journeys/pages.md). Loaded apart, and its failure let
  // go, so the book draws whatever the worker kept. Offline the worker gives the kit and this painting's 354 and 708 px
  // files (#1060): a screen that asks for a wider one, a phone of density 3 among them, has a band of soot and words.
  import('./picture.js').then(({ wearPageHead }) => wearPageHead(document, 'latch-under-the-arch', { page: 'table/rules', focus: '50% 55%' })).catch(() => {});
  await load(document);
}
