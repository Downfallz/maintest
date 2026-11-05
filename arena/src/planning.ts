import { createArenaPlanning } from '../../table/arena-planning.js';
import { mutationChoices } from '../../table/arena-mutations.js';
import { mutationCards } from './mutations';
import { tempoControls } from './tempo';
import { arenaTieGroups } from '../../table/arena-order.js';
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
  const tempos = tempoControls(controller, name);
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
    const budget = choices.remaining == null ? 'Team pick count unavailable' : `${choices.remaining} team pick${choices.remaining === 1 ? '' : 's'} available`;
    el('planning-help').textContent = `${budget}. One mutation per creature this opportunity. Select a card, then confirm. Pass gives up all remaining team picks this round.`;
  }
  type Assignment = { id: number; position: number | null };
  type Fixed = { position: number; label: string; roll: string };
  const assignmentText = (one: Assignment) => `${one.position == null ? 'Position unknown' : '#' + one.position} → ${name(one.id)}`;
  const fixedText = (one: Fixed) => (one.roll ? `#${one.position} ${one.label} (${one.roll})` : `#${one.position} ${one.label}`);
  // The opponent orders its own creatures within its places too: with two or more, only the places are certain.
  function opponentPlacesText(detail: { fixed: Fixed[]; opponentOrderOpen: boolean }) {
    if (detail.opponentOrderOpen) return `Opponent places stay fixed: ${detail.fixed.map(one => '#' + one.position).join(', ')} · their order is revealed with yours`;
    return `Opponent places stay fixed: ${detail.fixed.map(fixedText).join(' · ')}`;
  }
  function ties(model: ReturnType<Controller['model']>) {
    el('planning-creatures').replaceChildren();
    el('planning-help').textContent = 'Your team keeps its allocated places. Choose which creature takes each place within a tie, then confirm. Opponent positions stay fixed; both teams reveal together.';
    const groups = model.latest.options.tieOrder?.ties ?? [];
    const context = arenaTieGroups(model.latest, model.tapped);
    const rows = untapped(groups, model.tapped).map((left: number[], index: number) => {
      const row = document.createElement('section'); row.append(line(`Tie ${index + 1}: ${orderOf([groups[index]], model.tapped).map(name).join(' → ')}`));
      row.className = 'tie-group';
      const detail = context[index];
      row.append(line(detail.places.length ? `Your fixed places: ${detail.places.join(', ')}` : 'Positions not available in this snapshot.'),
        line(detail.rolled ? 'Recorded d20 rolls allocated these team places. Your choice changes only their occupants.' : 'No d20 roll recorded for these creatures; a same-team-only tie needs none.'),
        line(detail.assignments.map(assignmentText).join(' · ')));
      if (detail.fixed.length) row.append(line(opponentPlacesText(detail)));
      if (left.length > 1) for (const id of left) { const node = button(name(id), () => controller.order(id)); node.dataset.tie = String(id); row.append(node); }
      return row;
    });
    const keep = button('Keep current order', () => controller.choose({ keep: true })); keep.id = 'planning-keep';
    el('planning-choices').replaceChildren(...rows, keep, button('Start again', () => controller.restart()));
  }
  const headings: Record<string, string> = { Evolution: 'Choose mutation', Speed: 'Team tempos', TieOrder: 'Order ties' };
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
    if (model.tempo) {
      if (model.tempo.needsRefresh) return 'Refreshing accepted choices…';
      const accepted = model.tempo.accepted.length ? ` · ${model.tempo.accepted.length} already accepted` : '';
      return `${model.tempo.chosen}/${model.tempo.total} remaining tempos ready${accepted}`;
    }
    const chosen = model.choice;
    if (chosen?.pass) return 'Pass remaining picks';
    if (chosen?.tier) return `${name(chosen.creature)} · ${catalogue?.packages?.find(pack => pack.id === chosen.tier)?.name ?? chosen.tier}`;
    if (chosen?.keep) return 'Current order';
    return model.latest?.waitingFor === 'TieOrder' ? 'Chosen order' : 'Select a choice';
  }
  // The choices are rebuilt when the question, the offer, the taps or the chosen creature change, and
  // the focus of a player ordering ties is kept on the ties instead of falling off the removed button.
  function rebuild(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const kind = model.latest.waitingFor;
    if (kind === 'Evolution') creature = mutationChoices(model.latest, catalogue, creature).selected;
    const key = JSON.stringify([model.question, model.latest.options, model.latest.board.evolutionChoices,
      kind === 'Evolution' ? model.latest.board.allies.map((one: { id: number; isAlive?: boolean; acquiredTiers?: string[] }) => [one.id, one.isAlive, one.acquiredTiers]) : null, model.tapped, creature,
      model.tempo, kind === 'Speed' && [model.latest.board.allies, model.latest.guidance]]);
    if (key === rendered && catalogue === lastCatalogue) return;
    const refocus = focusKept(kind);
    for (const id of ['planning-creatures', 'planning-choices']) {
      el(id).removeAttribute('role'); el(id).removeAttribute('aria-labelledby');
    }
    if (kind === 'Evolution') mutations(model, catalogue); else if (kind === 'Speed') tempos(model, catalogue); else ties(model);
    rendered = key; lastCatalogue = catalogue;
    if (refocus) dialog.querySelector<HTMLButtonElement>(refocus)?.focus({ preventScroll: true });
  }
  // The control a keyboard player was on, found again once the panel is rebuilt around it: the ties they are
  // ordering (or Confirm once they are settled), the creature tab they chose, the tempo tab or tempo they pressed.
  function focusKept(kind: string): string | null {
    const focused = document.activeElement as HTMLElement | null;
    // Document order puts the ties before Confirm, so the first match is a tie left to order, else Confirm.
    if (kind === 'TieOrder' && el('planning-choices').contains(focused)) return '#planning-choices [data-tie], #planning-confirm';
    if (kind === 'Evolution' && el('planning-creatures').contains(focused)) return `#planning-creatures [data-creature="${creature}"]`;
    if (kind !== 'Speed') return null;
    if (focused?.dataset.speed) return `[data-speed="${focused.dataset.speed}"]`;
    return focused?.dataset.tempoTab ? `[data-tempo-tab="${focused.dataset.tempoTab}"]` : null;
  }
  function confirmLabel(model: ReturnType<Controller['model']>) {
    if (model.sending) return 'Sending…';
    if (model.choice?.tier) return 'Acquire · 1 pick';
    if (model.choice?.pass) return 'Pass all remaining picks';
    return model.tempo ? `Confirm tempos · ${model.tempo.chosen}/${model.tempo.total}` : 'Confirm choice';
  }
  function render(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const label = headings[model.latest?.waitingFor as string] ?? 'Order ties';
    el('planning-order').hidden = !model.opened || model.latest?.waitingFor !== 'TieOrder';
    el('planning-open').hidden = !model.available || model.opened; el('planning-open').textContent = label;
    el<HTMLButtonElement>('planning-open').disabled = model.sending;
    el('planning-error').textContent = model.error; el('planning-error').hidden = !model.error || model.opened;
    el('planning-dialog-error').textContent = model.error;
    el<HTMLButtonElement>('planning-close').disabled = model.sending;
    if (!model.opened) { closeChoices(); return; }
    dialog.dataset.kind = model.latest.waitingFor;
    el('planning-heading').textContent = label;
    dialog.dataset.kind = model.latest.waitingFor;
    rebuild(model, catalogue);
    for (const node of Array.from(el('planning-choices').querySelectorAll<HTMLButtonElement>('button'))) {
      node.disabled = !model.canEdit || node.dataset.offered === 'false';
      node.setAttribute('aria-pressed', String(isPressed(node, model.choice)));
      if (node.dataset.mutation) node.querySelector('.mutation-select-label')!.textContent = isPressed(node, model.choice) ? 'Selected · confirm below' : 'Select mutation';
    }
    for (const node of Array.from(el('planning-creatures').querySelectorAll<HTMLButtonElement>('button'))) node.disabled = !model.canEdit;
    el('planning-selection').textContent = selectionText(model, catalogue);
    el<HTMLButtonElement>('planning-confirm').disabled = !model.canSubmit;
    el('planning-confirm').textContent = confirmLabel(model);
    // The accessible name starts with the visible label (WCAG 2.5.3) and adds the recipient; while sending it is the label.
    if (model.choice?.tier && !model.sending) el('planning-confirm').setAttribute('aria-label', `Acquire · 1 pick: ${selectionText(model, catalogue)}`);
    else el('planning-confirm').removeAttribute('aria-label');
    if (!dialog.open) dialog.showModal();
  }
  return { render };
}
