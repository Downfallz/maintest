import Phaser from 'phaser';
import { ArenaScene } from './scene';
import type { Placement, SceneFrame } from './types';
import { httpTransport } from '../../table/transport.js';
import { arenaSeats, seatPath } from '../../table/arena-seats.js';
import { createLiveConnection, livePresentation } from '../../table/arena-live.js';
import { chipText, chipSource, laneOf } from '../../table/board.js';
import { signInPath } from '../../table/account.js';
import { phaseLabel, tempoLabel, decisionLabel, stepLabel } from '../../table/terms.js';

interface Choice { seat: string; token: string; table: { id: string; format: string; round?: number; seats?: { seat: string; name: string }[] } }
interface Creature { id: string; name: string; label: string; enemy: boolean; health: number; maxHealth: number; energy: number;
  totalDefense: number; currentInitiative: number; isAlive: boolean; isStunned: boolean; isStunImmune: boolean;
  knownSpells: string[]; conditions: { effect: { kind?: string; $type?: string; amount?: number }; remainingRounds: number | null }[] }
interface View { board: object; over: boolean; waiting: unknown; waitingFor: string | null;
  outcome?: { winner?: string; reason?: string } }
interface Catalogue { cards?: { id: string; name: string }[] }
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { $(id).textContent = value; };
const scene = new ArenaScene(new URL('arena-art/', document.baseURI).href);
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const reduced = () => motion.matches || $<HTMLInputElement>('reduce-motion').checked;
// Short reads time out; the stream keeps its connection's abort signal instead.
const request = (path: RequestInfo | URL, options?: RequestInit) =>
  fetch(path, { ...options, signal: options?.signal ?? AbortSignal.timeout(15000) });
let storage: Storage | null = null;
try { storage = localStorage; } catch { /* A linked seat still works when storage is unavailable. */ }
let ready = false;
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
const seatName = (seat: string) => seat === 'player1' ? 'Player 1' : 'Player 2';
const connection = createLiveConnection({ onState: status, onView: (next: View | null, content: Catalogue | null) => {
  view = next; catalogue = content; render();
} });

function status(state: string) {
  $('arena').dataset.connection = state;
  const messages: Record<string, string> = { live: 'LIVE · READ ONLY', connecting: 'Connecting…',
    stale: 'Connection interrupted · retrying', signin: 'Sign in to continue', revoked: 'Seat access is no longer valid' };
  text('connection', messages[state] ?? state);
  if (state === 'live') text('updated', `Updated ${new Date().toLocaleTimeString()}`);
  if (state === 'signin' || state === 'revoked') {
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
}

function renderInspection() {
  const one = creatures.find(one => one.id === inspected);
  if (!one) { $<HTMLDialogElement>('inspect-dialog').close(); return; }
  text('inspect-name', one.label);
  text('inspect-side', one.enemy ? 'OPPONENT' : 'YOUR TEAM');
  text('inspect-stats', `${one.health}/${one.maxHealth} HP · ${one.energy} energy · ${one.totalDefense} defense · ${one.currentInitiative} initiative`);
  const cards = new Map(catalogue?.cards?.map(card => [card.id, card]) ?? []);
  const effects = one.conditions.map(condition => {
    const lane = laneOf(condition);
    const duration = lane === null ? 'permanent' : lane === 'new' ? 'new · first countdown pending' : `${lane} rounds`;
    return [chipText(condition), duration, chipSource(condition, cards)].filter(Boolean).join(' · ');
  });
  text('inspect-effects', [!one.isAlive ? 'Defeated' : '', one.isStunned ? 'Stunned' : '', one.isStunImmune ? 'Stun immune' : '', ...effects].filter(Boolean).join(' / ') || 'No ongoing effects');
  $('inspect-spells').replaceChildren(...one.knownSpells.map(id => {
    const item = document.createElement('li'); item.textContent = catalogue?.cards?.find(card => card.id === id)?.name ?? id; return item;
  }));
}

function render() {
  const board = livePresentation(view);
  creatures = board.creatures;
  frame = board.frame as SceneFrame;
  const ids = new Set(creatures.map(one => one.id));
  for (const [id, node] of nodes) if (!ids.has(id)) { node.remove(); nodes.delete(id); }
  for (const one of creatures) {
    let node = nodes.get(one.id);
    if (!node) {
      node = document.createElement('button'); node.type = 'button'; node.dataset.creature = one.id;
      node.innerHTML = '<span class="creature-tag"></span><span class="caption"><span class="creature-name"></span><span class="health-track" aria-hidden="true"><span class="health-fill"></span></span><span class="creature-stats"></span></span>';
      node.addEventListener('click', () => { inspected = one.id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal(); });
      $('actor-layer').append(node); nodes.set(one.id, node);
    }
    node.className = `creature ${one.enemy ? 'enemy' : 'ally'}${one.isAlive ? '' : ' is-defeated'}`;
    node.disabled = !ready;
    const condition = !one.isAlive ? 'DEFEATED' : one.isStunned ? 'STUNNED' : frame.actor === one.id ? 'ACTING' : '';
    node.querySelector('.creature-name')!.textContent = one.label;
    node.querySelector('.creature-tag')!.textContent = condition;
    node.querySelector('.creature-stats')!.textContent = `${one.health}/${one.maxHealth} HP · ${one.energy} EN`;
    (node.querySelector('.health-fill') as HTMLElement).style.width = `${one.maxHealth > 0 ? Math.max(0, Math.min(100, 100 * one.health / one.maxHealth)) : 0}%`;
    node.setAttribute('aria-label', `${one.enemy ? 'Opponent' : 'Your team'}: ${one.label}, ${one.health} of ${one.maxHealth} HP, ${one.energy} energy, ${one.totalDefense} defense. ${condition}. Inspect creature.`);
  }
  text('round-number', board.round == null ? '—' : String(board.round).padStart(2, '0'));
  text('round-phase', phaseLabel(board.phase).toUpperCase());
  text('ally-label', view ? 'YOUR TEAM' : ''); text('enemy-label', view ? 'OPPONENT' : '');
  $('timeline').replaceChildren(...board.timeline.map((slot: { creature: unknown; owner: string; position: number; speed: string }, index: number) => {
    const item = document.createElement('li'); item.textContent = `${slot.position} ${name(slot.creature)}`;
    const enemy = creatures.find(one => one.id === String(slot.creature))?.enemy;
    if (enemy) item.className = 'enemy';
    if (index === board.cursor) item.setAttribute('aria-current', 'step');
    const side = document.createElement('span'); side.className = 'timeline-side'; side.textContent = `${enemy ? 'Foe' : 'You'} · ${tempoLabel(slot.speed)}`; item.append(side); return item;
  }));
  $('timeline').hidden = board.timeline.length === 0;
  $('no-timeline').hidden = board.timeline.length > 0;
  text('no-timeline', view ? 'Activation order has not been set' : 'Waiting for a board');
  text('actor-name', view?.over ? 'Match complete' : board.turn ? name(board.turn.slot.creature) : view ? phaseLabel(board.phase) || 'Waiting room' : 'Live arena');
  text('actor-meta', view ? stepLabel(board.subPhase) || 'Waiting for players' : 'Choose a seat to enter');
  text('instruction', view?.over ? (view.outcome?.winner ? `${view.outcome.winner} wins. Review the match at the table.` : 'The match has ended. Review it at the table.') : view?.waitingFor ? `${decisionLabel(view.waitingFor)} is waiting at the table. Inspect creatures here; return there to decide.` : 'Inspect the real board here. Make decisions at the table.');
  $('loading').hidden = Boolean(view && ready && !rendererFailed);
  if (!view) {
    inspected = null; renderInspection();
    for (const id of ['inspect-name', 'inspect-side', 'inspect-stats', 'inspect-effects', 'inspect-spells']) $(id).replaceChildren();
    text('updated', 'No board loaded');
  } else renderInspection();
  scene.present(frame, reduced(), 1);
}

function choose(choice: Choice) {
  if (rendererFailed) return;
  discovering++;
  selected = choice;
  $('seat-choices').replaceChildren(); $('retry').hidden = true;
  text('perspective', `${seatName(choice.seat).toUpperCase()} · YOUR VIEWPOINT`);
  $<HTMLAnchorElement>('play-link').href = seatPath('/play', choice, storage); $('play-link').hidden = false;
  connection.start(httpTransport(choice.seat, choice.token, request));
}

async function discover() {
  if (rendererFailed) return;
  const ticket = ++discovering;
  connection.stop(); selected = null; view = null; catalogue = null; render();
  $('play-link').hidden = true; $('retry').hidden = true; $('seat-choices').replaceChildren();
  text('perspective', 'YOUR VIEWPOINT'); text('connection', 'Choose a seat');
  text('load-title', 'Choose your viewpoint'); text('load-detail', 'Verifying the seats saved in this browser…');
  text('access-link', 'Open tables'); $<HTMLAnchorElement>('access-link').href = '/';
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

scene.onLayout = (positions: Placement[]) => creatures.forEach((one, index) => {
  const node = nodes.get(one.id)!; const position = positions[index];
  node.style.left = `${position.x - position.hitWidth / 2}px`; node.style.top = `${Math.max(0, position.y - position.size)}px`;
  node.style.width = `${position.hitWidth}px`; node.style.height = `${Math.max(44, position.size + 42)}px`; node.style.zIndex = String(Math.round(position.y));
});
scene.onReady = () => { ready = true; $('arena').dataset.ready = 'true'; $('arena').dataset.assetFallback = String(scene.assetFailed); render(); };
function failRenderer() {
  rendererFailed = true; ready = false; connection.stop();
  render(); text('load-title', 'The renderer was interrupted'); text('load-detail', 'Reload to restore the scene, or continue playing at the table.');
  text('retry', 'Reload scene'); $('retry').hidden = false;
}
$('switch-seat').addEventListener('click', () => { void discover(); });
$('retry').addEventListener('click', () => { if (rendererFailed) location.reload(); else if (selected) void connection.refresh(); else void discover(); });
$('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
$('reduce-motion').addEventListener('change', render); motion.addEventListener('change', render);
function suspend() { discovering++; connection.stop(); view = null; catalogue = null; render(); text('connection', 'Paused'); }
document.addEventListener('visibilitychange', () => {
  if (document.hidden) suspend(); else if (selected) choose(selected); else void discover();
});
window.addEventListener('pagehide', suspend);
window.addEventListener('pageshow', event => { if (event.persisted) { if (selected) choose(selected); else void discover(); } });
try {
  const game = new Phaser.Game({ type: Phaser.AUTO, parent: 'phaser-mount', backgroundColor: '#17272b',
    scale: { mode: Phaser.Scale.RESIZE, width: '100%', height: '100%' }, audio: { noAudio: true }, scene, banner: false });
  const resize = new ResizeObserver(() => { const { width, height } = $('phaser-mount').getBoundingClientRect(); if (width && height) game.scale.resize(width, height); });
  resize.observe($('phaser-mount')); game.events.once(Phaser.Core.Events.DESTROY, () => resize.disconnect());
  game.events.once(Phaser.Core.Events.READY, () => game.canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); failRenderer(); }));
} catch { failRenderer(); }
setTimeout(() => { if (!ready && !rendererFailed) failRenderer(); }, 15000);
void discover();
