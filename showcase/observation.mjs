import { EVOLUTION_REGISTRY } from '../table/battle-evolution-art.js';
import { resolveEvolution } from '../table/battle-evolution.js';

/**
 * The annotated specimen: each step of `path` names a mutation by its short id, found among the catalogue's
 * enabled mutations whatever its version, and acquired in order with the same rules as a battle.
 * Returns the mutations owned, in order, and the artwork of the ancestor and of each step.
 */
export function observe(catalogue, path) {
  const packages = new Map(catalogue.nodes.map(t => [t.id, t]));
  const owned = [];
  const specimen = [EVOLUTION_REGISTRY.base()];
  for (const step of path) {
    const candidates = catalogue.nodes.filter(t => t.id.split(':')[1] === step);
    const node = candidates.length === 1 ? candidates[0] : null;
    const reachable = node?.prerequisites.every(p => owned.includes(p)) && (node.anyOf.length === 0 || node.anyOf.some(p => owned.includes(p)));
    if (!reachable) throw new Error(`Unreachable observation step ${step}`);
    owned.push(node.id);
    const visual = resolveEvolution({ acquiredTiers: owned }, packages, { log: { order: () => owned } });
    specimen.push(EVOLUTION_REGISTRY.art(visual.artworkId));
  }
  return { owned, specimen };
}
