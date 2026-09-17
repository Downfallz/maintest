import { httpTransport, seatFromLocation } from './transport.js';

// The page renders what the host serves and submits what a player taps. It holds no rule: which spells are
// castable, which targets are legal and how many, whose turn it is -- all of that arrives in `options`, built
// by the engine's own gates. Nothing here decides anything, and nothing here knows a spell by name.
const identity = seatFromLocation(globalThis.location?.search ?? '');
const element = id => document.getElementById(id);

if (!identity) {
  element('phase').textContent = 'Open the link the host printed: it carries this seat and its token.';
} else {
  start(httpTransport(identity.seat, identity.token));
}

function start(transport) {
  const state = { seat: identity.seat, passed: false, waitingFor: null, sending: false };
  element('seat').textContent = identity.seat === 'player1' ? 'Player 1' : 'Player 2';
  element('pass-ready').addEventListener('click', () => {
    state.passed = true;
    refresh(transport, state);
  });

  refresh(transport, state);
  setInterval(() => refresh(transport, state), 700);
}

async function refresh(transport, state) {
  if (state.sending) return;

  const answer = await transport.seat();
  if (!answer.ok) {
    element('phase').textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
    return;
  }

  render(transport, state, answer.body);
}

function render(transport, state, view) {
  const asked = view.waitingFor !== null && view.waitingFor !== undefined;

  // The device is passed when this seat is asked something new. Until the player says they are the one
  // holding it, the board stays behind the pass screen.
  if (asked && view.waitingFor !== state.waitingFor) {
    state.waitingFor = view.waitingFor;
    state.passed = false;
  }
  if (!asked) {
    state.waitingFor = null;
  }

  const hide = asked && !state.passed;
  element('pass').hidden = !hide;
  element('table').hidden = hide;
  element('pass-seat').textContent = element('seat').textContent;
  element('pass-seat-again').textContent = element('seat').textContent;
  if (hide) return;

  element('phase').textContent = view.over
    ? 'The match is over.'
    : `Round ${view.board.roundNumber ?? '—'} · ${view.board.subPhase ?? '—'}`;
  renderBoard(view.board);
  renderDecision(transport, state, view);
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

function renderDecision(transport, state, view) {
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

  if (!view.waitingFor) {
    asking.textContent = 'Waiting for the other seat…';
    choices.replaceChildren();
    return;
  }

  asking.textContent = titleOf(view);
  choices.replaceChildren(...buttonsFor(transport, state, view));
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
function buttonsFor(transport, state, view) {
  const send = decision => () => submit(transport, state, decision);
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
      return targetButtons(transport, state, view);
    default:
      return [];
  }
}

function targetButtons(transport, state, view) {
  const legal = view.options.target?.legalTargets ?? { candidates: [], minTargets: 0, maxTargets: 0 };
  state.picked ??= [];
  const picked = state.picked;

  // A spell with nothing left to hit is revealed with no targets and fizzles, so binding none is the action
  // rather than a dead end (docs/tabletop/rulebook.md, 6.2).
  if (legal.candidates.length < legal.minTargets) {
    return [button('No legal target — cast anyway', () => submit(transport, state, { kind: 'Target', targets: [] }))];
  }

  const buttons = legal.candidates.map(candidate => {
    const chosen = picked.includes(candidate);
    const face = button(`${chosen ? '✓ ' : ''}Creature ${candidate}`, () => {
      state.picked = chosen ? picked.filter(one => one !== candidate) : [...picked, candidate].slice(0, legal.maxTargets);
      refresh(transport, state);
    });
    if (chosen) face.classList.add('chosen');
    return face;
  });

  const confirm = button(`Cast on ${picked.length} of ${legal.maxTargets}`, () => submit(transport, state, { kind: 'Target', targets: picked }));
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

async function submit(transport, state, decision) {
  state.sending = true;
  try {
    const answer = await transport.decide(decision);
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

  await refresh(transport, state);
}
