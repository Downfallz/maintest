import { recapEffectText as effectText } from './feed.js';
export { recapEffectText as effectText } from './feed.js';

// Presentation only: every amount, refusal and critical chance is computed by the host.
export function chance(value) {
  return `${Math.round((value ?? 0) * 100)}%`;
}

export function spellSummary(guide) {
  if (!guide) return '';
  if (guide.unavailableReason) return guide.unavailableReason;
  const timing = guide.turn ? `Turn ${guide.turn} · ` : '';
  const critical = guide.chosenSpeed === 'Quick' ? guide.quickCriticalChance : guide.standardCriticalChance;
  const parts = [`${timing}${guide.cost} energy → ${guide.energyAfterCost} after cost`];
  if (critical > 0) parts.push(`${chance(critical)} critical`);
  return parts.join(' · ');
}


export function targetLines(guide, picked = []) {
  const candidates = guide?.targets ?? [];
  return candidates.filter(row => picked.length === 0 || picked.includes(row.target)).map(row => ({
    target: row.target,
    plain: row.plain.map(effectText).join(', ') || 'No effect on this target',
    critical: row.critical.map(effectText).join(', ') || 'No effect on this target',
  }));
}

// Candidates the spell would do exactly the same thing to share one line: a heal on three unhurt allies is one
// sentence, not three copies of it. The order is the host's, by each group's first candidate.
export function groupTargetLines(lines) {
  const groups = new Map();
  for (const row of lines ?? []) {
    const key = JSON.stringify([row.plain, row.critical]);
    if (!groups.has(key)) groups.set(key, { targets: [], plain: row.plain, critical: row.critical });
    groups.get(key).targets.push(row.target);
  }
  return [...groups.values()];
}

function targetsLabel(targets) {
  return targets.length === 1 ? `Creature ${targets[0]}` : `Creatures ${targets.join(', ')}`;
}

export function guidancePanel(document, view, chosen, picked) {
  const panel = document.createElement('section');
  panel.className = 'decision-guide';
  panel.setAttribute('aria-label', 'Before you confirm');
  if (!['Speed', 'Intent', 'Target'].includes(view.waitingFor)) return panel;
  const line = text => {
    const p = document.createElement('p');
    p.textContent = text;
    panel.append(p);
  };
  if (view.waitingFor === 'Speed') {
    line('Quick acts first, without crits. Standard keeps its crits, shown below.');
    return panel;
  }
  const spell = view.waitingFor === 'Target' ? view.options.target?.spell : chosen;
  const guide = view.guidance?.find(one => one.spell === spell);
  if (!guide) return panel;
  line(spellSummary(guide));
  if (guide.unavailableReason) return panel;
  const heading = document.createElement('strong');
  heading.textContent = 'If cast on the current board';
  panel.append(heading);
  for (const group of groupTargetLines(targetLines(guide, picked))) {
    const critical = group.plain === group.critical ? '' : ` | Critical: ${group.critical}`;
    line(`${targetsLabel(group.targets)} · ${group.plain}${critical}`);
  }
  if (guide.casterEffects?.length) line(`On caster, once: ${guide.casterEffects.map(effectText).join(', ')}`);
  const caveat = document.createElement('small');
  caveat.textContent = 'An estimate before caps and stacking: earlier actions, hidden choices and rolls can change the outcome.';
  panel.append(caveat);
  return panel;
}
