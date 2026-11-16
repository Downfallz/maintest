// When each creature bought each of its mutations, as the host recorded it. A board's `acquiredTiers` is a set
// (the domain keeps a HashSet), so its order says nothing about purchase order. The authoritative record is the
// public PurchasesRevealed event: one per Evolution sub-phase, its picks in the order they were made (ADR 0089),
// and every feed entry carries its sequence. This log keeps those picks as the feed brings them. What a creature
// owns is the frame's own word; the log only orders it, and a purchase's place in that order never changes.
export function purchaseLog() {
  const picks = [];
  const seen = new Set();
  return {
    // Every view's feed, as it arrives; an entry read twice (a reload, an overlapping poll) counts once.
    receive(feed) {
      for (const entry of feed ?? []) {
        if (entry?.event?.kind !== 'PurchasesRevealed' || !Number.isInteger(entry.sequence) || seen.has(entry.sequence)) continue;
        seen.add(entry.sequence);
        (entry.event.choices ?? []).forEach((choice, index) => {
          if (choice?.creature == null || typeof choice.tier !== 'string') return;
          picks.push({ sequence: entry.sequence, index, creature: String(choice.creature), tier: choice.tier });
        });
      }
      picks.sort((a, b) => a.sequence - b.sequence || a.index - b.index);
    },
    // The creature's purchases, oldest first, as known through `through` (a feed sequence; the present by default).
    order(creature, through = Infinity) {
      return picks.filter(one => one.creature === String(creature) && one.sequence <= through).map(one => one.tier);
    },
    reset() { picks.length = 0; seen.clear(); },
    size: () => picks.length,
  };
}
