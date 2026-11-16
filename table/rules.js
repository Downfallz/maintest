// The official rulebook's page (/rules, ADR 0188, docs/tabletop/rulebook-plan.md R3): the book built from rules/
// into rules.json, drawn as it is, with its contents, a deep link per section, a filter and what changed. The page
// writes no rule: every word and number is the book's, and the book draws its numbers from the table's rule set.

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

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

/** A change as the page lists it: its date in words, then what changed. */
export function changeText(change) {
  const [year, month, day] = change.date.split('-').map(Number);
  return `${day} ${MONTHS[month - 1]} ${year} · ${change.text}`;
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

// A concept card, as the battle draws it in a tutorial: its title, its lines and its example.
function drawCard(doc, card) {
  const aside = node(doc, 'aside', 'rules-card');
  aside.setAttribute('aria-label', `Concept card: ${card.title}`);
  const lines = node(doc, 'div', 'concept-lines');
  lines.append(...card.lines.map(line => node(doc, 'p', null, line)));
  aside.append(node(doc, 'p', 'eyebrow', 'Concept card'), node(doc, 'h4', 'rules-card-title', card.title), lines, node(doc, 'p', 'concept-example', card.example));
  return aside;
}

function heading(doc, tag, number, title) {
  const element = node(doc, tag);
  element.tabIndex = -1;
  element.append(node(doc, 'span', 'rules-number', String(number)), doc.createTextNode(` ${title}`));
  return element;
}

// The section's own address, beside its heading rather than in it, so the heading reads as its title alone.
function anchorTo(doc, id, number, title) {
  const anchor = node(doc, 'a', 'rules-anchor', '#');
  anchor.href = `#${id}`;
  anchor.setAttribute('aria-label', `Link to ${number} ${title}`);
  return anchor;
}

function backToContents(doc) {
  const link = node(doc, 'a', 'rules-to-contents', 'Contents');
  link.href = '#rules-contents';
  return link;
}

/** The book, every part and section with its blocks and card, into the page's article. */
export function drawBook(doc, book) {
  const article = doc.getElementById('rules-book');
  const parts = book.parts.map(part => {
    const element = node(doc, 'section', 'rules-part');
    element.id = part.id;
    element.dataset.part = part.id;
    const partHead = node(doc, 'div', 'rules-part-head');
    partHead.append(heading(doc, 'h2', part.number, part.title), anchorTo(doc, part.id, part.number, part.title));
    element.append(partHead, ...part.blocks.map(block => drawBlock(doc, block)));
    for (const section of part.sections) {
      const entry = node(doc, 'section', 'rules-section');
      entry.id = section.id;
      entry.dataset.section = section.id;
      const head = node(doc, 'div', 'rules-section-head');
      const links = node(doc, 'div', 'rules-section-links');
      links.append(anchorTo(doc, section.id, section.number, section.title), backToContents(doc));
      head.append(heading(doc, 'h3', section.number, section.title), links);
      entry.append(head, ...section.blocks.map(block => drawBlock(doc, block)));
      if (section.card) entry.append(drawCard(doc, section.card));
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
    const openingLink = node(doc, 'a', null, 'Introduction');
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
  changes.replaceChildren(...ordered.map(change => node(doc, 'li', null, changeText(change))));
  const latest = ordered[0];
  doc.getElementById('rules-drawn').textContent = `Every number in this book is drawn from the rule set and the content this table plays${latest ? `. Last changed ${changeText(latest).split(' · ')[0]}.` : '.'}`;
}

const SECTION_COUNT = count => `${count} section${count === 1 ? '' : 's'}`;

/** Shows what the filter keeps, in the book and in the contents, and says how much that is. */
export function applyFilter(doc, book, query) {
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
  const status = doc.getElementById('rules-status');
  if (!kept) status.textContent = `${book.parts.length} parts, ${SECTION_COUNT(total)}`;
  else if (kept.sections.size === 0 && kept.parts.size === 0) status.textContent = 'No rule holds those words. Try another word.';
  else status.textContent = `${SECTION_COUNT(kept.sections.size)} of ${total} hold those words`;
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

export function mountRules(doc, book, win = globalThis) {
  drawBook(doc, book);
  const filter = doc.getElementById('rules-filter');
  filter.addEventListener('input', () => applyFilter(doc, book, filter.value));
  doc.getElementById('rules-filter-form').addEventListener('submit', event => {
    event.preventDefault();
    const first = doc.querySelector('#rules-book [data-section]:not([hidden])');
    if (!first) return;
    // The address already there fires no hashchange: the landing is then the page's own.
    if (decoded(win.location.hash) === first.id) land(doc, book, win.location.hash, { win });
    else win.location.hash = first.id;
  });
  applyFilter(doc, book, filter.value);
  win.addEventListener('hashchange', () => land(doc, book, win.location.hash, { win }));
  doc.getElementById('rules-loading').hidden = true;
  doc.body.dataset.rules = 'ready';
  // The book arrives after the browser looked for the anchor, so the first landing is the page's own.
  if (win.location.hash) land(doc, book, win.location.hash, { win, focus: false });
}

async function load(doc) {
  try {
    const response = await fetch('rules.json', { credentials: 'same-origin' });
    if (!response.ok) throw new Error(`The host answered ${response.status}.`);
    mountRules(doc, await response.json());
  } catch {
    doc.getElementById('rules-loading').hidden = true;
    const problem = doc.getElementById('rules-problem');
    problem.textContent = 'Could not read the rulebook. Check your connection and reload the page.';
    problem.hidden = false;
  }
}

if (typeof document !== 'undefined') void load(document);
