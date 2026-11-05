import { createArenaPlanning } from '../../table/arena-planning.js';
import { mutationChoices } from '../../table/arena-mutations.js';
import { mutationCards } from './mutations';
import { SPEEDS, TEMPO_RULE, tempoLabel } from '../../table/terms.js';
import { untapped, orderOf } from '../../table/ties.js';
import type { SpellCatalogue } from './combat';

export interface Package { id: string; name: string; level: number; initiativeBonus: number; spells: string[];
  prerequisites?: string[]; anyOf?: string[]; passiveLines?: string[]; tags?: string[]; visual?: { family: string; motif?: string | null } | null }
export interface Catalogue extends SpellCatalogue { packages?: Package[]; effects?: { kind: string; glyph?: string; harmful: boolean }[] }
type Controller = ReturnType<typeof createArenaPlanning>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function button(label: string, action: () => void) {
  const node = document.createElement('button'); node.type = 'button'; node.textContent = label; node.addEventListener('click', action); return node;
}
function line(value: string) { const node = document.createElement('p'); node.textContent = value; return node; }

export function planningControls(controller: Controller, name: (id: unknown) => string, beforeOpen: () => void) {
  const dialog = el<HTMLDialogElement>('planning-dialog');
  let creature: number | null = null, rendered = '', lastCatalogue: Catalogue | null = null;
  el('planning-open').addEventListener('click', () => { beforeOpen(); controller.open(); });
  el('planning-close').addEventListener('click', () => controller.close());
  el('planning-confirm').addEventListener('click', () => { void controller.submit(); });
  dialog.addEventListener('cancel', event => { event.preventDefault(); controller.close(); });

  function mutations(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const choices = mutationChoices(model.latest, catalogue, creature);
    creature = choices.selected;
    el('planning-creatures').replaceChildren(...choices.roster.map((one: { id: number; name: string; context: string }) => {
      const node = button(`${one.name} · ${one.context}`, () => {
        if (one.id === creature) return;
        creature = one.id; rendered = ''; controller.choose(null);
      });
      node.dataset.creature = String(one.id); node.setAttribute('aria-pressed', String(one.id === creature)); return node;
    }));
    el('planning-choices').replaceChildren(...mutationCards(choices, catalogue, tier => controller.choose({ creature, tier })));
    const pass = button('Pass remaining picks', () => controller.choose({ pass: true })); pass.id = 'planning-pass'; el('planning-choices').append(pass);
    const budget = choices.effective == null ? 'Team pick count unavailable.' : `${choices.effective} team pick${choices.effective === 1 ? '' : 's'} available` +
      (choices.remaining !== choices.effective ? ` (${choices.remaining} unused; ${choices.eligible} eligible creature${choices.eligible === 1 ? '' : 's'})` : '');
    el('planning-help').textContent = `${budget}. One mutation per creature this opportunity. Select a card, then confirm. Pass gives up all remaining team picks this round.`;
  }

  function tempos(model: ReturnType<Controller['model']>) {
    el('planning-creatures').replaceChildren();
    el('planning-help').textContent = `${TEMPO_RULE} Choose for ${name(model.latest.waitingCreature)}. Each creature confirms separately; both teams reveal together.`;
    el('planning-choices').replaceChildren(...SPEEDS.map((speed: string) => {
      const node = button(tempoLabel(speed), () => controller.choose({ speed })); node.dataset.speed = speed; return node;
    }));
  }
  function ties(model: ReturnType<Controller['model']>) {
    el('planning-creatures').replaceChildren();
    el('planning-help').textContent = 'The dice gave your team its places. Choose who acts first within each tie, then confirm. The last creature takes the remaining place.';
    const groups = model.latest.options.tieOrder?.ties ?? [];
    const rows = untapped(groups, model.tapped).map((left: number[], index: number) => {
      const row = document.createElement('section'); row.append(line(`Tie ${index + 1}: ${orderOf([groups[index]], model.tapped).map(name).join(' → ')}`));
      if (left.length > 1) for (const id of left) { const node = button(name(id), () => controller.order(id)); node.dataset.tie = String(id); row.append(node); }
      return row;
    });
    const keep = button('Keep the dice order', () => controller.choose({ keep: true })); keep.id = 'planning-keep';
    el('planning-choices').replaceChildren(...rows, keep, button('Start again', () => controller.restart()));
  }
  const headings: Record<string, string> = { Evolution: 'Choose mutation', Speed: 'Choose tempo', TieOrder: 'Order ties' };
  function closeChoices() {
    if (dialog.open) dialog.close();
    rendered = ''; creature = null;
    el('planning-choices').replaceChildren(); el('planning-creatures').replaceChildren();
  }
  // The same selection is drawn pressed wherever it sits: a mutation button, a tempo, pass or the dice order.
  function isPressed(node: HTMLButtonElement, chosen: ReturnType<Controller['model']>['choice']) {
    if (node.dataset.mutation) return chosen?.tier === node.dataset.mutation && chosen?.creature === creature;
    if (node.dataset.speed) return chosen?.speed === node.dataset.speed;
    if (node.id === 'planning-pass') return Boolean(chosen?.pass);
    return node.id === 'planning-keep' && Boolean(chosen?.keep);
  }
  function selectionText(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const chosen = model.choice;
    if (chosen?.pass) return 'Pass remaining picks';
    if (chosen?.tier) return `${name(chosen.creature)} · ${catalogue?.packages?.find(pack => pack.id === chosen.tier)?.name ?? chosen.tier}`;
    if (chosen?.speed) return `${name(model.latest.waitingCreature)} · ${tempoLabel(chosen.speed)}`;
    if (chosen?.keep) return 'Dice order';
    return model.latest?.waitingFor === 'TieOrder' ? 'Chosen order' : 'Select a choice';
  }
  // The choices are rebuilt when the question, the offer, the taps or the chosen creature change, and
  // the focus of a player ordering ties is kept on the ties instead of falling off the removed button.
  function rebuild(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const kind = model.latest.waitingFor;
    if (kind === 'Evolution') creature = mutationChoices(model.latest, catalogue, creature).selected;
    const key = JSON.stringify([model.question, model.latest.options, model.latest.board.evolutionChoices,
      kind === 'Evolution' ? model.latest.board.allies.map((one: { id: number; isAlive?: boolean; acquiredTiers?: string[] }) => [one.id, one.isAlive, one.acquiredTiers]) : null, model.tapped, creature]);
    if (key === rendered && catalogue === lastCatalogue) return;
    const wasOrdering = kind === 'TieOrder' && el('planning-choices').contains(document.activeElement);
    if (kind === 'Evolution') mutations(model, catalogue); else if (kind === 'Speed') tempos(model); else ties(model);
    rendered = key; lastCatalogue = catalogue;
    if (wasOrdering) (el('planning-choices').querySelector<HTMLButtonElement>('[data-tie]') ?? el<HTMLButtonElement>('planning-confirm')).focus({ preventScroll: true });
  }
  function render(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const label = headings[model.latest?.waitingFor as string] ?? 'Order ties';
    el('planning-open').hidden = !model.available || model.opened; el('planning-open').textContent = label;
    el<HTMLButtonElement>('planning-open').disabled = model.sending;
    el('planning-error').textContent = model.error; el('planning-error').hidden = !model.error || model.opened;
    el('planning-dialog-error').textContent = model.error;
    el<HTMLButtonElement>('planning-close').disabled = model.sending;
    if (!model.opened) { closeChoices(); return; }
    dialog.dataset.kind = model.latest.waitingFor;
    el('planning-heading').textContent = label;
    rebuild(model, catalogue);
    for (const node of Array.from(el('planning-choices').querySelectorAll<HTMLButtonElement>('button'))) {
      node.disabled = !model.canEdit || node.dataset.offered === 'false';
      node.setAttribute('aria-pressed', String(isPressed(node, model.choice)));
      if (node.dataset.mutation) node.querySelector('.mutation-select-label')!.textContent = isPressed(node, model.choice) ? 'Selected · confirm below' : 'Select mutation';
    }
    for (const node of Array.from(el('planning-creatures').querySelectorAll<HTMLButtonElement>('button'))) node.disabled = !model.canEdit;
    el('planning-selection').textContent = selectionText(model, catalogue);
    el<HTMLButtonElement>('planning-confirm').disabled = !model.canSubmit;
    el('planning-confirm').textContent = model.sending ? 'Sending…' : model.choice?.tier ? 'Acquire · 1 pick' : model.choice?.pass ? 'Pass all remaining picks' : 'Confirm choice';
    el('planning-confirm').setAttribute('aria-label', model.choice?.tier ? `Acquire ${selectionText(model, catalogue)} · use 1 team pick` : el('planning-confirm').textContent!);
    if (!dialog.open) dialog.showModal();
  }
  return { render };
}
