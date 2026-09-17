import { httpTransport } from './transport.js';
import { activeSeat, isAsked, needsPass } from './seats.js';
import { forget, heldSeats } from './session.js';

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
  const state = { seats, holder: null, shown: null, sending: false, picked: [] };
  element('pass-ready').addEventListener('click', () => {
    state.holder = element('pass-ready').dataset.seat ?? state.holder;
    refresh(state);
  });

  refresh(state);
  setInterval(() => refresh(state), 700);
}

async function refresh(state) {
  if (state.sending) return;

  // Every seat this page holds, every poll: the host answers one seat per payload, and which one is being
  // asked is exactly what the page cannot know without asking. Over a copy, because a seat can be dropped
  // here.
  const views = [];
  for (const seat of [...state.seats]) {
    const answer = await seat.transport.seat();

    // A token this host does not know is a seat from another table -- an earlier session, or the browser of
    // somebody who played here yesterday. Only that seat goes: a code typed for *this* table may be on the
    // same page, and it has already been taken out of the address bar, so forgetting it too would mean
    // reading it off the host's screen again.
    if (answer.status === 403) {
      forget(storage, seat.seat);
      state.seats = state.seats.filter(held => held.seat !== seat.seat);
      continue;
    }

    if (!answer.ok) {
      element('phase').textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      return;
    }

    views.push({ ...seat, view: answer.body });
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
  renderBoard(view.board);
  renderDecision(state, current);
}

function renderBoard(board) {
  const lines = [...(board.allies ?? []).map(creature => line(creature, 'ally')), ...(board.enemies ?? []).map(creature => line(creature, 'enemy'))];
  element('board').replaceChildren(...lines);
}

function line(creature, side) {
  const box = document.createElement('div');
  box.className = `creature ${side}${creature.isAlive === false ? ' dead' : ''}`;
  const who = document.createElement('div');
  who.textContent = `${side === 'ally' ? 'Yours' : 'Theirs'} · creature ${creature.id}`;
  const stats = document.createElement('div');
  stats.className = 'stats';
  stats.textContent = `health ${creature.health} · energy ${creature.energy} · defense ${creature.totalDefense} · initiative ${creature.currentInitiative} · ${(creature.conditions ?? []).length} condition(s)`;
  box.append(who, stats);
  return box;
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
          button(`Creature ${creature.creature}: ${spell}`, send({ kind: 'Evolution', creature: creature.creature, spell }))));
      return [...unlocks, button('Pass', send({ kind: 'Evolution', pass: true }))];
    }
    case 'Speed':
      return ['Quick', 'Standard'].map(speed =>
        button(speed, send({ kind: 'Speed', creature: view.waitingCreature, speed })));
    case 'Intent': {
      const option = (view.options.intent?.creatures ?? []).find(candidate => candidate.creature === view.waitingCreature);
      return (option?.castableSpells ?? []).map(spell =>
        button(spell, send({ kind: 'Intent', creature: view.waitingCreature, spell })));
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
