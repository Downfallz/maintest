import { sampleAction } from './arena-lab-fixture.js';
import { initialLabState, labTransition, createLabPlayback } from './arena-lab-state.js';
import { phaseLabel, tempoLabel } from './terms.js';

const $ = id => document.getElementById(id);
const lab = $('lab');
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const playback = createLabPlayback();
let state = initialLabState();
const nodes = new Map();

function allCreatures() { return [...state.board.allies, ...state.board.enemies]; }
function nameOf(id) { return allCreatures().find(one => one.id === id)?.name ?? id; }
function busy() { return ['windup', 'impact'].includes(state.stage); }
function dispatch(action) {
  state = labTransition(state, action);
  render();
}

function makeCreature(creature, side, index) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'creature';
  button.dataset.creature = creature.id;
  // Only fixed, authored form identifiers enter this SVG reference. All display text uses textContent.
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('aria-hidden', 'true');
  const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  use.setAttribute('href', `#${creature.form}`);
  svg.append(use);
  button.append(svg);
  for (const kind of ['number', 'name', 'tag', 'stats', 'result']) {
    const span = document.createElement('span');
    span.className = `creature-${kind}`;
    button.append(span);
  }
  button.querySelector('.creature-number').textContent = `${side === 'allies' ? 'A' : 'B'}${index + 1}`;
  const track = document.createElement('span');
  track.className = 'health-track';
  track.setAttribute('aria-hidden', 'true');
  const fill = document.createElement('span');
  fill.className = 'health-fill';
  track.append(fill);
  button.insertBefore(track, button.querySelector('.creature-stats'));
  button.addEventListener('click', () => {
    dispatch({ type: 'inspect', id: creature.id });
    if (state.stage === 'targeting') dispatch({ type: 'target', id: creature.id });
  });
  $(side).append(button);
  nodes.set(creature.id, button);
}

function drawCreature(creature) {
  const node = nodes.get(creature.id);
  const targetable = state.stage === 'targeting' && sampleAction.targets.includes(creature.id);
  node.classList.toggle('is-active', creature.id === sampleAction.actor);
  node.classList.toggle('is-targetable', targetable);
  node.classList.toggle('is-selected', creature.id === state.target);
  node.classList.toggle('is-inspected', creature.id === state.inspected);
  node.classList.toggle('is-defeated', !creature.isAlive);
  node.setAttribute('aria-pressed', String(targetable ? creature.id === state.target : creature.id === state.inspected));
  node.setAttribute('aria-label', `${creature.name}, ${creature.health} of ${creature.maxHealth} HP, ${creature.energy} energy, ${creature.totalDefense} defense. ${targetable ? 'Choose as sample target.' : 'Inspect creature.'}`);
  node.querySelector('.creature-name').textContent = creature.name;
  node.querySelector('.creature-tag').textContent = creatureTag(creature, targetable);
  node.querySelector('.creature-stats').textContent = `${creature.health}/${creature.maxHealth} HP · ${creature.energy} EN · ${creature.totalDefense} DEF`;
  node.querySelector('.health-fill').style.width = `${100 * creature.health / creature.maxHealth}%`;
  const change = state.changes.find(one => one.creature === creature.id);
  node.querySelector('.creature-result').textContent = change?.health != null ? `${change.health} HP` : '';
}

function creatureTag(creature, targetable) {
  if (!creature.isAlive) return 'DEFEATED';
  if (creature.id === state.target) return 'SELECTED TARGET';
  if (targetable) return 'SELECT TARGET';
  if (creature.id === sampleAction.actor) return 'ACTING NOW';
  return creature.conditions.length ? creature.conditions[0] : ' ';
}

function instruction() {
  const target = nameOf(state.target);
  switch (state.stage) {
    case 'decision': return 'Choose the sample action. Tap any creature to inspect it.';
    case 'targeting': return state.target ? `${target} selected. Confirm to play the sample.` : 'Choose one of the three outlined opponents.';
    case 'windup': return `${nameOf(sampleAction.actor)} is using ${sampleAction.label} on ${target}…`;
    case 'impact': return `${target} takes the sample impact.`;
    case 'resolved': {
      const frame = state.changes.find(one => one.creature === state.target);
      return `${target}: ${frame.health} HP${frame.defeated ? ' · defeated' : ''}. Fixed sample result — reset to compare another target.`;
    }
    default: return '';
  }
}

function render() {
  lab.dataset.stage = state.stage;
  allCreatures().forEach(drawCreature);
  $('phase-label').textContent = phaseLabel(state.board.phase);
  $('actor-name').textContent = nameOf(sampleAction.actor);
  $('actor-meta').textContent = `${tempoLabel(state.board.timeline[0].speed)} · ${state.board.allies[0].energy} energy`;
  $('action-name').textContent = sampleAction.label;
  $('instruction').textContent = instruction();
  $('choose').disabled = state.stage !== 'decision';
  $('choose').setAttribute('aria-pressed', String(state.stage !== 'decision'));
  $('cancel').hidden = state.stage !== 'targeting';
  $('skip').hidden = !busy();
  $('confirm').hidden = busy();
  $('confirm').disabled = state.stage !== 'resolved' && !(state.stage === 'targeting' && state.target);
  $('confirm').textContent = state.stage === 'resolved' ? 'Reset preview' : state.stage === 'targeting' ? 'Play sample action' : 'Choose an action';
  const inspected = allCreatures().find(one => one.id === state.inspected);
  $('inspect-name').textContent = inspected.name;
  $('inspect-stats').textContent = `${inspected.health}/${inspected.maxHealth} HP · ${inspected.energy} energy · ${inspected.totalDefense} defense`;
  $('inspect-effects').textContent = inspected.conditions.join(' / ') || 'No ongoing effects';
}

function reset() {
  playback.cancel();
  dispatch({ type: 'reset' });
  $('choose').focus({ preventScroll: true });
}

function applyMotionPreference() {
  lab.dataset.motion = motion.matches || $('reduce-motion').checked ? 'reduced' : 'full';
  if (lab.dataset.motion === 'reduced' && busy()) skip();
}

function skip() {
  playback.cancel();
  dispatch({ type: 'skip' });
  $('confirm').focus({ preventScroll: true });
}

for (const side of ['allies', 'enemies']) state.board[side].forEach((one, index) => makeCreature(one, side, index));
for (const [index, slot] of state.board.timeline.entries()) {
  const item = document.createElement('li');
  if (index === 0) item.setAttribute('aria-current', 'step');
  item.textContent = `${index + 1} · ${nameOf(slot.creature)}`;
  const tempo = document.createElement('span');
  tempo.className = 'tempo';
  tempo.textContent = `${slot.owner === state.board.slot ? 'Yours' : 'Opponent'} · ${tempoLabel(slot.speed)}`;
  item.append(tempo);
  $('timeline').append(item);
}
for (const button of document.querySelectorAll('button[data-layout]')) {
  button.addEventListener('click', () => {
    lab.dataset.layout = button.dataset.layout;
    for (const option of document.querySelectorAll('button[data-layout]')) option.setAttribute('aria-pressed', String(option === button));
  });
}
$('choose').addEventListener('click', () => {
  dispatch({ type: 'choose' });
  nodes.get(sampleAction.targets[0]).focus({ preventScroll: true });
});
$('cancel').addEventListener('click', () => {
  dispatch({ type: 'cancel' });
  $('choose').focus({ preventScroll: true });
});
$('confirm').addEventListener('click', () => {
  if (state.stage === 'resolved') { reset(); return; }
  dispatch({ type: 'play' });
  if (!busy()) return;
  lab.style.setProperty('--playback-rate', $('playback-speed').value);
  playback.play(action => {
    const skipHadFocus = document.activeElement === $('skip');
    dispatch(action);
    if (state.stage === 'resolved' && skipHadFocus) $('confirm').focus({ preventScroll: true });
  }, { reducedMotion: lab.dataset.motion === 'reduced', speed: Number($('playback-speed').value) });
  (busy() ? $('skip') : $('confirm')).focus({ preventScroll: true });
});
$('skip').addEventListener('click', skip);
$('reset').addEventListener('click', reset);
$('reduce-motion').addEventListener('change', applyMotionPreference);
motion.addEventListener('change', applyMotionPreference);
document.addEventListener('keydown', event => {
  if (event.key !== 'Escape') return;
  if (busy()) skip();
  else if (state.stage === 'targeting') $('cancel').click();
});
document.addEventListener('visibilitychange', () => {
  if (document.hidden && busy()) {
    playback.cancel();
    dispatch({ type: 'skip' });
  }
});
window.addEventListener('pagehide', () => {
  playback.cancel();
  if (busy()) dispatch({ type: 'skip' });
});
applyMotionPreference();
render();
