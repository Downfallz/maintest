import { createArenaCombat } from '../../table/arena-combat.js';
import { cardTitle, cardsById } from '../../table/card.js';
import { phaseLabel, tempoLabel } from '../../table/terms.js';
import { spellbookControls } from './spellbook';

type Controller = ReturnType<typeof createArenaCombat>;
type Model = ReturnType<Controller['model']>;
export interface SpellCatalogue { cards?: { id: string; name: string; type?: string; creatureClass?: string; cost?: number; targeting?: string; effects?: string[]; casterEffects?: string[]; critical?: string; criticalThreshold?: number | null; criticalNote?: string }[] }

// The combat form owns HTML only. Choices and request lifetimes live in the controller; the scene only
// receives the current highlights. Rebuilding a list is keyed so polls never steal keyboard focus or scroll.
export function combatControls(controller: Controller, name: (id: unknown) => string, beforeOpen: () => void = () => {}) {
  const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
  const dialog = el<HTMLDialogElement>('combat-dialog');
  const spellbook = spellbookControls(controller);
  const click = (id: string, action: () => void) => el(id).addEventListener('click', action);
  click('combat-open', () => { beforeOpen(); controller.open(); });
  click('combat-back', () => controller.back());
  click('combat-cancel', () => controller.close());
  click('combat-dialog-close', () => controller.close());
  click('combat-confirm', () => { void controller.submit(); });
  click('combat-declare', () => { void controller.submit(); });
  dialog.addEventListener('cancel', event => { event.preventDefault(); controller.close(); });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && controller.model().target && !document.querySelector('dialog[open]')) { event.preventDefault(); controller.close(); }
  });

  function render(model: Model, catalogue: SpellCatalogue | null) {
    const cards = cardsById(catalogue);
    const targeting = model.opened && Boolean(model.target);
    const choosing = model.opened && !targeting;
    el('arena').dataset.combat = targeting ? 'targets' : choosing ? 'spells' : 'closed';
    el('combat-open').hidden = !model.available || model.opened;
    el<HTMLButtonElement>('combat-open').disabled = model.sending || !model.online;
    el('combat-open').textContent = model.waitingFor === 'Target' ? 'Choose targets' : 'Choose spell';
    el('combat-targets').hidden = !targeting;
    el('combat-error').textContent = model.error; el('combat-error').hidden = !model.error || choosing;
    el('combat-dialog-error').textContent = model.error; el('combat-dialog-error').hidden = !model.error;
    el<HTMLButtonElement>('combat-dialog-close').disabled = model.sending;
    el<HTMLButtonElement>('combat-cancel').disabled = model.sending;
    if (choosing) {
      el('combat-heading').textContent = `${name(model.actor)} · choose a spell`;
      const context = model.reference;
      el('combat-context').textContent = [context.round == null ? null : `Round ${context.round}`, phaseLabel(context.phase),
        context.turn == null ? null : `Turn ${context.turn}`, context.tempo ? tempoLabel(context.tempo) : null,
        context.energy == null ? 'Energy unavailable' : `${context.energy} energy`].filter(Boolean).join(' · ');
      el('combat-help').textContent = model.waitingFor === 'Action' ? 'Choose a spell, then select its targets on the field. Confirm to cast.' : 'Declare a hidden intent. Its targets will be chosen when it resolves.';
      spellbook.render(model, catalogue);
      const confirm = el<HTMLButtonElement>('combat-declare');
      confirm.hidden = model.waitingFor !== 'Intent'; confirm.disabled = !model.canSubmit;
      confirm.textContent = model.sending ? 'Sending…' : model.chosen ? `Declare ${cardTitle(cards.get(model.chosen)) || model.chosen}${model.short.includes(model.chosen) ? ' · may fizzle' : ''}` : 'Select a spell';
      if (!dialog.open) { dialog.showModal(); spellbook.opened(); }
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
