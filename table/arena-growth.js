import { cardsById } from './card.js';
import { talentClasses, familyRoot } from './mat.js';
import { creatureLabel } from './arena-live.js';

// A public reference: never pass evolutionChoices to talentClasses. Pending own picks belong only in
// the private command sheet; both teams' acquired tiers change at PurchasesRevealed on the host.
export function mutationReference(view, catalogue, selectedId) {
  const board = view?.board;
  const roster = [...(board?.allies ?? []).map(one => ({ ...one, enemy: false })),
    ...(board?.enemies ?? []).map(one => ({ ...one, enemy: true }))];
  const creature = roster.find(one => String(one.id) === String(selectedId)) ?? roster[0];
  const offer = !creature?.enemy && view?.waitingFor === 'Evolution' ? view.options?.evolution : null;
  const packages = talentClasses(catalogue, cardsById(catalogue), creature, offer);
  const byId = new Map(packages.map(pack => [pack.id, pack]));
  const families = new Map();
  for (const pack of packages.filter(one => one.status === 'known')) {
    const root = familyRoot(pack, byId), current = families.get(root.id);
    families.set(root.id, { name: root.name, stage: Math.max(current?.stage ?? 0, pack.level) });
  }
  return { available: Boolean(board && !view.waiting), selected: creature?.id ?? null,
    roster: roster.map(one => ({ id: one.id, name: creatureLabel(board, one.id), enemy: one.enemy, alive: one.isAlive !== false })),
    packages, families: [...families.values()],
    next: Number.isInteger(board?.nextEvolutionRound) ? board.nextEvolutionRound : null };
}

// The initial read is a baseline, including on reload/return from a hidden tab. Only a new public reveal
// can announce an unlock; a private submission, catalogue change, or board diff alone cannot do it.
export function createArenaGrowth() {
  let seen = null, known = new Map(), pending = new Map(), presented = new Map(), receipt = null;
  function reset() { seen = null; known.clear(); pending.clear(); presented.clear(); receipt = null; }
  function receive(view, catalogue) {
    if (!view?.board || view.replay || view.waiting) { reset(); return; }
    const entries = (view.feed ?? []).filter(entry => Number.isInteger(entry.sequence));
    const end = Math.max(-1, ...entries.map(entry => entry.sequence), Number.isInteger(view.feedNext) ? view.feedNext - 1 : -1);
    if (seen !== null) {
      const fresh = [...new Map(entries.filter(entry => entry.sequence > seen).map(entry => [entry.sequence, entry])).values()].sort((a, b) => a.sequence - b.sequence);
      for (const entry of fresh) {
        if (entry.event?.kind !== 'PurchasesRevealed') continue;
        const rows = [];
        for (const choice of entry.event.choices ?? []) {
          const pack = catalogue?.packages?.find(one => one.id === choice.tier);
          const creature = [...(view.board.allies ?? []), ...(view.board.enemies ?? [])].find(one => one.id === choice.creature);
          if (!creature) continue;
          const before = known.get(choice.creature) ?? new Set();
          const spells = (pack?.spells ?? []).filter(id => !before.has(id) && creature.knownSpells?.includes(id));
          for (const id of spells) before.add(id);
          known.set(choice.creature, before);
          if (view.board.allies?.some(one => one.id === choice.creature)) {
            const ids = pending.get(choice.creature) ?? new Set();
            spells.forEach(id => ids.add(id)); pending.set(choice.creature, ids);
          }
          rows.push({ creature: choice.creature, name: creatureLabel(view.board, choice.creature),
            enemy: !view.board.allies?.some(one => one.id === choice.creature), mutation: pack?.name ?? choice.tier,
            spells: spells.map(id => catalogue?.cards?.find(card => card.id === id)?.name ?? id) });
        }
        // Catch-up keeps the latest public reveal. Every reveal remains available in History.
        if (rows.length) receipt = { sequence: entry.sequence, round: entry.event.roundId ?? entry.round, rows };
      }
    }
    seen = Math.max(seen ?? -1, end);
    known = new Map([...(view.board.allies ?? []), ...(view.board.enemies ?? [])].map(one => [one.id, new Set(one.knownSpells ?? [])]));
  }
  function highlights(action, visible) {
    for (const [actor, shown] of presented) if (shown.question !== action.question) presented.delete(actor);
    if (!visible || !action.opened || action.target || !['Action', 'Intent'].includes(action.waitingFor)) return [];
    const ids = pending.get(action.actor);
    const shown = presented.get(action.actor) ?? { question: action.question, ids: new Set() };
    for (const id of ids ?? []) if (action.known.includes(id) || action.spells.includes(id)) { shown.ids.add(id); ids.delete(id); }
    if (shown.ids.size) presented.set(action.actor, shown);
    return [...shown.ids];
  }
  return { receive, reset, highlights, receipt: () => receipt, dismiss: () => { receipt = null; } };
}
