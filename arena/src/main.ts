import Phaser from 'phaser';
import { ArenaScene } from './scene';
import { creaturesOf, isPlaying, type Creature, type LabState, type Placement } from './types';
import { initialLabState, labTransition, createLabPlayback } from '../../table/arena-lab-state.js';
import { sampleAction } from '../../table/arena-lab-fixture.js';
import { tempoLabel } from '../../table/terms.js';

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const button = (id: string) => $<HTMLButtonElement>(id);
const lab = $('lab');
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const playback = createLabPlayback();
let state = initialLabState() as LabState;
let ready = false;
let audio: AudioContext | null = null;
let soundEnabled = false;
const nodes = new Map<string, HTMLButtonElement>();
const assetRoot = new URL('arena-art/', document.baseURI).href;
const scene = new ArenaScene(assetRoot);
const reduced = () => motion.matches || $<HTMLInputElement>('reduce-motion').checked;
const speed = () => Number($<HTMLSelectElement>('playback-speed').value);
const nameOf = (id: string | null) => creaturesOf(state).find(one => one.id === id)?.name ?? '';

function dispatch(action: { type: string; id?: string }) {
  state = labTransition(state, action) as LabState;
  render();
}

function makeCreature(creature: Creature, index: number) {
  const node = document.createElement('button');
  node.type = 'button';
  node.className = `creature ${index >= 3 ? 'enemy' : 'ally'}`;
  node.dataset.creature = creature.id;
  node.disabled = true;
  // Static structure only; authored text is assigned through textContent below.
  node.innerHTML = '<span class="creature-tag"></span><span class="creature-result"></span><span class="caption"><span class="creature-name"></span><span class="health-track" aria-hidden="true"><span class="health-fill"></span></span><span class="creature-stats"></span></span>';
  node.addEventListener('click', () => {
    dispatch({ type: 'inspect', id: creature.id });
    if (state.stage === 'targeting') dispatch({ type: 'target', id: creature.id });
  });
  $('actor-layer').append(node);
  nodes.set(creature.id, node);
}

function placeTargets(positions: Placement[]) {
  [...nodes.values()].forEach((node, index) => {
    const position = positions[index];
    node.style.left = `${position.x - position.hitWidth / 2}px`;
    node.style.top = `${Math.max(0, position.y - position.size)}px`;
    node.style.width = `${position.hitWidth}px`;
    node.style.height = `${Math.max(44, position.size + 42)}px`;
    node.style.zIndex = String(Math.round(position.y));
  });
}

function updateCreature(creature: Creature) {
  const node = nodes.get(creature.id)!;
  const targetable = state.stage === 'targeting' && sampleAction.targets.includes(creature.id);
  const selected = creature.id === state.target;
  node.disabled = !ready || isPlaying(state);
  node.classList.toggle('is-selected', selected);
  node.classList.toggle('is-targetable', targetable);
  node.classList.toggle('is-defeated', !creature.isAlive);
  node.setAttribute('aria-pressed', String(targetable ? selected : creature.id === state.inspected));
  node.setAttribute('aria-label', `${creature.name}, ${creature.health} of ${creature.maxHealth} HP, ${creature.energy} energy, ${creature.totalDefense} defense. ${targetable ? 'Select target.' : 'Inspect creature.'}`);
  node.querySelector('.creature-name')!.textContent = creature.name;
  node.querySelector('.creature-stats')!.textContent = `${creature.health}/${creature.maxHealth} HP`;
  (node.querySelector('.health-fill') as HTMLElement).style.width = `${100 * creature.health / creature.maxHealth}%`;
  let tag = '';
  if (!creature.isAlive) tag = 'DEFEATED';
  else if (selected) tag = 'SELECTED';
  else if (targetable) tag = 'TARGET';
  else if (creature.id === sampleAction.actor) tag = 'ACTING';
  node.querySelector('.creature-tag')!.textContent = tag;
  const change = state.changes.find(one => one.creature === creature.id);
  node.querySelector('.creature-result')!.textContent = change?.health == null ? '' : `${change.health} HP`;
}

function instruction() {
  switch (state.stage) {
    case 'decision': return 'Your move. Choose Strike, then a target.';
    case 'targeting': return state.target ? `${nameOf(state.target)} selected. Confirm your action.` : 'Select an opposing creature in the arena.';
    case 'windup': return `${nameOf(sampleAction.actor)} → ${nameOf(state.target)}`;
    case 'impact': return 'Impact. Reading the sample outcome…';
    case 'resolved': return `${nameOf(state.target)}: ${state.changes.find(one => one.creature === state.target)?.health} HP. Sample complete.`;
    default: return '';
  }
}

function render() {
  lab.dataset.stage = state.stage;
  lab.dataset.motion = reduced() ? 'reduced' : 'full';
  creaturesOf(state).forEach(updateCreature);
  $('actor-meta').textContent = `${tempoLabel(state.board.timeline[0].speed)} · ${state.board.allies[0].energy} energy`;
  $('instruction').textContent = ready ? instruction() : 'Preparing the scene…';
  button('choose').disabled = !ready || state.stage !== 'decision';
  button('choose').setAttribute('aria-pressed', String(state.stage !== 'decision'));
  button('cancel').hidden = state.stage !== 'targeting';
  button('skip').hidden = !isPlaying(state);
  button('confirm').hidden = isPlaying(state);
  button('confirm').disabled = !ready || (state.stage !== 'resolved' && !(state.stage === 'targeting' && state.target));
  button('confirm').textContent = state.stage === 'resolved' ? 'Play again' : state.stage === 'targeting' ? 'Cast Strike' : 'Choose Strike';
  const inspected = creaturesOf(state).find(one => one.id === state.inspected)!;
  $('inspect-name').textContent = inspected.name;
  $('inspect-stats').textContent = `${inspected.health}/${inspected.maxHealth} HP · ${inspected.energy} energy · ${inspected.totalDefense} defense`;
  $('inspect-effects').textContent = inspected.conditions.join(' / ') || 'No ongoing effects';
  $('callout-kicker').textContent = state.stage === 'targeting' ? 'CHOOSE YOUR TARGET' : isPlaying(state) ? 'BRUTE' : '';
  $('callout-title').textContent = isPlaying(state) ? 'Strike' : state.stage === 'resolved' ? 'Sample complete' : '';
  scene.present(state, reduced(), speed());
}

function reset() {
  playback.cancel();
  dispatch({ type: 'reset' });
  button('choose').focus({ preventScroll: true });
}

function skip(focus = true) {
  playback.cancel();
  dispatch({ type: 'skip' });
  if (focus) button('confirm').focus({ preventScroll: true });
}

function fail(message: string) {
  ready = false;
  lab.dataset.ready = 'false';
  $('loading').hidden = false;
  $('load-detail').textContent = message;
  button('reload').hidden = false;
  render();
}

async function enableSound() {
  soundEnabled = $<HTMLInputElement>('sound').checked;
  if (!soundEnabled) return;
  try {
    audio ??= new AudioContext();
    await audio.resume();
  } catch {
    soundEnabled = false;
    $<HTMLInputElement>('sound').checked = false;
  }
}

function impactSound() {
  if (!soundEnabled || audio?.state !== 'running') return;
  const oscillator = audio.createOscillator();
  const gain = audio.createGain();
  oscillator.type = 'triangle';
  oscillator.frequency.setValueAtTime(150, audio.currentTime);
  oscillator.frequency.exponentialRampToValueAtTime(42, audio.currentTime + .18);
  gain.gain.setValueAtTime(.13, audio.currentTime);
  gain.gain.exponentialRampToValueAtTime(.001, audio.currentTime + .22);
  oscillator.connect(gain); gain.connect(audio.destination);
  oscillator.start(); oscillator.stop(audio.currentTime + .23);
  oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); };
}

creaturesOf(state).forEach(makeCreature);
for (const [index, slot] of state.board.timeline.entries()) {
  const item = document.createElement('li');
  if (index === 0) item.setAttribute('aria-current', 'step');
  if (slot.owner !== state.board.slot) item.className = 'enemy';
  item.textContent = `${index + 1} ${nameOf(slot.creature)}`;
  item.title = `${nameOf(slot.creature)} · ${slot.owner === state.board.slot ? 'Yours' : 'Opponent'} · ${tempoLabel(slot.speed)}`;
  const side = document.createElement('span');
  side.className = 'timeline-side';
  side.textContent = `${slot.owner === state.board.slot ? 'You' : 'Foe'} · ${tempoLabel(slot.speed)}`;
  item.append(side); $('timeline').append(item);
}

button('choose').addEventListener('click', () => {
  dispatch({ type: 'choose' });
  nodes.get(sampleAction.targets[0])!.focus({ preventScroll: true });
});
button('cancel').addEventListener('click', () => {
  dispatch({ type: 'cancel' }); button('choose').focus({ preventScroll: true });
});
button('confirm').addEventListener('click', () => {
  if (state.stage === 'resolved') { reset(); return; }
  dispatch({ type: 'play' });
  if (!isPlaying(state)) return;
  playback.play((action: { type: string }) => {
    const focus = document.activeElement === button('skip');
    dispatch(action);
    if (state.stage === 'resolved' && focus) button('confirm').focus({ preventScroll: true });
  }, { reducedMotion: reduced(), speed: speed() });
  (isPlaying(state) ? button('skip') : button('confirm')).focus({ preventScroll: true });
});
button('skip').addEventListener('click', () => skip());
button('reset').addEventListener('click', reset);
button('reload').addEventListener('click', () => location.reload());
button('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
button('inspect').addEventListener('click', () => $<HTMLDialogElement>('inspect-dialog').showModal());
$<HTMLInputElement>('sound').addEventListener('change', enableSound);
function motionChanged() { if (isPlaying(state)) skip(false); else render(); }
$<HTMLInputElement>('reduce-motion').addEventListener('change', motionChanged);
motion.addEventListener('change', motionChanged);
$<HTMLSelectElement>('playback-speed').addEventListener('change', () => { if (isPlaying(state)) skip(false); else render(); });
button('fullscreen').disabled = !document.fullscreenEnabled;
button('fullscreen').addEventListener('click', async () => {
  try { if (document.fullscreenElement) await document.exitFullscreen(); else await lab.requestFullscreen(); }
  catch { button('fullscreen').title = 'Fullscreen is unavailable in this browser'; }
});
document.addEventListener('fullscreenchange', () => button('fullscreen').setAttribute('aria-label', document.fullscreenElement ? 'Exit fullscreen' : 'Enter fullscreen'));
document.addEventListener('keydown', event => {
  if (document.querySelector('dialog[open]')) return;
  if (event.key === '1' && state.stage === 'decision') button('choose').click();
  if (event.key === 'Escape') {
    if (isPlaying(state)) skip();
    else if (state.stage === 'targeting') button('cancel').click();
  }
});
document.addEventListener('visibilitychange', () => { if (document.hidden && isPlaying(state)) skip(false); });
window.addEventListener('pagehide', () => { playback.cancel(); if (isPlaying(state)) dispatch({ type: 'skip' }); });

scene.onLayout = placeTargets;
scene.onImpact = impactSound;
scene.onResizeDuringPlayback = () => skip(false);
scene.onReady = () => {
  ready = true; lab.dataset.ready = 'true'; lab.dataset.renderer = 'phaser';
  lab.dataset.assetFallback = String(scene.assetFailed);
  $('loading').hidden = true;
  render();
};
try {
  const game = new Phaser.Game({ type: Phaser.AUTO, parent: 'phaser-mount', backgroundColor: '#17272b',
    scale: { mode: Phaser.Scale.RESIZE, width: '100%', height: '100%' },
    render: { antialias: true, roundPixels: false }, fps: { target: 60, forceSetTimeOut: false },
    audio: { noAudio: true }, scene, banner: false });
  // Observe the stage itself: mobile browser chrome and rotation need not trigger
  // the same window-resize path as a desktop browser.
  const stage = $('phaser-mount');
  const resize = new ResizeObserver(() => {
    const { width, height } = stage.getBoundingClientRect();
    if (width > 0 && height > 0) game.scale.resize(width, height);
  });
  resize.observe(stage);
  game.events.once(Phaser.Core.Events.DESTROY, () => resize.disconnect());
  game.events.once(Phaser.Core.Events.READY, () => {
    game.canvas.addEventListener('webglcontextlost', event => {
      event.preventDefault(); skip(false); fail('The graphics context was interrupted. Reload to reopen the scene.');
    });
  });
} catch { fail('The renderer could not start. Reload the scene or use another browser.'); }
setTimeout(() => { if (!ready) fail('The scene could not finish loading. Check the connection and reload.'); }, 15000);
