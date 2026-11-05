import type { createArenaPlanning } from '../../table/arena-planning.js';
import { SPEEDS, TEMPO_RULE, tempoLabel } from '../../table/terms.js';
import { cardCost, cardsById, hasCritical, referenceLines } from '../../table/card.js';
import { chance } from '../../table/guidance.js';
import type { Catalogue } from './planning';

type Controller = ReturnType<typeof createArenaPlanning>;
const el = (id: string) => document.getElementById(id)!;
const paragraph = (text: string) => { const node = document.createElement('p'); node.textContent = text; return node; };
function nextTab(key: string, index: number, count: number) {
  if (key === 'Home') return 0;
  if (key === 'End') return count - 1;
  return (index + (key === 'ArrowRight' ? 1 : -1) + count) % count;
}

// The chance the host computed for the asked creature, or the catalogue's; neither when it is no chance at
// all, the way the card face and the inspect panel print nothing for a spell that never crits.
function criticalLabel(card: { critical?: string } | undefined, guide: { standardCriticalChance?: number } | null) {
  if (guide?.standardCriticalChance != null) return guide.standardCriticalChance > 0 ? `${chance(guide.standardCriticalChance)} Precision crit · current` : null;
  return hasCritical(card) ? `${card!.critical} Precision crit · catalogue` : null;
}

// A spell the player opened to read stays open while the panel is rebuilt around it, tab by tab.
function spellReference(selected: { knownSpells?: string[] } | undefined, view: ReturnType<Controller['model']>['latest'], focus: number, catalogue: Catalogue | null, expanded: Set<string>) {
  const reference = document.createElement('section'); reference.className = 'tempo-reference'; reference.setAttribute('aria-label', 'Spell reference');
  reference.append(paragraph(focus === view.waitingCreature ? 'Spell reference · current creature values when available' : 'Catalogue reference · creature-specific modifiers are not included'));
  const cards = cardsById(catalogue);
  for (const id of selected?.knownSpells ?? []) {
    const card = cards.get(id), guide = focus === view.waitingCreature ? view.guidance?.find((one: { spell: string }) => one.spell === id) : null;
    const details = document.createElement('details'), summary = document.createElement('summary'), key = `${focus}/${id}`;
    summary.textContent = [card?.name ?? id, cardCost(card) === '' ? null : `${cardCost(card)} energy`,
      criticalLabel(card, guide)].filter(Boolean).join(' · ');
    details.append(summary, ...referenceLines(card, guide).map(paragraph));
    details.open = expanded.has(key);
    details.addEventListener('toggle', () => { if (details.open) expanded.add(key); else expanded.delete(key); });
    reference.append(details);
  }
  if (!selected?.knownSpells?.length) reference.append(paragraph('No known spells recorded.'));
  return reference;
}

export function tempoControls(controller: Controller, name: (id: unknown) => string) {
  const expanded = new Set<string>();
  return (model: ReturnType<Controller['model']>, catalogue: Catalogue | null) => {
    const tempo = model.tempo!, view = model.latest, drafts = new Map<number, string>(tempo.drafts);
    const picker = el('planning-creatures'); picker.setAttribute('role', 'tablist'); picker.setAttribute('aria-label', 'Team tempos');
    picker.replaceChildren(...tempo.creatures.map((id: number, index: number) => {
      const tab = document.createElement('button'); tab.type = 'button'; tab.id = `tempo-tab-${id}`;
      tab.dataset.tempoTab = String(id); tab.setAttribute('role', 'tab'); tab.setAttribute('aria-controls', 'planning-choices');
      tab.setAttribute('aria-selected', String(id === tempo.focus)); tab.tabIndex = id === tempo.focus ? 0 : -1;
      const identity = document.createElement('span'); identity.textContent = name(id);
      const value = document.createElement('small'); value.textContent = drafts.has(id) ? tempoLabel(drafts.get(id)) : 'Not chosen';
      tab.append(identity, value);
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
    el('planning-help').textContent = `${TEMPO_RULE} Draft every creature, then confirm once. Your choices stay private until both teams reveal.`;
    const choices = el('planning-choices'); choices.setAttribute('role', 'tabpanel'); choices.setAttribute('aria-labelledby', `tempo-tab-${tempo.focus}`);
    const energy = selected?.energy == null ? '' : ` · ${selected.energy} energy`;
    const title = document.createElement('h3'); title.textContent = `${name(tempo.focus)}${energy}`;
    const buttons = document.createElement('div'); buttons.className = 'tempo-options';
    for (const speed of SPEEDS) {
      const button = document.createElement('button'); button.type = 'button'; button.dataset.speed = speed;
      const label = document.createElement('strong'); label.textContent = tempoLabel(speed);
      const detail = document.createElement('span'); detail.textContent = speed === 'Quick' ? 'Acts before Precision · cannot crit' : 'Acts after Instinct · can crit';
      button.append(label, detail); button.addEventListener('click', () => controller.choose({ speed })); buttons.append(button);
    }
    const reference = spellReference(selected, view, tempo.focus, catalogue, expanded);
    const summary = paragraph(['Team draft', ...tempo.creatures.map((id: number) => `${name(id)}: ${drafts.has(id) ? tempoLabel(drafts.get(id)) : 'Not chosen'}`)].join(' · '));
    summary.className = 'tempo-summary';
    choices.replaceChildren(title, buttons, summary);
    if (tempo.accepted.length) {
      const values = tempo.accepted.map((one: { creature: number; speed: string }) => `${name(one.creature)}: ${tempoLabel(one.speed)}`).join(' · ');
      const accepted = paragraph(`Accepted · ${values}`);
      accepted.className = 'tempo-accepted'; choices.append(accepted);
    }
    choices.append(reference);
  };
}
