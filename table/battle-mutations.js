import { cardsById, cardLines } from './card.js';
import { talentClasses, talentPalette, classColour, familyRoot, passiveOf, tagsOf, requirementLine } from './mat.js';
import { creatureLabel } from './battle-live.js';

// The host's offers are the only source of selectable ids; catalogue ancestry is explanatory, not legality.
export function mutationChoices(view, catalogue, selectedId) {
  const allies = view?.board?.allies ?? [], picks = view?.board?.evolutionChoices ?? [], offer = view?.options?.evolution;
  const cards = cardsById(catalogue), palette = talentPalette(catalogue, cards);
  const roster = allies.map(one => {
    const ids = offer?.creatures?.find(option => option.creature === one.id)?.availableTiers ?? [];
    const picked = picks.find(pick => pick.creature === one.id);
    return { id: one.id, name: creatureLabel(view.board, one.id), ids,
      context: one.isAlive === false ? 'Defeated' : picked ? 'Picked · hidden until reveal' : ids.length ? `${ids.length} available` : 'No mutation offered' };
  });
  const selected = roster.find(one => one.id === selectedId) ?? roster.find(one => one.ids.length) ?? roster[0];
  const creature = allies.find(one => one.id === selected?.id);
  const packages = talentClasses(catalogue, cards, creature, offer, picks);
  const byId = new Map(packages.map(pack => [pack.id, pack]));
  function face(id) {
    const pack = byId.get(id);
    if (!pack) return { id, name: id, family: 'Mutation', colour: '#aabdb1', stage: null, initiative: null,
      tags: [], spells: [], passives: [], requirement: '', missing: true };
    const root = familyRoot(pack, byId);
    return { id, name: pack.name, family: root.name, colour: classColour(root.id, palette), stage: pack.level,
      initiative: Number.isInteger(pack.initiativeBonus) ? pack.initiativeBonus : null, tags: tagsOf(pack),
      spells: (pack.spells ?? []).map(spell => {
        const card = cards.get(spell);
        return { id: spell, name: card?.name ?? spell, cost: Number.isInteger(card?.cost) ? card.cost : null,
          preview: card?.effects?.[0] ?? card?.targeting ?? '', lines: cardLines(card), missing: !card };
      }), passives: passiveOf(pack), requirement: requirementLine(pack, id => byId.get(id)?.name ?? id), missing: false };
  }
  // Already capped by the host at the creatures it can offer a pick to (EvolutionRules.RemainingPicksOf).
  const remaining = Number.isInteger(offer?.remainingPicks) ? offer.remainingPicks : null;
  return { selected: selected?.id ?? null, roster, packages, faces: (selected?.ids ?? []).map(face), remaining };
}

const count = (n, word) => `${n} ${word}${n === 1 ? '' : 's'}`;
// The team's picks of this Mutation phase, spelled out for a first-time player: picks are the team's, each
// creature takes one at most, and when there are fewer picks than creatures that could mutate, the player
// chooses which ones do now; later Mutation phases bring new picks for any creature. `sent` is the pick the
// seat sent that the host has not restated yet: its creature counts as picked and the budget as one less.
export function pickPlan(view, sent = null) {
  const offer = view?.options?.evolution, offered = offer?.remainingPicks;
  if (!Number.isInteger(offered)) return null;
  const allies = view?.board?.allies ?? [], picked = new Set((view?.board?.evolutionChoices ?? []).map(pick => pick.creature));
  const sentCreature = sent?.creature;
  if (Number.isInteger(sentCreature)) picked.add(sentCreature);
  let left = Math.max(0, offered - (sent ? 1 : 0));
  if (sent?.pass) left = 0;
  const spent = allies.filter(one => picked.has(one.id)).length;
  const open = allies.filter(one => one.isAlive !== false && !picked.has(one.id) &&
    (offer?.creatures?.find(option => option.creature === one.id)?.availableTiers?.length ?? 0) > 0).length;
  const budget = `${sent ? 'Pick sent · ' : ''}${count(left, 'team pick')} remaining`;
  let rule = '';
  if (left > 0 && open > left) {
    const which = left === 1 ? 'one mutates' : `${left} mutate`;
    rule = `${open} creatures can mutate, ${count(left, 'pick')} left: choose which ${which} now, one pick each. ` +
      'Later Mutation phases bring new picks for any of them.';
  } else if (left > 1 && open > 1) rule = 'One pick per creature.';
  return { spent, left, open, budget, rule };
}
