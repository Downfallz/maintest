import { createArenaPlanning } from '../../table/arena-planning.js';
import { talentClasses, packageForest, passiveOf, requirementLine } from '../../table/mat.js';
import { SPEEDS, TEMPO_RULE, tempoLabel, stageLabel } from '../../table/terms.js';
import { untapped, orderOf } from '../../table/ties.js';
import { cardLines, cardsById } from '../../table/card.js';
import type { SpellCatalogue } from './combat';

export interface Package { id: string; name: string; level: number; initiativeBonus: number; spells: string[];
  prerequisites?: string[]; anyOf?: string[]; passiveLines?: string[]; tags?: string[] }
export interface Catalogue extends SpellCatalogue { packages?: Package[] }
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
    const view = model.latest, allies = view.board.allies ?? [], offers = view.options.evolution;
    if (!allies.some((one: { id: number }) => one.id === creature)) creature = offers?.creatures?.[0]?.creature ?? allies[0]?.id;
    const picker = el('planning-creatures');
    picker.replaceChildren(...allies.map((one: { id: number }) => {
      // A mutation is picked for the creature whose tree is shown: switching creatures drops the selection.
      const node = button(name(one.id), () => { if (one.id !== creature) controller.choose(null); creature = one.id; rendered = ''; render(controller.model(), catalogue); });
      node.dataset.creature = String(one.id); node.setAttribute('aria-pressed', String(one.id === creature)); node.disabled = model.sending; return node;
    }));
    const cards = cardsById(catalogue), selected = allies.find((one: { id: number }) => one.id === creature);
    const packs = talentClasses(catalogue, cards, selected, offers, view.board.evolutionChoices);
    const byId = new Map<string, Package & { status: string }>(packs.map((pack: Package & { status: string }) => [pack.id, pack]));
    const statuses: Record<string, string> = { known: 'Acquired', picked: 'Picked · hidden until reveal', available: 'Available', future: 'Not offered this pick' };
    function branch(node: Package & { children: typeof node[] }): HTMLDetailsElement {
      const pack = byId.get(node.id)!;
      const details = document.createElement('details'); details.className = 'mutation-branch'; details.open = node.level === 1 || pack.status === 'available';
      const summary = document.createElement('summary'); summary.textContent = `${pack.name} · ${stageLabel(pack.level)} · ${statuses[pack.status]}`;
      const face = document.createElement('div'); face.className = 'mutation-face';
      face.append(line(`${pack.initiativeBonus > 0 ? '+' : ''}${pack.initiativeBonus} initiative · ${(pack.tags ?? []).join(' · ')}`),
        line(requirementLine(pack, (id: string) => byId.get(id)?.name ?? id)));
      for (const passive of passiveOf(pack)) face.append(line(`Held: ${passive}`));
      for (const id of pack.spells ?? []) {
        const card = cards.get(id); const spell = document.createElement('details');
        const title = document.createElement('summary'); title.textContent = card?.name ?? id;
        spell.append(title, line(cardLines(card).join(' · '))); face.append(spell);
      }
      const acquire = button(`Select ${pack.name}`, () => controller.choose({ creature, tier: pack.id }));
      acquire.dataset.mutation = pack.id; acquire.disabled = pack.status !== 'available';
      acquire.dataset.offered = String(pack.status === 'available'); face.append(acquire);
      details.append(summary, face, ...node.children.map(branch)); return details;
    }
    el('planning-choices').replaceChildren(...packageForest(catalogue).map(branch));
    // Unknown catalogue entries remain playable, with the host identifier as their label.
    for (const id of offers?.creatures?.find((one: { creature: number }) => one.creature === creature)?.availableTiers ?? []) {
      if (!byId.has(id)) { const node = button(id, () => controller.choose({ creature, tier: id })); node.dataset.mutation = id; node.dataset.offered = 'true'; el('planning-choices').append(node); }
    }
    const pass = button('Pass this pick', () => controller.choose({ pass: true })); pass.id = 'planning-pass'; el('planning-choices').append(pass);
    el('planning-help').textContent = `${offers?.remainingPicks ?? 0} shared team picks left. At most one mutation per creature this opportunity. Picks reveal together. Explore branches, then select and confirm.`;
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
  const headings: Record<string, string> = { Evolution: 'Mutation Tree', Speed: 'Choose tempo', TieOrder: 'Order ties' };
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
    if (chosen?.pass) return 'Pass this pick';
    if (chosen?.tier) return `${name(chosen.creature)} · ${catalogue?.packages?.find(pack => pack.id === chosen.tier)?.name ?? chosen.tier}`;
    if (chosen?.speed) return `${name(model.latest.waitingCreature)} · ${tempoLabel(chosen.speed)}`;
    if (chosen?.keep) return 'Dice order';
    return model.latest?.waitingFor === 'TieOrder' ? 'Chosen order' : 'Select a choice';
  }
  // The choices are rebuilt when the question, the offer, the taps or the chosen creature change, and
  // the focus of a player ordering ties is kept on the ties instead of falling off the removed button.
  function rebuild(model: ReturnType<Controller['model']>, catalogue: Catalogue | null) {
    const kind = model.latest.waitingFor;
    const key = JSON.stringify([model.question, model.latest.options, model.latest.board.evolutionChoices, model.tapped, creature]);
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
    el('planning-heading').textContent = label;
    rebuild(model, catalogue);
    for (const node of Array.from(el('planning-choices').querySelectorAll<HTMLButtonElement>('button'))) {
      node.disabled = !model.canEdit || node.dataset.offered === 'false';
      node.setAttribute('aria-pressed', String(isPressed(node, model.choice)));
    }
    for (const node of Array.from(el('planning-creatures').querySelectorAll<HTMLButtonElement>('button'))) node.disabled = !model.canEdit;
    el('planning-selection').textContent = selectionText(model, catalogue);
    el<HTMLButtonElement>('planning-confirm').disabled = !model.canSubmit;
    el('planning-confirm').textContent = model.sending ? 'Sending…' : 'Confirm choice';
    if (!dialog.open) dialog.showModal();
  }
  return { render };
}
