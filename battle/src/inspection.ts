import { creatureInspection } from '../../table/battle-inspection.js';
import { cardsById } from '../../table/card.js';
import { conditionKey, iconElement } from './condition-icons';
import type { SpellCatalogue } from './combat';
import { spellCard } from './spell-card';

type Inspection = NonNullable<ReturnType<typeof creatureInspection>>;
type Cue = { kind: string; glyph?: string; harmful: boolean };
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
const node = (tag: string, value = '', className = '') => {
  const item = document.createElement(tag); if (value) item.textContent = value; if (className) item.className = className; return item;
};

// The creature at a glance: health with its bar, then energy, defense and initiative, each with the icon the
// field and the spell cards use for that stat. A screen reader hears the same as one sentence (#inspect-stats).
function sheetTiles(sheet: Inspection['sheet']) {
  const value = (one: number | null) => one === null ? '—' : String(one);
  const tile = (stat: string, icon: string, label: string, shown: string) => {
    const item = node('div', '', 'inspect-tile'); item.dataset.stat = stat;
    item.append(iconElement(icon), node('span', label, 'inspect-tile-label'), node('strong', shown, 'inspect-tile-value'));
    return item;
  };
  const health = tile('health', 'health-gain', 'Health', `${value(sheet.health)}/${value(sheet.maxHealth)}`);
  const share = sheet.health !== null && sheet.maxHealth ? Math.max(0, Math.min(1, sheet.health / sheet.maxHealth)) : 0;
  const bar = node('span', '', 'inspect-tile-bar'); const fill = node('span'); fill.style.width = `${Math.round(share * 100)}%`; bar.append(fill);
  health.append(bar);
  return [health, tile('energy', 'energy-gain', 'Energy', value(sheet.energy)),
    tile('defense', 'defense-gain', 'Defense', value(sheet.defense)), tile('initiative', 'initiative-gain', 'Initiative', value(sheet.initiative))];
}

// A status or a condition as a card: its icon in a medallion coloured by what it moves, its name, how long it
// lasts and who put it there.
function conditionCard(key: string, name: string, duration = '', source = '') {
  const item = node('li', '', 'inspect-condition'); item.dataset.cue = key;
  const medallion = node('span', '', 'inspect-condition-icon'); medallion.append(iconElement(key));
  const words = node('span', '', 'inspect-condition-text');
  words.append(node('strong', name));
  if (duration) words.append(node('span', ` · ${duration}`, 'inspect-condition-duration'));
  if (source) words.append(node('small', ` · ${source}`));
  item.append(medallion, words);
  return item;
}
const STATUS_ICONS: Record<string, string> = { defeated: 'health-loss', stun: 'stun', immune: 'stun' };

// Each disclosure is keyed independently: HP polls must not rebuild a card, reset scroll or steal focus.
export function inspectionControls() {
  let identity = '', mutationsKey = '', spellsKey = '', conditionsKey = '';
  const dialog = el<HTMLDialogElement>('inspect-dialog');
  // The portrait zooms: a tap shows the illustration large over the whole card, a tap anywhere on it or Escape
  // gives the card back. It shows what the portrait shows, never another image.
  const zoom = document.getElementById('inspect-zoom') ?? document.createElement('div');
  const picture = (document.getElementById('inspect-zoom-close') ?? document.createElement('button')) as HTMLButtonElement;
  zoom.hidden = true;
  function unzoom(focus = true) {
    if (zoom.hidden) return;
    zoom.hidden = true; if (focus) el('inspect-portrait').focus({ preventScroll: true });
  }
  el('inspect-portrait').addEventListener('click', () => {
    const portrait = el('inspect-portrait');
    if (!portrait.style.backgroundImage) return;
    picture.style.backgroundImage = portrait.style.backgroundImage; picture.dataset.side = portrait.dataset.side ?? 'ally';
    picture.dataset.defeated = portrait.dataset.defeated ?? 'false';
    zoom.hidden = false; picture.focus({ preventScroll: true });
  });
  picture.addEventListener('click', () => unzoom());
  dialog.addEventListener('cancel', event => { if (!zoom.hidden) { event.preventDefault(); unzoom(); } });
  dialog.addEventListener('close', () => unzoom(false));
  function clear() {
    identity = ''; mutationsKey = ''; spellsKey = ''; conditionsKey = '';
    unzoom(false); dialog.close();
    for (const id of ['inspect-name', 'inspect-side', 'inspect-context', 'inspect-stats', 'inspect-sheet', 'inspect-appearance', 'inspect-effects', 'inspect-mutations', 'inspect-spells', 'inspect-last-action']) el(id).replaceChildren();
    el('inspect-portrait').style.backgroundImage = ''; delete el('inspect-portrait').dataset.artwork;
    // The tiles are gone with the rest, so their key goes too: the same stats seen again must draw them again.
    delete el('inspect-sheet').dataset.content;
    el('inspect-history').hidden = true; el('inspect-growth').hidden = true;
  }
  function render(model: Inspection | null, context: string, historical: boolean, lastAction: string, catalogue: SpellCatalogue | null = null) {
    if (!model) { clear(); return; }
    if (identity !== model.id) { mutationsKey = ''; spellsKey = ''; conditionsKey = ''; el('inspect-body').scrollTop = 0; identity = model.id; }
    const cues: Cue[] = catalogue?.effects ?? [];
    text('inspect-name', model.name); text('inspect-side', model.side); text('inspect-context', context);
    const portrait = el('inspect-portrait');
    // The illustration the field shows (ADR 0151) once it has loaded; the audited sprite of its form until then,
    // and for good if it never loads. One image at a time: their transparent parts would show each other.
    const sprite = model.form ? `url("battle-art/${model.form}.webp")` : '';
    if (portrait.dataset.artwork !== (model.artworkFile ?? '')) {
      portrait.dataset.artwork = model.artworkFile ?? '';
      portrait.style.backgroundImage = sprite;
      if (model.artworkFile) {
        const wanted = model.artworkFile, image = new Image();
        image.onload = () => { if (portrait.dataset.artwork === wanted) portrait.style.backgroundImage = `url("battle-art/${wanted}")`; };
        image.src = `battle-art/${wanted}`;
      }
    } else if (!model.artworkFile) portrait.style.backgroundImage = sprite;
    portrait.dataset.side = model.enemy ? 'enemy' : 'ally'; portrait.dataset.defeated = String(model.statuses.some((one: { key: string }) => one.key === 'defeated'));
    portrait.setAttribute('aria-label', `Show ${model.name} large`);
    // A zoomed illustration follows the portrait when the image it loads arrives, or the creature changes.
    if (!zoom.hidden) { picture.style.backgroundImage = portrait.style.backgroundImage; picture.dataset.side = portrait.dataset.side; }
    text('inspect-stats', model.stats);
    const sheetKey = JSON.stringify(model.sheet);
    if (el('inspect-sheet').dataset.content !== sheetKey) { el('inspect-sheet').replaceChildren(...sheetTiles(model.sheet)); el('inspect-sheet').dataset.content = sheetKey; }
    const nextConditions = JSON.stringify([model.statuses, model.conditions, model.conditionsRecorded, cues.length]);
    if (nextConditions !== conditionsKey) {
      const cards = [...model.statuses.map(one => conditionCard(STATUS_ICONS[one.key] ?? one.key, one.text)),
        ...model.conditions.map((one: { kind: string | null; name: string; duration: string; source: string }) => conditionCard(conditionKey(one.kind, cues), one.name, one.duration, one.source))];
      if (!cards.length) cards.push(node('li', model.conditionsRecorded ? 'No ongoing effects' : 'Conditions not recorded', 'inspect-empty'));
      el('inspect-effects').replaceChildren(...cards);
      conditionsKey = nextConditions;
    }
    text('inspect-appearance', model.description ? `Appearance: ${model.description}` : 'Artwork unavailable · identity comes from the recorded creature');
    el('inspect-growth').dataset.growthCreature = model.id;
    el('inspect-growth').hidden = historical;
    text('inspect-action-heading', historical ? 'Latest loaded action at this point' : 'Last public action');
    text('inspect-last-action', lastAction || (historical ? 'See History for actions recorded at this point.' : 'No public cast received for this creature yet.'));
    el('inspect-history').hidden = !lastAction;
    const nextMutations = JSON.stringify([model.mutations, model.mutationsRecorded]);
    if (nextMutations !== mutationsKey) {
      el('inspect-mutations').replaceChildren(...model.mutations.map(pack => {
        const item = node('li', '', 'inspect-mutation'); item.dataset.inspectMutation = String(pack.id);
        const head = node('div', '', 'inspect-mutation-head'); head.append(node('strong', pack.name));
        const chips = node('span', '', 'inspect-mutation-chips');
        if (pack.stage != null) chips.append(node('span', `Stage ${pack.stage}`, 'inspect-chip stage'));
        const initiative = node('span', '', 'inspect-chip initiative');
        initiative.append(iconElement('initiative-gain'), document.createTextNode(pack.initiative == null ? 'Initiative unavailable' : `Initiative ${pack.initiative >= 0 ? '+' : ''}${pack.initiative}`));
        chips.append(initiative); head.append(chips); item.append(head);
        if (pack.tags.length) { const tags = node('p', '', 'inspect-tags'); tags.append(...pack.tags.map((tag: string) => node('span', tag))); item.append(tags); }
        for (const passive of pack.passives) item.append(node('p', passive, 'inspect-passive'));
        if (pack.missing) item.append(node('p', 'Mutation details unavailable.', 'inspect-missing'));
        return item;
      }));
      if (!model.mutations.length) el('inspect-mutations').append(node('li', model.mutationsRecorded ? 'No publicly acquired mutations.' : 'Mutation ownership not recorded.', 'inspect-empty'));
      mutationsKey = nextMutations;
    }
    // Known spells are the same cards as the spell choice and the mutation tree: name, cost and crit chips, who
    // it hits, its effect lines with their icons, then the mutation that taught it.
    const nextSpells = JSON.stringify([model.spells, model.spellsRecorded, cues.length]);
    if (nextSpells !== spellsKey) {
      const cards = cardsById(catalogue);
      const list = el('inspect-spells');
      list.replaceChildren(...model.spells.map(spell => {
        const item = node('article', '', 'inspect-spell'); item.dataset.inspectSpell = String(spell.id);
        if (spell.colour) item.style.setProperty('--spell-accent', spell.colour);
        // The crit chip names its tempo and the d20's face, as the catalogue does.
        const face = spellCard(cards.get(String(spell.id)), String(spell.id), cues, { crit: spell.crit });
        // The spell choice answers [data-spell]; a reference card in a closed dialog must not.
        delete face.dataset.spell;
        item.append(face, node('p', spell.sources.length ? `Learned from ${spell.sources.join(', ')}` : 'Known spell · no acquired mutation source recorded.', 'inspect-spell-source'));
        return item;
      }));
      if (!model.spells.length) list.append(node('p', model.spellsRecorded ? 'No known spells.' : 'Known spells not recorded.', 'inspect-empty'));
      spellsKey = nextSpells;
    }
  }
  return { render, clear };
}
