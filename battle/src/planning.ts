import { createBattlePlanning } from '../../table/battle-planning.js';
import { mutationChoices, pickPlan } from '../../table/battle-mutations.js';
import { mutationTree } from './mutation-tree';
import type { atlasControls, AtlasView } from './mutation-atlas';
import { tempoControls } from './tempo';
import { battleTieGroups } from '../../table/battle-order.js';
import { untapped, orderOf } from '../../table/ties.js';
import type { SpellCatalogue } from './combat';

export interface Package { id: string; name: string; level: number; initiativeBonus: number; spells: string[];
  prerequisites?: string[]; anyOf?: string[]; passiveLines?: string[]; tags?: string[]; visual?: { family: string; motif?: string | null; artwork?: string | null; portrait?: string | null } | null }
export interface Catalogue extends SpellCatalogue { packages?: Package[]; effects?: { kind: string; glyph?: string; harmful: boolean }[]; rules?: { evolutionInterval?: number } }
type Controller = ReturnType<typeof createBattlePlanning>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function button(label: string, action: () => void) {
  const node = document.createElement('button'); node.type = 'button'; node.textContent = label; node.addEventListener('click', action); return node;
}
function line(value: string) { const node = document.createElement('p'); node.textContent = value; return node; }

// The mutation pick, the team's tempos and the order of tied creatures dock beside the field (ADR 0138, ADR 0150):
// each is about creatures the field shows.
const docked = (kind: string | undefined) => kind === 'Evolution' || kind === 'Speed' || kind === 'TieOrder';

export function planningControls(controller: Controller, name: (id: unknown) => string, beforeOpen: () => void, inspect: (id: string) => void,
  atlas: ReturnType<typeof atlasControls>, follow: (creature: number | null) => void = () => {}) {
  const dialog = el<HTMLDialogElement>('planning-dialog');
  let tree: ReturnType<typeof mutationTree> | null = null;
  const tempos = tempoControls(controller, name);
  let creature: number | null = null, rendered = '', lastCatalogue: Catalogue | null = null, question = '';
  // What opened the sheet, for the focus when it closes without a pick (UX12b): the command, or a creature on the field.
  let opener: HTMLElement | null = null;
  el('planning-open').addEventListener('click', () => { opener = el('planning-open'); beforeOpen(); controller.open(); });
  // Closed without a confirm: the draft goes, and the focus goes back to its opener, else to the command that reopens it.
  function dismiss() {
    const kind = controller.model().latest?.waitingFor;
    controller.close();
    if (kind !== 'Evolution' || controller.model().opened) return;
    const back = opener?.isConnected && opener.getClientRects().length && !(opener as HTMLButtonElement).disabled ? opener : el('planning-open');
    opener = null;
    if (!back.hidden) back.focus({ preventScroll: true });
  }
  el('planning-close').addEventListener('click', dismiss);
  // The team's last pick accepted by the host closes the sheet: the focus goes once to the creature it went to, or after
  // a pass to the team's first living creature (UX12b). A pick with picks left keeps the sheet on the next creature.
  el('planning-confirm').addEventListener('click', async () => {
    const before = controller.model(), pick = before.latest?.waitingFor === 'Evolution' ? before.choice : null;
    await controller.submit();
    const after = controller.model();
    if (pick && after.accepted && !after.opened) { opener = null; follow(pick.pass ? null : pick.creature); }
  });
  dialog.addEventListener('cancel', event => { event.preventDefault(); dismiss(); });

  function selectCreature(id: number) {
    if (id === creature) return;
    creature = id; rendered = ''; controller.choose(null);
  }
  // A creature tapped while the draft cannot change (sending, or a failed acknowledgement)
  // is inspected instead: the tap never does nothing, and never drops the draft being sent.
  function openCreature(id: number) {
    if (!controller.model().opened) { opener = document.querySelector<HTMLElement>(`#actor-layer [data-creature="${id}"]`); beforeOpen(); controller.open(); }
    if (controller.model().canEdit) selectCreature(id);
    else inspect(String(id));
  }
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && dialog.open && docked(dialog.dataset.kind) && !document.querySelector('dialog:modal')) {
      event.preventDefault(); dismiss();
    }
  });
  const pickNote = document.createElement('p'); pickNote.id = 'planning-pick-rule'; pickNote.hidden = true;
  const pickFull = document.createElement('span'); pickFull.id = 'planning-pick-schedule'; pickFull.className = 'pick-full';
  const pickShort = document.createElement('span'); pickShort.className = 'pick-short'; pickShort.setAttribute('aria-hidden', 'true');
  function mutations(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const choices = mutationChoices(model.latest, catalogue, creature);
    creature = choices.selected;
    el('planning-creatures').replaceChildren(...choices.roster.map((one: { id: number; name: string; context: string }) => {
      // Named by the words it shows (#1087): the creature, and its state written under it; an offer's count, not
      // written, is its description.
      const node = button(one.name, () => selectCreature(one.id));
      if (!one.context.endsWith('available')) { const status = document.createElement('small'); status.textContent = one.context; node.append(status); }
      else node.setAttribute('aria-description', one.context);
      node.dataset.creature = String(one.id); node.setAttribute('aria-pressed', String(one.id === creature)); return node;
    }));
    tree?.dispose();
    tree = mutationTree(choices, catalogue, tier => controller.choose({ creature, tier }));
    el('planning-choices').replaceChildren(pickNote, tree.graph);
    el('mutation-preview-region').replaceChildren(tree.pane);
    // The whole tree opens full screen on the mutation in hand; an offer can be chosen from its card there.
    // The atlas covers the field and so holds the draft (canEdit is false under it); it offers the choice
    // whenever the draft could take one, and makes it only once it has closed.
    const view = (id: number | null): AtlasView | null => {
      const now = controller.model();
      if (!now.opened || now.latest?.waitingFor !== 'Evolution') return null;
      const shown = mutationChoices(now.latest, catalogue, id);
      const subject = shown.roster.find((one: { id: number }) => one.id === shown.selected);
      // Over the pick the tree says the pick's short form: the schedule and the count, in the pick's own words.
      return { title: `${subject?.name ?? 'Creature'} · Mutation Tree`, eyebrow: 'MUTATION TREE · YOUR PICK', progress: planOf(now)?.short ?? '', packages: shown.packages,
        roster: shown.roster.map((one: { id: number; name: string; context: string }) => ({ id: String(one.id), name: `${one.name} · ${one.context}` })),
        creature: String(shown.selected), focus: now.choice?.creature === shown.selected ? now.choice?.tier ?? null : null,
        choose: now.available && !now.sending ? tier => { selectCreature(shown.selected); controller.choose({ creature: shown.selected, tier }); } : undefined };
    };
    // The sheet's own header holds what the whole pick is about: the full tree to read, and passing what is left.
    const full = button('Full Mutation Tree', () => { const shown = view(creature); if (shown) atlas.open(shown, catalogue, full, next => view(Number(next))); }); full.id = 'mutation-atlas-open'; full.className = 'planning-action atlas-open';
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
  type Fixed = { position: number; label: string };
  const assignmentText = (one: Assignment) => `${one.position == null ? 'Position unknown' : '#' + one.position} → ${name(one.id)}`;
  const fixedText = (one: Fixed) => `#${one.position} ${one.label}`;
  // A tie between the teams is settled at random (ADR 0208): the window says which team got the earlier place.
  const EARLIER: Record<string, string> = { 'Your team': 'Settled at random: your team got the earlier place.', Opponent: 'Settled at random: the opponent got the earlier place.' };
  const earlierText = (earlier: string | null) => EARLIER[earlier ?? ''] ?? 'The tie between the teams was settled at random.';
  // The opponent orders its own creatures within its places too: with two or more, only the places are certain.
  function opponentPlacesText(detail: { fixed: Fixed[]; opponentOrderOpen: boolean }) {
    if (detail.opponentOrderOpen) return `Opponent places stay fixed: ${detail.fixed.map(one => '#' + one.position).join(', ')} · their order is revealed with yours`;
    return `Opponent places stay fixed: ${detail.fixed.map(fixedText).join(' · ')}`;
  }
  // Each tie a card: the places it shares as the order's badges with who takes each now, the creatures still to place
  // as pills to tap in acting order, and what a random tie and the opponent fixed, in small type under them.
  function ties(model: ReturnType<Controller['model']>) {
    el('planning-creatures').replaceChildren();
    el('planning-help').textContent = 'Tap your tied creatures in the order they act. Your team keeps its places; the opponent keeps theirs, and both reveal together.';
    const groups = model.latest.options.tieOrder?.ties ?? [];
    const context = battleTieGroups(model.latest, model.tapped);
    const note = (value: string, className: string) => { const node = line(value); node.className = className; return node; };
    const rows = untapped(groups, model.tapped).map((left: number[], index: number) => {
      const row = document.createElement('section'); row.className = 'tie-group';
      const detail = context[index];
      const head = document.createElement('div'); head.className = 'tie-head';
      const title = document.createElement('h3'); title.textContent = `Tie ${index + 1}`;
      const kind = document.createElement('span'); kind.className = 'tie-kind'; kind.dataset.random = String(Boolean(detail.random));
      kind.textContent = detail.random ? 'Settled at random' : 'Same team';
      head.append(title, kind);
      const places = document.createElement('div'); places.className = 'tie-places';
      places.append(...detail.assignments.map((one: Assignment) => {
        const slot = document.createElement('span'); slot.className = 'tie-place';
        const badge = document.createElement('span'); badge.className = 'tie-badge'; badge.textContent = one.position == null ? '?' : String(one.position);
        const who = document.createElement('span'); who.textContent = name(one.id); slot.append(badge, who); return slot;
      }));
      // The same facts in words, for assistive technology and anyone reading closely.
      row.append(head, places, note(detail.assignments.map(assignmentText).join(' · '), 'tie-assignments unseen'),
        note(detail.places.length ? `Your fixed places: ${detail.places.join(', ')}` : 'Positions not available in this snapshot.', 'tie-note'),
        note(detail.random ? `${earlierText(detail.earlier)} Your choice changes only who takes your places.` : 'Only your team is tied here: the order is yours.', 'tie-note'));
      if (detail.fixed.length) row.append(note(opponentPlacesText(detail), 'tie-note tie-opponent'));
      if (left.length > 1) {
        const pills = document.createElement('div'); pills.className = 'tie-pills';
        const hint = document.createElement('p'); hint.className = 'tie-hint'; hint.textContent = `Tap who acts first · ${orderOf([groups[index]], model.tapped).map(name).join(' → ')}`;
        for (const id of left) { const node = button(name(id), () => controller.order(id)); node.dataset.tie = String(id); node.className = 'planning-action'; pills.append(node); }
        row.append(hint, pills);
      }
      return row;
    });
    const actions = document.createElement('div'); actions.className = 'planning-actions tie-actions';
    const keep = button('Keep current order', () => controller.choose({ keep: true })); keep.id = 'planning-keep'; keep.className = 'planning-action';
    const again = button('Start again', () => controller.restart()); again.className = 'planning-action';
    actions.append(keep, again);
    el('planning-choices').replaceChildren(...rows, actions);
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
  // The same selection is drawn pressed wherever it sits: a mutation button, a tempo, pass or the kept order.
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
  // The team's picks are the budget a player spends here. Between a sent pick and the next asking the count is
  // the one the host has not restated yet, so it says the pick went and what is left after it. The note at the
  // top of the tree spells the budget out: a token per pick of the phase, filled while unspent, and, when there
  // are fewer picks than creatures that could take one, that the player chooses which mutate now, with a link to the
  // rulebook's section for what later phases bring. It scrolls with the tree, so it costs a short sheet none of the tree's fixed room.
  const planOf = (model: ReturnType<Controller['model']>) =>
    pickPlan(model.latest, model.accepted && !model.available ? model.choice ?? {} : null, lastCatalogue?.rules?.evolutionInterval ?? null);
  function pickBudget(model: ReturnType<Controller['model']>) {
    const plan = planOf(model);
    pickNote.hidden = !plan?.left;
    if (!plan) { el('planning-help').textContent = 'Team pick count unavailable'; pickNote.replaceChildren(); return; }
    // The first line in two forms the sheet's height picks (battle.css, the 660 px of #1031): the sentence, which
    // stays the sheet's description where a short screen shows the label instead.
    pickFull.textContent = plan.full; pickShort.textContent = plan.short;
    if (pickFull.parentElement !== el('planning-help')) el('planning-help').replaceChildren(pickFull, pickShort);
    const pips = document.createElement('span'); pips.className = 'pick-pips'; pips.setAttribute('aria-hidden', 'true');
    for (let index = 0; index < plan.spent + plan.left; index++) {
      const pip = document.createElement('span'); pip.className = 'pick-pip'; pip.dataset.spent = String(index < plan.spent); pips.append(pip);
    }
    if (!plan.rules) { pickNote.replaceChildren(pips, document.createTextNode(plan.rule)); return; }
    const rules = document.createElement('a'); rules.href = plan.rules; rules.target = '_blank'; rules.rel = 'noopener';
    rules.textContent = 'Rules'; rules.setAttribute('aria-label', 'Rules, Mutations, opens in a new tab');
    pickNote.replaceChildren(pips, document.createTextNode(plan.rule ? `${plan.rule} ` : ''), rules);
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
    el('planning-open').hidden = !model.available || model.opened; el('planning-open').textContent = model.latest?.waitingFor === 'Evolution' ? 'Mutation Tree ↗' : label;
    el<HTMLButtonElement>('planning-open').disabled = model.sending;
    el('planning-error').textContent = model.error; el('planning-error').hidden = !model.error || model.opened;
    el('planning-dialog-error').textContent = model.error;
    el<HTMLButtonElement>('planning-close').disabled = model.sending;
    if (!model.opened) { closeChoices(); return; }
    dialog.dataset.kind = model.latest.waitingFor;
    // The pick's rule sentence describes its sheet, so it is still said where a short screen hides it (#1031).
    if (model.latest.waitingFor === 'Evolution') dialog.setAttribute('aria-describedby', `${pickFull.id} ${pickNote.id}`); else dialog.removeAttribute('aria-describedby');
    if (model.latest.waitingFor !== 'Evolution') el('planning-tools').replaceChildren();
    el('planning-heading').textContent = model.latest.waitingFor === 'Evolution' ? 'Mutation Tree' : label;
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
    if (model.latest.waitingFor === 'Evolution') pickBudget(model);
    if (!dialog.open) {
      if (model.latest.waitingFor === 'Evolution') { dialog.show(); focusStart(); }
      else if (model.latest.waitingFor === 'Speed') { dialog.show(); tempoStart(); }
      else { dialog.show(); dialog.querySelector<HTMLElement>('#planning-choices [data-tie], #planning-keep')?.focus({ preventScroll: true }); }
    } else if (continued && (!dialog.contains(document.activeElement) || (document.activeElement as HTMLButtonElement).disabled)) focusStart();
  }
  return { render, openCreature, recipient: () => creature };
}
