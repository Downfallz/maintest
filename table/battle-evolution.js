import { EVOLUTION_REGISTRY, composeCapstones } from './battle-evolution-art.js';

// Which illustration a creature shows, from the mutations it bought and when it bought them. Presentation only: it
// reads what the host says the creature owns and never changes what it owns, can buy or does (ADR 0151).
//
//   Stage 0: nothing bought, the ancestral creature.
//   Stage 1 only: the latest Stage 1 bought.
//   Stage 2, no Stage 3: among the families holding a Stage 2, the one with the most mutations bought (Stage 1 and
//     Stage 2 alike); a tie goes to the family whose latest Stage 2 is the most recent. Its latest Stage 2 shows.
//   Stage 3: the first Stage 3 bought is the creature's identity for good; a later Stage 3 never replaces it. A
//     hybrid of that Stage 3 may show instead, if one was drawn: first with another Stage 2 of its own family (not
//     the one that led to it), then with a Stage 2 of another family, then with a Stage 1 of another family; within
//     each, the latest bought first. A pair nobody drew is skipped; with none, the Stage 3's own image shows.
//   Capstones (Stage 4) never change the image; each adds its effect.
// Which illustration a package draws (`visual.artwork`) and which effect a capstone lends (`visual.effect`) come with
// the catalogue's packages; a package without one draws nothing new, and the page names no package.
const FAMILIES = ['brute', 'predator', 'warped'];

// A purchase order nobody recorded is not guessed from the set: tiers the purchase log does not know (bought before
// the feed this page read began, or with no log at all) count as older than every recorded one, among themselves by
// stage, then family, then id, which a prerequisite never contradicts. `chronology` says how much was recorded.
function purchaseOrder(acquired, known) {
  const recorded = known.filter((tier, index) => acquired.has(tier) && known.indexOf(tier) === index);
  const unrecorded = [...acquired.keys()].filter(tier => !recorded.includes(tier)).sort((a, b) => {
    const one = acquired.get(a), other = acquired.get(b);
    return one.stage - other.stage || FAMILIES.indexOf(one.family) - FAMILIES.indexOf(other.family) || a.localeCompare(b, 'en');
  });
  return { order: [...unrecorded, ...recorded], chronology: !acquired.size || !unrecorded.length ? 'complete' : recorded.length ? 'partial' : 'none' };
}

const latest = (tiers, rank) => [...tiers].sort((a, b) => rank.get(b) - rank.get(a));

/**
 * @param {{ id?: string | number, acquiredTiers?: string[] } | null} creature
 * @param {Map<string, any>} [packages]
 * @param {{ log?: { order: (creature: any) => string[] } } | null} [history]
 * @param {ReturnType<typeof import('./battle-evolution-art.js').createEvolutionRegistry>} [registry]
 */
export function resolveEvolution(creature, packages = new Map(), history = null, registry = EVOLUTION_REGISTRY) {
  const acquired = new Map();
  for (const id of new Set(Array.isArray(creature?.acquiredTiers) ? creature.acquiredTiers : [])) {
    const pack = packages?.get(id);
    if (!FAMILIES.includes(pack?.visual?.family) || !Number.isInteger(pack.level) || pack.level < 1) continue;
    acquired.set(id, { stage: pack.level, family: pack.visual.family, artwork: pack.visual.artwork ?? null, effect: pack.visual.effect ?? null,
      prerequisites: [...(pack.prerequisites ?? []), ...(pack.anyOf ?? [])] });
  }
  // What the creature owns is the frame's own word; the log only orders it. A purchase's place in that order never
  // changes, so the whole log orders any frame, a replayed one or one an interlude borrowed from later in its round.
  const known = history?.log ? history.log.order(creature?.id) : [];
  const { order, chronology } = purchaseOrder(acquired, known);
  const rank = new Map(order.map((tier, index) => [tier, index]));
  const at = stage => order.filter(tier => acquired.get(tier).stage === stage);
  const capstoneEffectIds = composeCapstones(order.filter(tier => acquired.get(tier).stage >= 4).map(tier => acquired.get(tier).effect).filter(Boolean));
  const artOf = tier => acquired.get(tier)?.artwork ?? null;
  const result = (stage, dominantTierId, artTier, hybridTierId = null, art = null) => {
    const asset = art ?? (artTier ? registry.art(artOf(artTier)) : registry.base());
    return { stage, family: dominantTierId ? acquired.get(dominantTierId).family : null, dominantTierId, hybridTierId,
      artworkId: asset?.id ?? null, artworkFile: asset?.file ?? null, capstoneEffectIds, chronology };
  };

  const thirds = at(3);
  if (thirds.length) {
    const dominant = thirds[0], family = acquired.get(dominant).family;
    const leading = new Set(acquired.get(dominant).prerequisites.filter(tier => acquired.get(tier)?.stage === 2 && acquired.get(tier).family === family)
      .sort((a, b) => rank.get(a) - rank.get(b)).slice(0, 1));
    const seconds = at(2), firsts = at(1);
    const candidates = [
      ...latest(seconds.filter(tier => acquired.get(tier).family === family && !leading.has(tier)), rank),
      ...latest(seconds.filter(tier => acquired.get(tier).family !== family), rank),
      ...latest(firsts.filter(tier => acquired.get(tier).family !== family), rank),
    ];
    for (const tier of candidates) {
      const art = registry.hybrid(artOf(dominant), artOf(tier));
      if (art) return result(3, dominant, null, tier, art);
    }
    return result(3, dominant, dominant);
  }

  const seconds = at(2);
  if (seconds.length) {
    const count = family => order.filter(tier => acquired.get(tier).family === family && acquired.get(tier).stage <= 2).length;
    const lastSecond = family => Math.max(...seconds.filter(tier => acquired.get(tier).family === family).map(tier => rank.get(tier)));
    const families = [...new Set(seconds.map(tier => acquired.get(tier).family))]
      .sort((a, b) => count(b) - count(a) || lastSecond(b) - lastSecond(a));
    const shown = latest(seconds.filter(tier => acquired.get(tier).family === families[0]), rank)[0];
    return { ...result(2, null, shown), family: families[0] };
  }

  const firsts = at(1);
  if (firsts.length) {
    const shown = firsts.at(-1);
    return { ...result(1, null, shown), family: acquired.get(shown).family };
  }
  return result(0, null, null);
}
