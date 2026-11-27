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
    return { id, name: pack.name, portrait: pack.visual?.portrait, artwork: pack.visual?.artwork, family: root.name, colour: classColour(root.id, palette), stage: pack.level,
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
// chooses which ones do now. What later Mutation phases bring is the rulebook's to say: `rules` links its section
// (#1022). `sent` is the pick the
// seat sent that the host has not restated yet: its creature counts as picked and the budget as one less.
// The first line says how often a mutation round comes (`interval`, the rule set's `evolutionInterval`) and the
// picks, as a sentence (`full`) and as a label for a short screen (`short`): docs/art/journeys/windows.md.
/**
 * @param {any} view
 * @param {any} [sent]
 * @param {number | null} [interval]
 */
export function pickPlan(view, sent = null, interval = null) {
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
  // The gap between two mutation rounds is the rule set's and the count the offer's, which the host caps at the
  // creatures that can take a pick. With no gap served the schedule is left out and the count stands alone.
  const gap = Number.isInteger(interval) && interval > 0 ? (interval === 1 ? 'every round' : `every ${interval} rounds`) : '';
  const picks = `${count(left, 'pick')} left`;
  let full = `${picks}.`, short = picks;
  if (sent) { full = left ? `Pick sent · ${picks}.` : 'Pick sent.'; short = left ? `Pick sent · ${picks}` : 'Pick sent'; }
  else if (!spent && left) full = `Your team has ${count(left, 'pick')}${left > 1 ? ', each for a different creature' : ''}.`;
  // Once every pick is sent there is no schedule left to plan with; a pick sent with more to give keeps the short
  // form to what changed.
  if (gap && (!sent || left)) full = `A mutation round comes ${gap}. ${full}`;
  if (gap && !sent) short = `Mutation round ${gap} · ${short}`;
  let rule = '';
  if (left > 0 && open > left) rule = `${open} creatures can mutate: choose which ${left === 1 ? 'one mutates' : `${left} mutate`} now.`;
  return { spent, left, open, full, short, rule, rules: left > 0 ? '/rules#evolution' : null };
}
