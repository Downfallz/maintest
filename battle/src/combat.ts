import { battleTargetPreview } from '../../table/battle-targets.js';
import { targetPreviewControls } from './target-preview';
import { createBattleCombat } from '../../table/battle-combat.js';
import { cardTitle, cardsById } from '../../table/card.js';
import { phaseLabel, tempoLabel, TEMPO_RULE } from '../../table/terms.js';
import { iconElement } from './condition-icons';
import { spellbookControls } from './spellbook';

type Controller = ReturnType<typeof createBattleCombat>;
type Model = ReturnType<Controller['model']>;
export interface SpellCatalogue { cards?: { id: string; name: string; type?: string; creatureClass?: string; cost?: number; targeting?: string; effects?: string[]; casterEffects?: string[]; critical?: string; criticalThreshold?: number | null; criticalNote?: string; marks?: { kind: string; onCaster: boolean }[] }[]; effects?: { kind: string; glyph?: string; harmful: boolean }[] }

// The combat form owns HTML only. Choices and request lifetimes live in the controller; the scene only
// receives the current highlights. Rebuilding a list is keyed so polls never steal keyboard focus or scroll.
export function combatControls(controller: Controller, name: (id: unknown) => string, beforeOpen: () => void = () => {}) {
  const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
  const dialog = el<HTMLDialogElement>('combat-dialog');
  const spellbook = spellbookControls(controller);
  const targetPreview = targetPreviewControls('combat-preview'), intentPreview = targetPreviewControls('intent-preview');
  const click = (id: string, action: () => void) => el(id).addEventListener('click', action);
  click('combat-open', () => { beforeOpen(); controller.open(); });
  click('combat-back', () => controller.back());
  click('combat-cancel', () => controller.close());
  click('combat-dialog-close', () => controller.close());
  click('combat-confirm', () => { void controller.submit(); });
  click('combat-declare', () => { void controller.submit(); });
  dialog.addEventListener('cancel', event => { event.preventDefault(); controller.close(); });
  document.addEventListener('keydown', event => {
    if ((event.target as Element | null)?.closest?.('.home-header, .navigation-panels')) return;
    if (event.key !== 'Escape' || document.querySelector('dialog:modal')) return;
    // The docked spell choice is not modal, so Escape is heard here rather than as its dialog's cancel.
    if (controller.model().target && !document.querySelector('dialog[open]')) { event.preventDefault(); controller.close(); }
    else if (dialog.open) { event.preventDefault(); controller.close(); }
  });

  // Right under the acting creature's name, what it plays with: its place in the turn order, its tempo (Instinct
  // acts first, only Precision crits) and the energy it has to spend.
  function drawActor(context: Model['reference']) {
    const holder = el('combat-actor');
    const key = JSON.stringify([context.turn, context.tempo, context.energy]);
    if (holder.dataset.content === key) return;
    holder.dataset.content = key;
    const chip = (kind: string, ...parts: (Node | string)[]) => {
      const node = document.createElement('span'); node.className = 'actor-chip'; node.dataset.kind = kind; node.append(...parts); return node;
    };
    const number = (value: string) => { const node = document.createElement('span'); node.className = 'order-number'; node.textContent = value; return node; };
    const chips: HTMLElement[] = [];
    if (context.turn != null) chips.push(chip('order', number(String(context.turn)), ' in turn order'));
    if (context.tempo) {
      const tempo = chip('tempo', context.tempo === 'Quick' ? '⚡ ' : '✦ ', tempoLabel(context.tempo));
      tempo.dataset.tempo = context.tempo; tempo.title = TEMPO_RULE; chips.push(tempo);
    }
    chips.push(chip('energy', iconElement('energy-gain'), context.energy == null ? 'Energy unavailable' : `${context.energy} energy`));
    for (const one of chips) { const tempo = chips.find(item => item.dataset.kind === 'tempo')?.dataset.tempo; if (tempo) one.dataset.tempo = tempo; }
    holder.replaceChildren(...chips);
  }

  // `held`: results are being read on the field. The docked choice steps aside, its draft kept, and comes back
  // once they are read: no command is offered while results are still to be read (ADR 0139).
  function render(model: Model, catalogue: SpellCatalogue | null, newSpells: string[] = [], held = false) {
    const cards = cardsById(catalogue);
    const targeting = model.opened && Boolean(model.target);
    const choosing = model.opened && !targeting;
    el('battle').dataset.combat = targeting ? 'targets' : choosing ? 'spells' : 'closed';
    el('combat-open').hidden = !model.available || model.opened;
    el<HTMLButtonElement>('combat-open').disabled = model.sending || !model.online;
    el('combat-open').textContent = model.waitingFor === 'Target' ? 'Choose targets' : 'Choose spell';
    el('combat-targets').hidden = !targeting;
    const preview = battleTargetPreview(model);
    const about = { name: preview ? cardTitle(cards.get(preview.spell)) || '' : '', effects: catalogue?.effects ?? [] };
    targetPreview.render(targeting ? preview : null, about);
    intentPreview.render(choosing && model.waitingFor === 'Intent' ? preview : null, about);
    el('combat-error').textContent = model.error; el('combat-error').hidden = !model.error || choosing;
    el('combat-dialog-error').textContent = model.error; el('combat-dialog-error').hidden = !model.error;
    el<HTMLButtonElement>('combat-dialog-close').disabled = model.sending;
    el<HTMLButtonElement>('combat-cancel').disabled = model.sending;
    if (choosing) {
      el('combat-heading').textContent = `${name(model.actor)} · choose a spell`;
      const context = model.reference;
      el('combat-context').textContent = [context.round == null ? null : `Round ${context.round}`, phaseLabel(context.phase)].filter(Boolean).join(' · ');
      drawActor(context);
      el('combat-help').textContent = model.waitingFor === 'Action' ? 'Choose a spell, then select its targets on the field. Confirm to cast.' : 'Declare a hidden intent. Its targets will be chosen when it resolves.';
      spellbook.render(model, catalogue, newSpells);
      const confirm = el<HTMLButtonElement>('combat-declare');
      confirm.hidden = model.waitingFor !== 'Intent'; confirm.disabled = !model.canSubmit;
      confirm.textContent = model.sending ? 'Sending…' : model.chosen ? `Declare ${cardTitle(cards.get(model.chosen)) || model.chosen}${model.short.includes(model.chosen) ? ' · may fizzle' : ''}` : 'Select a spell';
      // Docked beside the field, as the mutation choice is (ADR 0149): the board stays in view while a spell is chosen.
      if (held) { if (dialog.open) dialog.close(); }
      else if (!dialog.open) { dialog.show(); spellbook.opened(); }
    } else if (dialog.open) dialog.close();
    if (targeting) {
      const legal = model.target.legalTargets;
      el('combat-title').textContent = `${name(model.actor)} · ${cardTitle(cards.get(model.target.spell)) || model.target.spell}`;
      el('combat-selection').textContent = model.emptyCast ? 'Too few legal targets remain. This cast may fizzle.' : legal.maxTargets === 0 ? 'No targets required. Confirm to cast.' : `${model.picked.length} selected · choose ${legal.minTargets === legal.maxTargets ? legal.maxTargets : `${legal.minTargets}–${legal.maxTargets}`}`;
      const confirm = el<HTMLButtonElement>('combat-confirm'); confirm.disabled = !model.canSubmit;
      confirm.textContent = model.sending ? 'Sending…' : model.emptyCast ? 'Cast without targets' : 'Confirm cast';
      const back = el<HTMLButtonElement>('combat-back'); back.hidden = model.waitingFor !== 'Action'; back.disabled = !model.canEdit;
    }
    if (!model.opened) {
      spellbook.clear();
      for (const id of ['combat-heading', 'combat-context', 'combat-help', 'combat-title', 'combat-selection']) el(id).textContent = '';
      el('combat-actor').replaceChildren(); el('combat-actor').dataset.content = '';
      el('combat-declare').textContent = 'Declare spell';
    }
  }
  return { render, target: (id: string) => {
    const model = controller.model();
    if (!model.opened || !model.target) return false;
    const candidate = model.target.legalTargets.candidates.find((one: unknown) => String(one) === id);
    if (candidate !== undefined) controller.pick(candidate);
    return true;
  } };
}
