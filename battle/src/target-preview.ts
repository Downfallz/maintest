import { battleTargetPreview } from '../../table/battle-targets.js';
import { outcomeChip } from '../../table/effect-chips.js';
import { recapEffectText } from '../../table/feed.js';
import { chipElement, conditionKey, iconElement, type EffectChip } from './condition-icons';

type Preview = ReturnType<typeof battleTargetPreview>;
type Cue = { kind: string; glyph?: string; harmful: boolean };
const line = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
type Chosen = { card: { effects?: string[]; casterEffects?: string[]; criticalNote?: string; marks?: { kind: string }[] | null }; crit: { value: string; hint?: string } | null } | null;
type Outcome = { effect?: { amount?: number; amountPerRound?: number; duration?: { rounds?: number; isPermanent?: boolean } } };
/** How long an outcome lasts, as the chip shows it beside its amount: "2 rds", "/ rd", "perm."; empty when it is at once. */
export function lasting(outcome: Outcome) {
  const effect = outcome?.effect, duration = effect?.duration;
  const perRound = Number.isInteger(effect?.amountPerRound) && !Number.isInteger(effect?.amount) ? '/ rd' : '';
  const span = duration?.isPermanent === true ? 'perm.' : Number.isInteger(duration?.rounds) ? `${duration!.rounds} rd${duration!.rounds === 1 ? '' : 's'}` : '';
  return [perRound, span].filter(Boolean).join(' · ');
}
// A host outcome as the field's chip: its icon and amount, how long it lasts beside them, and the engine's words
// kept for screen readers. An estimate is read before a cast is confirmed, so a lasting condition shows its turns.
function chips(outcomes: unknown[], effects: Cue[]) {
  const row = line('span', '', 'preview-chips');
  row.append(...outcomes.map(outcome => {
    const chip = outcomeChip(outcome) as EffectChip;
    const node = chipElement({ ...chip, text: chip.text || recapEffectText(outcome) }, effects);
    const span = lasting(outcome as Outcome);
    if (span) { const tail = line('span', span, 'chip-lasting'); tail.setAttribute('aria-hidden', 'true'); node.querySelector('.unseen')!.before(tail); }
    return node;
  }));
  return row;
}

// The estimate under the field while targets are chosen, in the spell card's design: the spell's name with its
// cost and crit chips, the chosen card in full (its lines in the host's words, its crit chance and what it holds
// for, its crit note: what the spellbook's faces leave, read before the spell is declared), then what each
// selected target would take, plain and on a crit, as the field's effect
// chips, and what the caster takes once. The scroll region and its position survive polls; only changed facts
// replace the non-interactive content, and the confirmation buttons stay outside it.
function chosenCard({ card, crit }: NonNullable<Chosen>, effects: Cue[]) {
  const nodes: HTMLElement[] = [];
  const lines = [...(card.effects ?? []), ...(card.casterEffects ?? [])];
  if (lines.length) {
    const list = line('ul', '', 'spell-card-effects target-card');
    list.append(...lines.map((text, index) => {
      const item = line('li', '', 'spell-card-effect'), mark = card.marks?.[index];
      const key = mark ? conditionKey(mark.kind, effects) : 'other';
      item.dataset.cue = key; item.append(iconElement(key), line('span', text));
      if (index >= (card.effects ?? []).length) item.dataset.caster = 'true';
      return item;
    }));
    nodes.push(list);
  }
  if (crit) nodes.push(line('p', [`Crit ${crit.value}`, crit.hint].filter(Boolean).join(' · '), 'target-crit-hint'));
  if (crit && card.criticalNote) nodes.push(line('p', card.criticalNote, 'spell-card-note'));
  return nodes;
}

// The spell's name with its cost, the energy left after it, and its crit chance at this tempo.
function headOf(model: NonNullable<Preview>, name: string) {
  const head = line('div', '', 'spell-card-head target-head');
  const badges = line('span', '', 'spell-card-chips');
  const cost = line('span', '', 'spell-chip spell-chip-cost target-cost');
  if (Number.isFinite(model.costValue)) {
    cost.append(iconElement('energy-gain'), line('span', String(model.costValue)), line('span', ' energy', 'spell-card-sr'));
    if (Number.isFinite(model.energyAfter)) cost.append(line('small', ` → ${model.energyAfter} left`));
  } else cost.append(line('span', 'Cost unavailable'));
  cost.title = model.cost;
  badges.append(cost);
  if (Number.isFinite(model.critChance) && model.critChance! > 0) {
    const crit = line('span', '', 'spell-chip spell-chip-crit'); crit.title = model.critical;
    crit.append(line('span', '✦', 'spell-crit-mark'), document.createTextNode(`${Math.round(model.critChance! * 100)}%`), line('span', ' crit at this tempo', 'spell-card-sr'));
    badges.append(crit);
  }
  head.append(line('strong', name || model.spell, 'spell-card-name'), badges);
  return head;
}

// What one selected target would take, plain and on a crit.
function estimate(row: NonNullable<Preview>['rows'][number], effects: Cue[]) {
  const one = line('div', '', 'target-estimate');
  one.append(line('strong', row.name, 'target-name'));
  if (row.plainEffects) one.append(row.plainEffects.length ? chips(row.plainEffects, effects) : line('span', 'No effect on this target', 'target-none'));
  else one.append(line('span', row.plain, 'target-none'));
  if (row.criticalEffects.length) {
    const crit = line('span', '', 'target-crit'); crit.append(line('span', '✦ Crit', 'target-crit-label'), chips(row.criticalEffects, effects));
    one.append(crit);
  }
  return one;
}

export function targetPreviewControls(id: string) {
  const region = document.getElementById(id)!;
  let key = '';
  function render(model: Preview, { name = '', effects = [], chosen = null }: { name?: string; effects?: Cue[]; chosen?: Chosen } = {}) {
    region.hidden = !model;
    const nextKey = JSON.stringify([model, name, chosen]);
    if (key === nextKey) return;
    key = nextKey;
    const scroll = region.scrollTop;
    if (!model) { region.replaceChildren(); region.scrollTop = 0; return; }
    const head = headOf(model, name);
    const nodes: HTMLElement[] = [head];
    if (chosen) nodes.push(...chosenCard(chosen, effects));
    if (model.reason) nodes.push(line('p', model.reason, 'target-reason'));
    if (model.note) nodes.push(line('p', model.note, 'target-note'));
    nodes.push(...model.rows.map(row => estimate(row, effects)));
    if (model.casterEffects.length) {
      const caster = line('div', '', 'target-estimate target-caster');
      caster.append(line('strong', 'Caster, once', 'target-name'), chips(model.casterEffects, effects));
      nodes.push(caster);
    } else if (model.caster) nodes.push(line('p', model.caster, 'target-caster'));
    nodes.push(line('small', model.caveat));
    region.replaceChildren(...nodes); region.scrollTop = scroll;
  }
  return { render };
}
