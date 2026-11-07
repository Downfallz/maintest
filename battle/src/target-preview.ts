import { battleTargetPreview } from '../../table/battle-targets.js';
import { outcomeChip } from '../../table/effect-chips.js';
import { recapEffectText } from '../../table/feed.js';
import { chipElement, iconElement, type EffectChip } from './condition-icons';

type Preview = ReturnType<typeof battleTargetPreview>;
type Cue = { kind: string; glyph?: string; harmful: boolean };
const line = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
// A host outcome as the field's chip: its icon and amount, with the engine's words kept for screen readers.
function chips(outcomes: unknown[], effects: Cue[]) {
  const row = line('span', '', 'preview-chips');
  row.append(...outcomes.map(outcome => {
    const chip = outcomeChip(outcome) as EffectChip;
    return chipElement({ ...chip, text: chip.text || recapEffectText(outcome) }, effects);
  }));
  return row;
}

// The estimate under the field while targets are chosen, in the spell card's design: the spell's name with its
// cost and crit chips, then what each selected target would take, plain and on a crit, as the field's effect
// chips, and what the caster takes once. The scroll region and its position survive polls; only changed facts
// replace the non-interactive content, and the confirmation buttons stay outside it.
export function targetPreviewControls(id: string) {
  const region = document.getElementById(id)!;
  let key = '';
  function render(model: Preview, { name = '', effects = [] }: { name?: string; effects?: Cue[] } = {}) {
    region.hidden = !model;
    const nextKey = JSON.stringify([model, name]);
    if (key === nextKey) return;
    key = nextKey;
    const scroll = region.scrollTop;
    if (!model) { region.replaceChildren(); region.scrollTop = 0; return; }
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
    const nodes: HTMLElement[] = [head];
    if (model.reason) nodes.push(line('p', model.reason, 'target-reason'));
    if (model.note) nodes.push(line('p', model.note, 'target-note'));
    for (const row of model.rows) {
      const one = line('div', '', 'target-estimate');
      one.append(line('strong', row.name, 'target-name'));
      if (row.plainEffects) one.append(row.plainEffects.length ? chips(row.plainEffects, effects) : line('span', 'No effect on this target', 'target-none'));
      else one.append(line('span', row.plain, 'target-none'));
      if (row.criticalEffects.length) {
        const crit = line('span', '', 'target-crit'); crit.append(line('span', '✦ Crit', 'target-crit-label'), chips(row.criticalEffects, effects));
        one.append(crit);
      }
      nodes.push(one);
    }
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
