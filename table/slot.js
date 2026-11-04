// A spell chosen at the slot (ADR 0110): what the host asks as one question and the page plays as two screens.
//
// Under a rule set that declares no intent the host asks a creature's owner, when its slot comes up, for one
// `Action`: a spell and its targets together, out of `options.action` -- every spell the creature can cast now,
// each with its legal targets on the board as it stands. The page already has a screen for choosing a spell
// out of a creature's castable cards (the Intent question) and one for choosing a spell's targets (the Target
// question), and the action is exactly the first followed by the second. So the page is handed the question as
// those two, by the choice it has made so far: no spell chosen yet reads as an Intent question over the spells
// on offer, a spell chosen reads as a Target question for it. Everything that draws, focuses and checks those
// screens then works unchanged; only committing differs, and `askedAs` says which it is. Nothing is sent until
// the targets are confirmed, so going back to choose another spell costs nothing.

/// <summary>The view as the page draws it: an Action question as its spell screen or its target screen.</summary>
export function slotView(view, chosen = null) {
  const action = view?.waitingFor === 'Action' ? view.options?.action : null;
  if (!action) return view;

  const offered = action.spells ?? [];
  const spell = offered.find(one => one.spell === chosen);
  if (!spell) {
    const intent = { creatures: [{ creature: action.actor, castableSpells: offered.map(one => one.spell), unaffordableSpells: [] }] };
    return { ...view, askedAs: 'Action', waitingFor: 'Intent', options: { ...view.options, intent } };
  }

  return { ...view, askedAs: 'Action', waitingFor: 'Target', options: { ...view.options, target: spell } };
}

/// <summary>
/// Which asking a view is, as an identity the page keeps a choice against: the seat, the host's count of its
/// askings, the kind the host asked -- an Action whichever of its two screens is drawn, so choosing a spell does
/// not read as a new question and wipe the choice -- and the creature.
/// </summary>
export function askedKey(seat, view) {
  return `${seat}/${view?.waitingAsked ?? ''}/${view?.askedAs ?? view?.waitingFor ?? ''}/${view?.waitingCreature ?? ''}`;
}

/// <summary>What the page posts for an action at the slot: the creature, the spell on the target screen, the targets.</summary>
export function actionDecision(view, targets) {
  return { kind: 'Action', creature: view.waitingCreature, spell: view.options?.target?.spell ?? null, targets: [...targets] };
}

// The host's target set and bounds, shared by both clients. Picking one creature twice is never a second
// target. When a revealed spell has too few candidates, the table explicitly permits an empty cast to fizzle.
export function validTargets(legal, targets) {
  return Boolean(legal) && new Set(targets).size === targets.length &&
    targets.length >= legal.minTargets && targets.length <= legal.maxTargets &&
    targets.every(id => legal.candidates.includes(id));
}

export function emptyCastAllowed(legal) {
  return Boolean(legal) && legal.candidates.length < legal.minTargets;
}

// A drawn combat question to its wire command. Choosing the spell screen of an Action is local; only its
// target screen commits. Intent's unaffordable offerings are deliberate plans, offered by the host (ADR 0107).
export function combatDecision(view, chosen, targets = []) {
  if (!view || view.over || view.playedByBot) return null;
  if (view.waitingFor === 'Intent' && view.askedAs !== 'Action') {
    const option = view.options?.intent?.creatures?.find(one => one.creature === view.waitingCreature);
    if (![...(option?.castableSpells ?? []), ...(option?.unaffordableSpells ?? [])].includes(chosen)) return null;
    return { kind: 'Intent', creature: view.waitingCreature, spell: chosen };
  }
  if (view.waitingFor !== 'Target') return null;
  const legal = view.options?.target?.legalTargets;
  if (!validTargets(legal, targets) && !(targets.length === 0 && emptyCastAllowed(legal))) return null;
  return view.askedAs === 'Action' ? actionDecision(view, targets) : { kind: 'Target', targets: [...targets] };
}
