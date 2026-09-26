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
  return `${timing}${guide.cost} energy → ${guide.energyAfterCost} after cost · ${chance(critical)} critical`;
}


export function targetLines(guide, picked = []) {
  const candidates = guide?.targets ?? [];
  return candidates.filter(row => picked.length === 0 || picked.includes(row.target)).map(row => ({
    target: row.target,
    plain: row.plain.map(effectText).join(', ') || 'No effect on this target',
    critical: row.critical.map(effectText).join(', ') || 'No effect on this target',
  }));
}

export function guidancePanel(document, view, chosen, picked, cards) {
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
    line('Quick acts before every Standard slot and cannot roll a critical. Standard keeps your critical chance. Initiative orders each band; ties are settled afterwards.');
    for (const guide of view.guidance ?? []) {
      if (guide.unavailableReason) continue;
      line(`${cards.get(guide.spell)?.name ?? guide.spell} · Quick ${chance(guide.quickCriticalChance)} / Standard ${chance(guide.standardCriticalChance)} critical`);
    }
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
  for (const row of targetLines(guide, picked)) {
    line(`Creature ${row.target} · ${row.plain}${row.plain === row.critical ? '' : ` | Critical: ${row.critical}`}`);
  }
  if (guide.casterEffects?.length) line(`On caster, once: ${guide.casterEffects.map(effectText).join(', ')}`);
  const caveat = document.createElement('small');
  caveat.textContent = 'Computed effects before health/energy caps and condition stacking. Earlier actions can change these results. Hidden choices and future rolls are unknown; this is not the round outcome.';
  panel.append(caveat);
  return panel;
}
