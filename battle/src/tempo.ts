import type { createBattlePlanning } from '../../table/battle-planning.js';
import { SPEEDS, TEMPO_RULE, tempoLabel } from '../../table/terms.js';
import { cardsById, hasCritical } from '../../table/card.js';
import { chance } from '../../table/guidance.js';
import { iconElement } from './condition-icons';
import { spellCard } from './spell-card';
import type { Catalogue } from './planning';

type Controller = ReturnType<typeof createBattlePlanning>;
const el = (id: string) => document.getElementById(id)!;
const node = (tag: string, text = '', className = '') => {
  const item = document.createElement(tag); if (text) item.textContent = text; if (className) item.className = className; return item;
};
function nextTab(key: string, index: number, count: number) {
  if (key === 'Home') return 0;
  if (key === 'End') return count - 1;
  return (index + (key === 'ArrowRight' ? 1 : -1) + count) % count;
}
// Each tempo wears the mark the spell choice's tempo chip uses: a bolt for Instinct, a star for Precision.
const GLYPH: Record<string, string> = { Quick: '⚡', Standard: '✦' };
const LINE: Record<string, string> = { Quick: 'Acts before Precision · cannot crit', Standard: 'Acts after Instinct · can crit' };

// The chance the host computed for the asked creature, or the catalogue's; none when it is no chance at all,
// the way the card face prints nothing for a spell that never crits. It is a Precision chance: Instinct never crits.
function criticalChip(card: { critical?: string } | undefined, guide: { standardCriticalChance?: number } | null) {
  if (guide?.standardCriticalChance != null) return guide.standardCriticalChance > 0 ? { value: chance(guide.standardCriticalChance), hint: 'Precision · current' } : null;
  return hasCritical(card) ? { value: card!.critical!, hint: 'Precision · catalogue' } : null;
}

// What the creature in hand could cast, as the spell choice deals it: the same cards, their crit chip at Precision.
function spellReference(selected: { knownSpells?: string[] } | undefined, view: ReturnType<Controller['model']>['latest'], focus: number, catalogue: Catalogue | null) {
  const reference = node('section', '', 'tempo-reference'); reference.setAttribute('aria-label', 'Spell reference');
  reference.append(node('p', focus === view.waitingCreature ? 'Its spells · crit chances at Precision, current values' : 'Its spells · catalogue values, creature modifiers not included', 'tempo-reference-note'));
  const cards = cardsById(catalogue), grid = node('div', '', 'tempo-spells');
  for (const id of selected?.knownSpells ?? []) {
    const card = cards.get(id), guide = focus === view.waitingCreature ? view.guidance?.find((one: { spell: string }) => one.spell === id) : null;
    const face = spellCard(card, id, catalogue?.effects ?? [], { compact: true, cost: Number.isInteger(guide?.cost) ? guide.cost : undefined, crit: criticalChip(card, guide) });
    // The spell choice answers [data-spell]; a reference card must not.
    delete face.dataset.spell; face.dataset.referenceSpell = id;
    grid.append(face);
  }
  reference.append(selected?.knownSpells?.length ? grid : node('p', 'No known spells recorded.'));
  return reference;
}

export function tempoControls(controller: Controller, name: (id: unknown) => string) {
  return (model: ReturnType<Controller['model']>, catalogue: Catalogue | null) => {
    const tempo = model.tempo!, view = model.latest, drafts = new Map<number, string>(tempo.drafts);
    // A tab per creature still to draft, wearing the tempo drafted for it.
    const picker = el('planning-creatures'); picker.setAttribute('role', 'tablist'); picker.setAttribute('aria-label', 'Team tempos');
    picker.replaceChildren(...tempo.creatures.map((id: number, index: number) => {
      const tab = document.createElement('button'); tab.type = 'button'; tab.id = `tempo-tab-${id}`;
      tab.dataset.tempoTab = String(id); tab.setAttribute('role', 'tab'); tab.setAttribute('aria-controls', 'planning-choices');
      tab.setAttribute('aria-selected', String(id === tempo.focus)); tab.tabIndex = id === tempo.focus ? 0 : -1;
      const drafted = drafts.get(id);
      tab.dataset.tempo = drafted ?? '';
      const value = node('small', drafted ? `${GLYPH[drafted] ?? ''} ${tempoLabel(drafted)}` : 'Not chosen', 'tempo-tab-value');
      tab.append(node('span', name(id)), value);
      tab.addEventListener('click', () => controller.selectTempo(id));
      tab.addEventListener('keydown', event => {
        const keys = ['ArrowLeft', 'ArrowRight', 'Home', 'End'];
        if (!keys.includes(event.key)) return;
        event.preventDefault();
        const count = tempo.creatures.length;
        const next = nextTab(event.key, index, count);
        controller.selectTempo(tempo.creatures[next]); el(`tempo-tab-${tempo.creatures[next]}`).focus({ preventScroll: true });
      });
      return tab;
    }));
    const selected = view.board.allies.find((one: { id: number }) => one.id === tempo.focus);
    el('planning-help').textContent = `${TEMPO_RULE} Draft each creature, confirm once: private until both teams reveal.`;
    const choices = el('planning-choices'); choices.setAttribute('role', 'tabpanel'); choices.setAttribute('aria-labelledby', `tempo-tab-${tempo.focus}`);
    // The creature in hand, with the energy it will have to spend, as the spell choice heads its sheet.
    const head = node('div', '', 'tempo-head');
    const title = node('h3', name(tempo.focus));
    const energy = node('span', '', 'actor-chip'); energy.dataset.kind = 'energy';
    energy.append(iconElement('energy-gain'), selected?.energy == null ? 'Energy unavailable' : `${selected.energy} energy`);
    head.append(title, energy);
    // The two tempos as cards: the tempo's mark in a medallion, its name and what it does to the turn.
    const buttons = node('div', '', 'tempo-options');
    for (const speed of SPEEDS) {
      const button = document.createElement('button'); button.type = 'button'; button.dataset.speed = speed; button.className = 'tempo-option';
      const mark = node('span', GLYPH[speed] ?? '', 'tempo-mark'); mark.setAttribute('aria-hidden', 'true');
      const words = node('span', '', 'tempo-words');
      words.append(node('strong', tempoLabel(speed)), node('span', LINE[speed] ?? ''));
      button.append(mark, words); button.addEventListener('click', () => controller.choose({ speed })); buttons.append(button);
    }
    choices.replaceChildren(head, buttons);
    if (tempo.accepted.length) {
      const values = tempo.accepted.map((one: { creature: number; speed: string }) => `${name(one.creature)}: ${tempoLabel(one.speed)}`).join(' · ');
      choices.append(node('p', `Accepted · ${values}`, 'tempo-accepted'));
    }
    choices.append(spellReference(selected, view, tempo.focus, catalogue));
  };
}
