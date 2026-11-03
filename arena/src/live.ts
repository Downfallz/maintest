import { ArenaScene } from './scene';
import type { SceneFrame } from './types';
import { creatureButton, placeHitTargets } from './targets';
import { bootScene } from './boot';
import { combatControls, type SpellCatalogue } from './combat';
import { createArenaCombat } from '../../table/arena-combat.js';
import { httpTransport } from '../../table/transport.js';
import { arenaSeats, seatPath } from '../../table/arena-seats.js';
import { createLiveConnection, livePresentation } from '../../table/arena-live.js';
import { chipText, chipSource, laneOf } from '../../table/board.js';
import { signInPath } from '../../table/account.js';
import { seatLabel } from '../../table/invitation.js';
import { phaseLabel, tempoLabel, decisionLabel, stepLabel } from '../../table/terms.js';

interface Choice { seat: string; token: string; table: { id: string; format: string; round?: number; seats?: { seat: string; name: string }[] } }
interface Creature { id: string; name: string; label: string; enemy: boolean; health: number; maxHealth: number; energy: number;
  totalDefense: number; currentInitiative: number; isAlive: boolean; isStunned: boolean; isStunImmune: boolean;
  knownSpells?: string[]; conditions: { effect: { kind?: string; $type?: string; amount?: number }; remainingRounds: number | null }[] }
interface View { board: object; over: boolean; waiting: unknown; waitingFor: string | null;
  outcome?: { winner?: string; reason?: string } }
type Catalogue = SpellCatalogue;
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
const seatName = (seat: string): string => seatLabel(seat);
const connection = createLiveConnection({ onState: status, onView: (next: View | null, content: Catalogue | null) => {
  view = next; catalogue = content ?? catalogue; combat.receive(next);
} });
const combat = createArenaCombat({ connection, onChange: () => render(),
  visible: () => ready && !rendererFailed && !document.hidden && !$<HTMLDialogElement>('inspect-dialog').open && !$<HTMLDialogElement>('settings-dialog').open });
const controls = combatControls(combat, name);
let wasTargeting = false;

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
  combat.setOnline(state === 'live');
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

// Combat is available here; other questions still belong to the table until A7.
function instruction() {
  if (!view) return 'Inspect the real board here. Make decisions at the table.';
  if (view.over) return view.outcome?.winner ? `${view.outcome.winner} wins. Review the match at the table.` : 'The match has ended. Review it at the table.';
  if (combat.model().target) return 'Select figures, then confirm. Tap again to deselect.';
  if (combat.model().available) return 'Choose a spell or target to answer this combat question here.';
  if (view.waitingFor) return `${decisionLabel(view.waitingFor)} is waiting at the table. Inspect creatures here; return there to decide.`;
  return 'Inspect the real board here. Make decisions at the table.';
}

function render() {
  const board = livePresentation(view);
  creatures = board.creatures;
  const action = combat.model();
  const targeting = action.opened && Boolean(action.target);
  frame = board.frame as SceneFrame;
  if (targeting) frame = { ...frame, stage: 'targeting', actor: String(action.actor),
    targets: action.emptyCast ? [] : action.target.legalTargets.candidates.map(String), selected: action.picked.map(String) };
  controls.render(action, catalogue);
  $('actor-layer').setAttribute('aria-label', targeting ? 'Select targets' : 'Inspect creatures');
  if ($('arena').dataset.connection === 'live') text('connection', action.opened ? 'LIVE · COMBAT' : 'LIVE · READ ONLY');
  const ids = new Set(creatures.map(one => one.id));
  for (const [id, node] of nodes) if (!ids.has(id)) { node.remove(); nodes.delete(id); }
  for (const one of creatures) {
    let node = nodes.get(one.id);
    if (!node) {
      node = creatureButton(one.id);
      node.addEventListener('click', () => { if (controls.target(one.id)) return; inspected = one.id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal(); });
      $('actor-layer').append(node); nodes.set(one.id, node);
    }
    node.className = `creature ${one.enemy ? 'enemy' : 'ally'}${one.isAlive ? '' : ' is-defeated'}`;
    const targetable = targeting && frame.targets.includes(one.id);
    if (targetable) node.classList.add('is-targetable');
    node.disabled = !ready || (targeting && (!targetable || !action.canEdit));
    if (targeting) node.setAttribute('aria-pressed', String(frame.selected?.includes(one.id) === true));
    else node.removeAttribute('aria-pressed');
    const condition = tagOf(one);
    node.querySelector('.creature-name')!.textContent = one.label;
    node.querySelector('.creature-tag')!.textContent = condition;
    node.querySelector('.creature-stats')!.textContent = `${one.health}/${one.maxHealth} HP · ${one.energy} EN`;
    (node.querySelector('.health-fill') as HTMLElement).style.width = `${one.maxHealth > 0 ? Math.max(0, Math.min(100, 100 * one.health / one.maxHealth)) : 0}%`;
    node.setAttribute('aria-label', `${one.enemy ? 'Opponent' : 'Your team'}: ${one.label}, ${one.health} of ${one.maxHealth} HP, ${one.energy} energy, ${one.totalDefense} defense. ${condition}. ${targeting ? 'Select target.' : 'Inspect creature.'}`);
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
  text('actor-name', headline(board));
  text('actor-meta', view ? stepLabel(board.subPhase) || 'Waiting for players' : 'Choose a seat to enter');
  text('instruction', instruction());
  $('instruction').hidden = Boolean(action.error);
  $('loading').hidden = Boolean(view && ready && !rendererFailed);
  if (!view) {
    inspected = null; renderInspection();
    for (const id of ['inspect-name', 'inspect-side', 'inspect-stats', 'inspect-effects', 'inspect-spells']) $(id).replaceChildren();
    text('updated', 'No board loaded');
  } else renderInspection();
  scene.present(frame, reduced(), 1);
  if (targeting && !wasTargeting) (document.querySelector<HTMLButtonElement>('.is-targetable:not(:disabled)') ?? $<HTMLButtonElement>('combat-confirm')).focus({ preventScroll: true });
  wasTargeting = targeting;
}

function choose(choice: Choice) {
  if (rendererFailed) return;
  discovering++;
  selected = choice;
  $('seat-choices').replaceChildren(); $('retry').hidden = true;
  text('perspective', `${seatName(choice.seat).toUpperCase()} · YOUR VIEWPOINT`);
  $<HTMLAnchorElement>('play-link').href = seatPath('/play', choice, storage); $('play-link').hidden = false;
  combat.reset(choice.seat);
  connection.start(httpTransport(choice.seat, choice.token, request), catalogue);
}

async function discover() {
  if (rendererFailed) return;
  const ticket = ++discovering;
  connection.stop(); combat.reset(); selected = null; view = null; catalogue = null; render();
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

scene.onLayout = positions => placeHitTargets(creatures.map(one => nodes.get(one.id)!), positions);
scene.onReady = () => { ready = true; $('arena').dataset.ready = 'true'; $('arena').dataset.assetFallback = String(scene.assetFailed); render(); };
function failRenderer() {
  rendererFailed = true; ready = false; connection.stop(); combat.reset();
  render(); text('load-title', 'The renderer was interrupted'); text('load-detail', 'Reload to restore the scene, or continue playing at the table.');
  text('retry', 'Reload scene'); $('retry').hidden = false;
}
$('switch-seat').addEventListener('click', () => { void discover(); });
$('retry').addEventListener('click', () => { if (rendererFailed) location.reload(); else if (selected) void connection.refresh(); else void discover(); });
$('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
$('reduce-motion').addEventListener('change', render); motion.addEventListener('change', render);
// The board goes with the page's eyes; the catalogue stays, it cannot change while the host runs.
function suspend() { discovering++; connection.stop(); combat.reset(); view = null; render(); text('connection', 'Paused'); }
document.addEventListener('visibilitychange', () => {
  if (document.hidden) suspend(); else if (selected) choose(selected); else void discover();
});
window.addEventListener('pagehide', suspend);
window.addEventListener('pageshow', event => { if (event.persisted) { if (selected) choose(selected); else void discover(); } });
try {
  bootScene(scene, $('phaser-mount'), { isReady: () => ready || rendererFailed, onContextLost: failRenderer, onNeverReady: failRenderer });
} catch { failRenderer(); }
void discover();
