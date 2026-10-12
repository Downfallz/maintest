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
