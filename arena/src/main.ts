import { ArenaAudio, soundControl } from './audio';
import { ArenaScene } from './scene';
import { creaturesOf, isEnemy, labSceneFrame, type Creature, type LabState } from './types';
import { creatureButton, placeHitTargets } from './targets';
import { bootScene } from './boot';
import { initialLabState, isLabPlaying, labTransition, createLabPlayback } from '../../table/arena-lab-state.js';
import { CREATURE_ART } from '../../table/arena-art.js';
import { phaseLabel, tempoLabel } from '../../table/terms.js';

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const button = (id: string) => $<HTMLButtonElement>(id);
const lab = $('lab');
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const playback = createLabPlayback();
let state = initialLabState() as LabState;
let ready = false;
const audio = new ArenaAudio();
const nodes = new Map<string, HTMLButtonElement>();
const assetRoot = new URL('arena-art/', document.baseURI).href;
const scene = new ArenaScene(assetRoot);
const reduced = () => motion.matches || $<HTMLInputElement>('reduce-motion').checked;
const speed = () => Number($<HTMLSelectElement>('playback-speed').value);
const creature = (id: string | null) => creaturesOf(state).find(one => one.id === id);
const nameOf = (id: string | null) => creature(id)?.name ?? '';
const actorName = () => nameOf(state.action.actor);
const pad = (value: number) => String(value).padStart(2, '0');

function dispatch(action: { type: string; id?: string }) {
  state = labTransition(state, action) as LabState;
  render();
}

function makeCreature(one: Creature) {
  const node = creatureButton(one.id);
  node.className = `creature ${isEnemy(state, one.id) ? 'enemy' : 'ally'}`;
  node.addEventListener('click', () => {
    dispatch({ type: 'inspect', id: one.id });
    if (state.stage === 'targeting') dispatch({ type: 'target', id: one.id });
  });
  $('actor-layer').append(node);
  nodes.set(one.id, node);
}

function tagOf(one: Creature, selected: boolean, targetable: boolean) {
  if (!one.isAlive) return 'DEFEATED';
  if (selected) return 'SELECTED';
  if (targetable) return 'TARGET';
  return one.id === state.action.actor ? 'ACTING' : '';
}

function updateCreature(one: Creature) {
  const node = nodes.get(one.id)!;
  node.dataset.form = one.form;
  const targetable = state.stage === 'targeting' && state.action.targets.includes(one.id);
  const selected = one.id === state.target;
  node.disabled = !ready || isLabPlaying(state);
  node.classList.toggle('is-selected', selected);
  node.classList.toggle('is-targetable', targetable);
  node.classList.toggle('is-defeated', !one.isAlive);
  node.setAttribute('aria-pressed', String(targetable ? selected : one.id === state.inspected));
  node.setAttribute('aria-label', `${one.name}, ${one.health} of ${one.maxHealth} HP, ${one.energy} energy, ${one.totalDefense} defense. ${targetable ? 'Select target.' : 'Inspect creature.'}`);
  node.querySelector('.creature-name')!.textContent = one.name;
  node.querySelector('.creature-stats')!.textContent = `${one.health}/${one.maxHealth} HP`;
  (node.querySelector('.health-fill') as HTMLElement).style.width = `${100 * one.health / one.maxHealth}%`;
  node.querySelector('.creature-tag')!.textContent = tagOf(one, selected, targetable);
  const change = state.changes.find(it => it.creature === one.id);
  node.querySelector('.creature-result')!.textContent = change?.health == null ? '' : `${change.health} HP`;
}

function instruction() {
  const { label } = state.action;
  switch (state.stage) {
    case 'decision': return `Your move. Choose ${label}, then a target.`;
    case 'targeting': return state.target ? `${nameOf(state.target)} selected. Confirm your action.` : 'Select an opposing creature in the arena.';
    case 'windup': return `${actorName()} → ${nameOf(state.target)}`;
    case 'impact': return 'Impact. Reading the sample outcome…';
    case 'resolved': return `${nameOf(state.target)}: ${state.changes.find(one => one.creature === state.target)?.health} HP. Sample complete.`;
    default: return '';
  }
}

// What the one primary button does next, which is the stage's own word for it.
function confirmLabel() {
  if (state.stage === 'resolved') return 'Play again';
  return state.stage === 'targeting' ? `Cast ${state.action.label}` : `Choose ${state.action.label}`;
}

function callout() {
  if (isLabPlaying(state)) return { kicker: actorName().toUpperCase(), title: state.action.label };
  if (state.stage === 'targeting') return { kicker: 'CHOOSE YOUR TARGET', title: '' };
  return { kicker: '', title: state.stage === 'resolved' ? 'Sample complete' : '' };
}

function render() {
  lab.dataset.stage = state.stage;
  lab.dataset.motion = reduced() ? 'reduced' : 'full';
  creaturesOf(state).forEach(updateCreature);
  const actor = creature(state.action.actor)!;
  $('round-number').textContent = pad(state.board.roundNumber);
  $('round-phase').textContent = phaseLabel(state.board.phase).toUpperCase();
  $('actor-name').textContent = actor.name;
  const art = CREATURE_ART.find(one => one.form === actor.form) ?? CREATURE_ART[0];
  $('actor-portrait').style.backgroundImage = `url("${assetRoot}${art.file}")`;
  $('actor-meta').textContent = `${tempoLabel(state.board.timeline[0].speed)} · ${actor.energy} energy`;
  $('action-label').textContent = state.action.label;
  $('instruction').textContent = ready ? instruction() : 'Preparing the scene…';
  button('choose').disabled = !ready || state.stage !== 'decision';
  button('choose').setAttribute('aria-pressed', String(state.stage !== 'decision'));
  button('cancel').hidden = state.stage !== 'targeting';
  button('skip').hidden = !isLabPlaying(state);
  button('confirm').hidden = isLabPlaying(state);
  button('confirm').disabled = !ready || (state.stage !== 'resolved' && !(state.stage === 'targeting' && state.target));
  button('confirm').textContent = confirmLabel();
  const inspected = creature(state.inspected)!;
  $('inspect-name').textContent = inspected.name;
  $('inspect-stats').textContent = `${inspected.health}/${inspected.maxHealth} HP · ${inspected.energy} energy · ${inspected.totalDefense} defense`;
  $('inspect-effects').textContent = inspected.conditions.join(' / ') || 'No ongoing effects';
  const { kicker, title } = callout();
  $('callout-kicker').textContent = kicker;
  $('callout-title').textContent = title;
  scene.present(labSceneFrame(state), reduced(), speed());
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

creaturesOf(state).forEach(makeCreature);
for (const [index, slot] of state.board.timeline.entries()) {
  const item = document.createElement('li');
  const yours = slot.owner === state.board.slot;
  if (index === 0) item.setAttribute('aria-current', 'step');
  if (!yours) item.className = 'enemy';
  item.textContent = `${index + 1} ${nameOf(slot.creature)}`;
  item.title = `${nameOf(slot.creature)} · ${yours ? 'Yours' : 'Opponent'} · ${tempoLabel(slot.speed)}`;
  const side = document.createElement('span');
  side.className = 'timeline-side';
  side.textContent = `${yours ? 'You' : 'Foe'} · ${tempoLabel(slot.speed)}`;
  item.append(side); $('timeline').append(item);
}
// The HUD reads from the state from the first paint, so nothing is spelled twice between the page and the fixture.
render();

button('choose').addEventListener('click', () => {
  dispatch({ type: 'choose' });
  nodes.get(state.action.targets[0])!.focus({ preventScroll: true });
});
button('cancel').addEventListener('click', () => {
  dispatch({ type: 'cancel' }); button('choose').focus({ preventScroll: true });
});
button('confirm').addEventListener('click', () => {
  if (state.stage === 'resolved') { reset(); return; }
  dispatch({ type: 'play' });
  if (!isLabPlaying(state)) return;
  playback.play((action: { type: string }) => {
    const focus = document.activeElement === button('skip');
    dispatch(action);
    if (state.stage === 'resolved' && focus) button('confirm').focus({ preventScroll: true });
  }, { reducedMotion: reduced(), speed: speed() });
  (isLabPlaying(state) ? button('skip') : button('confirm')).focus({ preventScroll: true });
});
button('skip').addEventListener('click', () => skip());
button('reset').addEventListener('click', reset);
button('reload').addEventListener('click', () => location.reload());
button('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
button('inspect').addEventListener('click', () => $<HTMLDialogElement>('inspect-dialog').showModal());
soundControl(audio, $<HTMLInputElement>('sound'));
// A presentation setting that changes mid-cast settles the cast on its final frame rather than replaying
// it under the new setting: the outcome is the same either way, and the player asked for less, not more.
function settingChanged() { if (isLabPlaying(state)) skip(false); else render(); }
$<HTMLInputElement>('reduce-motion').addEventListener('change', settingChanged);
motion.addEventListener('change', settingChanged);
$<HTMLSelectElement>('playback-speed').addEventListener('change', settingChanged);
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
    if (isLabPlaying(state)) skip();
    else if (state.stage === 'targeting') button('cancel').click();
  }
});
document.addEventListener('visibilitychange', () => { if (document.hidden && isLabPlaying(state)) skip(false); });
window.addEventListener('pagehide', () => { audio.stop(); playback.cancel(); if (isLabPlaying(state)) dispatch({ type: 'skip' }); });

scene.onLayout = positions => placeHitTargets(creaturesOf(state).map(one => nodes.get(one.id)!), positions);
scene.onImpact = () => audio.play({ family: 'physical', motif: 'impact', delivery: 'melee' });
scene.onCancel = () => audio.silence();
scene.onResizeDuringPlayback = () => skip(false);
scene.onReady = () => {
  ready = true; lab.dataset.ready = 'true'; lab.dataset.renderer = 'phaser';
  lab.dataset.assetFallback = String(scene.assetFailed);
  $('loading').hidden = true;
  render();
};
try {
  bootScene(scene, $('phaser-mount'), {
    isReady: () => ready,
    onContextLost: () => { skip(false); fail('The graphics context was interrupted. Reload to reopen the scene.'); },
    onNeverReady: () => fail('The scene could not finish loading. Check the connection and reload.'),
  });
} catch { fail('The renderer could not start. Reload the scene or use another browser.'); }
