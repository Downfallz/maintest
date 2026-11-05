import { arenaOrder, createArenaOrderReveal } from '../../table/arena-order.js';
import { orderControls } from './order';
import { createArenaGrowth } from '../../table/arena-growth.js';
import { growthControls } from './growth';
import { createArenaUpkeep } from '../../table/arena-upkeep.js';
import { upkeepControls } from './upkeep';
import { createArenaPlayback } from '../../table/arena-playback.js';
import { createArenaHistory } from '../../table/arena-history.js';
import { historyControls } from './history';
import { arenaGuidance } from '../../table/arena-guidance.js';
import { guidanceControls } from './guidance';
import { ArenaAudio, soundControl } from './audio';
import { ArenaScene } from './scene';
import type { SceneFrame } from './types';
import { creatureButton, placeHitTargets } from './targets';
import { bootScene } from './boot';
import { combatControls } from './combat';
import { createArenaCombat } from '../../table/arena-combat.js';
import { planningControls, type Catalogue } from './planning';
import { matchControls } from './match';
import { createArenaPlanning } from '../../table/arena-planning.js';
import { createArenaMatch } from '../../table/arena-match.js';
import { replayOf, operatorToken } from '../../table/session.js';
import { httpTransport, replayTransport } from '../../table/transport.js';
import { arenaSeats, seatPath } from '../../table/arena-seats.js';
import { createLiveConnection } from '../../table/arena-live.js';
import { chipText, chipSource, laneOf } from '../../table/board.js';
import { signInPath } from '../../table/account.js';
import { seatLabel } from '../../table/invitation.js';
import { phaseLabel, stepLabel } from '../../table/terms.js';

interface Choice { seat: string; token: string; table: { id: string; format: string; round?: number; seats?: { seat: string; name: string }[] } }
interface Creature { form: string; description: string; id: string; name: string; label: string; enemy: boolean; health: number; maxHealth: number; energy: number;
  totalDefense: number; currentInitiative: number; isAlive: boolean; isStunned: boolean; isStunImmune: boolean;
  knownSpells?: string[]; conditions: { effect: { kind?: string; $type?: string; amount?: number }; remainingRounds: number | null }[] }
interface View { board: object; over: boolean; waiting: unknown; waitingFor: string | null;
  outcome?: { winner?: string; reason?: string } }
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if ($(id).textContent !== value) $(id).textContent = value; };
const audio = new ArenaAudio();
const scene = new ArenaScene(new URL('arena-art/', document.baseURI).href);
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const reduced = () => motion.matches || $<HTMLInputElement>('reduce-motion').checked;
// Short reads time out; the stream keeps its connection's abort signal instead.
const request = (path: RequestInfo | URL, options?: RequestInit) =>
  fetch(path, { ...options, signal: options?.signal ?? AbortSignal.timeout(15000) });
let storage: Storage | null = null;
try { storage = localStorage; } catch { /* A linked seat still works when storage is unavailable. */ }
let ready = false;
let receiving = false;
let selected: Choice | null = null;
let view: View | null = null;
let catalogue: Catalogue | null = null;
let inspected: string | null = null;
let creatures: Creature[] = [];
let frame: SceneFrame = { stage: 'decision', target: null, actor: null, targets: [], creatures: [] };
let discovering = 0;
let rendererFailed = false;
const nodes = new Map<string, HTMLButtonElement>();
const name = (id: unknown) => creatures.find(one => one.id === String(id))?.label ?? `Creature ${id}`;
const seatName = (seat: string): string => seatLabel(seat);
const connection = createLiveConnection({ onState: status, onView: (next: View | null, content: Catalogue | null) => {
  receiving = true;
  try {
    view = next; catalogue = content ?? catalogue;
    history.receive(next, catalogue); upkeep.receive(next); orderReveal.receive(next); growth.receive(next, catalogue);
    combat.receive(next); planning.receive(next); match.receive(next, catalogue);
    playback.receive(next, new Map(catalogue?.cards?.map(card => [card.id, card]) ?? []),
      { effects: catalogue?.effects ?? [], packages: new Map(catalogue?.packages?.map(pack => [pack.id, pack]) ?? []), animate: visible() && !combat.model().opened && !planning.model().opened && !match.model().active });
  } finally { receiving = false; }
  render();
} });
const visible = () => ready && !rendererFailed && !document.hidden &&
  !['inspect-dialog', 'settings-dialog', 'result-dialog', 'round-dialog', 'match-dialog', 'concede-dialog', 'history-dialog', 'help-dialog', 'upkeep-dialog', 'order-dialog', 'growth-dialog'].some(id => $<HTMLDialogElement>(id).open);
let tabStorage: Storage | null = null;
try { tabStorage = sessionStorage; } catch { /* The receipt still works without storage. */ }
const upkeep = createArenaUpkeep(tabStorage);
const history = createArenaHistory({ load: () => connection.history(), onChange: () => render() });
const orderReveal = createArenaOrderReveal();
const growth = createArenaGrowth();
const combat = createArenaCombat({ connection, onChange: () => render(), visible });
const planning = createArenaPlanning({ connection, onChange: () => render(), visible });
const match = createArenaMatch({ connection, onChange: () => render() });
const playback = createArenaPlayback({ onChange: () => render(), onUpkeepHandled: round => upkeep.presented(round) });
const controls = combatControls(combat, name, () => playback.skip());
const planningUI = planningControls(planning, name, () => playback.skip());
// The round review is read-only: it covers a draft without dropping it, the way the inspection does.
const matchUI = matchControls(match, () => { playback.skip(); combat.close(); planning.close(); }, () => playback.skip());
const historyUI = historyControls(history, () => playback.skip(), () => render());
const growthUI = growthControls(growth, () => playback.skip(), () => render());
const helpUI = guidanceControls(storage, () => playback.skip(), () => render());
const upkeepUI = upkeepControls(upkeep, id => {
  inspected = id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal();
}, () => render());
const orderUI = orderControls(orderReveal, () => playback.skip(), id => {
  inspected = id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal();
}, () => render());
let wasTargeting = false;

function status(state: string) {
  $('arena').dataset.connection = state;
  // Losing the connection or the seat fences a pending sound opt-in; an animation's own cancellation does not.
  if (state === 'stale' || state === 'signin' || state === 'revoked') audio.stop();
  if (state === 'stale') playback.skip();
  const messages: Record<string, string> = { live: 'LIVE', connecting: 'Connecting…',
    stale: 'Connection interrupted · retrying', signin: 'Sign in to continue', revoked: 'Seat access is no longer valid' };
  text('connection', messages[state] ?? state);
  if (state === 'live') text('updated', `Updated ${new Date().toLocaleTimeString()}`);
  if ((state === 'signin' || state === 'revoked') && replayOf(location.search)) {
    // A recording is read with the operator token the admin panel holds; no account sign-in grants it.
    text('load-title', 'This recording needs the operator');
    text('load-detail', 'Open it from the admin panel, which holds the operator token.');
    $<HTMLAnchorElement>('access-link').href = '/admin'; text('access-link', 'Open admin');
    $('retry').hidden = false;
  } else if (state === 'signin' || state === 'revoked') {
    selected = null;
    $('play-link').hidden = true;
    text('perspective', 'YOUR VIEWPOINT');
    text('load-title', messages[state]);
    text('load-detail', state === 'signin' ? 'Sign in with the account that holds this seat.' : 'Return to the tables to find a current seat.');
    $<HTMLAnchorElement>('access-link').href = state === 'signin' ? signInPath(location.pathname + location.search) : '/';
    text('access-link', state === 'signin' ? 'Sign in' : 'Open tables');
    $('retry').hidden = false;
  } else if (!view) {
    text('load-title', state === 'stale' ? 'Waiting for the connection' : 'Opening your board');
    text('load-detail', 'The host is verifying this seat and loading its current board.');
    $('retry').hidden = state !== 'stale';
  }
  combat.setOnline(state === 'live'); planning.setOnline(state === 'live');
}

function renderInspection() {
  const one = creatures.find(one => one.id === inspected);
  if (!one) { $<HTMLDialogElement>('inspect-dialog').close(); return; }
  text('inspect-name', one.label);
  $('inspect-growth').dataset.growthCreature = one.id;
  text('inspect-side', one.enemy ? 'OPPONENT' : 'YOUR TEAM');
  text('inspect-appearance', `Appearance: ${one.description}`);
  text('inspect-stats', `${one.health}/${one.maxHealth} HP · ${one.energy} energy · ${one.totalDefense} defense · ${one.currentInitiative} initiative`);
  const cards = new Map(catalogue?.cards?.map(card => [card.id, card]) ?? []);
  const effects = one.conditions.map(condition => {
    const lane = laneOf(condition);
    const duration = lane === null ? 'permanent' : lane === 'new' ? 'new · first countdown pending' : `${lane} rounds`;
    return [chipText(condition), duration, chipSource(condition, cards)].filter(Boolean).join(' · ');
  });
  text('inspect-effects', [!one.isAlive ? 'Defeated' : '', one.isStunned ? 'Stunned' : '', one.isStunImmune ? 'Stun immune' : '', ...effects].filter(Boolean).join(' / ') || 'No ongoing effects');
  $('inspect-spells').replaceChildren(...(one.knownSpells ?? []).map(id => {
    const item = document.createElement('li'); item.textContent = catalogue?.cards?.find(card => card.id === id)?.name ?? id; return item;
  }));
}

// The one word over a figure: what stops it acting first, then whose slot it is.
function tagOf(one: Creature) {
  if (frame.selected?.includes(one.id)) return 'SELECTED';
  if (!one.isAlive) return 'DEFEATED';
  if (one.isStunned) return 'STUNNED';
  return frame.actor === one.id ? 'ACTING' : '';
}

// The dock's heading: the match's end, the creature whose slot it is, or where the round stands.
function headline(board: { turn: { slot: { creature: unknown } } | null; phase: string }) {
  if (!view) return 'Live arena';
  if (view.over) return 'Match complete';
  if (board.turn) return name(board.turn.slot.creature);
  return phaseLabel(board.phase) || 'Waiting room';
}


function connectionText(replaying: boolean, combatOpened: boolean) {
  if (replaying) return 'RECORDED REPLAY';
  if (view?.over) return 'MATCH COMPLETE';
  if (combatOpened) return 'LIVE · COMBAT';
  return planning.model().opened ? 'LIVE · COMMAND' : 'LIVE';
}

let receiptKey = '', receiptRound: number | null = null;
function renderReceipt(receipt: ReturnType<typeof playback.model>['receipt'], round: number | null) {
  $('playback-receipt').hidden = !receipt;
  const key = JSON.stringify([round, receipt]);
  if (key === receiptKey) return;
  const scroll = receiptRound === round ? $('playback-receipt').scrollTop : 0;
  receiptKey = key; receiptRound = round;
  $('receipt-title').textContent = receipt?.category === 'Upkeep' ? 'Upkeep' : receipt?.title ?? '';
  $('receipt-sections').replaceChildren(...(receipt?.sections ?? []).map((section: { title: string; items: { text: string; tone: string }[] }) => {
    const group = document.createElement('section'), title = document.createElement('h3');
    title.textContent = section.title; group.append(title);
    for (const row of section.items) { const line = document.createElement('p'); line.textContent = row.text; line.dataset.tone = row.tone; group.append(line); }
    return group;
  }));
  $('playback-receipt').scrollTop = scroll;
}

function render() {
  if (receiving) return;
  const playbackState = playback.model();
  const replay = match.model();
  const board = replay.active ? replay.board! : playbackState.board;
  creatures = board.creatures;
  const action = combat.model();
  const targeting = action.opened && Boolean(action.target);
  const guide = arenaGuidance(rendererFailed ? null : view, catalogue, {
    accepted: action.accepted || planning.model().accepted, targeting,
    historical: playbackState.active || replay.active, online: $('arena').dataset.connection === 'live' });
  frame = (replay.active ? board.frame : playbackState.frame) as SceneFrame;
  $('arena').dataset.playback = playbackState.stage;
  $('arena').dataset.sequence = String(playbackState.sequence ?? '');
  $('playback-skip').hidden = !playbackState.active;
  text('playback-skip', `Skip to present (${playbackState.remaining})`);
  $('playback-pacing').hidden = !view || replay.active;
  $<HTMLSelectElement>('playback-mode').value = playbackState.mode;
  $<HTMLSelectElement>('playback-preference').value = playbackState.mode;
  $('playback-next').hidden = !playbackState.active || playbackState.mode !== 'step';
  $<HTMLButtonElement>('playback-next').disabled = !playbackState.canNext;
  text('playback-next', playbackState.remaining > 1 ? 'Next result' : 'Return to present');
  renderReceipt(playbackState.receipt, board.round);
  $('arena').dataset.receipt = String(Boolean(playbackState.receipt));
  $('playback-result').hidden = !playbackState.summary;
  text('result-text', playbackState.summary);
  text('playback-announcement', playbackState.stage === 'windup' ? '' : playbackState.summary);
  $('playback-result').setAttribute('aria-label', `Action result: ${playbackState.summary}`);
  text('result-label', playbackState.active ? 'Confirmed result' : 'Last result');
  if (!playbackState.summary) $<HTMLDialogElement>('result-dialog').close();
  if (targeting) frame = { ...frame, stage: 'targeting', actor: String(action.actor),
    targets: action.emptyCast ? [] : action.target.legalTargets.candidates.map(String), selected: action.picked.map(String) };
  growthUI.render(view, catalogue, visible() && !playbackState.active && !replay.active && !action.opened && !planning.model().opened);
  controls.render(action, catalogue, growth.highlights(action, visible() && !playbackState.active && !replay.active)); planningUI.render(planning.model(), catalogue); matchUI.render(); historyUI.render(history.model());
  $('actor-layer').setAttribute('aria-label', targeting ? 'Select targets' : 'Inspect creatures');
  if ($('arena').dataset.connection === 'live') text('connection', playbackState.active ? 'READING CONFIRMED EVENTS' : connectionText(replay.active, action.opened));
  const ids = new Set(creatures.map(one => one.id));
  for (const [id, node] of nodes) if (!ids.has(id)) { node.remove(); nodes.delete(id); }
  for (const one of creatures) {
    let node = nodes.get(one.id);
    if (!node) {
      node = creatureButton(one.id);
      node.addEventListener('click', () => { if (controls.target(one.id)) return; inspected = one.id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal(); });
      $('actor-layer').append(node); nodes.set(one.id, node);
    }
    node.dataset.form = one.form;
    node.className = `creature ${one.enemy ? 'enemy' : 'ally'}${one.isAlive ? '' : ' is-defeated'}`;
    const targetable = targeting && frame.targets.includes(one.id);
    if (targetable) node.classList.add('is-targetable');
    node.disabled = !ready || (targeting && (!targetable || !action.canEdit));
    if (targeting) node.setAttribute('aria-pressed', String(frame.selected?.includes(one.id) === true));
    else node.removeAttribute('aria-pressed');
    const condition = tagOf(one);
    let effect = node.querySelector<HTMLElement>('.creature-effect');
    if (!effect) { effect = document.createElement('span'); effect.className = 'creature-effect'; node.append(effect); }
    const effects = playbackState.stage === 'impact' || playbackState.stage === 'resolved'
      ? playbackState.cue?.effects.filter((item: { creature: string }) => item.creature === one.id) ?? [] : [];
    effect.textContent = effects.map((item: { text: string }) => item.text).join(' · ');
    effect.hidden = effects.length === 0;
    node.querySelector('.creature-name')!.textContent = one.label;
    node.querySelector('.creature-tag')!.textContent = condition;
    node.querySelector('.creature-stats')!.textContent = `${one.health}/${one.maxHealth} HP · ${one.energy} EN`;
    (node.querySelector('.health-fill') as HTMLElement).style.width = `${one.maxHealth > 0 ? Math.max(0, Math.min(100, 100 * one.health / one.maxHealth)) : 0}%`;
    node.setAttribute('aria-label', `${one.enemy ? 'Opponent' : 'Your team'}: ${one.label}, ${one.health} of ${one.maxHealth} HP, ${one.energy} energy, ${one.totalDefense} defense. ${condition}. ${targeting ? 'Select target.' : 'Inspect creature.'}`);
  }
  text('round-number', board.round == null ? '—' : String(board.round).padStart(2, '0'));
  text('round-phase', phaseLabel(board.phase).toUpperCase());
  text('ally-label', view ? 'YOUR TEAM' : ''); text('enemy-label', view ? 'OPPONENT' : '');
  const order = arenaOrder(view?.waiting && !view.over ? null : board);
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, false);
  text('actor-name', playbackState.receipt ? playbackState.receipt.category : playbackState.active ? name(frame.actor) : headline(board));
  const phase = view ? stepLabel(board.subPhase) || 'Waiting for players' : 'Choose a seat to enter';
  text('actor-meta', playbackState.active ? playbackState.label : phase);
  text('instruction', playbackState.active ? `${playbackState.mode === 'step' ? 'Read each result, then choose Next.' : 'Results advance automatically.'} Skip to present or open your next choice at any time.` : guide.objective);
  $('instruction').hidden = Boolean(action.error || planning.model().error || replay.error);
  $('loading').hidden = Boolean(view && ready && !rendererFailed);
  if (!view) {
    inspected = null; renderInspection();
    for (const id of ['inspect-name', 'inspect-side', 'inspect-stats', 'inspect-appearance', 'inspect-effects', 'inspect-spells']) $(id).replaceChildren();
    text('updated', 'No board loaded');
  } else renderInspection();
  $('arena').dataset.rate = String(playbackState.rate);
  scene.present(frame, reduced(), playbackState.rate);
  if (targeting && !wasTargeting) (document.querySelector<HTMLButtonElement>('.is-targetable:not(:disabled)') ?? $<HTMLButtonElement>('combat-confirm')).focus({ preventScroll: true });
  wasTargeting = targeting;
  const canAuto = () => ready && !rendererFailed && !document.hidden && $('arena').dataset.connection === 'live' &&
    !playbackState.active && !replay.active && !action.opened && !action.sending &&
    !planning.model().opened && !planning.model().sending && !document.querySelector('dialog[open]') && !orderUI.isOpening();
  upkeepUI.render(canAuto());
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, canAuto() && !upkeep.model().pending);
  helpUI.render(guide, canAuto() && !upkeep.model().pending && !orderReveal.model().pending);
}

function choose(choice: Choice) {
  audio.stop();
  if (rendererFailed) return;
  discovering++;
  selected = choice;
  $('seat-choices').replaceChildren(); $('retry').hidden = true;
  text('perspective', `${seatName(choice.seat).toUpperCase()} · YOUR VIEWPOINT`);
  $<HTMLAnchorElement>('play-link').href = seatPath('/play', choice, storage); $('play-link').hidden = false;
  upkeep.reset(`${choice.table.id}:${choice.seat}`); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(choice.seat); planning.reset(choice.seat); match.reset(choice.seat);
  connection.start(httpTransport(choice.seat, choice.token, request), catalogue);
}

async function discover() {
  audio.stop();
  if (rendererFailed) return;
  const ticket = ++discovering;
  connection.stop(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset(); selected = null; view = null; catalogue = null; render();
  $('play-link').hidden = true; $('retry').hidden = true; $('seat-choices').replaceChildren();
  text('perspective', 'YOUR VIEWPOINT'); text('connection', 'Choose a seat');
  text('load-title', 'Choose your viewpoint'); text('load-detail', 'Verifying the seats saved in this browser…');
  text('access-link', 'Open tables'); $<HTMLAnchorElement>('access-link').href = '/';
  const recorded = replayOf(location.search);
  if (recorded) {
    text('perspective', `${seatName(recorded.seat).toUpperCase()} · RECORDED MATCH`);
    $('switch-seat').hidden = true;
    match.reset(recorded.seat);
    connection.start(replayTransport(recorded.id, recorded.seat, operatorToken(storage), request));
    return;
  }
  try {
    const result = await arenaSeats(location.search, storage, request);
    if (ticket !== discovering) return;
    text('load-detail', result.choices.length ? 'Only the selected seat will be loaded. Choose who is holding this device.' : 'No accepted seat was found for this link or browser.');
    for (const choice of result.choices as Choice[]) {
      const button = document.createElement('button'); button.type = 'button';
      const players = choice.table.seats?.map(one => one.name).join(' vs ') ?? choice.table.format;
      button.textContent = `${seatName(choice.seat)} · ${players} · ${choice.table.id}`;
      button.addEventListener('click', () => choose(choice)); $('seat-choices').append(button);
    }
    if (result.invitations.length) {
      text('load-detail', 'This link is an invitation. Accept it at the table before opening its live view.');
      text('access-link', 'Review invitation'); $<HTMLAnchorElement>('access-link').href = `/play${location.search}`;
    }
    $('retry').hidden = result.choices.length > 0;
  } catch (error) {
    if (ticket !== discovering) return;
    if ((error as { status?: number }).status === 401) status('signin');
    else { text('load-detail', 'Could not verify saved seats. Check the connection and try again.'); $('retry').hidden = false; }
  }
}

scene.onLayout = positions => placeHitTargets(creatures.map(one => nodes.get(one.id)!), positions);
scene.onReady = () => { ready = true; $('arena').dataset.ready = 'true'; $('arena').dataset.assetFallback = String(scene.assetFailed); render(); };
function failRenderer() {
  audio.stop();
  rendererFailed = true; ready = false; view = null; connection.stop(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset();
  render(); text('load-title', 'The renderer was interrupted'); text('load-detail', 'Reload to restore the scene, or continue playing at the table.');
  text('retry', 'Reload scene'); $('retry').hidden = false;
}
$('switch-seat').addEventListener('click', () => { void discover(); });
$('retry').addEventListener('click', () => { if (rendererFailed) location.reload(); else if (selected) void connection.refresh(); else void discover(); });
scene.onCancel = () => audio.silence();
scene.onImpact = () => {
  if (playback.model().active && !document.hidden && !match.model().active) audio.play(frame.cue?.visual);
};
soundControl(audio, $<HTMLInputElement>('sound'));
$<HTMLSelectElement>('playback-speed').value = '1';
$('playback-speed').addEventListener('change', () => playback.setRate(Number($<HTMLSelectElement>('playback-speed').value)));
$('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
function motionChanged() { playback.setReduced(reduced()); render(); }
$('reduce-motion').addEventListener('change', motionChanged); motion.addEventListener('change', motionChanged);
playback.setReduced(reduced());
$('playback-preference').addEventListener('change', () => playback.setMode($<HTMLSelectElement>('playback-preference').value));
$('playback-mode').addEventListener('change', () => playback.setMode($<HTMLSelectElement>('playback-mode').value));
$('playback-next').addEventListener('click', () => {
  playback.advance();
  if (!playback.model().active && !document.querySelector('dialog[open]'))
    (['planning-open', 'combat-open', 'match-open'].map(id => $(id)).find(node => !node.hidden) ?? $('settings')).focus({ preventScroll: true });
});
$('playback-skip').addEventListener('click', () => playback.skip());
$('playback-result').addEventListener('click', () => $<HTMLDialogElement>('result-dialog').showModal());
scene.onResizeDuringPlayback = () => playback.skip();
// A held Step or a text receipt has no running Phaser tween, but rotation still cancels it.
window.addEventListener('resize', () => { if (playback.model().active) playback.skip(); });
// The board goes with the page's eyes; the catalogue stays, it cannot change while the host runs.
function suspend() { audio.stop(); discovering++; connection.stop(); view = null; upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset(); render(); text('connection', 'Paused'); }
document.addEventListener('visibilitychange', () => {
  if (document.hidden) suspend(); else if (selected) choose(selected); else void discover();
});
window.addEventListener('pagehide', suspend);
window.addEventListener('pageshow', event => { if (event.persisted) { if (selected) choose(selected); else void discover(); } });
try {
  bootScene(scene, $('phaser-mount'), { isReady: () => ready || rendererFailed, onContextLost: failRenderer, onNeverReady: failRenderer });
} catch { failRenderer(); }
void discover();

for (const id of ['inspect-dialog', 'settings-dialog', 'result-dialog', 'round-dialog', 'match-dialog', 'concede-dialog']) {
  $(id).addEventListener('close', () => render());
}
