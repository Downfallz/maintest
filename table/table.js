import { httpTransport } from './transport.js';
import { activeSeat, isAsked, needsPass } from './seats.js';
import { forget, heldSeats } from './session.js';
import { cardCost, cardHead, cardLines, cardTitle, loadCatalogue } from './card.js';
import { chipText, conditionDock, healthShare, healthText, statPairs } from './board.js';
import { bands, side, withCursor } from './timeline.js';
import { drawn, matBands } from './mat.js';

// The page renders what the host serves and submits what a player taps. It holds no rule: which spells are
// castable, which targets are legal and how many, whose turn it is -- all of that arrives in `options`, built
// by the engine's own gates. Nothing here decides anything, and nothing here knows a spell by name.
const storage = kept();
const held = heldSeats(globalThis.location?.search ?? '', storage);
const element = id => document.getElementById(id);

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
  const state = { seats, holder: null, shown: null, sending: false, picked: [], cards: new Map(), catalogue: null, tab: 'board' };
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

  refresh(state);
  setInterval(() => refresh(state), 700);
}

// The catalogue the match is playing, through any seat this page holds: it is the same for both, and it is
// what every card on this screen is drawn from. A page that does not get it prints spell ids and still plays.
async function load(state) {
  state.catalogue = await loadCatalogue(state.seats);
  state.cards = new Map((state.catalogue?.cards ?? []).map(card => [card.id, card]));
  element('rules').textContent = ruleLine(state.catalogue);
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
    const answer = await seat.transport.seat();

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

    views.push({ ...seat, view: answer.body });
  }

  // Dropped after the round of polls rather than inside it, so nothing this loop reads changes while it runs.
  for (const seat of refused) {
    forget(storage, seat);
    state.seats = state.seats.filter(held => held.seat !== seat);
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

  if (current.seat !== state.shown) {
    state.shown = current.seat;
    state.picked = [];
  }

  // Until the player being asked says they are the one holding the device, the board stays behind the pass
  // screen (seats.js).
  const fence = needsPass(current, state.holder);
  element('seat').textContent = nameOf(current.seat);
  element('pass-seat').textContent = nameOf(current.seat);
  element('pass-seat-again').textContent = nameOf(current.seat);
  element('pass-ready').dataset.seat = current.seat;
  element('pass').hidden = !fence;
  element('table').hidden = fence;
  if (fence) return;

  element('phase').textContent = view.over
    ? 'The match is over.'
    : `Round ${view.board.roundNumber ?? '—'} · ${view.board.subPhase ?? '—'}`;
  renderTimeline(view.board);
  renderBoard(state, view);
  renderMat(state, view.board);
  renderFeed(view.feed);
  renderDecision(state, current);
}

// The initiative track: the engine's order, banded by speed, scrolling sideways at 360 px. The strip never
// sorts -- ties and all, this is the order the round is played in (timeline.js).
function renderTimeline(board) {
  const cursor = withCursor(board.timeline, board.resolveCursor);
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
  element('board').replaceChildren(
    hand(state, board, view.opponentIntents),
    ...(board.allies ?? []).map(creature => line(creature, 'ally')),
    ...(board.enemies ?? []).map(creature => line(creature, 'enemy')),
  );
}

// What is face down. This seat's own backs are its own to read; the other side's is a count and carries no
// data at all -- that is the whole of the hidden information, and it is the server that keeps it so.
function hand(state, board, opponentIntents) {
  const box = document.createElement('div');
  box.className = 'hand';

  const mine = document.createElement('div');
  mine.className = 'backs ally';
  mine.textContent = (board.intents ?? [])
    .map(intent => `${intent.actor}: ${state.cards.get(intent.spell)?.name ?? intent.spell}`)
    .join(' · ') || 'nothing declared';

  const theirs = document.createElement('div');
  theirs.className = 'backs enemy';
  theirs.textContent = `${Number(opponentIntents) || 0} face down`;

  box.append(mine, theirs);
  return box;
}

// A creature board: numbers and a bar, never a rail, and the dock under it (board.js).
function line(creature, which) {
  const box = document.createElement('div');
  box.className = `creature ${which}${creature.isAlive === false ? ' dead' : ''}`;

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

  box.append(who, health, stats, dock(creature.conditions));
  return box;
}

// The condition dock: chips grouped by what is left of them, permanent in their own group at the end.
function dock(conditions) {
  const box = document.createElement('div');
  box.className = 'dock';
  for (const group of conditionDock(conditions)) {
    const one = document.createElement('div');
    one.className = 'dock-group';

    const label = document.createElement('span');
    label.className = 'dock-rounds';
    label.textContent = group.rounds === null ? 'permanent' : `${group.rounds}`;
    one.append(label);

    for (const condition of group.conditions) {
      const chip = document.createElement('span');
      chip.className = 'chip';
      chip.textContent = chipText(condition);
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

// What has happened, as this seat may be told it. The kinds are the engine's own words, off the wire.
function renderFeed(feed) {
  const lines = (feed ?? []).slice(-12).reverse().map(entry => {
    const item = document.createElement('li');
    item.textContent = `${entry.round ?? '—'} · ${entry.subPhase ?? ''} · ${entry.event?.kind ?? ''}`;
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
    case 'Intent': {
      const option = (view.options.intent?.creatures ?? []).find(candidate => candidate.creature === view.waitingCreature);
      return (option?.castableSpells ?? []).map(spell =>
        card(state, spell, '', send({ kind: 'Intent', creature: view.waitingCreature, spell })));
    }
    case 'Target':
      return targetButtons(state, current);
    default:
      return [];
  }
}

function targetButtons(state, current) {
  const legal = current.view.options.target?.legalTargets ?? { candidates: [], minTargets: 0, maxTargets: 0 };
  const picked = state.picked;

  // A spell with nothing left to hit is revealed with no targets and fizzles, so binding none is the action
  // rather than a dead end (docs/tabletop/rulebook.md, 6.2).
  if (legal.candidates.length < legal.minTargets) {
    return [button('No legal target — cast anyway', () => submit(state, current, { kind: 'Target', targets: [] }))];
  }

  const buttons = legal.candidates.map(candidate => {
    const chosen = picked.includes(candidate);
    const face = button(`${chosen ? '✓ ' : ''}Creature ${candidate}`, () => {
      state.picked = chosen ? picked.filter(one => one !== candidate) : [...picked, candidate].slice(0, legal.maxTargets);
      refresh(state);
    });
    if (chosen) face.classList.add('chosen');
    return face;
  });

  const confirm = button(`Cast on ${picked.length} of ${legal.maxTargets}`, () => submit(state, current, { kind: 'Target', targets: picked }));
  confirm.disabled = picked.length < legal.minTargets;
  return [...buttons, confirm];
}

// A spell as the card the host serves, and as the id it was offered by when the catalogue has no card for it.
// Nothing here knows what any of these words mean: they arrive rendered (card.js).
function card(state, spell, prefix, onClick) {
  const face = state.cards.get(spell);
  if (!face) {
    return button([prefix, spell].filter(Boolean).join(' · '), onClick);
  }

  const choice = document.createElement('button');
  choice.type = 'button';
  choice.className = 'card';
  choice.addEventListener('click', onClick);

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

  choice.append(head, body);
  return choice;
}

function button(label, onClick) {
  const face = document.createElement('button');
  face.type = 'button';
  face.textContent = label;
  face.addEventListener('click', onClick);
  return face;
}

async function submit(state, current, decision) {
  state.sending = true;
  try {
    const answer = await current.transport.decide(decision);
    if (!answer.ok) {
      const problem = element('problem');
      problem.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      problem.hidden = false;
      return;
    }

    state.picked = [];
  } finally {
    state.sending = false;
  }

  await refresh(state);
}
