// What has happened, one line an entry.
//
// Every event is its kind and nothing more: the kinds are the engine's own words, off the wire, and the page
// has never heard of any of them. One event is the exception, and it is the exception on purpose. A resolution
// is the only entry whose fields a player has to read -- above all whether the cast crit, which stage 4 owes
// the screen (app-roadmap.md, "The critical, on screen"): the boards' own totals cannot tell, because Defense,
// caps and overkill all move the number a critical produced. So a resolution is formatted, and everything else
// is named.

export function feedLine(entry, cards) {
  const head = `${entry?.round ?? '—'} · ${entry?.subPhase ?? ''} · ${entry?.event?.kind ?? ''}`;
  const detail = entry?.event?.kind === 'CombatActionResolved' ? resolutionText(entry.event, cards) : '';
  return detail === '' ? head : `${head} — ${detail}`;
}

// A resolution, in the order a player reads one: who cast what, whether it crit, what it did, and what it
// failed to reach. The applied outcomes are what the board actually took, which can be less than what the
// rules computed -- that is the pair the engine keeps apart, and this prints the one that happened.
export function resolutionText(event, cards) {
  const resolution = event?.resolution ?? {};
  const action = resolution.action ?? {};
  const spell = cards?.get?.(action.spell)?.name ?? action.spell ?? '';

  const parts = [`${action.actor ?? ''}: ${spell}`];

  // Said both ways, and not only when it landed. Stage 4 owes the line "whether it landed" (app-roadmap.md),
  // and silence does not say that: a player who sees nothing cannot tell a roll that missed from a line that
  // forgot to mention it, and the card they cast advertised a threshold they were watching for. A fizzle is
  // the one resolution that never rolled, so it is the one that says nothing about the die.
  if (resolution.fizzled === true) {
    parts.push(`fizzled: ${errorText(resolution.fizzleReason)}`);
  } else {
    parts.push(resolution.isCritical === true ? 'critical' : 'no critical');
  }

  const outcomes = (event?.appliedOutcomes ?? []).map(outcomeText).filter(Boolean);
  if (outcomes.length > 0) parts.push(outcomes.join(', '));

  const dropped = (resolution.droppedTargets ?? []).map(droppedText).filter(Boolean);
  if (dropped.length > 0) parts.push(`dropped ${dropped.join(', ')}`);

  return parts.join(' · ');
}

// One consequence on one target. The kind is the engine's word with its trailing `Outcome` taken off, because
// that suffix is a C# class name and not something a player is owed. The word itself is never this page's.
export function outcomeText(outcome) {
  const kind = typeof outcome?.kind === 'string' ? outcome.kind.replace(/Outcome$/, '') : '';
  if (kind === '') return '';
  const amount = Number.isInteger(outcome?.amount) ? ` ${outcome.amount}` : '';
  const crit = outcome?.critical === true ? '!' : '';
  return `${kind}${amount}${crit} on ${outcome?.target ?? '?'}`;
}

function droppedText(failure) {
  return failure?.target === null || failure?.target === undefined
    ? errorText(failure?.error)
    : `${failure.target} (${errorText(failure?.error)})`;
}

function errorText(error) {
  return error?.message ?? error?.code ?? 'no reason given';
}

// The sequence to ask the host for next: one past the highest entry this page holds, or 0 when it holds none.
// The host's `since` is inclusive, so asking for the highest again would fetch a line already on the screen.
// Trace sequence numbers have gaps by design -- the entries this seat may not see are filtered out and their
// numbers go with them -- so this is the highest seen and never a count.
export function nextSince(kept) {
  let highest = -1;
  for (const entry of kept ?? []) {
    if (Number.isInteger(entry?.sequence) && entry.sequence > highest) highest = entry.sequence;
  }

  return highest + 1;
}

// What the page holds after a poll: what it held, plus what arrived, bounded to the last `limit`. Bounded
// because the log shows twelve lines and a match is thousands of events; an entry already held is not added
// twice, so a poll that re-sends one -- a request that crossed a reload, say -- cannot double a line.
export function accumulate(kept, arriving, limit) {
  const seen = new Set((kept ?? []).map(entry => entry?.sequence));
  const fresh = (arriving ?? []).filter(entry => !seen.has(entry?.sequence));
  const all = [...(kept ?? []), ...fresh];
  return Number.isInteger(limit) && limit > 0 && all.length > limit ? all.slice(all.length - limit) : all;
}
