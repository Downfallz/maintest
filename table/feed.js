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

// What the page holds after a poll: what it held, plus what arrived, bounded to the last `limit`. Bounded
// because the log shows twelve lines and a match is thousands of events; an entry already held is not added
// twice, so a poll that re-sends one -- a request that crossed a reload, say -- cannot double a line.
export function accumulate(kept, arriving, limit) {
  const seen = new Set((kept ?? []).map(entry => entry?.sequence));
  const fresh = (arriving ?? []).filter(entry => !seen.has(entry?.sequence));
  const all = [...(kept ?? []), ...fresh];
  return Number.isInteger(limit) && limit > 0 && all.length > limit ? all.slice(all.length - limit) : all;
}

// Kept separately from the short activity log, per seat, before that log is trimmed. Only public combat
// events enter a recap. A first poll can contain a whole match; retain just the current and previous round.
export function retainRoundEvents(kept, arriving, currentRound) {
  const relevant = entry => entry?.event?.kind === 'CombatActionResolved' || entry?.event?.kind === 'RoundEnded';
  const events = accumulate(kept, (arriving ?? []).filter(relevant));
  const latest = Math.max(Number.isInteger(currentRound) ? currentRound : 0, ...events.map(eventRound));
  return events.filter(entry => eventRound(entry) >= latest - 1 && eventRound(entry) <= latest)
    .sort((left, right) => left.sequence - right.sequence);
}

// Trace snapshots are taken after a command. Finalizing a round also starts the next, so a RoundEnded
// entry can carry the next snapshot's round number. The event's own roundId is the authoritative number.
function eventRound(entry) {
  return entry?.event?.roundId ?? entry?.round ?? 0;
}

export function roundRecap(entries, board, cards) {
  const completed = (entries ?? []).filter(entry => entry?.event?.kind === 'RoundEnded').map(eventRound);
  if (completed.length === 0) return null;
  const round = Math.max(...completed);
  const actions = (entries ?? [])
    .filter(entry => eventRound(entry) === round && entry?.event?.kind === 'CombatActionResolved')
    .map(entry => recapAction(entry, board, cards));
  return { round, actions };
}

function recapCreature(id, board) {
  const ally = (board?.allies ?? []).find(creature => creature.id === id);
  const enemy = (board?.enemies ?? []).find(creature => creature.id === id);
  const creature = ally ?? enemy;
  return {
    label: `Creature ${id ?? '?'}`,
    side: ally ? 'ally' : enemy ? 'enemy' : 'neutral',
  };
}

function recapAction(entry, board, cards) {
  const event = entry.event;
  const resolution = event.resolution ?? {};
  const action = resolution.action ?? {};
  return {
    sequence: entry.sequence,
    actor: recapCreature(action.actor, board),
    spell: cards?.get?.(action.spell)?.name ?? action.spell ?? 'Unknown spell',
    targets: (action.targets ?? []).map(id => recapCreature(id, board)),
    status: resolution.fizzled ? 'Fizzled' : resolution.isCritical ? 'Critical' : 'Resolved',
    reason: resolution.fizzled ? errorText(resolution.fizzleReason) : '',
    effects: (event.appliedOutcomes ?? []).map(outcome => ({
      target: recapCreature(outcome.target, board),
      text: recapEffectText(outcome),
      tone: outcomeTone(outcome.kind),
    })),
    dropped: (resolution.droppedTargets ?? []).map(failure =>
      `${failure.target == null ? '' : `${recapCreature(failure.target, board).label}: `}${errorText(failure.error)}`),
  };
}

function recapEffectText(outcome) {
  const effect = outcome?.effect;
  const kind = effect?.kind ?? (outcome?.kind ?? '').replace(/Outcome$/, '');
  const amount = effect?.amount ?? effect?.amountPerRound ?? outcome?.amount;
  const duration = effect?.duration;
  const lasting = duration?.isPermanent === true ? 'permanent'
    : Number.isInteger(duration?.rounds) ? `${duration.rounds} ${duration.rounds === 1 ? 'round' : 'rounds'}` : '';
  const label = (kind || 'Effect').replace(/([a-z])([A-Z])/g, '$1 $2');
  const perRound = Number.isInteger(effect?.amountPerRound) && !Number.isInteger(effect?.amount) ? ' / round' : '';
  return [
    `${label}${Number.isInteger(amount) ? ` ${amount}${perRound}` : ''}${outcome?.critical ? '!' : ''}`,
    lasting,
    outcome?.onCaster ? 'caster' : '',
  ].filter(Boolean).join(' · ');
}

// These are visual categories of wire outcome types, never predictions from a spell's rules.
function outcomeTone(kind) {
  switch (kind) {
    case 'DamageOutcome': return 'harm';
    case 'HealOutcome': return 'recovery';
    case 'EnergyOutcome':
    case 'EnergyDrainOutcome': return 'energy';
    case 'ConditionOutcome': return 'condition';
    default: return 'neutral';
  }
}
