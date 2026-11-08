import { createBattlePlanning } from '../../table/battle-planning.js';
import { mutationChoices } from '../../table/battle-mutations.js';
import { mutationTree } from './mutation-tree';
import type { atlasControls, AtlasView } from './mutation-atlas';
import { tempoControls } from './tempo';
import { battleTieGroups } from '../../table/battle-order.js';
import { untapped, orderOf } from '../../table/ties.js';
import type { SpellCatalogue } from './combat';

export interface Package { id: string; name: string; level: number; initiativeBonus: number; spells: string[];
  prerequisites?: string[]; anyOf?: string[]; passiveLines?: string[]; tags?: string[]; visual?: { family: string; motif?: string | null } | null }
export interface Catalogue extends SpellCatalogue { packages?: Package[]; effects?: { kind: string; glyph?: string; harmful: boolean }[] }
type Controller = ReturnType<typeof createBattlePlanning>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function button(label: string, action: () => void) {
  const node = document.createElement('button'); node.type = 'button'; node.textContent = label; node.addEventListener('click', action); return node;
}
function line(value: string) { const node = document.createElement('p'); node.textContent = value; return node; }

// The mutation pick and the team's tempos dock beside the field (ADR 0138, ADR 0150); the dice order stays a modal.
const docked = (kind: string | undefined) => kind === 'Evolution' || kind === 'Speed';

export function planningControls(controller: Controller, name: (id: unknown) => string, beforeOpen: () => void, inspect: (id: string) => void,
  atlas: ReturnType<typeof atlasControls>) {
  const dialog = el<HTMLDialogElement>('planning-dialog');
  let tree: ReturnType<typeof mutationTree> | null = null;
  const tempos = tempoControls(controller, name);
  let creature: number | null = null, rendered = '', lastCatalogue: Catalogue | null = null, question = '';
  el('planning-open').addEventListener('click', () => { beforeOpen(); controller.open(); });
  el('planning-close').addEventListener('click', () => controller.close());
  el('planning-confirm').addEventListener('click', () => { void controller.submit(); });
  dialog.addEventListener('cancel', event => { event.preventDefault(); controller.close(); });

  function selectCreature(id: number) {
    if (id === creature) return;
    creature = id; rendered = ''; controller.choose(null);
  }
  // A creature tapped while the draft cannot change (sending, or a failed acknowledgement)
  // is inspected instead: the tap never does nothing, and never drops the draft being sent.
  function openCreature(id: number) {
    if (!controller.model().opened) { beforeOpen(); controller.open(); }
    if (controller.model().canEdit) selectCreature(id);
    else inspect(String(id));
  }
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && dialog.open && docked(dialog.dataset.kind) && !document.querySelector('dialog:modal')) {
      event.preventDefault(); controller.close();
    }
  });
  function mutations(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const choices = mutationChoices(model.latest, catalogue, creature);
    creature = choices.selected;
    el('planning-creatures').replaceChildren(...choices.roster.map((one: { id: number; name: string; context: string }) => {
      const node = button(one.name, () => selectCreature(one.id));
      node.setAttribute('aria-label', `${one.name} · ${one.context}`);
      if (!one.context.endsWith('available')) { const status = document.createElement('small'); status.textContent = one.context; node.append(status); }
      node.dataset.creature = String(one.id); node.setAttribute('aria-pressed', String(one.id === creature)); return node;
    }));
    tree?.dispose();
    tree = mutationTree(choices, catalogue, tier => controller.choose({ creature, tier }));
    el('planning-choices').replaceChildren(tree.graph);
    el('mutation-preview-region').replaceChildren(tree.pane);
    // The whole tree opens full screen on the mutation in hand; an offer can be chosen from its card there.
    // The atlas covers the field and so holds the draft (canEdit is false under it); it offers the choice
    // whenever the draft could take one, and makes it only once it has closed.
    const view = (id: number | null): AtlasView | null => {
      const now = controller.model();
      if (!now.opened || now.latest?.waitingFor !== 'Evolution') return null;
      const shown = mutationChoices(now.latest, catalogue, id);
      const subject = shown.roster.find((one: { id: number }) => one.id === shown.selected);
      return { title: `${subject?.name ?? 'Creature'} · Mutation tree`, eyebrow: 'MUTATION TREE · YOUR PICK', packages: shown.packages,
        roster: shown.roster.map((one: { id: number; name: string; context: string }) => ({ id: String(one.id), name: `${one.name} · ${one.context}` })),
        creature: String(shown.selected), focus: (now.choice?.creature === shown.selected ? now.choice?.tier : null) ?? shown.faces[0]?.id ?? null,
        choose: now.available && !now.sending ? tier => { selectCreature(shown.selected); controller.choose({ creature: shown.selected, tier }); } : undefined };
    };
    // The sheet's own header holds what the whole pick is about: the full tree to read, and passing what is left.
    const full = button('Full mutation tree', () => { const shown = view(creature); if (shown) atlas.open(shown, catalogue, full, next => view(Number(next))); }); full.id = 'mutation-atlas-open'; full.className = 'planning-action atlas-open';
    const pass = button('Pass remaining picks', () => controller.choose({ pass: true })); pass.id = 'planning-pass'; pass.className = 'planning-action';
    el('planning-tools').replaceChildren(full, pass);
    if (creature != null) {
      const actions = document.createElement('div'); actions.className = 'planning-actions';
      const inspectButton = button('Inspect creature', () => inspect(String(creature)));
      inspectButton.id = 'mutation-inspect'; inspectButton.className = 'planning-action'; actions.append(inspectButton);
      el('planning-choices').append(actions);
    }
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
    const context = battleTieGroups(model.latest, model.tapped);
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
    if (!tree && !dialog.open && !rendered) return;
    tree?.dispose(); tree = null;
    el('planning-tools').replaceChildren(); el('mutation-preview-region').replaceChildren(); el('mutation-preview-region').hidden = true;
    if (dialog.open) dialog.close();
    rendered = ''; creature = null; question = '';
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
  // The team's picks are the budget a player spends here. Between a sent pick and the next asking the count
  // is the one the host has not restated yet, so it says the pick went and what is left after it.
  function pickBudget(model: ReturnType<Controller['model']>) {
    const offered = model.latest?.options?.evolution?.remainingPicks;
    if (!Number.isInteger(offered)) return 'Team pick count unavailable';
    const sent = model.accepted && !model.available, left = sent ? offered - 1 : offered;
    return `${sent ? 'Pick sent · ' : ''}${left} team pick${left === 1 ? '' : 's'} remaining`;
  }
  // Where a keyboard starts in the mutation sheet: the recipient tab, or the tree when a short sheet hides them.
  function focusStart() {
    [dialog.querySelector<HTMLElement>('#planning-creatures [aria-pressed="true"]'), ...dialog.querySelectorAll<HTMLElement>('#planning-choices button:not(:disabled)')]
      .find(node => node && node.getClientRects().length > 0)?.focus({ preventScroll: true });
  }
  // A keyboard starts the tempo sheet on the tempo drafted for the creature in hand, else its first tempo.
  function tempoStart() {
    (dialog.querySelector<HTMLElement>('#planning-choices [data-speed][aria-pressed="true"]') ?? dialog.querySelector<HTMLElement>('#planning-choices [data-speed]'))?.focus({ preventScroll: true });
  }
  function confirmLabel(model: ReturnType<Controller['model']>) {
    if (model.sending) return 'Sending…';
    if (model.choice?.tier) return 'Confirm ' + (lastCatalogue?.packages?.find(pack => pack.id === model.choice.tier)?.name ?? model.choice.tier);
    if (model.choice?.pass) return 'Pass all remaining picks';
    return model.tempo ? `Confirm tempos · ${model.tempo.chosen}/${model.tempo.total}` : 'Confirm choice';
  }
  function render(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const label = headings[model.latest?.waitingFor as string] ?? 'Order ties';
    el('planning-order').hidden = !model.opened || model.latest?.waitingFor !== 'TieOrder';
    el('planning-open').hidden = !model.available || model.opened; el('planning-open').textContent = model.latest?.waitingFor === 'Evolution' ? 'Mutation tree ↗' : label;
    el<HTMLButtonElement>('planning-open').disabled = model.sending;
    el('planning-error').textContent = model.error; el('planning-error').hidden = !model.error || model.opened;
    el('planning-dialog-error').textContent = model.error;
    el<HTMLButtonElement>('planning-close').disabled = model.sending;
    if (!model.opened) { closeChoices(); return; }
    dialog.dataset.kind = model.latest.waitingFor;
    if (model.latest.waitingFor !== 'Evolution') el('planning-tools').replaceChildren();
    el('planning-heading').textContent = model.latest.waitingFor === 'Evolution' ? 'Mutation tree' : label;
    // The seat's next asking after a pick arrives in the open sheet: the creature that took the pick has nothing
    // left to offer, so the next creature with offers is shown, and a keyboard starts there again.
    const continued = dialog.open && question !== '' && model.question !== question && model.latest.waitingFor === 'Evolution';
    if (continued && !mutationChoices(model.latest, catalogue, creature).faces.length) creature = null;
    question = model.question;
    rebuild(model, catalogue);
    for (const node of [...el('planning-tools').querySelectorAll<HTMLButtonElement>('button'), ...el('planning-choices').querySelectorAll<HTMLButtonElement>('button')]) {
      // Inspecting and reading the full tree never edit the draft, so they stay available while sending.
      const reading = node.id === 'mutation-inspect' || node.id === 'mutation-atlas-open';
      node.disabled = !reading && (!model.canEdit || node.dataset.offered === 'false');
      if (!reading) node.setAttribute('aria-pressed', String(isPressed(node, model.choice)));
    }
    for (const node of Array.from(el('planning-creatures').querySelectorAll<HTMLButtonElement>('button'))) node.disabled = !model.canEdit;
    el('mutation-preview-region').hidden = model.latest.waitingFor !== 'Evolution' || !model.choice?.tier;
    tree?.select(model.choice?.tier ?? null);
    el('planning-selection').textContent = selectionText(model, catalogue);
    el<HTMLButtonElement>('planning-confirm').disabled = !model.canSubmit;
    el('planning-confirm').textContent = confirmLabel(model);
    // The accessible name starts with the visible label (WCAG 2.5.3) and adds the recipient; while sending it is the label.
    if (model.choice?.tier && !model.sending) el('planning-confirm').setAttribute('aria-label', `${confirmLabel(model)} · 1 pick · ${name(model.choice.creature)}`);
    else el('planning-confirm').removeAttribute('aria-label');
    if (model.latest.waitingFor === 'Evolution') el('planning-help').textContent = pickBudget(model);
    if (!dialog.open) {
      if (model.latest.waitingFor === 'Evolution') { dialog.show(); focusStart(); }
      else if (model.latest.waitingFor === 'Speed') { dialog.show(); tempoStart(); }
      else dialog.showModal();
    } else if (continued && (!dialog.contains(document.activeElement) || (document.activeElement as HTMLButtonElement).disabled)) focusStart();
  }
  return { render, openCreature, recipient: () => creature };
}
