import { httpTransport } from './transport.js';
import { activeSeat, isAsked, needsPass } from './seats.js';
import { forget, heldSeats } from './session.js';
import { cardCost, cardHead, cardDetails, cardStats, cardTitle, loadCatalogue } from './card.js';
import { badges, chipSource, chipText, conditionDock, healthShare, healthText, laneOf, statPairs, turnOrder, liveChoice } from './board.js';
import { handRows } from './hand.js';
import { accumulate, feedLine, lastResolved, lastResolvedText, latestResolution, opponentSteps, resolvedSince, retainRoundEvents, roundRecap, roundUpkeep, stepStart } from './feed.js';
import { bands, cursorOf, rollText, side, speedReveal, withCursor } from './timeline.js';
import { classColour, talentClasses, packageForest, talentPalette } from './mat.js';
import { isSettled, orderOf, tap, untapped } from './ties.js';
import { NOTHING_TO_RECORD, TAPPED, commentIsOpen, commentNote, noted, notesAreKept, tappedNote } from './notes.js';
import { healthChange, playbackBoard, playbackChanges } from './replay.js';
import { chance, guidancePanel, spellSummary } from './guidance.js';
import { practice, storage } from './startup.js';

// The page renders what the host serves and submits what a player taps. It holds no rule: which spells are
// castable, which targets are legal and how many, whose turn it is -- all of that arrives in `options`, built
// by the engine's own gates. Nothing here decides anything, and nothing here knows a spell by name.
const element = id => document.getElementById(id);

function setPhase(text, repeated = false) {
  element('phase').textContent = text;
  element('phase').dataset.repeated = String(repeated);
}

// How many feed entries the page keeps. The log draws the last twelve; a few times that leaves room to scroll
// back through the round without holding a whole match in memory on a phone.
const FeedKept = 60;

// How long an opponent's action stays on screen before auto mode OKs it: long enough to read the line, short
// enough that a round of bot slots is over in a few seconds.
const StepPause = 1400;

// How long the turn order stays open on its own after the reveal: long enough to read six slots, and then out of
// the way of the question it covers.
const OrderPause = 9000;

// Which tables this browser muted the pop-ups of, by their seat tokens: a token lasts one match, so a mute does
// too, and it survives a reload in the middle of it.
const QuietKey = 'downfall.table.quiet';

boot(practice);

function boot(practice) {
  const held = practice?.seats ?? heldSeats(globalThis.location?.search ?? '', storage);
  if (held.length === 0) {
    setPhase(practice ? 'Choose a practice scenario below.' : 'Type the code the host printed, or open the link it printed.');
  } else {
    // Practice keeps its separate capability so a reload can rejoin without touching normal seat storage.
    if (!practice) tidy();
    start(held.map(({ seat, token }) => ({ seat, token, transport: httpTransport(seat, token) })), practice ? 'player1' : null);
  }
}

function tidy() {
  if (globalThis.location?.search && globalThis.history?.replaceState) {
    globalThis.history.replaceState(null, '', globalThis.location.pathname);
  }
}

function start(seats, holder = null) {
  // `holder` is the seat the person now holding the device said they are, which is the only thing that lets
  // the board be shown at all. `shown` is the seat on screen, so picked targets never survive a handover.
  // `cards` is the catalogue, fetched once: it cannot change while a host runs.
  const state = {
    seats, views: [], holder, shown: null, asked: null,
    acknowledged: null, announced: null, announcing: null,
    rendered: null, revision: 0, polling: false, sending: false, error: '',
    picked: [], chosen: null, evolving: null, expandedHands: new Set(),
    cards: new Map(), packages: new Map(), catalogue: null, tab: 'board', boardOpen: false, feeds: new Map(),
    stepSeen: new Map(), stepAuto: null, quietKey: seats.map(seat => seat.token).join('/'),
  };
  state.quiet = quietTables().includes(state.quietKey);
  load(state);
  setupTalentWindow(state);
  setupPhaseControls(state);
  document.addEventListener('keydown', event => keyboardDecision(state, event));
  element('pass-ready').addEventListener('click', () => {
    state.holder = element('pass-ready').dataset.seat ?? state.holder;
    redraw(state);
  });

  // Two tabs, one screen. The mat is a tab because it is touched once a round and the board is touched all
  // the time; switching draws from what the last poll already fetched, so it never waits.
  for (const [tab, name] of [['tab-board', 'board'], ['tab-mat', 'mat']]) {
    element(tab).addEventListener('click', () => {
      if (name === 'mat') openTalents(state);
      else { closeTalents(state); element('board').scrollIntoView({ block: 'start', behavior: 'instant' }); }
    });
  }

  element('board-toggle').addEventListener('click', () => showBoard(state, !state.boardOpen));
  globalThis.addEventListener?.('resize', () => placeBoard(state));
  element('hand-talents').addEventListener('click', () => openTalents(state));
  element('hand-view').addEventListener('click', () => {
    state.fullSpellView = !state.fullSpellView;
    element('planning').classList.toggle('full-spells', state.fullSpellView);
    element('hand-view').textContent = state.fullSpellView ? 'Compact view' : 'Full details';
    element('hand-view').setAttribute('aria-pressed', String(state.fullSpellView));
  });

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
function announce(state, current, drawn) {
  const since = state.feeds.get(current.seat)?.next ?? 0;
  const acknowledged = `${current.seat}/${drawn}`;

  // Kept, because a decision must not overtake it. The controls stay live -- disabling them for a round trip
  // would make every handover feel broken to protect a number -- and `submit` waits on this instead, so a tap
  // inside the window is delayed by the request it would otherwise have raced rather than being refused. If
  // it were raced and lost, the host would have no moment for this question and would record the duration as
  // unknown, which is honest but is one measurement gone.
  // Remembered only once it has landed. Marking it sent and then losing the request would leave a board on
  // screen that the host has no moment for, and the render never offers to say so again: the decision made on
  // it would be recorded with no duration at all, silently.
  state.announcing = acknowledged;
  state.announced = current.transport.seat(since, drawn)
    .then(answer => {
      if (answer.ok) {
        state.acknowledged = acknowledged;
      }
    })
    .catch(() => {
      // A page that cannot reach its host has a louder problem than a clock, and the next poll reports it.
    })
    .finally(() => { state.announcing = null; });
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

  let answer;
  try {
    answer = await current.transport.note(body);
  } catch {
    line.textContent = 'Could not save the note. Check your connection and try again.';
    line.hidden = false;
    return false;
  }
  line.textContent = answer.ok ? noted(body.kind) : (answer.body?.message ?? `The host answered ${answer.status}.`);
  line.hidden = false;
  return answer.ok;
}

// The catalogue the match is playing, through any seat this page holds: it is the same for both, and it is
// what every card on this screen is drawn from. A page that does not get it prints spell ids and still plays.
async function load(state) {
  try {
    state.catalogue = await loadCatalogue(state.seats);
  } catch {
    return; // The seat poll reports connection failures; a later poll retries the catalogue.
  }
  state.cards = new Map((state.catalogue?.cards ?? []).map(card => [card.id, card]));
  state.packages = new Map((state.catalogue?.packages ?? []).map(face => [face.id, face]));
  element('rules').textContent = ruleLine(state.catalogue);
  renderShape(state.catalogue?.round);
  redraw(state);
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
  // The first round is printed beside the interval, not folded into it: rounds 1, 3, 5 and rounds 2, 4, 6 are
  // the same interval and a different game, and this line exists to tell two games apart at a glance.
  const every = rules.evolutionInterval === 1 ? 'every round' : `every ${rules.evolutionInterval} rounds`;
  const cadence = `${every} from round ${rules.firstEvolutionRound}`;
  return `${rules.teamSize} creatures · ${rules.energyPerRound} energy · ${rules.evolutionPicksPerOpportunity} picks ${cadence} · ${rules.roundCap} rounds · x${rules.criticalMultiplier} crit · ${String(catalogue.contentHash ?? '').slice(0, 6)}`;
}

// Only one poll can be in flight. An old response must never replace a newer decision.
async function refresh(state) {
  if (state.sending || state.polling) return;
  state.polling = true;
  try {
    await poll(state);
  } catch {
    state.rendered = null;
    setPhase('Connection lost · retrying…');
  } finally {
    state.polling = false;
  }
}

async function poll(state) {
  const revision = state.revision;

  // Every seat this page holds, every poll: the host answers one seat per payload, and which one is being
  // asked is exactly what the page cannot know without asking.
  const views = [];
  const refused = [];
  for (const seat of state.seats) {
    // Only the entries this page has not seen yet. The feed is the whole match's history and it only grows, so
    // a poll every 700 ms that asked for all of it would serialize and download the match again each time --
    // and over a half-hour session that is quadratic in the number of events, for twelve lines on screen.
    // An ordinary poll acknowledges nothing. What the host times a decision from is the page saying it has
    // *drawn* an asking, which is sent from the render below -- this request is the one fetching the question,
    // and its answer still has to arrive and be laid out before anybody has read anything.
    const answer = await seat.transport.seat(state.feeds.get(seat.seat)?.next ?? 0);
    if (revision !== state.revision) return;

    // A token this host does not know is a seat from another table -- an earlier session, or the browser of
    // somebody who played here yesterday. Only that seat goes: a code typed for *this* table may be on the
    // same page, and it has already been taken out of the address bar, so forgetting it too would mean
    // reading it off the host's screen again.
    if (answer.status === 403) {
      refused.push(seat.seat);
      continue;
    }

    if (!answer.ok) {
      state.rendered = null;
      setPhase(answer.body?.message ?? `The host answered ${answer.status}.`);
      return;
    }

    // The cursor is the host's `feedNext`, not the highest entry on the screen: the two differ whenever the
    // end of the trace is the other seat's own decisions, which this seat is never shown.
    const held = state.feeds.get(seat.seat);
    const kept = accumulate(held?.entries, answer.body?.feed, FeedKept);
    const next = Number.isInteger(answer.body?.feedNext) ? answer.body.feedNext : held?.next ?? 0;
    const roundEvents = retainRoundEvents(held?.roundEvents, answer.body?.feed, answer.body?.board?.roundNumber);
    state.feeds.set(seat.seat, { entries: kept, next, roundEvents });
    views.push({ ...seat, view: { ...answer.body, feed: kept, roundEvents } });
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
    state.views = [];
    element('table').hidden = true;
    element('pass').hidden = true;
    setPhase('This browser holds no seat at this table. Type the code the host printed.');
    return;
  }

  if (state.sending) return;
  state.views = views;
  render(state, views);
  if (!state.catalogue) await load(state);
}

function redraw(state) {
  if (state.views.length > 0) render(state, state.views);
}

function showTab(state, name) {
  state.tab = name;
  for (const [id, panel] of [['tab-board', 'board'], ['tab-mat', 'mat']]) {
    element(id).classList.toggle('on', name === panel);
    element(id).setAttribute('aria-pressed', String(name === panel));
    if (panel === 'board') element(panel).hidden = false;
  }
  element('talent-window').hidden = name !== 'mat';
}

// Below the laptop layout the battlefield is one tap from any question and one tap back. It opens over the
// page, under the round bar that holds the button, so the spellbook stays where the player had scrolled it
// and nothing has to be scrolled to reach the board or to come back from it.
function showBoard(state, open) {
  // Taking the board out of the page shortens it, which can clamp the scroll; closing puts the player back.
  if (open && !state.boardOpen) state.boardReturn = globalThis.scrollY ?? 0;
  const returning = !open && state.boardOpen;
  state.boardOpen = open;
  document.body?.classList.toggle('board-open', open);
  const toggle = element('board-toggle');
  toggle.setAttribute('aria-pressed', String(open));
  toggle.textContent = open ? 'Battlefield ✕' : 'Battlefield';
  toggle.title = open ? 'Back to your move' : 'Show every creature';
  placeBoard(state);
  if (open) {
    element('board').scrollTop = 0;
    // Opened while a target is asked for, the battlefield starts at the first creature the spell may take,
    // which for a spell aimed at an ally is the player's own team rather than the opponent above it.
    revealRow(state, state.targetAnchor);
  }
  if (!returning) return;
  if (element('board').contains(document.activeElement)) element('board-toggle').focus({ preventScroll: true });
  globalThis.scrollTo?.(0, state.boardReturn ?? 0);
  const current = activeSeat(state.views, state.holder);
  if (current && !state.playback && !needsPass(current, state.holder)) guideDecision(state, current);
}

function placeBoard(state) {
  if (!state.boardOpen) return;
  const bottom = element('phase-dock').getBoundingClientRect().bottom;
  document.documentElement?.style.setProperty('--board-top', `${Math.max(0, bottom)}px`);
}

function selectable(face, selected, onClick) {
  face.tabIndex = 0;
  face.setAttribute('role', 'button');
  face.setAttribute('aria-pressed', String(selected));
  face.addEventListener('click', onClick);
  face.addEventListener('keydown', event => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!event.repeat) return onClick(event, true);
    }
  });
}

// Stable keys preserve keyboard focus and each hand's scroll offset when a selection changes.
function rememberPosition() {
  return {
    focus: document.activeElement?.dataset?.focus,
    scrolls: [...document.querySelectorAll('[data-scroll]')].map(node => [node.dataset.scroll, node.scrollLeft, node.scrollTop]),
  };
}

function restorePosition(saved) {
  for (const node of document.querySelectorAll('[data-scroll]')) {
    const previous = saved.scrolls.find(([key]) => key === node.dataset.scroll);
    node.scrollLeft = previous?.[1] ?? 0;
    node.scrollTop = previous?.[2] ?? 0;
  }
  if (saved.focus) {
    [...document.querySelectorAll('[data-focus]')].find(node => node.dataset.focus === saved.focus)?.focus({ preventScroll: true });
  }
}

const nameOf = seat => (seat === 'player1' ? 'Player 1' : 'Player 2');

function render(state, views) {
  const current = activeSeat(views, state.holder);
  const view = current.view;

  // What the seat is being asked, as an identity. A choice belongs to one question and to no other: a decision
  // refused as stale (409) leaves the choice standing, and the next question of the same seat is very often a
  // different creature that knows the same spell -- which would arrive with a card already selected and one tap
  // from being committed, through the confirmation that exists to stop exactly that.
  const asked = `${current.seat}/${view.waitingAsked ?? ''}/${view.waitingFor ?? ''}/${view.waitingCreature ?? ''}`;
  if (asked !== state.asked) {
    state.shown = current.seat;
    state.asked = asked;
    state.picked = [];
    state.chosen = null;
    state.error = '';
    state.evolving = null;
    state.inspectCreature = null;
    state.ordered = [];
    element('decision').scrollTop = 0;
    // On a phone the spellbook and the desk scroll on their own: the next creature's speed or spell starts at the
    // top of its list, not where the last creature's choice left it.
    element('hand-section').scrollTop = 0;
    element('planning').scrollTop = 0;
    if (['Speed', 'TieOrder', 'Intent', 'Target'].includes(view.waitingFor)) showTab(state, 'board');
  }

  // Until the player being asked says they are the one holding the device, the board stays behind the pass
  // screen (seats.js).
  const fence = needsPass(current, state.holder);
  if (!fence) syncPlayback(state, current);
  // The opponent's actions this seat has not read yet hold its next question back, one OK each.
  state.stepSeen ??= new Map();
  state.step = fence ? null : stepOf(state, current);

  // What the host is told has been drawn, and it is the asking rather than the seat. One seat is asked several
  // questions in a row -- two Evolution picks are two askings of the same shape -- so acknowledging per seat
  // would let the host start the second one's clock while it was still serving it, with the network and the
  // layout inside the player's duration. Nothing is acknowledged while the pass screen is up: the board is
  // behind it and the person being asked has not picked the device up yet.
  // Per seat as well as per asking: the two seats count their own questions, so they are at the same number
  // whenever they have decided the same number of times -- which in hotseat is most of the time. A bare number
  // would call the other seat's question already acknowledged and never announce it.
  const drawn = fence || state.playback || state.step ? null : view.waitingAsked ?? null;
  const acknowledgement = `${current.seat}/${drawn}`;
  if (drawn !== null && acknowledgement !== state.acknowledged && acknowledgement !== state.announcing) {
    announce(state, current, drawn);
  }
  const identity = JSON.stringify([current.seat, view, fence, state.chosen, state.picked, state.evolving, state.ordered, state.inspectCreature, state.inspectClass, state.catalogue, state.error, state.playback, state.stepSeen.get(current.seat), state.stepAuto, state.quiet]);
  if (state.rendered === identity) return;
  state.rendered = identity;
  const saved = rememberPosition();
  element('seat').textContent = nameOf(current.seat);
  element('pass-seat').textContent = nameOf(current.seat);
  element('pass-seat-again').textContent = nameOf(current.seat);
  element('pass-ready').dataset.seat = current.seat;
  element('pass').hidden = !fence;
  element('table').hidden = fence;
  if (fence) {
    showBoard(state, false);
    hidePhaseNotice(state);
    clearTimeout(state.stepTimer);
    state.stepTimerFor = null;
    element('order').open = false;
    element('upkeep').open = false;
    element('announcements').open = false;
    element('phase-progress').open = false;
    element('pass-ready').focus({ preventScroll: true });
    return;
  }

  setPhase(view.over
    ? 'The match is over.'
    : `Round ${view.board.roundNumber ?? '—'} of ${state.catalogue?.rules?.roundCap ?? '—'} · ${(view.board.subPhase ?? '—').replace(/([a-z])([A-Z])/g, '$1 $2')}`, !view.over);
  state.palette = talentPalette(state.catalogue, state.cards);
  // A replay the player opened, or the opponent's action being stepped through: the battlefield as it stood
  // right after that action (when the host recorded it), and no question on it.
  const replay = state.playback ?? state.step;
  const display = replay ? { ...current, view: {
    ...view, board: playbackBoard(replay, view.board), waitingFor: null, options: {},
    roundEvents: (view.roundEvents ?? []).filter(entry => entry.sequence < replay.actions[replay.index].sequence
      || (replay.stage === 'after' && entry.sequence === replay.actions[replay.index].sequence)),
  } } : current;
  renderTimeline(display.view.board);
  // The spellbook stays the live one while an opponent action is read: it is the player's, not the replay's.
  renderBoard(state, display, state.playback ? display : current);
  renderMat(state, display);
  renderFeed(state, view.feed);
  renderRecap(state, view);
  renderDecision(state, current);
  renderCombatLine(state, view);
  if (state.playback) renderPlayback(state, current);
  else {
    renderPhaseGuide(state, view, current.seat);
    renderOrder(state, view, current.seat);
    // Only while the round bar is on that action's combat: the last slot of a round is often read once the host
    // has moved on to the next round or the result, and the bar must not mix the two.
    if (state.step && state.step.round === view.board.roundNumber && view.board.subPhase === 'Activation' && !view.over) {
      element('phase-turn').textContent = stepTurnText(state.step, display.view.board);
    }
  }
  element('planning').hidden = Boolean(state.playback);
  element('playback').hidden = !state.playback;
  element('playback-board-note').hidden = !state.playback;
  element('phase-progress').hidden = Boolean(state.playback);
  element('order').hidden ||= Boolean(state.playback);
  const phaseHeight = element('phase-dock').getBoundingClientRect().height ?? 0;
  element('table').style.setProperty('--phase-height', `${phaseHeight}px`);
  element('board').style.setProperty('--target-offset', `${18 + phaseHeight}px`);
  renderNotes(view);
  element('shortcut-context').textContent = view.waitingFor === 'Speed'
    ? '1 Quick · 2 Standard' : view.waitingFor === 'Evolution'
      ? '← → Choose creature · ↓ Browse packages · Enter to buy'
      : view.waitingFor === 'TieOrder' ? '← → Browse tied creatures · Enter to order · Confirm when ready'
        : '1–9 Select card / target · Enter to confirm';
  restorePosition(saved);
  placeBoard(state);
  if (!state.playback) guideDecision(state, current);
  else if (state.guidedPlayback !== `${state.playback.seat}/${state.playback.round}`) {
    state.guidedPlayback = `${state.playback.seat}/${state.playback.round}`;
    element('playback').scrollIntoView({ block: 'nearest', behavior: 'instant' });
  }
}

// Guide each new question once, after the handover fence is down. Polls and local selections never
// pull the player back after they deliberately scroll elsewhere to inspect the board.
function guideDecision(state, current) {
  // A player reading the open battlefield is not moved under it; closing it guides them instead.
  if (state.step || state.boardOpen || state.guidedAsking === state.asked || current.view.over || current.view.playedByBot) return;
  state.guidedAsking = state.asked;
  const kind = current.view.waitingFor;
  const desktop = globalThis.innerWidth >= 1100;
  const anchor = desktop && ['Speed', 'TieOrder', 'Intent', 'Target'].includes(kind) ? element('board')
    : ['Speed', 'Intent'].includes(kind) ? element('planning') : kind === 'Target' ? element('decision') : null;
  if (!anchor) return;
  const rect = anchor.getBoundingClientRect();
  const dockHeight = element('phase-dock').getBoundingClientRect().height ?? 0;
  if (rect.top < dockHeight + 12 || rect.bottom > globalThis.innerHeight - 12) {
    anchor.scrollIntoView({ block: 'start', behavior: 'instant' });
  }
}

// A handler can outlive its DOM node or its question. Only the visible seat's current asking may act.
function canInteract(state, current, kind) {
  const active = activeSeat(state.views, state.holder);
  return !state.playback && !state.step && !state.sending && !current.view.over && !current.view.playedByBot &&
    current.view.waitingFor === kind && current.seat === state.shown &&
    active?.seat === current.seat && active.view.waitingAsked === current.view.waitingAsked &&
    !needsPass(current, state.holder);
}

// The notes, and the comment box only once the match is decided: asking for prose while somebody is deciding
// is asking them to stop playing. A table that keeps nothing offers neither.
function renderNotes(view) {
  const kept = notesAreKept(view);
  element('notes').hidden = !kept;
  element('comment').hidden = !kept || !commentIsOpen(view);
}

// The initiative track: the engine's order, banded by speed, wrapping on a phone rather than scrolling. The strip never
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
      const order = document.createElement('span');
      order.className = 'slot-order';
      order.textContent = `${turnOrder({ id: slot.creature }, board)}`;
      order.style.setProperty('--turn-color', turnColour(Number(order.textContent), board.timeline.length));
      const creature = document.createElement('strong');
      creature.textContent = `Creature ${slot.creature}`;
      const initiative = document.createElement('span');
      initiative.className = 'slot-initiative';
      initiative.textContent = `Initiative ${slot.initiative}`;
      one.setAttribute('aria-label', `Turn ${order.textContent}, Creature ${slot.creature}, ${band.speed}, initiative ${slot.initiative}${slot.isNow ? ', acting now' : ''}`);
      one.append(order, creature, initiative);
      const rolled = rollText(board.rollOffs, slot.creature);
      if (rolled) {
        const dice = document.createElement('span');
        dice.className = 'roll';
        dice.textContent = rolled;
        one.append(dice);
      }
      slots.append(one);
    }

    box.append(slots);
    return box;
  });

  element('timeline').replaceChildren(...strip);
}

function renderBoard(state, current, spellbook = current) {
  const view = current.view;
  const board = view.board;
  const handView = element('hand-view');
  handView.hidden = view.waitingFor !== 'Intent' || !isAsked(view);
  handView.textContent = state.fullSpellView ? 'Compact view' : 'Full details';
  handView.setAttribute('aria-pressed', String(Boolean(state.fullSpellView)));
  element('planning').classList.toggle('full-spells', Boolean(state.fullSpellView));
  state.targetAnchor = null;
  state.creatureRows = new Map();
  // What a row may do and say this poll. Targeting is a tap on a legal creature (playtest-app.md §3.2), so
  // the candidates the options offer are the rows that are tappable, and no others.
  // Live, the action resolved last is marked the way a replayed one is: its caster, its targets and what it
  // changed, on the board as it stands (ADR 0083). A replay the player opened takes its place.
  const replay = state.playback ?? state.step;
  const live = replay ? null : lastResolved(view.roundEvents, board, state.cards);
  const marks = {
    picked: state.picked,
    board,
    roundEvents: view.roundEvents,
    active: replay ? replay.actions[replay.index]?.actor.id : isAsked(view) ? view.waitingCreature : null,
    playback: replay?.actions[replay.index] ?? live,
    playbackStage: replay?.stage ?? (live ? 'after' : undefined),
    live: Boolean(live),
    draftSpell: isAsked(view) && view.waitingFor === 'Intent' ? state.chosen : null,
    targeting: isAsked(view) && view.waitingFor === 'Target',
    candidates: !replay && isAsked(view) && view.waitingFor === 'Target' ? view.options.target?.legalTargets?.candidates ?? [] : [],
    onPick: candidate => pick(state, current, candidate),
    canConfirm: canCastTargets(state, view),
    // A replayed or stepped action outlines its caster as the one acting; the live cursor is somewhere else.
    turn: replay ? null : activeTurn(board)?.slot.creature ?? null,
  };
  element('enemies').replaceChildren(...(board.enemies ?? []).map(creature => line(state, creature, 'enemy', marks)));
  element('allies').replaceChildren(...(board.allies ?? []).map(creature => line(state, creature, 'ally', marks)));
  element('mini-board').classList.toggle('targeting', marks.candidates.length > 0);
  element('mini-enemies').replaceChildren(...(board.enemies ?? []).map(creature => miniCreature(state, creature, 'enemy', marks)));
  element('mini-allies').replaceChildren(...(board.allies ?? []).map(creature => miniCreature(state, creature, 'ally', marks)));
  enemyBooks(state, current);
  element('own-hand').replaceChildren(hand(state, spellbook));
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
function hand(state, current) {
  const view = current.view;
  state.activeHand = null;
  const asked = isAsked(view) && ['Speed', 'Intent'].includes(view.waitingFor) ? view.waitingCreature : null;
  const rows = handRows(view.board.allies, view.options?.intent, view.board.intents);
  const box = document.createElement('div');
  box.className = 'hand-cards';
  box.hidden = rows.every(row => row.spells.length === 0);
  const ordered = [...rows.filter(row => row.creature === asked), ...rows.filter(row => row.creature !== asked)];
  box.append(...ordered.map(row => handRow(state, row, asked, current)));
  return box;
}

// One creature's row: who it is, whether it has declared, and its cards.
function handRow(state, row, asked, current) {
  const active = row.creature === asked;
  const reference = current.view.waitingFor === 'Speed';
  const one = document.createElement(active ? 'div' : 'details');
  state.expandedHands ??= new Set();
  if (!active) {
    one.open = state.expandedHands.has(row.creature);
    one.addEventListener('toggle', () => {
      if (one.open) state.expandedHands.add(row.creature);
      else state.expandedHands.delete(row.creature);
    });
  }
  one.className = `hand-row${active ? ' active' : ''}`;
  if (active) state.activeHand = one;

  const who = document.createElement(active ? 'div' : 'summary');
  who.className = 'hand-who';
  const order = turnOrder({ id: row.creature }, current.view.board);
  const acts = order === null ? '' : ` · acts ${order} of ${current.view.board.timeline.length}`;
  who.textContent = `Creature ${row.creature}${acts}${active ? (reference ? ' · choose speed · spell reference' : ' · choose a card') : row.declared ? ' · declared' : ''}`;

  const held = document.createElement('div');
  held.className = `held-cards${reference ? ' speed-reference' : ''}`;
  held.dataset.scroll = `hand-${row.creature}`;
  // Tappable only on the creature being asked: every row says what its creature could cast, which is what
  // makes the hand readable, but only one creature is being asked at a time.
  const card = spell => reference ? speedSpell(state, spell, row.creature, current)
    : heldCard(state, spell, active && spell.castable, row.creature, current);
  const types = active ? spellTypes(state, row.spells) : null;
  if (types) {
    held.className += ' grouped-spells';
    held.append(...[...types].map(([type, spells]) => spellGroup(type, spells.map(card))));
  } else held.append(...row.spells.map(card));

  one.append(who, held);
  return one;
}

// The host's chance for this creature when it has one, the card's own otherwise; a chance of zero is none.
function standardCritical(card, guide) {
  const chanceOf = guide?.standardCriticalChance;
  if (chanceOf == null) return cardStats(card)[0]?.value ?? '';
  return chanceOf > 0 ? chance(chanceOf) : '';
}

// Speed is a comparison of critical chances, with the full host-authored spell only a tap away.
function speedSpell(state, spell, creature, current) {
  const card = state.cards.get(spell.spell);
  const guide = creature === current.view.waitingCreature ? current.view.guidance?.find(one => one.spell === spell.spell) : null;
  const reference = document.createElement('details');
  reference.className = 'speed-spell';
  const summary = document.createElement('summary');
  const name = document.createElement('strong');
  name.textContent = cardTitle(card) || spell.spell;
  summary.append(name);
  const cost = cardCost(card);
  if (cost !== '') {
    const energy = document.createElement('span');
    energy.className = 'speed-cost';
    energy.textContent = `${cost} energy`;
    summary.append(energy);
  }
  const critical = standardCritical(card, guide);
  if (critical) {
    const badge = document.createElement('span');
    badge.className = 'speed-critical';
    badge.textContent = `✦ ${critical} crit`;
    badge.title = 'Critical chance at Standard speed';
    summary.append(badge);
  }
  const body = document.createElement('div');
  body.className = 'speed-spell-details';
  const lines = [...cardDetails(card).map(row => row.text), card?.criticalNote, guide?.unavailableReason].filter(Boolean);
  for (const text of lines) {
    const line = document.createElement('p');
    line.textContent = text;
    body.append(line);
  }
  reference.append(summary, body);
  return reference;
}

// One card in the hand: its whole face, dimmed when the creature cannot cast it, and a tap surface when it is
// the one being asked for. An opponent's revealed card is a reference: it says so rather than "not available".
function heldCard(state, spell, offered, creature, current, reference = false) {
  const face = document.createElement('div');
  const chosen = offered && spell.spell === state.chosen;
  face.className = ['card held', reference && 'reference', spell.castable && 'castable', offered && 'offered', chosen && 'chosen']
    .filter(Boolean)
    .join(' ');

  const parts = cardParts(state, spell.spell, '');
  if (parts === null) face.textContent = spell.spell;
  else face.append(...parts);

  const guide = creature === current.view.waitingCreature ? current.view.guidance?.find(one => one.spell === spell.spell) : null;
  if (guide) {
    const advice = document.createElement('p');
    advice.className = 'card-advice';
    advice.textContent = spellSummary(guide);
    face.append(advice);
  }
  const availability = document.createElement('span');
  availability.className = 'card-availability';
  availability.textContent = availabilityText(spell, offered, chosen, reference);
  face.append(availability);
  if (offered) offerCard(state, current, face, availability, spell, creature, chosen);
  return face;
}

// The catalogue owns each card's type. Only the active hand is grouped, and only when every spell has a type and
// there is more than one: the other creatures remain ordinary collapsible references.
function spellTypes(state, spells) {
  const types = new Map();
  for (const spell of spells) {
    const type = state.cards.get(spell.spell)?.type;
    if (!type) return null;
    if (!types.has(type)) types.set(type, []);
    types.get(type).push(spell);
  }
  return types.size > 1 ? types : null;
}

function spellGroup(type, cards) {
  const group = document.createElement('section');
  group.className = 'spell-group';
  const heading = document.createElement('h3');
  heading.textContent = `${type} · ${cards.length}`;
  const grid = document.createElement('div');
  grid.className = 'spell-grid';
  grid.append(...cards);
  group.append(heading, grid);
  return group;
}

function availabilityText(spell, offered, chosen, reference) {
  if (offered) return chosen ? '✓ Selected · declare above' : 'Select card →';
  if (reference) return 'Spell reference';
  return spell.castable ? 'Available' : 'Not available now';
}

// The asked creature's castable card: numbered for the keyboard, and selected by a tap.
function offerCard(state, current, face, availability, spell, creature, chosen) {
  const offeredSpells = current.view.options.intent?.creatures?.find(one => one.creature === creature)?.castableSpells ?? [];
  const number = offeredSpells.indexOf(spell.spell) + 1;
  if (number > 0 && number <= 9) availability.textContent = `[${number}] ${availability.textContent}`;
  face.dataset.focus = `card-${creature}-${spell.spell}`;
  selectable(face, chosen, (event, keyboard) => chooseCard(state, current, spell.spell, keyboard === true));
}

// A creature board: numbers and a bar, never a rail, and the dock under it (board.js).
function line(state, creature, which, marks) {
  const picked = (marks?.picked ?? []).includes(creature.id);
  const legal = (marks?.candidates ?? []).includes(creature.id);
  const box = document.createElement('div');
  box.className = `creature ${which}${creature.isAlive === false ? ' dead' : ''}${picked ? ' picked' : ''}${legal ? ' legal' : ''}${creature.id === marks?.active ? ' active' : ''}`;
  if (marks?.live) box.classList.toggle('live', true);
  if (marks?.playback?.actor.id === creature.id) box.classList.toggle('replay-caster', true);
  if (marks?.playback?.targets.some(target => target.id === creature.id)) box.classList.toggle('replay-target', true);
  state.creatureRows?.set(creature.id, box);
  if (legal) {
    state.targetAnchor ??= box;
    box.dataset.focus = `target-${creature.id}`;
    selectable(box, picked, () => marks.onPick(creature.id));
  }

  const who = document.createElement('div');
  who.className = 'who';
  const id = document.createElement('span');
  id.className = 'creature-id';
  id.textContent = creature.id;
  const name = document.createElement('div');
  name.className = 'creature-name';
  name.textContent = `Creature ${creature.id}`;
  const label = document.createElement('span');
  label.className = 'creature-label';
  label.textContent = creature.isAlive === false ? 'Defeated' : legal ? (picked ? (marks.canConfirm ? 'Tap again to cast' : 'Selected · choose more targets') : 'Select target') : creature.id === marks?.active ? 'Acting now' : which === 'ally' ? 'Your creature' : 'Opponent creature';
  if (legal) {
    const number = marks.candidates.indexOf(creature.id) + 1;
    if (number <= 9) label.textContent += ` · [${number}]`;
  }
  name.append(label);
  who.append(id, name);

  const health = document.createElement('div');
  health.className = 'health';
  const bar = document.createElement('span');
  bar.className = 'bar';
  bar.style.width = `${Math.round(healthShare(creature) * 100)}%`;
  const number = document.createElement('span');
  number.className = 'number';
  number.textContent = `${healthText(creature)} HP`;
  health.append(bar, number);

  const stats = document.createElement('div');
  stats.className = 'stats';
  for (const [name, value] of statPairs(creature)) {
    const stat = document.createElement('div');
    stat.className = `stat stat-${name}`;
    const amount = document.createElement('strong');
    amount.textContent = value;
    const label = document.createElement('span');
    label.textContent = name;
    stat.append(amount, label);
    stats.append(stat);
  }
  const tags = document.createElement('div');
  tags.className = 'badges';

  const order = turnOrder(creature, marks?.board);
  if (order !== null) {
    const turn = document.createElement('span');
    turn.className = 'turn-label';
    turn.textContent = 'Turn ';
    const number = document.createElement('span');
    number.className = `turn-order${order - 1 === cursorOf(marks?.board) ? ' now' : ''}`;
    number.textContent = order;
    number.style.setProperty('--turn-color', turnColour(order, marks.board.timeline.length));
    number.setAttribute('aria-label', `Acts ${order} of ${marks.board.timeline.length}`);
    turn.append(number);
    tags.append(turn);
  }

  for (const badge of badges(creature, marks?.board?.timeline)) {
    const one = document.createElement('span');
    one.className = 'badge';
    const speed = marks?.board?.timeline?.find(slot => slot.creature === creature.id)?.speed;
    one.textContent = badge === speed ? `Speed · ${badge}` : badge;
    if (badge === speed) one.dataset.speed = speed;
    tags.append(one);
  }

  box.append(who, health, stats, tags);
  if (which === 'enemy' && creature.acquiredTiers?.length) {
    const packages = document.createElement('div');
    packages.className = 'enemy-packages';
    packages.setAttribute('aria-label', `Creature ${creature.id} packages`);
    for (const tier of creature.acquiredTiers) {
      const chip = document.createElement('span');
      chip.className = 'enemy-package';
      chip.style.setProperty('--class-color', classColour(tier, state.palette));
      chip.textContent = state.packages.get(tier)?.name ?? tier;
      packages.append(chip);
    }
    box.append(packages);
  }
  box.append(dock(state, creature.conditions));
  if (marks?.playbackStage === 'after') {
    const changes = document.createElement('div');
    changes.className = 'replay-changes';
    for (const change of playbackChanges(marks.playback, creature)) {
      const chip = document.createElement('span');
      chip.className = `recap-effect ${change.tone}`;
      chip.textContent = change.text;
      changes.append(chip);
    }
    box.append(changes);
  }
  if (which === 'enemy') {
    box.append(enemyChoice(state, creature, marks?.board, marks?.roundEvents));
  } else {
    const choice = ownChoice(state, creature, marks);
    if (choice) box.append(choice);
  }

  // The marker, on the row rather than only in the sheet: a target is chosen against this creature's health,
  // its defense and what is already on it, so the choice has to be visible where those numbers are
  // (playtest-app.md §3.1). `picked` is what this seat has tapped and not yet sent. No other caster points at
  // it: every earlier action has already resolved (ADR 0083).
  if (picked) {
    const markers = document.createElement('div');
    markers.className = 'markers';
    const mine = document.createElement('span');
    mine.className = 'marker picked';
    mine.textContent = 'picked';
    markers.append(mine);
    box.append(markers);
  }

  return box;
}

// The battlefield at a glance, in the round bar below the laptop layout: each creature's number, health,
// energy and current initiative, and whether it is stunned. A legal target is picked here exactly as on its row, so a spell aimed at
// an ally never sends the player past the opponent to find one; any other creature opens the battlefield at its
// row, where its defense, conditions and choice are.
function miniCreature(state, creature, which, marks) {
  const picked = (marks.picked ?? []).includes(creature.id);
  const legal = (marks.candidates ?? []).includes(creature.id);
  const chip = document.createElement('button');
  chip.type = 'button';
  chip.className = miniClasses(creature, which, marks, legal, picked);
  const id = document.createElement('span');
  id.className = 'mini-id';
  id.textContent = creature.id;
  const health = document.createElement('span');
  health.className = 'mini-health';
  const bar = document.createElement('span');
  bar.className = 'mini-bar';
  bar.style.width = `${Math.round(healthShare(creature) * 100)}%`;
  const amount = document.createElement('span');
  amount.className = 'mini-hp';
  amount.textContent = `${creature.health ?? 0}`;
  health.append(bar, amount);
  const energy = document.createElement('span');
  energy.className = 'mini-energy';
  energy.textContent = `ϟ${creature.energy ?? 0}`;
  const stats = document.createElement('span');
  stats.className = 'mini-stats';
  stats.append(energy);
  // Defense only when there is some: a creature without any is the usual case, and a phone has no room to say so.
  if (creature.totalDefense > 0) {
    const defense = document.createElement('span');
    defense.className = 'mini-defense';
    defense.textContent = `◇${creature.totalDefense}`;
    stats.append(defense);
  }
  if (creature.currentInitiative != null && Number.isFinite(Number(creature.currentInitiative))) {
    const initiative = document.createElement('span');
    initiative.className = 'mini-initiative';
    initiative.textContent = `↟${creature.currentInitiative}`;
    stats.append(initiative);
  }
  chip.append(id, health, ...miniDelta(creature, marks), stats);
  if (creature.isStunned === true) {
    const stunned = document.createElement('span');
    stunned.className = 'mini-flag';
    stunned.textContent = '⊘';
    stunned.title = 'Stunned';
    chip.append(stunned);
  }
  const confirm = picked && marks.canConfirm;
  chip.setAttribute('aria-label', miniLabel(creature, which, marks, legal, picked));
  if (confirm) chip.title = 'Tap again to cast';
  // Its own key, so a redraw gives focus back to the chip and arrows walk the chips while the board is shut.
  if (legal) chip.dataset.focus = `mini-target-${creature.id}`;
  chip.addEventListener('click', () => (legal ? marks.onPick(creature.id) : openBoardAt(state, creature.id)));
  return chip;
}

function miniClasses(creature, which, marks, legal, picked) {
  const replay = marks.playback;
  return ['mini-creature', which, creature.isAlive === false && 'dead', creature.isStunned === true && 'stunned', legal && 'legal', picked && 'picked',
    picked && marks.canConfirm && 'confirm', creature.id === marks.active && 'active',
    creature.id === marks.turn && 'turn', replay?.actor.id === creature.id && 'replay-caster',
    replay?.targets.some(target => target.id === creature.id) && 'replay-target'].filter(Boolean).join(' ');
}

// What a screen reader hears, and what the picked chip's second tap does: it casts, as the row's does.
function miniLabel(creature, which, marks, legal, picked) {
  const role = which === 'ally' ? 'your creature' : 'opponent';
  const acting = creature.id === marks.active || creature.id === marks.turn;
  const status = [acting && 'acting now', creature.isAlive === false && 'defeated', creature.isStunned === true && 'stunned',
    legal && targetStatus(marks, picked)];
  const initiative = creature.currentInitiative != null && Number.isFinite(Number(creature.currentInitiative))
    ? `, ${creature.currentInitiative} initiative` : '';
  return [`Creature ${creature.id}, ${role}, ${healthText(creature)} health, ${creature.energy ?? 0} energy, ${creature.totalDefense ?? 0} defense${initiative}`, ...status.filter(Boolean)].join(', ');
}

function targetStatus(marks, picked) {
  if (!picked) return 'legal target';
  return marks.canConfirm ? 'selected target, tap again to cast' : 'selected target, choose more targets';
}

// Once a replayed action is applied, what it did to health is on the chip, where the bar just moved.
function miniDelta(creature, marks) {
  const change = marks.playbackStage === 'after' ? healthChange(marks.playback, creature) : 0;
  if (change === 0) return [];
  const delta = document.createElement('span');
  delta.className = `mini-delta ${change < 0 ? 'harm' : 'recovery'}`;
  delta.textContent = change < 0 ? `−${-change}` : `+${change}`;
  return [delta];
}

function openBoardAt(state, id) {
  showBoard(state, true);
  revealRow(state, state.creatureRows?.get(id));
  // The chip that opened it is hidden with the bar's chips; the battlefield takes focus so keys stay in reach.
  element('board').focus({ preventScroll: true });
}

// Scrolls the open battlefield, and only it, so a row is in view under the round bar.
function revealRow(state, row) {
  if (!state.boardOpen || !row) return;
  const board = element('board');
  board.scrollTop += row.getBoundingClientRect().top - board.getBoundingClientRect().top - 12;
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

// Inspect public knowledge independently of the opponent's still-secret choice for this round.
function enemyBooks(state, current) {
  state.expandedEnemyHands ??= new Set();
  const rows = (current.view.board.enemies ?? []).map(creature => {
    const key = `${current.seat}/${creature.id}`;
    const row = document.createElement('details');
    row.className = 'hand-row enemy-book';
    row.open = state.expandedEnemyHands.has(key);
    row.addEventListener('toggle', () => {
      if (row.open) state.expandedEnemyHands.add(key);
      else state.expandedEnemyHands.delete(key);
    });
    const spells = creature.knownSpells ?? [];
    const title = document.createElement('summary');
    title.className = 'hand-who';
    title.textContent = `Creature ${creature.id} · ${spells.length} revealed spells`;
    const cards = document.createElement('div');
    cards.className = 'held-cards';
    cards.dataset.scroll = `enemy-hand-${key}`;
    cards.append(...spells.map(spell => heldCard(state, { spell, castable: false }, false, creature.id, current, true)));
    if (spells.length === 0) {
      const empty = document.createElement('p');
      empty.className = 'muted';
      empty.textContent = 'No spells revealed yet.';
      cards.append(empty);
    }
    row.append(title, cards);
    return row;
  });
  element('enemy-hand').replaceChildren(...rows);
}

function openTalents(state) {
  state.talentOpener = document.activeElement;
  showTab(state, 'mat');
  state.clampTalentWindow?.();
  element('talent-grip').focus({ preventScroll: true });
}

// The atlas follows authored package prerequisites and uses only server offers for purchases.
function renderMat(state, current) {
  const view = current.view;
  const allies = view.board.allies ?? [];
  const firstOffered = view.waitingFor === 'Evolution' ? view.options.evolution?.creatures?.[0]?.creature : null;
  const preferred = state.inspectCreature ?? state.evolving ?? firstOffered ?? view.waitingCreature;
  const creature = allies.find(one => one.id === preferred) ?? allies[0];
  const evolution = isAsked(view) && view.waitingFor === 'Evolution' ? view.options.evolution : null;
  const classes = talentClasses(state.catalogue, state.cards, creature, evolution);
  const toolbar = document.createElement('div');
  toolbar.className = 'talent-toolbar';
  const heading = document.createElement('h2');
  heading.textContent = creature ? `Creature ${creature.id} · talents` : 'Choose your path';
  const context = document.createElement('span');
  context.className = 'atlas-context';
  context.textContent = evolution
    ? `Round ${view.board.roundNumber} · Evolution`
    : `Round ${view.board.roundNumber} · Spellbook reference`;
  heading.append(context);
  const help = document.createElement('p');
  help.className = 'muted';
  help.textContent = 'Tier 1 → Tier 2 → Tier 3. Select a package to inspect all its spells. Lines show required packages; one pick buys the whole package.';
  const picker = document.createElement('div');
  picker.className = 'creature-picker';
  picker.setAttribute('aria-label', 'Inspect talent progress');
  for (const ally of allies) {
    const choice = button(`Creature ${ally.id}`, () => {
      state.inspectCreature = ally.id;
      if (evolution?.creatures?.some(one => one.creature === ally.id)) state.evolving = ally.id;
      redraw(state);
    });
    choice.classList.toggle('chosen', ally === creature);
    choice.setAttribute('aria-pressed', String(ally === creature));
    choice.dataset.focus = `talent-creature-${ally.id}`;
    picker.append(choice);
  }
  const filter = document.createElement('select');
  filter.setAttribute('aria-label', 'Inspect a package');
  filter.dataset.focus = 'talent-class';
  for (const name of ['', ...classes.map(group => group.id)]) {
    const option = document.createElement('option');
    option.value = name;
    option.textContent = classes.find(group => group.id === name)?.name || 'All packages';
    filter.append(option);
  }
  filter.value = classes.some(group => group.id === state.inspectClass) ? state.inspectClass : '';
  filter.addEventListener('change', () => { state.inspectClass = filter.value; state.revealPackage = true; redraw(state); });
  toolbar.append(heading, help, picker, filter);
  if (evolution) toolbar.append(evolutionBudget(state, view));
  const forest = packageForest(state.catalogue);
  const graph = document.createElement('div');
  graph.className = 'tree-map';
  graph.dataset.scroll = 'talent-map';
  graph.setAttribute('aria-label', 'Package prerequisites');
  const roots = document.createElement('ul');
  roots.className = 'tree-roots';
  roots.append(...forest.map(node => treeNode(state, node, classes, creature)));
  graph.append(roots);
  // The overview is compact; full spell faces appear for the selected class only.
  const selected = classes.find(group => group.id === filter.value);
  const detail = atlasInspector(state, selected, graph, current, creature);
  if (classes.length === 0) help.textContent = 'The talent catalogue is not available yet.';
  element('mat').replaceChildren(toolbar, graph, detail);
  if (state.revealPackage && selected && globalThis.innerWidth <= 760) detail.scrollIntoView({ block: 'start' });
  state.revealPackage = false;
}

// On a phone the graph is a long list, so the package it opens is brought up with the way back to the list.
function atlasInspector(state, selected, graph, current, creature) {
  const detail = document.createElement('div');
  detail.className = 'talent-inspector';
  if (selected) {
    const back = button('↑ All packages', () => graph.scrollIntoView({ block: 'start' }));
    back.className = 'atlas-back reference-link';
    detail.append(back, talentLane(state, selected, current, creature));
    return detail;
  }
  const prompt = document.createElement('p');
  prompt.className = 'atlas-prompt';
  prompt.textContent = 'Select a package above to see its spells, initiative bonus and prerequisites.';
  detail.append(prompt);
  return detail;
}

function talentLane(state, group, current, creature) {
  const lane = document.createElement('section');
  lane.className = 'talent-lane';
  lane.style.setProperty('--class-color', classColour(group.id, state.palette));
  const title = document.createElement('h2');
  title.className = 'talent-class';
  title.textContent = `${group.name} · Tier ${group.level}`;
  const summary = document.createElement('p');
  summary.className = 'package-summary';
  const requires = (group.prerequisites ?? []).map(id => state.packages.get(id)?.name ?? id).join(' + ');
  summary.textContent = `↟ ${group.initiativeBonus >= 0 ? '+' : ''}${group.initiativeBonus} initiative on purchase · Requires: ${requires || 'No prerequisite package'}`;
  const status = document.createElement('p');
  status.className = 'talent-status';
  status.textContent = group.status === 'known' ? '✓ Package acquired' : group.status === 'available' ? '+ Available now · 1 team pick'
    : 'Not available this opportunity · check prerequisites and creature eligibility';
  lane.append(title, summary, status);
  if (group.status === 'available') {
    const buy = button(`Buy ${group.name} for creature ${creature.id}`, () => buyPackage(state, current, creature.id, group.id));
    buy.disabled = state.sending;
    buy.className = 'atlas-unlock';
    buy.dataset.focus = `atlas-buy-${creature.id}-${group.id}`;
    lane.append(buy);
  }
  const column = document.createElement('div');
  column.className = 'talent-tier';
  column.style.setProperty('--tier-columns', Math.min(2, group.spells.length));
  for (const spell of group.tiers[0].spells) {
    const face = document.createElement('article');
    face.className = `card talent-card ${spell.status}`;
    const known = document.createElement('span');
    known.className = 'talent-status';
    known.textContent = spell.status === 'known' ? '✓ Spell known' : 'Included in this package';
    face.append(known);
    const parts = cardParts(state, spell.spell, '');
    if (parts) face.append(...parts);
    else { const label = document.createElement('p'); label.textContent = spell.spell; face.append(label); }
    column.append(face);
  }
  lane.append(column);
  return lane;
}

function buyPackage(state, current, creature, tier) {
  if (!canInteract(state, current, 'Evolution')) return;
  const offer = current.view.options.evolution?.creatures?.find(one => one.creature === creature);
  if (offer?.availableTiers?.includes(tier)) return submit(state, current, { kind: 'Evolution', creature, tier });
}

// The opponent's next action this seat has not read, as a one-action replay: where it sits among the round's
// actions, so the battlefield can show the board as it stood right after it, and how many are left to read.
// Nothing is held for a bot's seat -- nobody is reading it.
function stepOf(state, current) {
  const view = current.view;
  if (view.playedByBot || state.playback) return null;
  if (!state.stepSeen.has(current.seat)) state.stepSeen.set(current.seat, stepStart(view.roundEvents, view.board, state.cards));
  const steps = opponentSteps(view.roundEvents, state.stepSeen.get(current.seat), view.board, state.cards);
  if (steps.length === 0) return null;
  const [action] = steps;
  const actions = resolvedSince(view.roundEvents, undefined, view.board, state.cards).filter(one => one.round === action.round);
  return {
    seat: current.seat, round: action.round, actions, index: actions.findIndex(one => one.sequence === action.sequence),
    stage: 'after', action, left: steps.length, last: steps.at(-1).sequence,
  };
}

function advanceStep(state, seat, sequence) {
  clearTimeout(state.stepTimer);
  state.stepTimerFor = null;
  if (sequence <= (state.stepSeen.get(seat) ?? -1)) return;
  state.stepSeen.set(seat, sequence);
  redraw(state);
}

// The opponent's action, one at a time, above the question it holds back. Auto OKs every opponent action of
// that round after a pause, including the ones still to come; it ends with the round.
function renderStep(state, current) {
  const step = state.step;
  element('combat-step').hidden = !step;
  element('planning').classList.toggle('stepping', Boolean(step));
  if (!step) {
    clearTimeout(state.stepTimer);
    state.stepTimerFor = null;
    return;
  }
  // Auto belongs to the seat that asked for it: in hotseat the other player reads their own actions by hand.
  const auto = state.stepAuto?.seat === step.seat && state.stepAuto.round === step.round;
  if (!auto && state.stepTimerFor !== null) {
    clearTimeout(state.stepTimer);
    state.stepTimerFor = null;
  }
  const asked = isAsked(current.view) && !current.view.over;
  element('combat-step-count').textContent = `Round ${step.round} · opponent action${step.left > 1 ? ` · ${step.left} to read` : ''}${auto ? ' · auto' : ''}`;
  element('combat-step-action').replaceChildren(recapRow(step.action));
  let label = 'OK';
  if (step.left > 1) label = 'OK · next action →';
  else if (asked) label = 'OK · your move →';
  const next = button(label, () => advanceStep(state, step.seat, step.action.sequence));
  next.dataset.focus = 'step-next';
  const fast = button(auto ? 'Stop auto' : 'Auto this round ▸▸', () => {
    state.stepAuto = auto ? null : { seat: step.seat, round: step.round };
    if (auto) redraw(state);
    else advanceStep(state, step.seat, step.action.sequence);
  });
  fast.className = 'secondary';
  fast.dataset.focus = 'step-auto';
  fast.setAttribute('aria-pressed', String(auto));
  const controls = [next, fast];
  if (step.left > 1) {
    const skip = button('Skip all', () => advanceStep(state, step.seat, step.last));
    skip.className = 'secondary';
    skip.dataset.focus = 'step-skip';
    controls.push(skip);
  }
  element('combat-step-controls').replaceChildren(...controls);
  if (auto && state.stepTimerFor !== step.action.sequence) {
    clearTimeout(state.stepTimer);
    state.stepTimerFor = step.action.sequence;
    state.stepTimer = setTimeout(() => advanceStep(state, step.seat, step.action.sequence), StepPause);
  }
}

// The round bar's one line of play-by-play: the opponent action being read, else the action resolved last while
// combat runs -- the player's own included, which resolved the moment its targets were confirmed (ADR 0083).
function renderCombatLine(state, view) {
  const action = state.step?.action ?? (state.playback ? null : lastResolved(view.roundEvents, view.board, state.cards));
  const line = element('combat-line');
  line.hidden = !action;
  line.dataset.pending = String(Boolean(state.step));
  const text = document.createElement('span');
  text.textContent = action ? lastResolvedText(action) : '';
  line.replaceChildren(text);
  line.title = text.textContent;
}

// The speeds both sides chose and the order they make, from the round bar. Below a laptop it opens by itself once
// a round, when the timeline first exists (speeds reveal together), and closes itself after a few seconds or on a
// tap: it is read, not worked in. Muting the phase pop-ups does not silence it -- it is the one place a phone
// shows the opponent's speeds without opening the battlefield.
function renderOrder(state, view, seat) {
  const panel = element('order');
  const reveal = speedReveal(view.board);
  panel.hidden = reveal.order.length === 0 || Boolean(view.over);
  if (panel.hidden) {
    panel.open = false;
    return;
  }
  const speeds = choices => choices.map(one => `Creature ${one.creature} ${one.speed}`).join(' · ') || '—';
  element('order-label').textContent = 'Turn order';
  element('order-title').textContent = `Round ${view.board.roundNumber} · speeds revealed`;
  element('order-theirs').textContent = `Opponent: ${speeds(reveal.theirs)}`;
  element('order-mine').textContent = `You: ${speeds(reveal.mine)}`;
  element('order-list').replaceChildren(...reveal.order.map(slot => {
    const item = document.createElement('li');
    item.className = `order-slot ${slot.side}${slot.isNow ? ' now' : ''}`;
    const position = document.createElement('span');
    position.className = 'turn-order';
    position.textContent = slot.position;
    position.style.setProperty('--turn-color', turnColour(slot.position, reveal.order.length));
    const who = document.createElement('strong');
    who.textContent = `Creature ${slot.creature}`;
    const detail = document.createElement('span');
    detail.textContent = [slot.side === 'ally' ? 'yours' : 'opponent', slot.speed, `initiative ${slot.initiative}`, slot.roll, slot.isNow && 'acting now']
      .filter(Boolean).join(' · ');
    item.append(position, who, detail);
    return item;
  }));
  state.orderShown ??= new Map();
  const key = `${view.board.roundNumber}`;
  // Not over an opponent action being read, nor over the tie order's own creature buttons: it waits for the
  // next question instead.
  if (state.orderShown.get(seat) === key || state.step || (isAsked(view) && view.waitingFor === 'TieOrder')) return;
  state.orderShown.set(seat, key);
  // A laptop has the turn order on screen beside the battlefield already; the pop-up would cover the desk.
  if (globalThis.innerWidth >= 1100) return;
  panel.open = true;
  clearTimeout(state.orderTimer);
  state.orderTimer = setTimeout(() => { element('order').open = false; }, OrderPause);
}

// Where the opponent action being read was played from, for the round bar.
function stepTurnText(step, board) {
  const slot = (board.timeline ?? []).findIndex(one => one.creature === step.action.actor.id);
  return slot < 0 ? '' : `Turn ${slot + 1} of ${board.timeline.length} · Creature ${step.action.actor.id}`;
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

// A completed round stays readable through the following round, outside the Battlefield/Talents tabs.
// New recaps stay closed and never move the table. Opening the floating panel is the player's choice.
function renderRecap(state, view) {
  const panel = element('recap');
  state.recaps ??= new Map();
  const previous = state.recaps.get(state.recapSeat);
  if (previous) previous.open = panel.open;
  const recap = roundRecap(view.roundEvents, view.board, state.cards);
  panel.hidden = recap === null;
  if (recap === null) return;

  const held = state.recaps.get(state.shown);
  const fresh = held?.round !== recap.round;
  panel.open = fresh ? false : held.open;
  state.recaps.set(state.shown, { round: recap.round, open: panel.open });
  state.recapSeat = state.shown;
  element('recap-title').textContent = `Round ${recap.round} recap`;
  element('recap-count').textContent = `${recap.actions.length} ${recap.actions.length === 1 ? 'cast' : 'casts'}`;
  const rows = recap.actions.map(action => recapRow(action));
  if (rows.length) {
    const replay = document.createElement('li');
    replay.append(button('Replay action by action', () => {
      startPlayback(state, state.shown, recap);
      panel.open = false;
      redraw(state);
      element('playback').scrollIntoView({ block: 'nearest', behavior: 'instant' });
    }));
    rows.unshift(replay);
  }
  if (rows.length === 0) {
    const empty = document.createElement('li');
    empty.className = 'muted';
    empty.textContent = 'No casts resolved this round.';
    rows.push(empty);
  }
  element('recap-actions').replaceChildren(...rows);
}

// A replay is only ever the player's choice, from the recap: each action was shown live as it resolved
// (ADR 0083), so the end of a round no longer plays them back. The next question is acknowledged only on exit;
// historical frames never replace the live view.
function syncPlayback(state, current) {
  if (state.playback && state.playback.seat !== current.seat) state.playback = null;
}

function startPlayback(state, seat, recap) {
  state.playback = { seat, round: recap.round, actions: recap.actions, index: 0, stage: recap.actions[0]?.frame ? 'before' : 'after' };
  state.picked = [];
  state.chosen = null;
  hidePhaseNotice(state);
  element('upkeep').open = false;
  element('order').open = false;
  element('announcements').open = false;
  element('phase-progress').open = false;
  if (state.tab === 'mat') closeTalents(state);
}

function finishPlayback(state) {
  state.playback = null;
  state.guidedAsking = null;
  redraw(state);
  element('asking').setAttribute('tabindex', '-1');
  element('asking').focus({ preventScroll: true });
}

function movePlayback(state, step) {
  const replay = state.playback;
  if (!replay) return;
  if (replay.actions[replay.index].frame && ((step > 0 && replay.stage === 'before') || (step < 0 && replay.stage === 'after'))) {
    replay.stage = step > 0 ? 'after' : 'before';
    redraw(state);
    return;
  }
  if (replay.index === 0 && step < 0) return;
  if (replay.index + step >= replay.actions.length) { finishPlayback(state); return; }
  replay.index = Math.max(0, replay.index + step);
  replay.stage = step > 0 && replay.actions[replay.index].frame ? 'before' : 'after';
  redraw(state);
}

function renderPlayback(state, current) {
  const replay = state.playback;
  const action = replay.actions[replay.index];
  const before = action.frame && replay.stage === 'before';
  const stage = before ? 'Before' : 'After';
  const position = `Action ${replay.index + 1} of ${replay.actions.length}`;
  element('phase-round').textContent = `Round ${replay.round}`;
  element('upkeep').hidden = true;
  element('phase-current').textContent = 'Round replay';
  element('phase-turn').textContent = `Action ${replay.index + 1} of ${replay.actions.length} · ${action.actor.label}`;
  element('phase-reminder').textContent = 'Next: apply / advance · Previous: review again · Skip: return to the match';
  setPhase(`Round ${replay.round} · Round replay`, true);
  element('playback-title').textContent = `${action.actor.label} ${before ? 'is about to act' : 'acted'}`;
  element('playback-count').textContent = action.frame ? `${position} · ${stage}` : position;
  element('playback-board-note').textContent = action.frame
    ? `${stage} action ${replay.index + 1} · recorded battlefield · cleanup and upkeep appear when you return to the match.`
    : 'This recording has no action snapshots · battlefield totals show the current state.';
  const held = element('playback-action');
  held.replaceChildren(recapRow(before ? { ...action, status: 'Ready', effects: [], reason: 'Press Next to apply this recorded action.', dropped: [] } : action));
  animatePlayback(state, held);
  const previous = button('← Previous', () => movePlayback(state, -1));
  previous.dataset.focus = 'playback-previous';
  previous.disabled = replay.index === 0 && (before || !action.frame);
  const last = replay.index === replay.actions.length - 1;
  const onward = current.view.over ? 'Match results' : `Continue to round ${current.view.board.roundNumber}`;
  let nextLabel = last ? onward : 'Next action →';
  if (before) nextLabel = 'Next: apply action →';
  const next = button(nextLabel, () => movePlayback(state, 1));
  next.dataset.focus = 'playback-next';
  const skip = button(current.view.over ? 'Skip to results' : `Skip to round ${current.view.board.roundNumber}`, () => finishPlayback(state));
  skip.dataset.focus = 'playback-skip';
  element('playback-controls').replaceChildren(previous, next, skip);
}

function animatePlayback(state, held) {
  const replay = state.playback;
  const key = `${replay.seat}/${replay.round}/${replay.index}/${replay.stage}`;
  if (state.playbackFrame === key) return;
  state.playbackFrame = key;
  if (!globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches) held.animate?.([{ opacity: .25 }, { opacity: 1 }], { duration: 240 });
}

function recapPerson(person) {
  const name = document.createElement('span');
  name.className = `recap-person ${person.side}`;
  name.textContent = `${person.label} · ${person.side === 'ally' ? 'yours' : person.side === 'enemy' ? 'opponent' : 'unknown side'}`;
  return name;
}

function recapRow(action) {
  const row = document.createElement('li');
  row.className = `recap-action ${action.actor.side}`;
  const head = document.createElement('div');
  head.className = 'recap-action-head';
  const spell = document.createElement('strong');
  spell.className = 'recap-spell';
  spell.textContent = action.spell;
  const status = document.createElement('span');
  status.className = `recap-status ${action.status.toLowerCase()}`;
  status.textContent = action.status;
  head.append(spell, status);
  const path = document.createElement('div');
  path.className = 'recap-path';
  path.append(recapPerson(action.actor));
  const arrow = document.createElement('span');
  arrow.textContent = '→';
  arrow.setAttribute('aria-label', 'targets');
  path.append(arrow);
  if (action.targets.length === 0) {
    const none = document.createElement('span');
    none.textContent = 'No targets';
    path.append(none);
  }
  path.append(...action.targets.map(recapPerson));
  row.append(head, path);
  const effects = document.createElement('div');
  effects.className = 'recap-effects';
  for (const effect of action.effects) {
    const chip = document.createElement('span');
    chip.className = `recap-effect ${effect.tone}`;
    chip.textContent = `${effect.text} → ${effect.target.label}`;
    effects.append(chip);
  }
  row.append(effects);
  if (action.reason || action.effects.length === 0) {
    const reason = document.createElement('p');
    reason.className = 'muted';
    reason.textContent = action.reason || 'No effects applied.';
    row.append(reason);
  }
  for (const dropped of action.dropped) {
    const reason = document.createElement('p');
    reason.className = 'recap-skipped';
    reason.textContent = `Skipped target · ${dropped}`;
    row.append(reason);
  }
  return row;
}

function renderDecision(state, current) {
  const view = current.view;
  element('planning').dataset.kind = view.waitingFor ?? 'Waiting';
  element('decision-context').textContent = '';
  element('evolution-budget').hidden = view.waitingFor !== 'Evolution' || !isAsked(view);
  element('evolution-budget').replaceChildren(...(view.waitingFor === 'Evolution' && isAsked(view) ? [evolutionBudget(state, view)] : []));
  const asking = element('asking');
  const choices = element('choices');
  element('decision-guide').replaceChildren();
  element('problem').hidden = !state.error;
  element('problem').textContent = state.error;
  element('decision').dataset.kind = view.waitingFor ?? 'Waiting';
  element('decision-state').textContent = view.over ? 'Finished' : view.playedByBot ? 'Bot playing' : isAsked(view) ? 'Your turn' : 'Waiting';
  element('decision-phase').textContent = decisionPhase(view);
  const turn = activeTurn(view.board) ?? plannedTurn(view);
  element('decision-turn').textContent = turn ? `${turn.planned ? 'Acts' : 'Turn'} ${turn.position} of ${turn.total}${turn.slot.speed ? ` · ${turn.slot.speed}` : ''}` : '';
  element('decision-turn').hidden = !turn;
  renderStep(state, current);
  if (state.step) {
    stepHeading(state, current);
    return;
  }
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

  const actor = (view.board.allies ?? []).find(creature => creature.id === view.waitingCreature);
  element('decision-context').textContent = actor
    ? `${healthText(actor)} HP · ${actor.energy ?? 0} energy`
    : '';
  asking.textContent = titleOf(state, view);
  const buttons = buttonsFor(state, current);
  // Spell details already live in the scrollable hand. A selected spell must not grow the fixed decision
  // above it: that moves the rows under the player's finger and makes the confirmation easy to miss.
  element('decision-guide').replaceChildren(...(view.waitingFor === 'Intent'
    ? [] : [guidancePanel(document, view, state.chosen, state.picked)]));
  choices.replaceChildren(...buttons);
}

// While an opponent action is read, the sheet's heading is that action's -- its turn, its caster, its spell --
// and the seat's own question is named underneath as what comes next. A heading that kept the question's turn
// would say "turn 6 of 6" over turns 3, 4 and 5.
function stepHeading(state, current) {
  const view = current.view;
  const step = state.step;
  const board = step.action.frame ? { timeline: step.action.frame.timeline } : view.board;
  const slot = (board.timeline ?? []).findIndex(one => one.creature === step.action.actor.id);
  element('decision-phase').textContent = "Opponent's turn";
  element('decision-turn').textContent = slot < 0 ? '' : `Turn ${slot + 1} of ${board.timeline.length}`;
  element('decision-turn').hidden = slot < 0;
  element('decision-state').textContent = 'Reading';
  element('asking').textContent = `${step.action.actor.label} · ${step.action.spell}`;
  element('choices').replaceChildren();
  const next = isAsked(view) && !view.over && !view.playedByBot;
  const turn = activeTurn(view.board) ?? plannedTurn(view);
  element('decision-context').textContent = next
    ? `Then your move: ${titleOf(state, view)}${turn ? ` (turn ${turn.position} of ${turn.total})` : ''}.`
    : '';
}

function titleOf(state, view) {
  switch (view.waitingFor) {
    case 'Evolution': return `Creature ${state.evolving ?? view.options.evolution?.creatures?.[0]?.creature ?? '—'} · buy a package`;
    case 'Speed': return `Creature ${view.waitingCreature} · choose speed`;
    case 'Intent': return `Creature ${view.waitingCreature} · choose spell`;
    case 'TieOrder': return 'Tied: which of your creatures acts first?';
    case 'Target': {
      const spell = view.options.target?.spell;
      return `Creature ${view.waitingCreature} · ${cardTitle(state.cards.get(spell)) || spell || 'Choose targets'}`;
    }
    default: return 'Your move';
  }
}

function decisionPhase(view) {
  if (view.over) return 'Match complete';
  const phases = { Evolution: 'Evolution', Speed: 'Choose speed', TieOrder: 'Order tied creatures', Intent: 'Choose spell', Target: 'Targeting' };
  return phases[view.waitingFor] ?? (view.board.subPhase === 'Activation' ? 'Combat' : 'Waiting');
}

function activeTurn(board) {
  const cursor = cursorOf(board);
  const slot = board.timeline?.[cursor];
  return slot ? { slot, position: cursor + 1, total: board.timeline.length } : null;
}

// Where the creature being asked for a spell will act. The timeline is built before intents are declared, so the
// order is already known while a spell is chosen for it.
function plannedTurn(view) {
  if (!isAsked(view) || view.waitingFor !== 'Intent') return null;
  const index = (view.board.timeline ?? []).findIndex(slot => slot.creature === view.waitingCreature);
  return index < 0 ? null : { slot: view.board.timeline[index], position: index + 1, total: view.board.timeline.length, planned: true };
}

function evolutionBudgetText(state, view) {
  const remaining = view.options.evolution?.remainingPicks ?? 0;
  const used = (view.board.evolutionChoices ?? []).length;
  const total = used + remaining;
  return `${remaining} / ${total} team picks remaining`;
}

function turnColour(position, count) {
  const fraction = (position - 1) / Math.max(1, count - 1);
  return `hsl(${Math.round(170 + fraction * 90)} 58% 76%)`;
}

function evolutionBudget(state, view) {
  const box = document.createElement('div');
  box.className = 'pick-budget';
  const title = document.createElement('strong');
  title.textContent = evolutionBudgetText(state, view);
  const used = view.board.evolutionChoices ?? [];
  const remaining = view.options.evolution?.remainingPicks ?? 0;
  const picks = document.createElement('div');
  picks.className = 'pick-tokens';
  for (const [index, choice] of used.entries()) {
    const token = document.createElement('span');
    token.className = 'pick-token spent';
    token.textContent = `✓ Pick ${index + 1} · Creature ${choice.creature}`;
    picks.append(token);
  }
  for (let index = 0; index < remaining; index++) {
    const token = document.createElement('span');
    token.className = 'pick-token available';
    token.textContent = `Pick ${used.length + index + 1} · available`;
    picks.append(token);
  }
  const note = document.createElement('span');
  note.className = 'pick-note';
  note.textContent = 'Shared team picks · at most one package per creature this opportunity';
  box.append(title, picks, note);
  return box;
}

// Public ownership is read from the board. Collect both team picks before the phase changes so the notice
// names the whole opportunity, even when the host serves each purchase as a separate asking. Creature ids are the
// match's, not a seat's, so one record serves both seats of a hotseat page: a purchase seen through either seat
// is in the recap each seat gets once the opportunity is over, whoever held the device when it was bought.
function unlockedPackages(state, seat, board, phaseVisible) {
  if (state.packageRound?.round !== board.roundNumber) {
    state.packageRound = { round: board.roundNumber, tiers: new Map(), unlocked: [], opportunity: false, recapped: new Set() };
  }
  const record = state.packageRound;
  for (const creature of [...(board.allies ?? []), ...(board.enemies ?? [])]) {
    const tiers = creature.acquiredTiers ?? [];
    const seen = record.tiers.get(creature.id);
    for (const tier of seen ? tiers.filter(one => !seen.includes(one)) : []) record.unlocked.push({ creature: creature.id, tier });
    record.tiers.set(creature.id, [...tiers]);
  }
  const evolving = board.subPhase === 'Evolution';
  record.opportunity ||= evolving;
  if (!record.opportunity || evolving || !phaseVisible || record.recapped.has(seat)) return [];
  record.recapped.add(seat);
  const allies = new Set((board.allies ?? []).map(creature => creature.id));
  return record.unlocked.map(one => ({ ...one, side: allies.has(one.creature) ? 'yours' : 'opponent' }));
}

function renderPhaseGuide(state, view, seat) {
  const phases = [
    ['Upkeep', [], 'Round upkeep is automatic.'],
    ['Evolve', ['Evolution'], 'Buy whole packages with shared team picks. At most one per creature this opportunity, or pass.'],
    ['Speed', ['Speed', 'TurnOrderResolution'], 'Pick a speed for each eligible creature. All speeds reveal together when everyone is done.'],
    ['Tie order', ['TieOrder'], 'The d20 settled places between teams. Order your own tied creatures; both orders reveal together.'],
    ['Spells', ['IntentSelection'], 'Declare one spell per creature. Opposing choices stay hidden.'],
    ['Combat', ['Activation'], 'In turn order, each spell is revealed, its owner chooses targets, and it resolves at once. A creature that cannot act fizzles.'],
    ['Cleanup', ['Cleanup', 'Finalization'], 'Conditions count down, then the next round begins.'],
  ];
  const current = view.board.phase === 'StartOfRound' ? 0 : phases.findIndex(([, names]) => names.includes(view.board.subPhase));
  element('phase-round').textContent = `Round ${view.board.roundNumber ?? '—'} / ${state.catalogue?.rules?.roundCap ?? '—'}`;
  element('phase-current').textContent = view.over ? 'Match complete' : phases[current]?.[0] ?? 'Waiting';
  const turn = activeTurn(view.board);
  element('phase-turn').textContent = turn ? `Turn ${turn.position} of ${turn.total} · Creature ${turn.slot.creature}` : '';
  element('phase-steps').replaceChildren(...phases.map(([label], index) => {
    const step = document.createElement('li');
    step.textContent = label;
    step.className = index === current ? 'current' : index < current ? 'complete' : '';
    if (index === current) step.setAttribute('aria-current', 'step');
    return step;
  }));
  element('phase-reminder').textContent = view.over ? 'Match finished. Open the recap to review the final round.'
    : `${phases[current]?.[2] ?? 'Waiting for the next phase.'}${view.board.nextEvolutionRound > view.board.roundNumber ? ` Next evolution: round ${view.board.nextEvolutionRound}.` : ''}`;
  const upkeep = roundUpkeep(view.roundEvents ?? view.feed, view.board.roundNumber);
  renderUpkeep(state, view, upkeep, seat);
  const unlocked = unlockedPackages(state, seat, view.board, current >= 0 || view.over);
  const key = `${view.board.roundNumber}/${view.over ? 'over' : current}`;
  state.phaseSeen ??= new Map();
  const previous = state.phaseSeen.get(seat);
  renderAnnouncements(state, seat);
  if (current < 0 && !view.over) return;
  if (previous?.key === key) return;
  state.phaseSeen.set(seat, { key, round: view.board.roundNumber });
  const newRound = previous?.round !== view.board.roundNumber;
  const begins = Boolean(previous) && newRound && !view.over;
  const label = view.over ? 'Match complete' : phases[current]?.[0] ?? 'Waiting';
  const title = unlocked.length ? `Round ${view.board.roundNumber} · Packages unlocked`
    : begins ? `Round ${view.board.roundNumber} begins` : newRound && upkeep && !view.over ? `Round ${upkeep.round} · Upkeep complete → ${label}`
      : `Round ${view.board.roundNumber} · ${label}`;
  const detail = `${begins ? `Now: ${label}. ` : ''}${element('phase-reminder').textContent}`;
  const changes = unlocked.length ? unlocked.map(one => ({ who: `Creature ${one.creature} · ${one.side}`, text: state.packages.get(one.tier)?.name ?? one.tier, tone: one.side }))
    : newRound && upkeep ? upkeepChanges(view.board, upkeep) : [];
  const entry = { title, detail, changes, upkeep: newRound && upkeep ? upkeepPreview(state) : '',
    steps: newRound && upkeep ? upkeepSteps(state, view.board, upkeep) : [], newRound: begins };
  state.announcements ??= new Map();
  state.announcements.set(seat, [...(state.announcements.get(seat) ?? []), entry].slice(-12));
  renderAnnouncements(state, seat);
  showPhaseNotice(state, entry);
}

function upkeepEnergy(state) {
  const amount = state.catalogue?.rules?.energyPerRound;
  return Number.isInteger(amount) ? `+${amount} energy per creature alive at round start.` : 'Round energy applied by the host.';
}

function upkeepPreview(state) {
  return `${upkeepEnergy(state)} Open Upkeep for details.`;
}

function activeConditions(board) {
  return [...(board.allies ?? []), ...(board.enemies ?? [])].flatMap(creature => (creature.conditions ?? [])
    .map(condition => ({ who: `Creature ${creature.id}`,
      text: chipText(condition) ? `${chipText(condition)} · ${remainingText(condition)}` : '', tone: 'active' }))
    .filter(one => one.text));
}

function remainingText(condition) {
  const lane = laneOf(condition);
  if (lane === null) return 'permanent';
  const remaining = condition.remainingRounds;
  return `${lane === 'new' ? 'new · ' : ''}${remaining} ${remaining === 1 ? 'round' : 'rounds'} left`;
}

// Applied healing and damage are host events, in their actual order. Energy is context on the first step,
// never a click per creature. The final step reads current condition counters from the board rather than
// pretending upkeep itself decremented them (the countdown happens during cleanup).
function upkeepSteps(state, board, upkeep) {
  const ticks = upkeep.rows.filter(row => row.tone !== 'energy' && row.amount > 0);
  const conditions = activeConditions(board);
  if (!ticks.length && !conditions.length) return [];
  const steps = ticks.map(row => ({
    label: row.label,
    changes: [{ who: `Creature ${row.creature}`, text: `${row.sign}${row.amount} ${row.unit}`, tone: row.tone }],
  }));
  if (conditions.length) steps.push({ label: 'Conditions remaining', changes: conditions });
  steps[0].label = `${upkeepEnergy(state)} ${steps[0].label}`;
  return steps;
}

function upkeepChanges(board, upkeep) {
  const applied = new Map();
  for (const row of upkeep.rows.filter(one => one.amount > 0)) {
    const who = `Creature ${row.creature}`;
    const parts = applied.get(who) ?? [];
    parts.push(`${row.sign}${row.amount} ${row.unit} · ${row.label}`);
    applied.set(who, parts);
  }
  const lasting = new Map();
  for (const condition of activeConditions(board)) {
    const parts = lasting.get(condition.who) ?? [];
    parts.push(condition.text);
    lasting.set(condition.who, parts);
  }
  return [
    ...[...applied].map(([who, parts]) => ({ who, text: parts.join(' · '), tone: 'applied' })),
    ...[...lasting].map(([who, parts]) => ({ who, text: `${parts.join(' · ')} · still active`, tone: 'active' })),
  ];
}

function renderUpkeep(state, view, upkeep, seat) {
  const panel = element('upkeep');
  panel.hidden = !upkeep;
  element('upkeep-conditions').hidden = !upkeep;
  const key = `${seat}/${view.board.roundNumber}`;
  if (state.upkeepShown !== key) panel.open = false;
  state.upkeepShown = key;
  if (!upkeep) return;
  element('upkeep-label').textContent = `Upkeep${upkeep.rows.length ? ` · ${upkeep.rows.length} ${upkeep.rows.length === 1 ? 'effect' : 'effects'}` : ''} ↗`;
  element('upkeep-title').textContent = `Round ${upkeep.round} · Upkeep applied`;
  element('upkeep-energy').textContent = upkeepEnergy(state);
  const rows = upkeep.rows.map(row => {
    const item = document.createElement('li');
    item.dataset.tone = row.tone;
    const who = document.createElement('strong');
    who.textContent = `Creature ${row.creature}`;
    const amount = document.createElement('b');
    amount.textContent = `${row.sign}${row.amount} ${row.unit}`;
    const why = document.createElement('span');
    why.textContent = `${row.label}${row.amount === 0 ? ' · no change' : ''}`;
    item.append(who, amount, why);
    return item;
  });
  if (!rows.length) {
    const empty = document.createElement('li');
    empty.textContent = 'No ongoing effects this round.';
    rows.push(empty);
  }
  element('upkeep-effects').replaceChildren(...rows);
  const conditions = activeConditions(view.board);
  const active = element('upkeep-conditions');
  active.hidden = conditions.length === 0;
  active.replaceChildren(...(conditions.length ? [
    Object.assign(document.createElement('strong'), { textContent: 'Still active after upkeep' }),
    ...conditions.map(one => {
      const row = document.createElement('p');
      row.textContent = `${one.who} · ${one.text}`;
      return row;
    }),
  ] : []));
}

function hidePhaseNotice(state) {
  clearTimeout(state.phaseTimer);
  state.phaseMotion?.cancel();
  if (element('phase-notice').contains?.(document.activeElement)) element('announcements-label').focus({ preventScroll: true });
  element('phase-notice').hidden = true;
  state.noticeEntry = null;
}

function showPhaseNotice(state, entry, replay = false) {
  hidePhaseNotice(state);
  // Muted, a phase change is still listed under Announcements, and an earlier one can still be opened from it.
  if (state.quiet && !replay) return;
  // One pop-up at a time: the turn order read at the reveal gives way to the next phase.
  element('order').open = false;
  state.noticeEntry = entry;
  state.noticeReplay = replay;
  state.noticeStep = 0;
  state.noticePinned = replay || entry.steps?.length > 1;
  state.noticeDuration = entry.newRound ? 8000 : 6000;
  const notice = element('phase-notice');
  notice.dataset.kind = entry.newRound ? 'round' : 'phase';
  renderNoticeContent(state, replay);
  notice.hidden = false;
  if (!globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
    state.phaseMotion = notice.animate?.([{ opacity: 0, transform: 'translateY(8px)' }, { opacity: 1, transform: 'translateY(0)' }], { duration: 280, easing: 'ease-out' });
  }
  schedulePhaseNotice(state);
}

function noticeContext(state, step, replay) {
  const earlier = replay ? 'Earlier announcement · ' : '';
  if (step) return `${earlier}Upkeep · ${state.noticeStep + 1} / ${state.noticeEntry.steps.length}`;
  return replay ? 'Earlier announcement · review' : 'Phase update';
}

function noticeDetail(entry, step, final) {
  if (step) return final ? `${step.label} · ${entry.detail}` : step.label;
  return entry.upkeep ? `${entry.upkeep} ${entry.detail}` : entry.detail;
}

function renderNoticeContent(state, replay = state.noticeReplay) {
  const entry = state.noticeEntry;
  const step = entry.steps?.[state.noticeStep];
  const final = state.noticeStep === entry.steps?.length - 1;
  const stepping = Boolean(step && entry.steps.length > 1);
  element('phase-notice-context').textContent = noticeContext(state, step, replay);
  element('phase-notice-title').textContent = entry.title;
  element('phase-notice-detail').textContent = noticeDetail(entry, step, final);
  const pin = element('phase-notice-pin');
  pin.hidden = stepping;
  pin.textContent = replay ? 'Kept open' : 'Keep open';
  pin.disabled = replay;
  const next = element('phase-notice-next');
  next.hidden = !stepping;
  next.textContent = final ? 'Done ✓' : 'Next effect →';
  // The steps wait for the player, so the way past them is one tap too: Upkeep keeps the whole list.
  element('phase-notice-skip').hidden = !stepping || final;
  const list = element('phase-notice-changes');
  const changes = step?.changes ?? entry.changes ?? [];
  list.hidden = changes.length === 0;
  list.replaceChildren(...changes.map(change => {
    const row = document.createElement('p');
    row.dataset.tone = change.tone;
    const who = document.createElement('strong');
    who.textContent = change.who;
    const value = document.createElement('span');
    value.textContent = change.text;
    row.append(who, value);
    return row;
  }));
}

function schedulePhaseNotice(state) {
  clearTimeout(state.phaseTimer);
  if (state.noticePinned || element('phase-notice').hidden) return;
  state.phaseTimer = setTimeout(() => { element('phase-notice').hidden = true; }, state.noticeDuration);
}

function renderAnnouncements(state, seat) {
  const history = state.announcements?.get(seat) ?? [];
  const key = JSON.stringify([seat, history, state.quiet]);
  if (state.announcementView === key) return;
  if (state.announcementSeat !== seat) element('announcements').open = false;
  state.announcementSeat = seat;
  state.announcementView = key;
  element('announcements-label').textContent = `Announcements · ${history.length}${state.quiet ? ' · muted' : ''}`;
  const mute = document.createElement('li');
  mute.className = 'announcement-mute';
  const toggle = button(state.quiet ? 'Phase pop-ups muted · turn back on' : 'Mute phase pop-ups for this match', () => setQuiet(state, !state.quiet));
  toggle.className = 'secondary';
  toggle.setAttribute('aria-pressed', String(Boolean(state.quiet)));
  mute.append(toggle);
  element('announcement-list').replaceChildren(mute, ...[...history].reverse().map(entry => {
    const item = document.createElement('li');
    item.append(button(entry.title, () => {
      element('announcements').open = false;
      showPhaseNotice(state, entry, true);
      element('phase-notice-close').focus({ preventScroll: true });
    }));
    return item;
  }));
}

function quietTables() {
  try {
    const kept = JSON.parse(storage?.getItem(QuietKey) ?? '[]');
    return Array.isArray(kept) ? kept : [];
  } catch {
    return [];
  }
}

// The phase pop-ups, for the rest of this match. The history stays under Announcements, whose first line turns
// them back on; only the interruption goes.
function setQuiet(state, quiet) {
  state.quiet = quiet;
  if (quiet) hidePhaseNotice(state);
  try {
    const kept = quietTables().filter(key => key !== state.quietKey);
    if (quiet) kept.push(state.quietKey);
    storage?.setItem(QuietKey, JSON.stringify(kept.slice(-8)));
  } catch {
    // A browser that keeps nothing mutes this page until it is reloaded.
  }
  redraw(state);
}

function setupPhaseControls(state) {
  const notice = element('phase-notice');
  element('order-panel').addEventListener('click', () => { element('order').open = false; });
  element('phase-notice-next').addEventListener('click', () => {
    if (!state.noticeEntry) return;
    if (state.noticeStep >= state.noticeEntry.steps.length - 1) return hidePhaseNotice(state);
    state.noticeStep += 1;
    renderNoticeContent(state);
    element('phase-notice-next').focus({ preventScroll: true });
  });
  element('phase-notice-skip').addEventListener('click', () => hidePhaseNotice(state));
  element('phase-notice-mute').addEventListener('click', () => {
    setQuiet(state, true);
    element('announcements-label').focus({ preventScroll: true });
  });
  element('phase-notice-close').addEventListener('click', () => {
    hidePhaseNotice(state);
    element('announcements-label').focus({ preventScroll: true });
  });
  element('phase-notice-pin').addEventListener('click', () => {
    state.noticePinned = true;
    clearTimeout(state.phaseTimer);
    element('phase-notice-pin').textContent = 'Kept open';
    element('phase-notice-pin').disabled = true;
    element('phase-notice-close').focus({ preventScroll: true });
  });
  for (const event of ['mouseenter', 'focusin']) notice.addEventListener(event, () => clearTimeout(state.phaseTimer));
  notice.addEventListener('mouseleave', () => {
    if (!notice.contains(document.activeElement)) schedulePhaseNotice(state);
  });
  notice.addEventListener('focusout', event => {
    if (!notice.contains(event.relatedTarget)) schedulePhaseNotice(state);
  });
  const panels = ['upkeep', 'recap', 'announcements', 'phase-progress', 'order'];
  for (const id of panels) {
    element(id).addEventListener('toggle', () => {
      if (!element(id).open) {
        if (id === 'order') clearTimeout(state.orderTimer);
        return;
      }
      hidePhaseNotice(state);
      for (const other of panels) if (other !== id) element(other).open = false;
    });
  }
  if (globalThis.ResizeObserver) new ResizeObserver(() => {
    element('table').style.setProperty('--phase-height', `${element('phase-dock').getBoundingClientRect().height}px`);
  }).observe(element('phase-dock'));
}

// One button per thing the options offer, and nothing else. A screen that offered more than the options do
// would be inventing a rule; a screen that offered less would be hiding one.
function buttonsFor(state, current) {
  const view = current.view;
  const send = decision => () => submit(state, current, decision);
  switch (view.waitingFor) {
    case 'Evolution': {
      return evolutionButtons(state, current);
    }
    case 'Speed':
      return ['Quick', 'Standard'].map(speed => {
        const choice = button(speed, send({ kind: 'Speed', creature: view.waitingCreature, speed }));
        choice.dataset.focus = `speed-${speed}`;
        return choice;
      });
    case 'TieOrder':
      return tieOrderButtons(state, current);
    case 'Intent':
      return intentButtons(state, current);
    case 'Target':
      return targetButtons(state, current);
    default:
      return [];
  }
}


// Only one creature's packages occupy the sheet at a time; every server-offered creature stays reachable.
function evolutionButtons(state, current) {
  const creatures = current.view.options.evolution?.creatures ?? [];
  const selected = creatures.find(one => one.creature === state.evolving) ?? creatures[0];
  const picker = document.createElement('div');
  picker.className = 'creature-picker';
  picker.setAttribute('aria-label', 'Choose a creature to evolve');
  for (const creature of creatures) {
    const choice = button(`Creature ${creature.creature}`, () => {
      state.evolving = creature.creature;
      state.inspectCreature = creature.creature;
      redraw(state);
    });
    choice.classList.toggle('chosen', creature === selected);
    choice.setAttribute('aria-pressed', String(creature === selected));
    choice.dataset.focus = `evolve-${creature.creature}`;
    picker.append(choice);
  }
  const cards = document.createElement('div');
  cards.className = 'choice-cards';
  // Per creature: another creature's offers start at their top, not at the offset the last one was left at.
  cards.dataset.scroll = `evolution-packages-${selected?.creature}`;
  for (const tier of selected?.availableTiers ?? []) {
    const choice = packageCard(state, tier, () => buyPackage(state, current, selected.creature, tier));
    choice.dataset.focus = `evolve-package-${selected.creature}-${tier}`;
    cards.append(choice);
  }
  const hint = document.createElement('p');
  hint.className = 'choice-help';
  hint.textContent = cards.children.length ? 'Choose a creature, then tap a package to buy it whole.' : 'No package left for this creature. Choose another creature or pass.';
  const pass = button('Pass this pick', () => submit(state, current, { kind: 'Evolution', pass: true }));
  pass.className = 'secondary';
  const explore = button('Talent atlas →', () => openTalents(state));
  explore.className = 'secondary';
  explore.dataset.focus = 'evolution-explorer';
  pass.dataset.focus = 'evolution-pass';
  return [picker, hint, cards, explore, pass];
}

// One package as a tappable card. A pick buys the whole thing, so the card names the whole thing: what it is,
// how deep it sits, the initiative it is worth for the rest of the match, and every spell it teaches. A player
// choosing between packages on the spell names alone would be choosing on a third of what they are buying.
function packageCard(state, tier, onClick) {
  const face = state.packages.get(tier);
  if (!face) {
    return button(tier, onClick);
  }

  const choice = document.createElement('button');
  choice.type = 'button';
  choice.className = 'card package-card';
  choice.style.setProperty('--class-color', classColour(tier, state.palette));
  choice.dataset.focus = `package-${tier}`;
  choice.addEventListener('click', onClick);

  const head = document.createElement('div');
  head.className = 'card-head';
  const title = document.createElement('div');
  title.className = 'card-title';
  title.textContent = face.name;
  const meta = document.createElement('span');
  meta.className = 'card-meta';
  meta.textContent = [`tier ${face.level}`, face.initiativeBonus > 0 ? `+${face.initiativeBonus} initiative` : 'no initiative'].join(' · ');
  title.append(meta);
  head.append(title);

  const body = document.createElement('div');
  body.className = 'card-body';
  body.textContent = face.spells.map(spell => cardTitle(state.cards.get(spell)) || spell).join(' · ');

  const requires = document.createElement('p');
  requires.className = 'package-requires';
  requires.textContent = `Requires: ${(face.prerequisites ?? []).map(id => state.packages.get(id)?.name ?? id).join(' + ') || 'No prerequisite package'}`;
  choice.append(head, body, requires);
  return choice;
}

// The seat's own tied creatures, ordered one tap at a time (ties.js, ADR 0063). The roll-off already gave
// each side its places; the first tap in a tie takes that tie's first place, and the last creature takes the
// last place untapped. Nothing is sent until every tie is settled and the order is confirmed, and the order as
// rolled can be kept in one tap: a tie nobody wants to reorder should cost nothing.
function tieOrderButtons(state, current) {
  const groups = current.view.options.tieOrder?.ties ?? [];
  const tapped = state.ordered ?? [];
  const send = order => () => {
    if (canInteract(state, current, 'TieOrder')) return submit(state, current, { kind: 'TieOrder', order });
  };

  const help = document.createElement('p');
  help.className = 'choice-help';
  help.textContent = 'The dice gave your side its places. Tap your creatures in the order they act.';

  const ties = untapped(groups, tapped).map((left, index) => {
    const row = document.createElement('div');
    row.className = 'creature-picker';
    row.setAttribute('aria-label', `Tie ${index + 1}`);
    const settled = orderOf([groups[index]], tapped);
    const status = document.createElement('p');
    status.className = 'muted';
    const names = settled.map(creature => `creature ${creature}`).join(' → ');
    status.textContent = `Order: ${names}`;
    row.append(status);
    if (left.length > 1) {
      for (const creature of left) {
        const choice = button(`Creature ${creature}`, () => {
          if (!canInteract(state, current, 'TieOrder')) return;
          state.ordered = tap(groups, tapped, creature);
          redraw(state);
        });
        choice.dataset.focus = `tie-${creature}`;
        row.append(choice);
      }
    }
    return row;
  });

  const confirm = button('Confirm this order', send(orderOf(groups, tapped)));
  confirm.disabled = !isSettled(groups, tapped);
  confirm.dataset.focus = 'tie-confirm';
  const keep = button('Keep the order the dice gave', send(orderOf(groups, [])));
  keep.className = 'secondary';
  keep.dataset.focus = 'tie-keep';
  const again = button('Start again', () => {
    if (!canInteract(state, current, 'TieOrder')) return;
    state.ordered = [];
    redraw(state);
  });
  again.className = 'secondary';
  again.dataset.focus = 'tie-reset';
  again.disabled = tapped.length === 0;
  return [help, ...ties, confirm, keep, again];
}

// A card tap only selects. The fixed button names the selected spell and is the explicit commitment: another
// tap on a moving spell row must never submit an irreversible choice.
function intentButtons(state, current) {
  const view = current.view;
  const option = (view.options.intent?.creatures ?? []).find(candidate => candidate.creature === view.waitingCreature);
  const castable = option?.castableSpells ?? [];
  const chosen = castable.includes(state.chosen) ? state.chosen : null;

  const name = chosen === null ? '' : state.cards.get(chosen)?.name ?? chosen;
  const confirm = button(chosen === null ? 'Select a spell' : `Declare ${name}`, () => {
    confirm.disabled = true;
    return declareChosen(state, current);
  });
  confirm.className = 'declare-spell';
  confirm.dataset.focus = 'declare-spell';
  confirm.disabled = chosen === null;
  return [confirm];
}

// A second tap on the chosen card does nothing: the row can move under a finger. A key cannot, so Enter or Space
// on the card that is already chosen declares it, as the fixed button does.
function chooseCard(state, current, spell, keyboard = false) {
  if (!canInteract(state, current, 'Intent')) return;
  const option = current.view.options.intent?.creatures?.find(one => one.creature === current.view.waitingCreature);
  if (!option?.castableSpells?.includes(spell)) return;
  if (state.chosen === spell) return keyboard ? declareChosen(state, current) : undefined;
  // Choosing a card is the player moving on: an announcement still over the decision would hide the Declare
  // button it has just named (a phone's notice sits exactly on it).
  if (!element('phase-notice').hidden) hidePhaseNotice(state);
  state.chosen = spell;
  redraw(state);
}

function declareChosen(state, current) {
  if (!canInteract(state, current, 'Intent')) return;
  const view = current.view;
  const option = view.options.intent?.creatures?.find(one => one.creature === view.waitingCreature);
  if (!option?.castableSpells?.includes(state.chosen)) return;
  return submit(state, current, { kind: 'Intent', creature: view.waitingCreature, spell: state.chosen });
}

// The host's candidate set and bounds apply equally to the board shortcut and the sheet's confirmation.
function canCastTargets(state, view) {
  const legal = view.options?.target?.legalTargets;
  return Boolean(legal) && state.picked.length >= legal.minTargets && state.picked.length <= legal.maxTargets &&
    state.picked.every(id => legal.candidates.includes(id));
}

function castTargets(state, current) {
  if (!canInteract(state, current, 'Target') || !canCastTargets(state, current.view)) return;
  return submit(state, current, { kind: 'Target', targets: [...state.picked] });
}

function pick(state, current, candidate) {
  if (!canInteract(state, current, 'Target')) return;
  const legal = current.view.options.target?.legalTargets;
  if (!legal?.candidates.includes(candidate)) return;
  if (state.picked.includes(candidate)) return castTargets(state, current);
  if (legal.maxTargets === 1) state.picked = [candidate];
  else if (state.picked.length < legal.maxTargets) state.picked = [...state.picked, candidate];
  redraw(state);
}

function targetRemovals(state, current) {
  const selected = document.createElement('div');
  selected.className = 'target-removals';
  for (const id of state.picked) {
    const remove = button(`Remove creature ${id} ×`, () => {
      if (!canInteract(state, current, 'Target')) return;
      state.picked = state.picked.filter(candidate => candidate !== id);
      redraw(state);
    });
    selected.append(remove);
  }
  return selected;
}

function targetButtons(state, current) {
  const context = document.createElement('p');
  context.className = 'target-instruction';
  const legal = current.view.options.target?.legalTargets ?? { candidates: [], minTargets: 0, maxTargets: 0 };
  const picked = state.picked;
  const howMany = legal.minTargets === legal.maxTargets ? `${legal.maxTargets}` : `${legal.minTargets}–${legal.maxTargets}`;
  const noun = legal.maxTargets === 1 ? 'target' : 'targets';
  context.textContent = canCastTargets(state, current.view)
    ? 'Tap a selected target again, or Cast, to confirm.'
    : `Choose ${howMany} ${noun} on the battlefield.`;

  // A spell with nothing left to hit is revealed with no targets and fizzles, so binding none is the action
  // rather than a dead end (docs/tabletop/rulebook.md, 6.2).
  if (legal.candidates.length < legal.minTargets) {
    return [context, button('No legal target — cast anyway', () => submit(state, current, { kind: 'Target', targets: [] }))];
  }

  // No button a candidate: the tap is on the creature's own row, where its health, its defense and what is
  // already on it are (playtest-app.md §3.2). The sheet holds `done` and the count it is enabled at.
  const help = document.createElement('details');
  help.className = 'selection-help';
  const summary = document.createElement('summary');
  summary.textContent = 'Selection help';
  const instructions = document.createElement('p');
  instructions.textContent = 'Click a selected target again or press Enter to confirm the selected group. Use Remove to change your selection.';
  help.append(summary, instructions);

  const confirm = button(`Cast on ${picked.length} of ${legal.maxTargets}`, () => {
    confirm.disabled = true;
    castTargets(state, current);
  });
  confirm.disabled = !canCastTargets(state, current.view);
  return [context, targetRemovals(state, current), help, confirm];
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
  head.dataset.tone = (face.cues ?? []).find(cue => cue.tone !== 'critical')?.tone ?? 'neutral';
  if (face.creatureClass) {
    head.classList.toggle('class-accent', true);
    head.style.setProperty('--class-color', classColour(face.creatureClass, state.palette));
  }
  const title = document.createElement('div');
  title.className = 'card-title';
  title.textContent = cardTitle(face);
  const meta = document.createElement('span');
  meta.className = 'card-meta';
  meta.textContent = [prefix, cardHead(face)].filter(Boolean).join(' · ');
  title.append(meta);
  head.append(title);

  const cost = document.createElement('span');
  cost.className = 'card-cost';
  cost.textContent = `ϟ ${cardCost(face)}`;
  const costLabel = document.createElement('small');
  costLabel.textContent = 'Energy';
  cost.append(costLabel);
  cost.setAttribute('aria-label', `Costs ${cardCost(face)} energy per cast`);
  cost.title = 'Energy spent when casting';
  if (cardCost(face) !== '') head.append(cost);

  const stats = document.createElement('div');
  stats.className = 'card-stats';
  for (const stat of cardStats(face)) {
    const box = document.createElement('div');
    box.className = 'card-stat';
    box.dataset.stat = stat.kind;
    const label = document.createElement('span');
    label.textContent = `${stat.symbol} ${stat.label}`;
    const value = document.createElement('strong');
    value.textContent = stat.value;
    const hint = document.createElement('small');
    hint.textContent = stat.hint;
    box.append(label, value, hint);
    stats.append(box);
  }

  const body = document.createElement('div');
  body.className = 'card-body';
  for (const line of cardDetails(face)) {
    const row = document.createElement('div');
    row.className = `card-line card-line-${line.role}`;
    row.textContent = `${line.role === 'target' ? '◎ ' : line.role === 'requires' ? '↳ ' : ''}${line.text}`;
    body.append(row);
  }

  const cues = document.createElement('div');
  cues.className = 'card-cues';
  const symbols = { harm: '↘', recovery: '+', protection: '◇', control: '◎', energy: 'ϟ', critical: '✦' };
  for (const cue of face.cues ?? []) {
    const badge = document.createElement('span');
    badge.className = 'card-cue';
    badge.dataset.tone = cue.tone;
    badge.textContent = `${symbols[cue.tone] ?? '•'} ${cue.label}`;
    cues.append(badge);
  }
  if (face.criticalNote) {
    const note = document.createElement('p');
    note.className = 'card-critical-note';
    note.textContent = face.criticalNote;
    body.append(note);
  }
  return [head, cues, stats, body];
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
  if (state.sending || state.playback || state.step) {
    return;
  }

  state.sending = true;
  state.revision += 1;
  element('decision').setAttribute('aria-busy', 'true');
  for (const control of element('choices').querySelectorAll('button')) control.disabled = true;
  try {
    // The host has to have been told this board is up before it is told what was decided on it, or it has
    // nothing to measure the decision against. On loopback this has already resolved; on a phone over a slow
    // link it is the difference between a duration and a blank.
    await state.announced;

    // The asking this answers travels with it: the host refuses a decision that names another, which is what
    // stops a tap validated against one question from landing on the next of the same shape.
    const answer = await current.transport.decide({ ...decision, asked: current.view.waitingAsked ?? null });
    if (!answer.ok) {
      state.error = answer.body?.message ?? `The host answered ${answer.status}.`;
      return;
    }

    state.picked = [];
    state.chosen = null;
    state.ordered = [];
    state.error = '';
    state.stepSeen ??= new Map();
    state.stepSeen.set(current.seat, Math.max(state.stepSeen.get(current.seat) ?? -1, latestResolution(current.view.roundEvents) ?? -1));
  } catch {
    state.error = 'Could not reach the host. Check your connection before trying again.';
  } finally {
    state.sending = false;
    element('decision').setAttribute('aria-busy', 'false');
    state.rendered = null;
    redraw(state);
  }

  await refresh(state);
}

function treeNode(state, node, classes, creature) {
  const branch = document.createElement('li');
  branch.style.setProperty('--class-color', state.palette?.get(node.key) ?? '#c9c2a8');
  const group = classes.find(one => one.id === node.id);
  const known = group?.status === 'known';
  const offered = group?.status === 'available';
  const pick = button('', () => {
    if (!group) return;
    state.inspectClass = group.id;
    state.revealPackage = true;
    redraw(state);
  });
  pick.className = ['tree-node', group?.id === state.inspectClass && 'selected', offered && 'unlockable', known && 'known'].filter(Boolean).join(' ');
  pick.dataset.focus = `tree-${node.key}`;
  pick.setAttribute('aria-pressed', String(group?.id === state.inspectClass));
  pick.disabled = !group;
  const title = document.createElement('strong');
  title.className = 'tree-title';
  title.textContent = node.name;
  const progress = document.createElement('span');
  progress.className = 'tree-progress';
  progress.textContent = `Tier ${node.level} · ${node.initiativeBonus >= 0 ? "+" : ""}${node.initiativeBonus} initiative`;
  const status = document.createElement('span');
  status.className = 'tree-offer';
  status.textContent = known ? '✓ Acquired' : offered ? '+ Buy now · 1 pick' : 'Inspect package';
  pick.append(title, progress, status);
  branch.append(pick);
  if (node.children.length) {
    const children = document.createElement('ul');
    children.append(...node.children.map(child => treeNode(state, child, classes, creature)));
    branch.append(children);
  }
  return branch;
}

function closeTalents(state) {
  showTab(state, 'board');
  if (state.talentOpener?.isConnected) state.talentOpener.focus({ preventScroll: true });
  else [element('tab-mat'), element('hand-talents')].find(node => node.checkVisibility?.() ?? true)?.focus({ preventScroll: true });
}

// Only the title grip moves the window. Card clicks and scrolling keep their ordinary meaning.
function setupTalentWindow(state) {
  const panel = element('talent-window');
  const grip = element('talent-grip');
  let drag = null;
  const desktop = () => globalThis.innerWidth >= 1100;
  const move = (left, top) => {
    const rect = panel.getBoundingClientRect();
    panel.style.left = `${Math.max(8, Math.min(left, globalThis.innerWidth - rect.width - 8))}px`;
    panel.style.top = `${Math.max(8, Math.min(top, globalThis.innerHeight - rect.height - 8))}px`;
  };
  state.clampTalentWindow = () => {
    if (!desktop() || panel.hidden || panel.classList.contains('maximized')) return;
    const rect = panel.getBoundingClientRect();
    move(rect.left, rect.top);
  };
  if (globalThis.ResizeObserver) new ResizeObserver(state.clampTalentWindow).observe(panel);
  const reset = () => {
    panel.style.left = ''; panel.style.top = ''; panel.style.width = ''; panel.style.height = '';
    panel.classList.toggle('maximized', false);
    element('talent-max').setAttribute('aria-pressed', 'false');
  };
  grip.addEventListener('pointerdown', event => {
    if (!desktop() || panel.classList.contains('maximized') || event.button !== 0) return;
    const rect = panel.getBoundingClientRect();
    drag = { x: event.clientX, y: event.clientY, left: rect.left, top: rect.top };
    grip.setPointerCapture(event.pointerId);
  });
  grip.addEventListener('pointermove', event => {
    if (drag) move(drag.left + event.clientX - drag.x, drag.top + event.clientY - drag.y);
  });
  for (const kind of ['pointerup', 'pointercancel', 'lostpointercapture']) grip.addEventListener(kind, () => { drag = null; });
  grip.addEventListener('keydown', event => {
    if (!desktop() || panel.classList.contains('maximized')) return;
    const direction = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
    if (!direction) return;
    event.preventDefault();
    const rect = panel.getBoundingClientRect();
    move(rect.left + direction[0] * 20, rect.top + direction[1] * 20);
  });
  element('talent-reset').addEventListener('click', reset);
  element('talent-max').addEventListener('click', () => {
    const maximized = !panel.classList.contains('maximized');
    panel.classList.toggle('maximized', maximized);
    element('talent-max').setAttribute('aria-pressed', String(maximized));
  });
  element('talent-close').addEventListener('click', () => closeTalents(state));
  panel.addEventListener('keydown', event => {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); closeTalents(state); }
  });
  globalThis.addEventListener('resize', () => {
    if (!desktop()) reset();
    else if (!panel.hidden) {
      const rect = panel.getBoundingClientRect();
      move(rect.left, rect.top);
    }
  });
}

// Keyboard shortcuts use the same guards and submission path as pointer input. Number keys select;
// repeating the same number never commits a card or target by accident. Enter is the explicit commit.
function keyboardDecision(state, event) {
  if (event.defaultPrevented || event.repeat || event.ctrlKey || event.metaKey || event.altKey || event.isComposing) return;
  if (event.key === 'Escape' && state.boardOpen) {
    event.preventDefault();
    showBoard(state, false);
    return;
  }
  const target = event.target;
  if (target?.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target?.tagName?.toUpperCase())) return;
  const current = activeSeat(state.views, state.holder);
  if (!current || needsPass(current, state.holder) || state.sending) return;
  const key = event.key.toLowerCase();
  if (key !== 'escape' && target?.closest?.('#phase-dock') && !target.closest('#mini-board')) return;
  if (state.playback && !target?.closest?.('#phase-dock')) {
    if (key === 'arrowright' || key === 'arrowleft' || key === 'escape') {
      event.preventDefault();
      if (key === 'escape') finishPlayback(state); else movePlayback(state, key === 'arrowright' ? 1 : -1);
    }
    return;
  }
  if (state.step && !target?.closest?.('#phase-dock')) {
    const focused = target?.closest?.('button, summary, [role="button"]');
    if ((key === 'enter' && !focused) || key === 'arrowright' || key === 'escape') {
      event.preventDefault();
      advanceStep(state, state.step.seat, key === 'escape' ? state.step.last : state.step.action.sequence);
      return;
    }
    if (key.startsWith('arrow') || /^[1-9]$/.test(key)) return;
  }
  if (key.startsWith('arrow')) { navigateChoices(state, current, event); return; }
  if (key === '?') {
    event.preventDefault();
    element('shortcuts').open = !element('shortcuts').open;
    return;
  }
  if (key === 't') {
    event.preventDefault();
    if (state.tab === 'mat') closeTalents(state); else openTalents(state);
    return;
  }
  if (key === 'escape') {
    event.preventDefault();
    if (state.tab === 'mat') closeTalents(state);
    else if (element('announcements').open) element('announcements').open = false;
    else if (element('order').open) element('order').open = false;
    else if (element('phase-progress').open) element('phase-progress').open = false;
    else if (element('upkeep').open) element('upkeep').open = false;
    else if (element('recap').open) element('recap').open = false;
    else if (!element('phase-notice').hidden) hidePhaseNotice(state);
    else { state.chosen = null; state.picked = []; redraw(state); }
    return;
  }
  const number = /^[1-9]$/.test(key) ? Number(key) - 1 : null;
  const view = current.view;
  if (state.tab === 'mat' || view.waitingFor === 'Evolution') {
    if (number === null) return;
    const id = state.tab === 'mat' ? view.board.allies?.[number]?.id : view.options.evolution?.creatures?.[number]?.creature;
    if (id === undefined) return;
    event.preventDefault();
    state.inspectCreature = id;
    if (view.options.evolution?.creatures?.some(one => one.creature === id)) state.evolving = id;
    redraw(state);
    return;
  }
  if (!canInteract(state, current, view.waitingFor)) return;
  if (number !== null) {
    event.preventDefault();
    if (view.waitingFor === 'Speed' && number < 2) {
      return submit(state, current, { kind: 'Speed', creature: view.waitingCreature, speed: number === 0 ? 'Quick' : 'Standard' });
    }
    if (view.waitingFor === 'TieOrder') {
      const offered = untapped(view.options.tieOrder?.ties, state.ordered).filter(group => group.length > 1).flat();
      if (offered[number] !== undefined) { state.ordered = tap(view.options.tieOrder.ties, state.ordered, offered[number]); redraw(state); }
    }
    if (view.waitingFor === 'Intent') {
      const spell = view.options.intent?.creatures?.find(one => one.creature === view.waitingCreature)?.castableSpells?.[number];
      if (spell) { state.chosen = spell; redraw(state); }
    }
    if (view.waitingFor === 'Target') {
      const candidates = view.options.target?.legalTargets?.candidates ?? [];
      const id = candidates[number];
      if (id !== undefined && !state.picked.includes(id)) pick(state, current, id);
    }
    return;
  }
  // A focused control already handles Enter; the global shortcut must not add a second activation.
  if (key !== 'enter' || target?.closest?.('button, summary, [role="button"]')) return;
  event.preventDefault();
  if (view.waitingFor === 'TieOrder' && isSettled(view.options.tieOrder?.ties, state.ordered)) return submit(state, current, { kind: 'TieOrder', order: orderOf(view.options.tieOrder.ties, state.ordered) });
  if (view.waitingFor === 'Intent') return declareChosen(state, current);
  if (view.waitingFor === 'Target') return castTargets(state, current);
}

function enemyChoice(state, creature, board, entries) {
  const choice = liveChoice(creature, board, entries);
  const box = document.createElement('div');
  box.className = `round-choice ${choice.action ? 'public' : 'hidden-choice'}`;
  const heading = document.createElement('span');
  heading.className = 'choice-round';
  heading.textContent = `Round ${choice.round ?? '—'} · ${choice.status}`;
  box.append(heading);
  const describe = action => {
    const name = state.cards.get(action.spell)?.name ?? action.spell;
    const targets = (action.targets ?? []).map(id => {
      return `Creature ${id}`;
    });
    return { name, targets: targets.length ? `→ ${targets.join(', ')}` : 'No targets' };
  };
  if (choice.action) {
    const text = describe(choice.action);
    const name = document.createElement('strong');
    name.className = 'choice-spell';
    name.textContent = text.name;
    const targets = document.createElement('span');
    targets.className = 'choice-targets';
    targets.textContent = text.targets;
    box.append(name, targets);
  } else if (choice.previous) {
    const text = describe(choice.previous.action);
    const last = document.createElement('span');
    last.className = 'choice-previous';
    last.textContent = `Last round (${choice.previous.round}): ${text.name} ${text.targets}`;
    box.append(last);
  }
  return box;
}

// Own declarations are private but visible to their owner as soon as the host accepts them. Draft selections
// are labelled separately; neither kind is ever consulted while drawing an opponent creature.
function ownChoice(state, creature, marks) {
  const board = marks?.board;
  if (liveChoice(creature, board, marks?.roundEvents).action) return enemyChoice(state, creature, board, marks?.roundEvents);
  const intent = board?.intents?.find(one => one.actor === creature.id);
  const active = creature.id === marks?.active;
  const spell = intent?.spell ?? (active ? marks?.draftSpell : null);
  if (!spell) return null;
  const box = document.createElement('div');
  box.className = 'round-choice private-choice';
  const heading = document.createElement('span');
  heading.className = 'choice-round';
  heading.textContent = `Round ${board.roundNumber} · ${intent ? 'Not revealed' : 'Not declared'}`;
  const name = document.createElement('strong');
  name.className = 'choice-spell';
  name.textContent = state.cards.get(spell)?.name ?? spell;
  const targets = document.createElement('span');
  targets.className = 'choice-targets';
  targets.textContent = active && marks.targeting && marks.picked.length
    ? `Selecting → ${marks.picked.map(id => `Creature ${id}`).join(', ')} · not confirmed`
    : 'No targets chosen yet';
  box.title = intent ? 'Only you can see this choice' : 'Preview · confirm to declare';
  box.append(heading, name, targets);
  return box;
}

// Arrow navigation moves visible focus. It never declares or casts; the focused control owns Enter.
function navigateChoices(state, current, event) {
  const view = current.view;
  if (state.tab !== 'mat' && !canInteract(state, current, view.waitingFor)) return;
  const all = [...document.querySelectorAll('[data-focus]')];
  const active = document.activeElement;
  const nodes = prefix => all.filter(node => node.dataset.focus.startsWith(prefix) && !node.disabled);
  const focus = node => {
    event.preventDefault();
    node?.focus();
    node?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  };
  const atlas = state.tab === 'mat';
  const evolving = view.waitingFor === 'Evolution';
  if (atlas || evolving) {
    const picker = nodes(atlas ? 'talent-creature-' : 'evolve-').filter(node => !node.dataset.focus.startsWith('evolve-package-'));
    const cards = nodes(atlas ? 'tree-' : 'evolve-package-');
    const explore = atlas ? nodes('atlas-buy-')[0] : nodes('evolution-explorer')[0];
    const pass = atlas ? null : nodes('evolution-pass')[0];
    if (active === explore || active === pass) {
      if (event.key === 'ArrowUp') focus(active === pass ? explore : cards.at(-1) ?? picker[0]);
      else if (event.key === 'ArrowDown') focus(pass ?? active);
      else focus(active === explore ? pass : explore);
      return;
    }
    const inCards = cards.includes(active);
    if (!inCards) {
      if (event.key === 'ArrowDown') { focus(cards[0] ?? explore); return; }
      if (!['ArrowLeft', 'ArrowRight'].includes(event.key) || !picker.length) return;
      const selected = picker.findIndex(node => node.getAttribute('aria-pressed') === 'true');
      const index = picker.includes(active) ? picker.indexOf(active) : Math.max(0, selected);
      const next = picker[(index + (event.key === 'ArrowRight' ? 1 : -1) + picker.length) % picker.length];
      event.preventDefault();
      const identity = next.dataset.focus;
      next.click();
      [...document.querySelectorAll('[data-focus]')].find(node => node.dataset.focus === identity)?.focus();
      return;
    }
    const next = arrowNeighbour(cards, active, event.key);
    if (event.key === 'ArrowUp' && !next) focus(picker.find(node => node.getAttribute('aria-pressed') === 'true') ?? picker[0]);
    else if (event.key === 'ArrowDown' && !next && explore) focus(explore);
    else focus(next ?? active);
    return;
  }
  const prefixes = { Speed: ['speed-'], TieOrder: ['tie-'], Intent: ['card-', 'declare-'], Target: ['target-', 'mini-target-'] }[view.waitingFor];
  if (!prefixes) return;
  const controls = prefixes.flatMap(nodes).filter(node => node.checkVisibility?.() ?? true);
  if (!controls.length) return;
  const explore = view.waitingFor === 'Intent' ? element('hand-talents') : null;
  if (active === explore) {
    focus(event.key === 'ArrowUp' ? controls.at(-1) : controls[0]);
    return;
  }
  if (controls.includes(active) && event.key === 'ArrowDown' && !arrowNeighbour(controls, active, event.key) && explore) {
    focus(explore);
    return;
  }
  focus(controls.includes(active) ? arrowNeighbour(controls, active, event.key) ?? active : controls[0]);
}

function arrowNeighbour(controls, active, key) {
  const rect = active.getBoundingClientRect();
  const x = rect.left + rect.width / 2;
  const horizontal = key === 'ArrowLeft' || key === 'ArrowRight';
  const forward = key === 'ArrowRight' || key === 'ArrowDown';
  const positioned = controls.map(node => ({ node, rect: node.getBoundingClientRect() }));
  if (horizontal) {
    const sameRow = positioned.filter(one => Math.abs(one.rect.top - rect.top) < 10).map(one => one.node);
    const row = sameRow.length > 1 ? sameRow : controls;
    const index = row.indexOf(active);
    return row[(index + (forward ? 1 : -1) + row.length) % row.length];
  }
  const candidates = positioned.filter(one => forward ? one.rect.top > rect.top + 10 : one.rect.top < rect.top - 10);
  candidates.sort((a, b) => Math.abs(a.rect.top - rect.top) - Math.abs(b.rect.top - rect.top) ||
    Math.abs(a.rect.left + a.rect.width / 2 - x) - Math.abs(b.rect.left + b.rect.width / 2 - x));
  return candidates[0]?.node;
}
