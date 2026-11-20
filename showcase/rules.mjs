import { contentsOf, latestFirst, targetOf, WORDS } from '../table/rules.js';
import { escape, layout } from './layout.mjs';

// The public book (docs/tabletop/rulebook-plan.md R7): the table's rulebook, table/rules.json or rules.fr.json,
// drawn once at build time as static HTML. Every word and number is the book's, as the table's page shows it;
// what the showcase adds is the page around it. No script: a section, a part, a card and an id the book moved
// are all plain fragments, so /rules/#tempo lands without one.

// Only the links the book's build lets through: a section, or a page on https.
const linkable = href => href.startsWith('#') || href.startsWith('https://');

function inline(spans) {
  return (spans ?? []).map(span => {
    let html = escape(span.text);
    if (span.href && linkable(span.href))
      html = `<a href="${escape(span.href)}"${span.href.startsWith('#') ? '' : ' rel="noopener"'}>${html}</a>`;
    if (span.emphasis) html = `<em>${html}</em>`;
    if (span.strong) html = `<strong>${html}</strong>`;
    return html;
  }).join('');
}

function block(entry) {
  if (entry.type === 'paragraph') return `<p>${inline(entry.spans)}</p>`;
  if (entry.type === 'list') {
    const tag = entry.ordered ? 'ol' : 'ul';
    return `<${tag}>${entry.items.map(item => `<li>${inline(item)}</li>`).join('')}</${tag}>`;
  }
  const head = entry.header.map(cell => '<th scope="col">' + inline(cell) + '</th>').join('');
  const body = entry.rows.map(row => '<tr>' + row.map(cell => '<td>' + inline(cell) + '</td>').join('') + '</tr>').join('');
  // A table wider than a phone scrolls; a keyboard reaches it to scroll it too.
  return `<div class="rules-table" tabindex="0"><table><thead><tr>${head}</tr></thead>
    <tbody>${body}</tbody></table></div>`;
}

// A concept card, as the battle and the table's page draw it: its title, its lines and its example, in the classes of
// the table's .concept-card rule, which the build copies into showcase.css (#810).
const card = (entry, words) => `<aside class="rules-card concept-card" aria-label="${escape(words.cardLabel(entry.title))}">
      <p class="eyebrow">${escape(words.conceptCard)}</p><h4 class="concept-card-title">${escape(entry.title)}</h4>
      <div class="concept-lines">${entry.lines.map(line => `<p>${escape(line)}</p>`).join('')}</div><p class="concept-example">${escape(entry.example)}</p></aside>`;

// The ids the page and its layout write: an alias never takes one, so the skip link and the contents stay theirs.
export const PAGE_IDS = ['top', 'main', 'rules-title', 'rules-contents', 'rules-contents-title', 'what-changed', 'rules-changes-title'];

/**
 * The addresses that land on each part or section beside its own id: an id the book moved (moved.json) and a
 * concept card's id, each where the table's page lands it (targetOf), and only when the book has no such id.
 */
function aliasesOf(book) {
  const own = new Set([...PAGE_IDS, ...book.parts.flatMap(part => [part.id, ...part.sections.map(section => section.id)])]);
  const aliases = new Map();
  for (const alias of [...Object.keys(book.moved ?? {}), ...(book.cards ?? []).map(entry => entry.id)]) {
    if (own.has(alias)) continue;
    const target = targetOf(book, '#' + alias);
    if (!target) continue;
    own.add(alias);
    aliases.set(target.id, [...(aliases.get(target.id) ?? []), alias]);
  }
  return aliases;
}

const anchors = (aliases, id) => (aliases.get(id) ?? []).map(alias => `<span class="rules-alias" id="${escape(alias)}"></span>`).join('');
const number = value => `<span class="rules-number">${escape(value)}</span>`;
const linkTo = (id, value, title, words) =>
  `<a class="rules-anchor" href="#${escape(id)}" aria-label="${escape(words.linkTo(value, title))}">#</a>`;

function date(change, words) {
  const [year, month, day] = change.date.split('-').map(Number);
  return words.date(year, month, day);
}

export function rulesPage(c, book, app) {
  const words = WORDS[book.language] ?? WORDS.en;
  const aliases = aliasesOf(book);
  const changes = latestFirst(book.changes);
  const contents = contentsOf(book).map(part => `<li><details><summary>${number(part.number)} ${escape(part.title)}</summary>
      <ol><li><a href="#${escape(part.id)}">${escape(words.introduction)}</a></li>${part.sections.map(section =>
    `<li><a href="#${escape(section.id)}">${number(section.number)} ${escape(section.title)}</a></li>`).join('')}</ol></details></li>`).join('');
  const parts = book.parts.map(part => `<section class="rules-part" id="${escape(part.id)}">${anchors(aliases, part.id)}
    <div class="rules-head"><h2>${number(part.number)} ${escape(part.title)}</h2>${linkTo(part.id, part.number, part.title, words)}</div>
    ${part.blocks.map(block).join('')}
    ${part.sections.map(section => `<section class="rules-section" id="${escape(section.id)}">${anchors(aliases, section.id)}
      <div class="rules-head"><h3>${number(section.number)} ${escape(section.title)}</h3><div class="rules-links">${linkTo(section.id, section.number, section.title, words)}<a class="rules-to-contents" href="#rules-contents">${escape(words.contents)}</a></div></div>
      ${section.blocks.map(block).join('')}${section.card ? card(section.card, words) : ''}
    </section>`).join('')}
  </section>`).join('');
  return layout(c, 'rules', app, c.rules, c.rulesDescription, `<div class="rules-page contain">
    <header class="rules-intro"><p class="kicker">${escape(words.eyebrow)}</p><h1 id="rules-title">${escape(words.heading)}</h1>
      <p class="lead">${escape(c.rulesLead)}</p>
      <a class="editorial-link" href="${new URL(`rules?lang=${book.language === 'fr' ? 'fr' : 'en'}`, app).href}">${escape(c.rulesInGame)} <span aria-hidden="true">→</span></a></header>
    <div class="rules-layout">
      <nav id="rules-contents" class="rules-contents" aria-labelledby="rules-contents-title">
        <h2 id="rules-contents-title">${escape(words.contents)}</h2><ol>${contents}</ol>
        <p><a href="#what-changed">${escape(words.whatChanged)}</a></p>
      </nav>
      <div class="rules-text">
        <article class="rules-book" aria-labelledby="rules-title">${parts}</article>
        <section id="what-changed" class="rules-changes" aria-labelledby="rules-changes-title">
          <h2 id="rules-changes-title">${escape(words.whatChanged)}</h2>
          <ul>${changes.map(change => `<li><time datetime="${escape(change.date)}">${escape(date(change, words))}</time> · ${escape(change.text)}</li>`).join('')}</ul>
          <p class="rules-drawn">${escape(c.rulesDrawn(changes[0] ? date(changes[0], words) : null))}</p>
        </section>
      </div>
    </div>
  </div>`);
}
