import { effectText, chance, groupTargetLines } from './guidance.js';

// A description of the current host offer, never a second targeting rule. A full multi-target selection
// can still deselect; a single-target spell can replace its target in one tap.
export function arenaTargetState(model, id) {
  if (!model.opened || !model.target) return null;
  const legal = model.target.legalTargets, picked = model.picked.map(String);
  if (model.emptyCast) return { state: 'unavailable', label: '— Too few targets', selectable: false };
  if (picked.includes(String(id))) return { state: 'selected', label: '✓ Selected', selectable: true };
  if (!legal.candidates.some(one => String(one) === String(id))) return { state: 'unavailable', label: '— Unavailable', selectable: false };
  if (legal.maxTargets > 1 && picked.length >= legal.maxTargets) return { state: 'unavailable', label: '— Selection full', selectable: false };
  return { state: 'legal', label: picked.length && legal.maxTargets === 1 ? '+ Change target' : '+ Select target', selectable: true };
}

function criticalChance(guide, tempo) {
  const speed = guide.chosenSpeed ?? tempo;
  if (speed === 'Quick') return guide.quickCriticalChance;
  if (speed === 'Standard') return guide.standardCriticalChance;
  return null;
}
const effects = values => Array.isArray(values) ? values.map(effectText).join(', ') || 'No effect on this target' : 'Preview unavailable';
function rowsFor(guide, selected, legal, names, critical) {
  const rows = selected.filter(id => legal.candidates.includes(id)).map(id => {
    const row = guide.targets?.find(one => one.target === id);
    return { target: id, plain: effects(row?.plain), critical: critical === 0 ? '' : effects(row?.critical) };
  });
  return groupTargetLines(rows).map(group => ({
    name: group.targets.map(id => names.get(id) ?? `Creature ${id}`).join(', '),
    plain: group.plain, critical: group.critical === group.plain ? '' : group.critical,
  }));
}
function targetNote(model) {
  if (!model.target) return 'Targets are chosen when this hidden spell resolves. Earlier actions can change this estimate.';
  if (model.emptyCast) return 'Too few legal targets remain. Confirming sends an empty cast that may fizzle.';
  if (model.target.legalTargets.maxTargets === 0) return 'No targets required. Confirm explicitly to cast.';
  if (!model.picked.length) return 'Select a legal target to preview its effects.';
  return '';
}

// All numeric predictions come from this asking's guidance. No catalogue calculation, cached guide,
// feed/replay data or opponent intent is read here. The host still decides whether a command is legal.
export function arenaTargetPreview(model) {
  const spell = model.target?.spell ?? model.chosen;
  if (!model.opened || !spell || !['Action', 'Intent', 'Target'].includes(model.waitingFor)) return null;
  const guide = model.reference.guidance.find(one => one.spell === spell);
  const note = targetNote(model);
  const base = { spell, cost: 'Cost preview unavailable', reason: '', critical: '', note, rows: [], caster: '',
    caveat: 'Estimate before caps and stacking. Rolls can change outcomes; History records the actual result.' };
  if (!guide) return { ...base, reason: 'Preview unavailable for this question.' };
  const cost = Number.isFinite(guide.cost) && Number.isFinite(guide.energyAfterCost)
    ? `${guide.cost} energy → ${guide.energyAfterCost} after cost` : base.cost;
  if (guide.unavailableReason) return { ...base, cost, reason: guide.unavailableReason };
  const critical = criticalChance(guide, model.reference.tempo);
  const names = new Map((model.reference.creatures ?? []).map(one => [one.id, one.name]));
  const rows = model.target && !model.emptyCast ? rowsFor(guide, model.picked, model.target.legalTargets, names, critical) : [];
  let caster = '';
  if (!model.emptyCast && guide.casterEffects?.length) caster = `On caster, once: ${guide.casterEffects.map(effectText).join(', ')}`;
  else if (model.target?.legalTargets.maxTargets === 0) caster = 'Caster preview unavailable.';
  return { ...base, cost, rows, caster, critical: Number.isFinite(critical) ? `${chance(critical)} critical at this tempo` : 'Critical chance unavailable for this tempo' };
}
