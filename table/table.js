import { httpTransport } from './transport.js';
import { activeSeat, isAsked, needsPass } from './seats.js';
import { forget, heldSeats } from './session.js';
import { cardCost, cardHead, cardLines, cardTitle, loadCatalogue } from './card.js';
import { badges, chipSource, chipText, conditionDock, healthShare, healthText, revealedText, statPairs, targetedBy } from './board.js';
import { backText, faceDown, handRows } from './hand.js';
import { accumulate, feedLine } from './feed.js';
import { bands, cursorOf, side, withCursor } from './timeline.js';
import { drawn, matBands } from './mat.js';
import { NOTHING_TO_RECORD, TAPPED, commentIsOpen, commentNote, noted, notesAreKept, tappedNote } from './notes.js';

// The page renders what the host serves and submits what a player taps. It holds no rule: which spells are
// castable, which targets are legal and how many, whose turn it is -- all of that arrives in `options`, built
// by the engine's own gates. Nothing here decides anything, and nothing here knows a spell by name.
const storage = kept();
const held = heldSeats(globalThis.location?.search ?? '', storage);
const element = id => document.getElementById(id);

// How many feed entries the page keeps. The log draws the last twelve; a few times that leaves room to scroll
// back through the round without holding a whole match in memory on a phone.
const FeedKept = 60;

if (held.length === 0) {
  element('phase').textContent = 'Type the code the host printed, or open the link it printed.';
} else {
  // The token is kept, so the address bar does not have to be. What is left is a page a reload brings back.
  tidy();
  start(held.map(({ seat, token }) => ({ seat, transport: httpTransport(seat, token) })));
}

function kept() {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    // A browser that refuses storage refuses reading the property too.
    return null;
  }
}

function tidy() {
  if (globalThis.location?.search && globalThis.history?.replaceState) {
    globalThis.history.replaceState(null, '', globalThis.location.pathname);
  }
}

function start(seats) {
  // `holder` is the seat the person now holding the device said they are, which is the only thing that lets
  // the board be shown at all. `shown` is the seat on screen, so picked targets never survive a handover.
  // `cards` is the catalogue, fetched once: it cannot change while a host runs.
  const state = { seats, holder: null, shown: null, displayed: null, announced: null, asked: null, sending: false, picked: [], chosen: null, cards: new Map(), catalogue: null, tab: 'board', feeds: new Map() };
  load(state);
  element('pass-ready').addEventListener('click', () => {
    state.holder = element('pass-ready').dataset.seat ?? state.holder;
    refresh(state);
  });

  // Two tabs, one screen. The mat is a tab because it is touched once a round and the board is touched all
  // the time; switching draws from what the last poll already fetched, so it never waits.
  for (const [tab, name] of [['tab-board', 'board'], ['tab-mat', 'mat']]) {
    element(tab).addEventListener('click', () => {
      state.tab = name;
      element('tab-board').classList.toggle('on', name === 'board');
      element('tab-mat').classList.toggle('on', name === 'mat');
      element('board').hidden = name !== 'board';
      element('mat').hidden = name !== 'mat';
    });
  }

  // The two one-tap notes, built once. They read the state at the moment they are tapped, so the note lands
  // against whichever seat is on screen then rather than whichever was when the page loaded.
  element('note-buttons').replaceChildren(...TAPPED.map(({ kind, label }) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.addEventListener('click', () => note(state, tappedNote(kind)));
    return button;
  }));

  element('comment-save').addEventListener('click', async () => {
    const box = element('comment-text');
    if (await note(state, commentNote(box.value))) {
      box.value = '';
    }
  });

  refresh(state);
  setInterval(() => refresh(state), 700);
}

// Tells the host this seat's question is now in front of somebody. It is the ordinary seat poll with the flag
// set, and its answer is dropped: the board on screen is the one this render already has, and the feed cursor
// is deliberately not advanced, so nothing this call fetches is lost -- the next poll asks for it again.
function announce(state, current) {
  const since = state.feeds.get(current.seat)?.next ?? 0;

  // Kept, because a decision must not overtake it. The controls stay live -- disabling them for a round trip
  // would make every handover feel broken to protect a number -- and `submit` waits on this instead, so a tap
  // inside the window is delayed by the request it would otherwise have raced rather than being refused. If
  // it were raced and lost, the host would have no moment for this question and would record the duration as
  // unknown, which is honest but is one measurement gone.
  state.announced = current.transport.seat(since, true).catch(() => {
    // A page that cannot reach its host has a louder problem than a clock, and the next poll reports it.
  });
}

// A note is the one thing in a session nothing else can reconstruct, so a refused one says so on screen
// instead of disappearing. It does not go through `submit`: a note is not a decision, it cannot be late, and
// nothing about the board changes because one was written.
async function note(state, body) {
  const line = element('noted');
  const current = state.seats.find(seat => seat.seat === state.shown);
  if (!body || !current) {
    line.textContent = NOTHING_TO_RECORD;
    line.hidden = false;
    return false;
  }

  const answer = await current.transport.note(body);
  line.textContent = answer.ok ? noted(body.kind) : (answer.body?.message ?? `The host answered ${answer.status}.`);
  line.hidden = false;
  return answer.ok;
}

// The catalogue the match is playing, through any seat this page holds: it is the same for both, and it is
// what every card on this screen is drawn from. A page that does not get it prints spell ids and still plays.
async function load(state) {
  state.catalogue = await loadCatalogue(state.seats);
  state.cards = new Map((state.catalogue?.cards ?? []).map(card => [card.id, card]));
  element('rules').textContent = ruleLine(state.catalogue);
  renderShape(state.catalogue?.round);
}

// The round's shape, as the printed board's collapsible strip: every step in the order it is played, and the
// orderings inside them that a player gets wrong. The host serves both -- a screen that listed the steps itself
// would be a screen holding a rule, and a step added to the round would be one it forgot.
function renderShape(round) {
  const steps = round?.subPhases ?? [];
  const orderings = round?.orderings ?? [];
  element('shape').hidden = steps.length === 0 && orderings.length === 0;
  element('sub-phases').replaceChildren(...steps.map(step => {
    const one = document.createElement('li');
    one.textContent = step;
    return one;
  }));

  element('orderings').replaceChildren(...orderings.map(ordering => {
    const one = document.createElement('li');
    one.textContent = ordering;
    return one;
  }));
}

// The one line a print sheet carries, so checking that a deck and a screen are the same game is one glance
// (ADR 0054). Every number in it is the host's.
function ruleLine(catalogue) {
  const rules = catalogue?.rules;
  if (!rules) return '';
  return `${rules.teamSize} creatures · ${rules.energyPerRound} energy · ${rules.evolutionPicksPerRound} picks · ${rules.roundCap} rounds · x${rules.criticalMultiplier} crit · ${String(catalogue.contentHash ?? '').slice(0, 6)}`;
}

async function refresh(state) {
  if (state.sending) return;

  // Every seat this page holds, every poll: the host answers one seat per payload, and which one is being
  // asked is exactly what the page cannot know without asking.
  const views = [];
  const refused = [];
  for (const seat of state.seats) {
    // Only the entries this page has not seen yet. The feed is the whole match's history and it only grows, so
    // a poll every 700 ms that asked for all of it would serialize and download the match again each time --
    // and over a half-hour session that is quadratic in the number of events, for twelve lines on screen.
    // `displayed` is the seat whose screen was actually up at the end of the last render: not merely the seat
    // the host is asking, because until the person being asked taps through the pass screen nobody is reading
    // it. The host times a decision from the poll that says so (DecisionClock), so a background poll of the
    // other seat must not claim to be one.
    const answer = await seat.transport.seat(state.feeds.get(seat.seat)?.next ?? 0, state.displayed === seat.seat);

    // A token this host does not know is a seat from another table -- an earlier session, or the browser of
    // somebody who played here yesterday. Only that seat goes: a code typed for *this* table may be on the
    // same page, and it has already been taken out of the address bar, so forgetting it too would mean
    // reading it off the host's screen again.
    if (answer.status === 403) {
      refused.push(seat.seat);
      continue;
    }

    if (!answer.ok) {
      element('phase').textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      return;
    }

    // The cursor is the host's `feedNext`, not the highest entry on the screen: the two differ whenever the
    // end of the trace is the other seat's own decisions, which this seat is never shown.
    const held = state.feeds.get(seat.seat);
    const kept = accumulate(held?.entries, answer.body?.feed, FeedKept);
    const next = Number.isInteger(answer.body?.feedNext) ? answer.body.feedNext : held?.next ?? 0;
    state.feeds.set(seat.seat, { entries: kept, next });
    views.push({ ...seat, view: { ...answer.body, feed: kept } });
  }

  // Dropped after the round of polls rather than inside it, so nothing this loop reads changes while it runs.
  for (const seat of refused) {
    forget(storage, seat);
    state.seats = state.seats.filter(held => held.seat !== seat);
    state.feeds.delete(seat);
  }

  // The catalogue may have been asked for through a seat this table has just refused. Now that only accepted
  // seats are left, it is worth asking again -- once, and only while there is nothing to draw cards from.
  if (refused.length > 0 && state.cards.size === 0 && state.seats.length > 0) {
    await load(state);
  }

  if (views.length === 0) {
    element('phase').textContent = 'This browser holds no seat at this table. Type the code the host printed.';
    return;
  }

  render(state, views);
}

const nameOf = seat => (seat === 'player1' ? 'Player 1' : 'Player 2');

function render(state, views) {
  const current = activeSeat(views, state.holder);
  const view = current.view;

  // What the seat is being asked, as an identity. A choice belongs to one question and to no other: a decision
  // refused as stale (409) leaves the choice standing, and the next question of the same seat is very often a
  // different creature that knows the same spell -- which would arrive with a card already selected and one tap
  // from being committed, through the confirmation that exists to stop exactly that.
  const asked = `${current.seat}/${view.waitingFor ?? ''}/${view.waitingCreature ?? ''}`;
  if (asked !== state.asked) {
    state.shown = current.seat;
    state.asked = asked;
    state.picked = [];
    state.chosen = null;
  }

  // Until the player being asked says they are the one holding the device, the board stays behind the pass
  // screen (seats.js).
  const fence = needsPass(current, state.holder);

  // What the host is told is on screen. Nothing, while the pass screen is up: the board is behind it and the
  // person being asked has not picked the device up yet. The moment it comes down, the host is told straight
  // away rather than on the next poll — the poll that fetched this board was sent while the fence was still
  // up, so waiting would start the decision's clock up to a polling interval late, and a decision faster than
  // that would be recorded as having taken no time at all.
  const showing = fence ? null : current.seat;
  if (showing !== null && showing !== state.displayed) {
    announce(state, current);
  }

  state.displayed = showing;
  element('seat').textContent = nameOf(current.seat);
  element('pass-seat').textContent = nameOf(current.seat);
  element('pass-seat-again').textContent = nameOf(current.seat);
  element('pass-ready').dataset.seat = current.seat;
  element('pass').hidden = !fence;
  element('table').hidden = fence;
  if (fence) return;

  element('phase').textContent = view.over
    ? 'The match is over.'
    : `Round ${view.board.roundNumber ?? '—'} of ${state.catalogue?.rules?.roundCap ?? '—'} · ${view.board.subPhase ?? '—'}`;
  renderTimeline(view.board);
  renderBoard(state, view);
  renderMat(state, view.board);
  renderFeed(state, view.feed);
  renderDecision(state, current);
  renderNotes(view);
}

// The notes, and the comment box only once the match is decided: asking for prose while somebody is deciding
// is asking them to stop playing. A table that keeps nothing offers neither.
function renderNotes(view) {
  const kept = notesAreKept(view);
  element('notes').hidden = !kept;
  element('comment').hidden = !kept || !commentIsOpen(view);
}

// The initiative track: the engine's order, banded by speed, scrolling sideways at 360 px. The strip never
// sorts -- ties and all, this is the order the round is played in (timeline.js).
function renderTimeline(board) {
  const cursor = withCursor(board.timeline, cursorOf(board));
  const strip = bands(cursor).map(band => {
    const box = document.createElement('div');
    box.className = 'band';

    const label = document.createElement('div');
    label.className = 'band-name';
    label.textContent = band.speed;
    box.append(label);

    const slots = document.createElement('div');
    slots.className = 'slots';
    for (const slot of band.slots) {
      const one = document.createElement('div');
      one.className = `slot ${side(slot, board.slot)}${slot.isNow ? ' now' : ''}`;
      one.textContent = `${slot.creature} · ${slot.initiative}`;
      slots.append(one);
    }

    box.append(slots);
    return box;
  });

  element('timeline').replaceChildren(...strip);
}

function renderBoard(state, view) {
  const board = view.board;
  // What a row may do and say this poll. Targeting is a tap on a legal creature (playtest-app.md §3.2), so
  // the candidates the options offer are the rows that are tappable, and no others.
  const marks = {
    picked: state.picked,
    board,
    candidates: isAsked(view) && view.waitingFor === 'Target' ? view.options.target?.legalTargets?.candidates ?? [] : [],
    onPick: candidate => pick(state, view, candidate),
  };
  element('enemies').replaceChildren(...(board.enemies ?? []).map(creature => line(state, creature, 'enemy', marks)));
  element('allies').replaceChildren(...(board.allies ?? []).map(creature => line(state, creature, 'ally', marks)));
  revealed(state, board.revealedActions);
  element('backs').replaceChildren(backs(state, board, view.opponentIntents));
  element('own-hand').replaceChildren(hand(state, view));
}

// The hand: every spell this seat's creatures know, drawn as the whole card, with the ones it could cast right
// now marked (hand.js). It is drawn whatever the sub-phase, and it draws the full face and not a label, because
// for a spell that is not castable this is the card's only appearance on the screen -- the decision sheet will
// never offer it -- and "what does this do and why can I not cast it" is a question a player answers by reading
// the card. Dimmed, never hidden. What is castable comes from the options and nothing else.
//
// It is also the picker. "Intent is the hand, filtered by the server" (playtest-app.md §3.2), so during an
// Intent question the asked creature's castable cards are the tap surface and the sheet holds only the
// confirmation -- the same shape targeting has. A second copy of each card in the sheet would be two of one
// card on one screen, and the enabled-looking one in the hand doing nothing.
function hand(state, view) {
  const asked = isAsked(view) && view.waitingFor === 'Intent' ? view.waitingCreature : null;
  const rows = handRows(view.board.allies, view.options?.intent, view.board.intents);
  const box = document.createElement('div');
  box.className = 'hand-cards';
  box.hidden = rows.every(row => row.spells.length === 0);
  box.append(...rows.map(row => handRow(state, row, asked)));
  return box;
}

// One creature's row: who it is, whether it has declared, and its cards.
function handRow(state, row, asked) {
  const one = document.createElement('div');
  one.className = 'hand-row';

  const who = document.createElement('div');
  who.className = 'hand-who';
  who.textContent = `${row.creature}${row.declared ? ' · declared' : ''}`;

  const held = document.createElement('div');
  held.className = 'held-cards';
  // Tappable only on the creature being asked: every row says what its creature could cast, which is what
  // makes the hand readable, but only one creature is being asked at a time.
  held.append(...row.spells.map(spell => heldCard(state, spell, row.creature === asked && spell.castable)));

  one.append(who, held);
  return one;
}

// One card in the hand: its whole face, dimmed when the creature cannot cast it, and a tap surface when it is
// the one being asked for.
function heldCard(state, spell, offered) {
  const face = document.createElement('div');
  face.className = ['card held', spell.castable ? 'castable' : '', offered ? 'offered' : '', offered && spell.spell === state.chosen ? 'chosen' : '']
    .filter(Boolean)
    .join(' ');

  const parts = cardParts(state, spell.spell, '');
  if (parts === null) {
    face.textContent = spell.spell;
  } else {
    face.append(...parts);
  }

  if (offered) {
    face.tabIndex = 0;
    face.setAttribute('role', 'button');
    face.addEventListener('click', () => {
      state.chosen = spell.spell;
      refresh(state);
    });
  }

  return face;
}

// The actions that are already face up, in the order they were revealed (board.js). Nothing is drawn while
// none is, so the board of a planning phase is the board it was.
function revealed(state, actions) {
  const box = element('revealed');
  box.hidden = (actions ?? []).length === 0;
  box.replaceChildren(...(actions ?? []).map(action => {
    const one = document.createElement('div');
    one.className = 'revealed-action';
    one.textContent = revealedText(action, state.cards);
    return one;
  }));
}

// What is face down. This seat's own backs are its own to read; the other side's is a count and carries no
// data at all -- that is the whole of the hidden information, and it is the server that keeps it so.
function backs(state, board, opponentIntents) {
  const box = document.createElement('div');
  box.className = 'hand';

  const mine = document.createElement('div');
  mine.className = 'backs ally';
  mine.textContent = faceDown(board)
    .map(intent => backText(intent, state.cards))
    .join(' · ') || 'nothing declared';

  const theirs = document.createElement('div');
  theirs.className = 'backs enemy';
  theirs.textContent = `${Number(opponentIntents) || 0} face down`;

  box.append(mine, theirs);
  return box;
}

// A creature board: numbers and a bar, never a rail, and the dock under it (board.js).
function line(state, creature, which, marks) {
  const picked = (marks?.picked ?? []).includes(creature.id);
  const legal = (marks?.candidates ?? []).includes(creature.id);
  const casters = targetedBy(creature.id, marks?.board);
  const box = document.createElement('div');
  box.className = `creature ${which}${creature.isAlive === false ? ' dead' : ''}${picked ? ' picked' : ''}${legal ? ' legal' : ''}`;
  if (legal) {
    box.tabIndex = 0;
    box.setAttribute('role', 'button');
    box.addEventListener('click', () => marks.onPick(creature.id));
  }

  const who = document.createElement('div');
  who.className = 'who';
  who.textContent = `${which === 'ally' ? 'Yours' : 'Theirs'} · ${creature.name ?? ''} ${creature.id}`;

  const health = document.createElement('div');
  health.className = 'health';
  const bar = document.createElement('span');
  bar.className = 'bar';
  bar.style.width = `${Math.round(healthShare(creature) * 100)}%`;
  const number = document.createElement('span');
  number.className = 'number';
  number.textContent = healthText(creature);
  health.append(bar, number);

  const stats = document.createElement('div');
  stats.className = 'stats';
  stats.textContent = statPairs(creature).map(([name, value]) => `${name} ${value}`).join(' · ');

  for (const badge of badges(creature, marks?.board?.timeline)) {
    const one = document.createElement('span');
    one.className = 'badge';
    one.textContent = badge;
    stats.append(one);
  }

  box.append(who, health, stats, dock(state, creature.conditions));

  // The markers, on the row rather than only in the sheet: a target is chosen against this creature's health,
  // its defense and what is already on it, so the choice has to be visible where those numbers are
  // (playtest-app.md §3.1). `picked` is what this seat has tapped and not yet sent; `Targeted by` is every
  // caster already pointing at it, which is what the printed board's row of boxes holds.
  if (picked || casters.length > 0) {
    const markers = document.createElement('div');
    markers.className = 'markers';
    if (picked) {
      const mine = document.createElement('span');
      mine.className = 'marker picked';
      mine.textContent = 'picked';
      markers.append(mine);
    }

    for (const caster of casters) {
      const marker = document.createElement('span');
      marker.className = 'marker';
      marker.textContent = `${caster}`;
      markers.append(marker);
    }

    box.append(markers);
  }

  return box;
}

// The condition dock: the printed board's lanes, `new` first and permanent last, each chip carrying its kind,
// its number and the cast that put it there (board.js).
function dock(state, conditions) {
  const box = document.createElement('div');
  box.className = 'dock';
  for (const group of conditionDock(conditions)) {
    const one = document.createElement('div');
    one.className = 'dock-group';

    const label = document.createElement('span');
    label.className = 'dock-rounds';
    label.textContent = group.lane === null ? 'permanent' : `${group.lane}`;
    one.append(label);

    for (const condition of group.conditions) {
      const chip = document.createElement('span');
      chip.className = 'chip';
      chip.textContent = chipText(condition);
      const source = chipSource(condition, state.cards);
      if (source !== '') {
        const from = document.createElement('span');
        from.className = 'chip-source';
        from.textContent = source;
        chip.append(from);
      }

      one.append(chip);
    }

    box.append(one);
  }

  return box;
}

// The talent mat, as a tab: every band, every spell, a pip per creature that knows it (mat.js).
function renderMat(state, board) {
  const rows = drawn(matBands(state.catalogue, board.allies, state.cards)).map(band => {
    const box = document.createElement('div');
    box.className = 'band-row';

    const name = document.createElement('div');
    name.className = 'band-name';
    name.textContent = `${band.name} · ${band.depth}`;
    box.append(name);

    for (const spell of band.spells) {
      const row = document.createElement('div');
      row.className = 'mat-spell';
      const label = document.createElement('span');
      label.textContent = spell.name;
      row.append(label);
      if (spell.requires !== '') {
        const gate = document.createElement('span');
        gate.className = 'gate';
        gate.textContent = spell.requires;
        row.append(gate);
      }

      for (const pip of spell.pips) {
        const dot = document.createElement('span');
        dot.className = `pip${pip.known ? ' known' : ''}`;
        dot.textContent = `${pip.creature}`;
        row.append(dot);
      }

      box.append(row);
    }

    return box;
  });

  element('mat').replaceChildren(...rows);
}

// What has happened, as this seat may be told it. The kinds are the engine's own words, off the wire, and a
// resolution carries its fields -- the critical above all (feed.js).
function renderFeed(state, feed) {
  const lines = (feed ?? []).slice(-12).reverse().map(entry => {
    const item = document.createElement('li');
    item.textContent = feedLine(entry, state.cards);
    return item;
  });

  element('feed').replaceChildren(...lines);
}

function renderDecision(state, current) {
  const view = current.view;
  const asking = element('asking');
  const choices = element('choices');
  element('problem').hidden = true;

  if (view.over) {
    asking.textContent = 'The match is over.';
    choices.replaceChildren();
    return;
  }

  if (view.playedByBot) {
    asking.textContent = 'A bot plays this seat.';
    choices.replaceChildren();
    return;
  }

  if (!isAsked(view)) {
    asking.textContent = 'Waiting for the other seat…';
    choices.replaceChildren();
    return;
  }

  asking.textContent = titleOf(view);
  choices.replaceChildren(...buttonsFor(state, current));
}

function titleOf(view) {
  switch (view.waitingFor) {
    case 'Evolution': return `Unlock a spell · ${view.options.evolution?.remainingPicks ?? 0} pick(s) left`;
    case 'Speed': return `Speed of creature ${view.waitingCreature}`;
    case 'Intent': return `What does creature ${view.waitingCreature} do?`;
    case 'Target': return `Targets for creature ${view.waitingCreature}`;
    default: return 'Your move';
  }
}

// One button per thing the options offer, and nothing else. A screen that offered more than the options do
// would be inventing a rule; a screen that offered less would be hiding one.
function buttonsFor(state, current) {
  const view = current.view;
  const send = decision => () => submit(state, current, decision);
  switch (view.waitingFor) {
    case 'Evolution': {
      const unlocks = (view.options.evolution?.creatures ?? []).flatMap(creature =>
        creature.unlockableSpells.map(spell =>
          card(state, spell, `Creature ${creature.creature}`, send({ kind: 'Evolution', creature: creature.creature, spell }))));
      return [...unlocks, button('Pass', send({ kind: 'Evolution', pass: true }))];
    }
    case 'Speed':
      return ['Quick', 'Standard'].map(speed =>
        button(speed, send({ kind: 'Speed', creature: view.waitingCreature, speed })));
    case 'Intent':
      return intentButtons(state, current);
    case 'Target':
      return targetButtons(state, current);
    default:
      return [];
  }
}

// An intent is declared in two taps, not one. A mis-tap on a phone is the misplay this app will produce most
// and there is no undo (playtest-app.md §3.3, §7), so the first tap chooses a card in the hand and the second
// commits it here. The chosen card stays on the screen, marked, which is what makes the second tap a reading
// of the first rather than a formality.
function intentButtons(state, current) {
  const view = current.view;
  const option = (view.options.intent?.creatures ?? []).find(candidate => candidate.creature === view.waitingCreature);
  const castable = option?.castableSpells ?? [];
  const chosen = castable.includes(state.chosen) ? state.chosen : null;

  // No card a choice: the cards are in the hand, where their whole face is, and the hand is the picker
  // (playtest-app.md §3.2). The sheet says what to tap and holds the commitment.
  const asking = document.createElement('p');
  asking.className = 'muted';
  asking.textContent = `Tap a card in the hand of creature ${view.waitingCreature}.`;

  const name = chosen === null ? '' : state.cards.get(chosen)?.name ?? chosen;
  const confirm = button(chosen === null ? 'Choose a card' : `Declare ${name}`, () => {
    confirm.disabled = true;
    submit(state, current, { kind: 'Intent', creature: view.waitingCreature, spell: chosen });
  });
  confirm.disabled = chosen === null;
  return [asking, confirm];
}

// Toggling one target. It lives here rather than in the row so the count bound by `maxTargets` is applied in
// one place: the row is a tap surface and the sheet holds `done`, and they cannot disagree about what is picked.
function pick(state, view, candidate) {
  const legal = view.options.target?.legalTargets ?? { candidates: [], maxTargets: 0 };
  state.picked = state.picked.includes(candidate)
    ? state.picked.filter(one => one !== candidate)
    : [...state.picked, candidate].slice(0, legal.maxTargets);
  refresh(state);
}

function targetButtons(state, current) {
  const legal = current.view.options.target?.legalTargets ?? { candidates: [], minTargets: 0, maxTargets: 0 };
  const picked = state.picked;

  // A spell with nothing left to hit is revealed with no targets and fizzles, so binding none is the action
  // rather than a dead end (docs/tabletop/rulebook.md, 6.2).
  if (legal.candidates.length < legal.minTargets) {
    return [button('No legal target — cast anyway', () => submit(state, current, { kind: 'Target', targets: [] }))];
  }

  // No button a candidate: the tap is on the creature's own row, where its health, its defense and what is
  // already on it are (playtest-app.md §3.2). The sheet holds `done` and the count it is enabled at.
  const asking = document.createElement('p');
  asking.className = 'muted';
  const howMany = legal.minTargets === legal.maxTargets ? `${legal.maxTargets}` : `${legal.minTargets} to ${legal.maxTargets}`;
  asking.textContent = `Tap ${howMany} on the board.`;

  const confirm = button(`Cast on ${picked.length} of ${legal.maxTargets}`, () => {
    confirm.disabled = true;
    submit(state, current, { kind: 'Target', targets: picked });
  });
  confirm.disabled = picked.length < legal.minTargets;
  return [asking, confirm];
}

// A spell as the card the host serves, and as the id it was offered by when the catalogue has no card for it.
// Nothing here knows what any of these words mean: they arrive rendered (card.js).
function card(state, spell, prefix, onClick) {
  const parts = cardParts(state, spell, prefix);
  if (parts === null) {
    return button([prefix, spell].filter(Boolean).join(' · '), onClick);
  }

  const choice = document.createElement('button');
  choice.type = 'button';
  choice.className = 'card';
  choice.addEventListener('click', onClick);
  choice.append(...parts);
  return choice;
}

// The face of a card, head and body, and null when the catalogue has no card for the spell. The hand and the
// decision sheet both draw this: the same card, and only what happens when it is touched differs -- one is a
// thing to read, the other a thing to tap. Every line of it comes from `card.js`, which is to say from the
// host's projection of the content.
function cardParts(state, spell, prefix) {
  const face = state.cards.get(spell);
  if (!face) {
    return null;
  }

  const head = document.createElement('div');
  head.className = 'card-head';
  head.textContent = [prefix, cardTitle(face), cardHead(face)].filter(Boolean).join(' · ');

  const cost = document.createElement('span');
  cost.className = 'card-cost';
  cost.textContent = cardCost(face);
  head.append(cost);

  const body = document.createElement('div');
  body.className = 'card-body';
  for (const line of cardLines(face)) {
    const row = document.createElement('div');
    row.textContent = line;
    body.append(row);
  }

  return [head, body];
}

function button(label, onClick) {
  const face = document.createElement('button');
  face.type = 'button';
  face.textContent = label;
  face.addEventListener('click', onClick);
  return face;
}

async function submit(state, current, decision) {
  // The guard is here and not only on the buttons, because there is no undo: on a slow connection a rapid
  // double tap posted twice, one call committing the decision and the other coming back 409, which showed the
  // player an error for a declaration that had in fact been accepted.
  if (state.sending) {
    return;
  }

  state.sending = true;
  try {
    // The host has to have been told this board is up before it is told what was decided on it, or it has
    // nothing to measure the decision against. On loopback this has already resolved; on a phone over a slow
    // link it is the difference between a duration and a blank.
    await state.announced;

    const answer = await current.transport.decide(decision);
    if (!answer.ok) {
      const problem = element('problem');
      problem.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      problem.hidden = false;
      return;
    }

    state.picked = [];
    state.chosen = null;
  } finally {
    state.sending = false;
  }

  await refresh(state);
}
