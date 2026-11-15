// Everything is already in the document. Search indexes the published names and opens the same entry as a direct link.
const search = document.querySelector('[data-search]');
const input = search.querySelector('input');
const clear = search.querySelector('[data-clear]');
const results = document.querySelector('[data-results]');
const list = results.querySelector('ul');
const empty = results.querySelector('[data-empty]');
const status = search.querySelector('[data-search-status]');
const french = document.documentElement.lang === 'fr';
const normalize = value => value.normalize('NFD').replace(/\p{M}/gu, '').replace(/[’']/g, ' ').toLowerCase().trim();
const entries = [...document.querySelectorAll('[data-form]')].map(form => ({
  form, name: form.dataset.form, spells: [...form.querySelectorAll('[data-spell-name]')],
}));

function reveal(hash, scroll = true) {
  const target = document.getElementById(hash.slice(1));
  if (!target) return;
  const record = target.closest('details');
  if (record) record.open = true;
  if (scroll) target.scrollIntoView({ block: 'start' });
}
window.addEventListener('hashchange', () => reveal(location.hash));
document.addEventListener('click', event => {
  const link = event.target.closest('a[href^="#"]');
  if (link) reveal(link.hash, false);
});
if (location.hash) reveal(location.hash);

function update() {
  const query = normalize(input.value);
  list.replaceChildren();
  results.hidden = clear.hidden = !query;
  if (!query) { status.textContent = ''; return; }
  for (const { form, name, spells } of entries) {
    const matching = spells.filter(spell => normalize(spell.dataset.spellName).includes(query));
    if (!matching.length && !normalize(name).includes(query)) continue;
    const item = document.createElement('li');
    const link = document.createElement('a');
    link.href = '#' + (matching[0]?.id ?? form.id);
    link.textContent = matching.length ? name + ' · ' + matching.map(s => s.dataset.spellName).join(' / ') : name;
    item.append(link);
    list.append(item);
  }
  empty.hidden = list.children.length > 0;
  status.textContent = list.children.length ? (french ? 'Formes trouvées' : 'Matching forms') + ' · ' + list.children.length : empty.textContent;
}
input.addEventListener('input', update);
clear.addEventListener('click', () => { input.value = ''; update(); input.focus(); });
search.hidden = false;
