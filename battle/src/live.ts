import { createBattleServices } from '../../table/battle-services.js';
import { serviceControls } from './services';
import { creatureInspection, creatureCues, spokenCues } from '../../table/battle-inspection.js';
import { chipElement, conditionIcons, iconElement, type EffectChip } from './condition-icons';
import { inspectionControls } from './inspection';
import { battleTargetState, battleTargetTag } from '../../table/battle-targets.js';
import { battleOrder, createBattleOrderReveal } from '../../table/battle-order.js';
import { orderControls } from './order';
import { createBattleGrowth } from '../../table/battle-growth.js';
import { growthControls } from './growth';
import { atlasControls } from './mutation-atlas';
import { createBattleUpkeep } from '../../table/battle-upkeep.js';
import { upkeepControls } from './upkeep';
import { createBattlePlayback } from '../../table/battle-playback.js';
import { createBattleHistory } from '../../table/battle-history.js';
import { historyControls } from './history';
import { battleGuidance, waitingOn } from '../../table/battle-guidance.js';
import { saveSeen, seenSince } from '../../table/battle-seen.js';
import { friendAction, opponentCard, opponentOf } from '../../table/battle-opponent.js';
import { guidanceControls } from './guidance';
import { effectiveSettings, gameOverride, loadSettings, localSettings, saveGameOverride } from '../../table/game-preferences.js';
import { BattleAudio, soundControl } from './audio';
import { mutationScene } from './mutation-scene';
import { BattleScene } from './scene';
import type { SceneFrame } from './types';
import { creatureButton, placeHitTargets } from './targets';
import { bootScene } from './boot';
import { combatControls } from './combat';
import { createBattleCombat } from '../../table/battle-combat.js';
import { planningControls, type Catalogue } from './planning';
import { matchControls } from './match';
import { createBattlePlanning } from '../../table/battle-planning.js';
import { createBattleMatch } from '../../table/battle-match.js';
import { replayOf, operatorToken } from '../../table/session.js';
import { httpTransport, replayTransport } from '../../table/transport.js';
import { battleSeats, seatPath } from '../../table/battle-seats.js';
import { createLiveConnection } from '../../table/battle-live.js';
import { signInPath } from '../../table/account.js';
import { seatLabel } from '../../table/invitation.js';
import { phaseLabel, stepLabel } from '../../table/terms.js';

interface Choice { seat: string; token: string; table: { id: string; format: string; round?: number; seats?: { seat: string; name: string; kind?: string; account?: string }[] } }
interface Creature { form: string; description: string; id: string; name: string; label: string; enemy: boolean; health: number; maxHealth: number; energy: number;
  totalDefense: number; currentInitiative: number; isAlive: boolean; isStunned: boolean; isStunImmune: boolean;
  knownSpells?: string[]; conditions: { effect: { kind?: string; $type?: string; amount?: number }; remainingRounds: number | null }[] }
interface View { board: object; over: boolean; waiting: unknown; waitingFor: string | null;
  outcome?: { winner?: string; reason?: string } }
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if ($(id).textContent !== value) $(id).textContent = value; };
const audio = new BattleAudio();
const scene = new BattleScene(new URL('battle-art/', document.baseURI).href);
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
let heldSeats: Choice[] = [];
let rendererFailed = false;
// The seat being shown, as this browser remembers what it saw of it, and the last mark kept for it.
let seenGame: string | null = null, seenKept: number | null = null;
const nodes = new Map<string, HTMLButtonElement>();
const name = (id: unknown) => creatures.find(one => one.id === String(id))?.label ?? `Creature ${id}`;
const seatName = (seat: string): string => seatLabel(seat);
const connection = createLiveConnection({ onState: status, onView: (next: View | null, content: Catalogue | null) => {
  receiving = true;
  // The match begins once everyone is seated: an opponent who took an open seat is named from then on.
  if (view?.waiting && next && !next.waiting) void loadOpponent(true);
  try {
    view = next; catalogue = content ?? catalogue;
    if (next) services.receive(next);
    history.receive(next, catalogue); upkeep.receive(next); orderReveal.receive(next); growth.receive(next, catalogue);
    combat.receive(next); planning.receive(next); match.receive(next, catalogue);
    playback.receive(next, new Map(catalogue?.cards?.map(card => [card.id, card]) ?? []),
      { effects: catalogue?.effects ?? [], packages: new Map(catalogue?.packages?.map(pack => [pack.id, pack]) ?? []), animate: visible() && !combat.model().target && !planning.model().opened && !match.model().active,
        // A page opened again plays what happened while it was closed, from the last result this browser showed.
        since: seenGame ? seenSince(storage, seenGame) : null });
  } finally { receiving = false; }
  render();
} });
const visible = () => ready && !rendererFailed && !document.hidden &&
  !document.querySelector('.navigation-panels dialog[open]') &&
  !['inspect-dialog', 'settings-dialog', 'result-dialog', 'match-dialog', 'concede-dialog', 'history-dialog', 'help-dialog', 'upkeep-dialog', 'order-dialog', 'growth-dialog', 'atlas-dialog', 'opponent-dialog'].some(id => $<HTMLDialogElement>(id).open) &&
  // A revealed turn order holds the field until Continue: what arrives meanwhile waits for it.
  !orderUI.isReading();
let tabStorage: Storage | null = null;
try { tabStorage = sessionStorage; } catch { /* The receipt still works without storage. */ }
const upkeep = createBattleUpkeep(tabStorage);
const history = createBattleHistory({ load: () => connection.history(), onChange: () => render() });
const orderReveal = createBattleOrderReveal();
const growth = createBattleGrowth();
const combat = createBattleCombat({ connection, onChange: () => render(), visible });
const planning = createBattlePlanning({ connection, onChange: () => render(), visible });
const match = createBattleMatch({ connection, onChange: () => render() });
const playback = createBattlePlayback({ onChange: () => render(), onUpkeepHandled: round => upkeep.presented(round) });
// Opening a live choice or the result is the player's skip: what was still queued, upkeep included, is passed over.
const passOver = () => playback.skip({ dismissUpkeep: true });
const controls = combatControls(combat, name, passOver);
// The full mutation tree, one view for the chooser, the public reference and a revealed mutation's path.
const atlasUI = atlasControls(() => render());
const planningUI = planningControls(planning, name, passOver, inspectCreature, atlasUI);
// The round review is read-only: it covers a draft without dropping it, the way the inspection does.
const matchUI = matchControls(match, () => { passOver(); combat.close(); planning.close(); });
const services = createBattleServices({ onChange: () => servicesUI.render() });
const servicesUI = serviceControls(services, storage);
const inspectionUI = inspectionControls();
const historyUI = historyControls(history, () => playback.skip(), () => render());
const growthUI = growthControls(growth, () => playback.skip(), () => render(), atlasUI);
const helpUI = guidanceControls(storage, () => playback.skip(), () => render(), muted => keepForGame({ help: !muted }));
function inspectCreature(id: string) {
  inspected = id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal();
}
const upkeepUI = upkeepControls(upkeep, inspectCreature, () => render());
const orderUI = orderControls(orderReveal, () => playback.skip(), id => {
  inspected = id; renderInspection(); $<HTMLDialogElement>('inspect-dialog').showModal();
}, () => render());
let wasTargeting = false;

function status(state: string) {
  $('battle').dataset.connection = state;
  // Losing the connection or the seat fences a pending sound opt-in; an animation's own cancellation does not.
  if (state === 'stale' || state === 'signin' || state === 'revoked') audio.stop();
  if (state === 'signin' || state === 'revoked' || (state === 'stale' && !view)) $('loading').hidden = false;
  if (['signin', 'revoked'].includes(state)) services.reset();
  services.setOnline(state === 'live');
  if (state === 'stale') playback.skip();
  const messages: Record<string, string> = { live: 'LIVE', connecting: 'Connecting…',
    stale: 'Connection interrupted · retrying', signin: 'Sign in to continue', revoked: 'Seat access is no longer valid' };
  text('connection', messages[state] ?? state);
  if ((state === 'signin' || state === 'revoked') && replayOf(location.search)) {
    // A recording is read with the operator token the admin panel holds; no account sign-in grants it.
    text('load-title', 'This recording needs the operator');
    text('load-detail', 'Open it from the admin panel, which holds the operator token.');
    $<HTMLAnchorElement>('access-link').href = '/admin'; text('access-link', 'Open admin');
    $('retry').hidden = false;
  } else if (state === 'signin' || state === 'revoked') {
    selected = null;
    text('perspective', 'YOUR TEAM');
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

function inspectionContext() {
  const replay = match.model(), playing = playback.model();
  const historical = replay.active || playing.active;
  const sequence = replay.active ? replay.sequence : playing.sequence;
  const before = replay.active ? replay.stage === 'before' : playing.stage === 'windup';
  return { historical, through: historical ? (sequence ?? -1) - (before ? 1 : 0) : Infinity,
    label: replay.active ? `Round ${replay.round} · ${replay.note}` : playing.active
      ? `Round ${playing.board.round} · ${before ? 'Before' : 'After'} recorded action · event ${sequence}`
      : `Round ${playback.model().board.round ?? '—'} · Current state` };
}
let inspectionSnapshot: { model: ReturnType<typeof creatureInspection>; context: ReturnType<typeof inspectionContext>; action: ReturnType<typeof history.lastAction> } | null = null;
function renderInspection() {
  const one = creatures.find(one => one.id === inspected);
  // A receipt has no exact board. Keep an already open reference explicitly pinned to its last
  // recorded frame; never substitute future stats, or dismiss a reader because playback advanced.
  if (!one && view && playback.model().receipt && inspectionSnapshot && $<HTMLDialogElement>('inspect-dialog').open) {
    const held = inspectionSnapshot;
    inspectionUI.render(held.model, `Pinned reference · ${held.context.label}`, true,
      held.action ? `Round ${held.action.round} · ${held.action.summary}` : '', catalogue);
    return;
  }
  const context = inspectionContext();
  const action = one ? history.lastAction(one.id, context.through) : null;
  const model = creatureInspection(one, creatures, catalogue);
  inspectionSnapshot = model ? { model, context, action } : null;
  inspectionUI.render(model, context.label, context.historical,
    action ? `Round ${action.round} · ${action.summary}` : '', catalogue);
}
$('inspect-history').addEventListener('click', () => {
  const action = inspectionSnapshot?.action;
  if (action) historyUI.openEntry(action, $('inspect-history'));
});

// The one word over a figure: what stops it acting first, then whose slot it is.
function tagOf(one: Creature) {
  if (frame.selected?.includes(one.id)) return 'SELECTED';
  if (!one.isAlive) return 'DEFEATED';
  if (one.isStunned) return 'STUNNED';
  return frame.actor === one.id ? 'ACTING' : '';
}

// The dock's heading: the match's end, the creature whose slot it is, or where the round stands.
function headline(board: { turn: { slot: { creature: unknown } } | null; phase: string }) {
  if (!view) return 'Battle view';
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

type GrowthRow = { creature: number; mutation: string; stage: number | null; colour: string; spells: string[]; passives: string[]; initiative: number | null };
// A new round gets a short announcement at the top of the field when the shown board reaches it live; a page
// opening on a round, a replay or a seat change is a baseline, never a new round.
let announcedRound: number | null = null, roundToastTimer = 0;
function announceRound(round: number | null) {
  if (round == null) { announcedRound = null; return; }
  if (announcedRound !== null && round > announcedRound) {
    const toast = $('round-toast');
    text('round-toast-number', `Round ${round}`);
    toast.hidden = false; toast.classList.remove('showing'); void toast.offsetWidth; toast.classList.add('showing');
    window.clearTimeout(roundToastTimer);
    roundToastTimer = window.setTimeout(() => { toast.hidden = true; toast.classList.remove('showing'); }, 2600);
  }
  announcedRound = round;
}
let matchShown = '', lastMutationReveal: number | null = null, mutationOpened = '', spellOpened = '', orderShown = false;
let dockShape = '', dockMoved = false;

function render() {
  if (receiving) return;
  const playbackState = playback.model();
  const replay = match.model();
  const board = replay.active ? replay.board! : playbackState.board;
  creatures = board.creatures;
  const action = combat.model();
  const targeting = action.opened && Boolean(action.target);
  const guide = battleGuidance(rendererFailed ? null : view, catalogue, {
    accepted: action.accepted || planning.model().accepted, targeting,
    historical: playbackState.active || replay.active, online: $('battle').dataset.connection === 'live' });
  frame = (replay.active ? board.frame : playbackState.frame) as SceneFrame;
  $('battle').dataset.playback = playbackState.stage;
  $('battle').dataset.reading = String(playbackState.active);
  $('battle').dataset.sequence = String(playbackState.sequence ?? '');
  // An opponent's action is read, never skipped: it offers OK alone (ADR 0139).
  // One result left is read with its own OK, or plays out by itself: Skip all is for two or more.
  $('playback-skip').hidden = !playbackState.active || playbackState.incoming || playbackState.remaining < 2;
  text('playback-skip', `Skip all (${playbackState.remaining})`);
  // Choosing targets settles playback and the host waits for this seat, so the row has nothing to pace there;
  // its height belongs to the estimate and the fixed confirmation. Settings keeps the preference.
  $<HTMLSelectElement>('playback-preference').value = playbackState.mode;
  // Every action asks for its OK; Step asks it of the round receipts too (ADR 0139).
  const nextWasUsable = $('playback-next').getAttribute('aria-disabled') === 'false' && !$('playback-next').hidden;
  $('playback-next').hidden = !playbackState.asks;
  // Not disabled: a focused button that becomes disabled drops focus between two results.
  $('playback-next').setAttribute('aria-disabled', String(!playbackState.canNext));
  // When the OK becomes available and focus has nowhere to be (a command it sat on was just withdrawn), it lands
  // on the OK, so a keyboard reads the result and moves on from the same place.
  const focused = document.activeElement as HTMLElement | null;
  if (playbackState.asks && playbackState.canNext && !nextWasUsable && !document.querySelector('dialog:modal')
    && (!focused || focused === document.body || !focused.getClientRects().length)) $('playback-next').focus({ preventScroll: true });
  text('playback-next', playbackState.remaining > 1 ? 'Next' : 'OK');
  // While results are read the dock is one card: whose action, what it did, and the OK beside it.
  $('playback-pacing').hidden = !view || replay.active || targeting || !playbackState.active;
  const reading = playbackState.reading;
  $('playback-pacing').dataset.kind = reading?.kind ?? '';
  const more = playbackState.remaining > 1 ? ` · ${playbackState.remaining - 1} more` : '';
  text('reading-kind', reading ? `${({ incoming: "Opponent's action", own: 'Your cast', summary: 'Round summary' } as Record<string, string>)[reading.kind]}${more}` : '');
  text('reading-title', reading?.title ?? ''); readingDetail(reading);
  $('reading-detail').hidden = !reading?.detail;
  $('reading-open').setAttribute('aria-label', `Result details: ${playbackState.summary}`);
  $('playback-result').hidden = !playbackState.summary;
  text('result-text', playbackState.summary);
  text('playback-announcement', playbackState.stage === 'windup' ? '' : playbackState.summary);
  $('playback-result').setAttribute('aria-label', `Action result: ${playbackState.summary}`);
  text('result-label', playbackState.active ? 'Confirmed result' : 'Last result');
  if (!playbackState.summary) $<HTMLDialogElement>('result-dialog').close();
  if (targeting) frame = { ...frame, stage: 'targeting', actor: String(action.actor),
    targets: action.emptyCast ? [] : action.target.legalTargets.candidates.map(String), selected: action.picked.map(String) };
  // The docked spell choice leaves the field in view, so a reveal stays on it while a spell is chosen; targets are
  // chosen on the field itself, which the reveal leaves to them.
  const showGrowth = visible() && !playbackState.active && !replay.active && !action.target && !planning.model().opened;
  growthUI.render(view, catalogue, showGrowth);
  // The docked spell choice steps aside, its draft kept, for whatever the field has to show first: results being read,
  // a recorded replay, an upkeep receipt still to open, and a turn order being revealed.
  const spellsHeld = playbackState.active || replay.active || upkeep.model().pending || orderUI.isReading() || orderUI.isOpening();
  controls.render(action, catalogue, growth.highlights(action, visible() && !playbackState.active && !replay.active), spellsHeld);
  const shape = `${$<HTMLDialogElement>('combat-dialog').open}|${$<HTMLDialogElement>('planning-dialog').open && $('planning-dialog').dataset.kind === 'Evolution'}`;
  if (shape !== dockShape) { dockShape = shape; dockMoved = playbackState.active; }
  else if (!playbackState.active) dockMoved = false; planningUI.render(planning.model(), catalogue); atlasUI.refresh(catalogue); matchUI.render(); servicesUI.render(); historyUI.render(history.model());
  // No command is offered while results are still to be read: the next choice appears once the player has
  // seen what led to it (ADR 0139). Skip all remains the deliberate way out, except past an opponent's action.
  if (playbackState.active) for (const id of ['combat-open', 'planning-open', 'match-open']) $(id).hidden = true;
  const plan = planning.model();
  const mutation = mutationScene(plan, catalogue, planningUI.recipient(), visible() && !replay.active && !playbackState.active && !action.opened, frame);
  frame = mutation.frame;
  $('battle').classList.toggle('mutation-entry', mutation.available && !plan.opened);
  $('actor-layer').setAttribute('aria-label', targeting ? 'Select targets' : mutation.available ? 'Choose a creature to mutate' : 'Inspect creatures');
  if ($('battle').dataset.connection === 'live') text('connection', playbackState.active ? 'READING RESULTS' : connectionText(replay.active, action.opened));
  const ids = new Set(creatures.map(one => one.id));
  for (const [id, node] of nodes) if (!ids.has(id)) { node.remove(); nodes.delete(id); }
  for (const one of creatures) {
    let node = nodes.get(one.id);
    if (!node) {
      node = creatureButton(one.id);
      node.addEventListener('click', event => {
        if (controls.target(one.id)) return;
        if ((event.currentTarget as HTMLElement).dataset.mutationRecipient === 'true') planningUI.openCreature(Number(one.id));
        else inspectCreature(one.id);
      });
      $('actor-layer').append(node); nodes.set(one.id, node);
    }
    const offered = mutation.eligible.has(one.id);
    node.dataset.mutationRecipient = String(offered);
    node.dataset.mutationFocus = String(mutation.recipient === one.id);
    if (mutation.recipient === one.id && mutation.previewForm) node.dataset.previewForm = mutation.previewForm;
    else delete node.dataset.previewForm;
    node.dataset.form = one.form;
    node.className = `creature ${one.enemy ? 'enemy' : 'ally'}${one.isAlive ? '' : ' is-defeated'}`;
    const targetState = battleTargetState(action, one.id);
    const targetable = Boolean(targetState?.selectable);
    if (targetState) node.dataset.targetState = targetState.state; else delete node.dataset.targetState;
    if (targetable) node.classList.add('is-targetable');
    node.disabled = !ready || (targeting && (!targetable || !action.canEdit));
    if (targeting) node.setAttribute('aria-pressed', String(frame.selected?.includes(one.id) === true));
    else node.removeAttribute('aria-pressed');
    const targetTag = battleTargetTag(targetState, one);
    const condition = mutation.tag(one.id) ?? targetTag?.tag ?? tagOf(one);
    node.querySelector('.creature-name')!.textContent = one.label;
    node.querySelector('.creature-tag')!.textContent = condition;
    statChips(node.querySelector<HTMLElement>('.creature-stats')!, one.health, one.maxHealth, one.energy);
    // Initiative, defense when it is above zero, and one icon per kind of condition, on every screen: the numbers
    // and the engine's words stay in each badge's title, in the spoken label and in the inspection.
    node.querySelector('.creature-init')!.textContent = `${one.currentInitiative ?? '—'} INIT`;
    const defense = node.querySelector<HTMLElement>('.creature-defense')!;
    const defended = Number.isFinite(one.totalDefense) && one.totalDefense > 0;
    defense.hidden = !defended; defense.title = defended ? `${one.totalDefense} defense` : '';
    if (defended && defense.dataset.value !== String(one.totalDefense)) {
      defense.dataset.value = String(one.totalDefense); defense.replaceChildren(iconElement('defense-gain'), document.createTextNode(String(one.totalDefense)));
    }
    const cues = node.querySelector<HTMLElement>('.creature-conditions')!;
    const icons = conditionIcons(one, catalogue?.effects ?? []);
    const iconKey = JSON.stringify(icons);
    if (cues.dataset.icons !== iconKey) {
      cues.dataset.icons = iconKey;
      cues.replaceChildren(...icons.map(icon => { const cue = document.createElement('span'); cue.className = 'cue'; cue.title = icon.title; cue.dataset.cue = icon.key; cue.append(iconElement(icon.key)); return cue; }));
    }
    cues.hidden = icons.length === 0;
    // The status tag stands above the whole caption, however many badge rows a narrow figure wraps it into.
    node.style.setProperty('--caption-height', `${node.querySelector<HTMLElement>('.caption')!.offsetHeight}px`);
    (node.querySelector('.health-fill') as HTMLElement).style.width = `${one.maxHealth > 0 ? Math.max(0, Math.min(100, 100 * one.health / one.maxHealth)) : 0}%`;
    node.setAttribute('aria-label', `${one.enemy ? 'Opponent' : 'Your team'}: ${one.label}, ${one.health} of ${one.maxHealth} HP, ${one.energy} energy, ${one.totalDefense} defense, ${one.currentInitiative ?? 'unrecorded'} initiative. ${[spokenCues(creatureCues(one), targetTag?.spoken ?? condition), offered ? 'Choose mutation for this creature.' : targeting ? 'Target selection.' : 'Inspect creature.'].filter(Boolean).join('. ')}`);
  }
  text('round-number', board.round == null ? '—' : String(board.round).padStart(2, '0'));
  announceRound(view && !replay.active ? board.round : null);
  text('round-phase', phaseLabel(board.phase).toUpperCase());
  text('ally-label', view ? 'YOUR TEAM' : ''); text('enemy-label', view ? opponentLabel() : '');
  const order = battleOrder(view?.waiting && !view.over ? null : board);
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, false);
  const orderReading = orderUI.isReading() && !replay.active;
  $('battle').dataset.orderReading = String(orderReading);
  $('order-continue').hidden = !orderReading;
  if (orderReading) {
    $('playback-pacing').hidden = false; $('playback-pacing').dataset.kind = 'order';
    $('playback-next').hidden = true; $('playback-skip').hidden = true;
    text('reading-kind', 'Turn order'); text('reading-title', `Round ${order.round ?? '—'}`);
    readingDetail({ detail: [...order.summary, order.hasRolls ? 'Ties broken by d20' : ''].filter(Boolean).join(' · ') });
    $('reading-detail').hidden = false; $('reading-open').setAttribute('aria-label', 'Turn order details');
    if (!orderShown && !document.querySelector('dialog:modal')) $('order-continue').focus({ preventScroll: true });
  }
  orderShown = orderReading;
  // Nothing asked of this seat while the other side plays or chooses: the dock says so, whom for and on what,
  // in the reading card's place, rather than a line of status text.
  const other = selected?.table.seats?.find(one => one.seat !== selected?.seat);
  const awaiting = view && visible() && !playbackState.active && !orderReading && !replay.active && !targeting && !action.opened && !planning.model().opened && !rendererFailed
    ? waitingOn(view, { accepted: action.accepted || planning.model().accepted, opponent: other && other.kind !== 'open' && other.name !== 'Player' ? other.name : null, bot: other?.kind === 'bot' })
    : null;
  $('battle').dataset.awaiting = awaiting?.kind ?? '';
  $('reading-dots').hidden = !awaiting;
  $('waiting-last').hidden = !awaiting || !playbackState.summary;
  if (awaiting) {
    $('playback-pacing').hidden = false; $('playback-pacing').dataset.kind = 'waiting';
    $('playback-next').hidden = true; $('playback-skip').hidden = true;
    text('reading-kind', awaiting.eyebrow); text('reading-title', awaiting.title);
    readingDetail({ detail: awaiting.detail }); $('reading-detail').hidden = false;
    $('reading-open').setAttribute('aria-label', `${awaiting.title}. ${awaiting.detail}`);
  }
  text('actor-name', playbackState.receipt ? playbackState.receipt.category : playbackState.active ? name(frame.actor) : headline(board));
  const phase = view ? stepLabel(board.subPhase) || 'Waiting for players' : '';
  text('actor-meta', playbackState.active ? playbackState.label : phase);
  text('instruction', !view ? '' : playbackState.active ? playbackState.asks ? 'Read the result, then choose OK. Your next choice follows.' : 'Results are playing. Your next choice follows; Skip all shows the current board.' : mutation.prompt ?? guide.objective);
  $('instruction').hidden = Boolean(action.error || planning.model().error || replay.error);
  if (view && ready && !rendererFailed) $('loading').hidden = true;
  // What this seat has been shown so far, kept for the next time the page opens on it.
  if (seenGame && view && !replay.active && playbackState.watermark !== null && playbackState.watermark !== seenKept) {
    seenKept = playbackState.watermark; saveSeen(storage, seenGame, seenKept);
  }
  if (!view) inspected = null;
  renderInspection();
  $('battle').dataset.rate = String(playbackState.rate);

  if (targeting && !wasTargeting) (document.querySelector<HTMLButtonElement>('.is-targetable:not(:disabled)') ?? $<HTMLButtonElement>('combat-confirm')).focus({ preventScroll: true });
  wasTargeting = targeting;
  const canAuto = () => ready && !rendererFailed && !document.hidden && $('battle').dataset.connection === 'live' &&
    !playbackState.active && !replay.active && !action.target && !action.sending &&
    !planning.model().opened && !planning.model().sending && !document.querySelector('dialog[open]') && !orderUI.isOpening() && !orderUI.isReading();
  upkeepUI.render(canAuto());
  const liveMarkers = upkeepUI.markers(board.round);
  if (liveMarkers) text('round-phase', 'UPKEEP');
  const markers = replay.active ? undefined : playbackState.paced?.markers ?? liveMarkers;
  // Every effect on a creature is a chip above it: a spell's outcomes at impact, an upkeep tick, a condition
  // ending. Only a turnover with no recorded board to stand on keeps its plaques in the scene, by name.
  const impact = Boolean(playbackState.cue) && (playbackState.stage === 'impact' || playbackState.stage === 'resolved');
  type OrderRow = { id: string; position: number; tempo: string; speed: string; roll: string };
  const orderRows = new Map<string, OrderRow>(orderUI.isReading() && !replay.active ? (order.rows as OrderRow[]).map(row => [row.id, row]) : []);
  for (const one of creatures) {
    const node = nodes.get(one.id); if (!node) continue;
    const place = orderRows.get(one.id);
    const chips: EffectChip[] = place ? [{ kind: 'order', value: String(place.position), label: place.tempo, tempo: place.speed, text: `Turn ${place.position} · ${place.tempo}${place.roll ? ` · ${place.roll}` : ''}` }] : impact
      ? (playbackState.cue?.effects ?? []).filter((item: { creature: string }) => item.creature === one.id).map((item: { chip: EffectChip }) => item.chip)
      : (markers ?? []).filter((marker: { id: string }) => marker.id === one.id).flatMap((marker: { lines: { chip?: EffectChip; text: string }[] }) => marker.lines.map(line => line.chip ?? { kind: 'other', value: line.text, text: line.text }));
    effectChips(node, chips);
  }
  if (markers && board.round != null && !board.creatures.length) frame = { ...frame, upkeep: { markers, recorded: true, round: board.round } };
  // The reveal's crests stand on the battlefield while its receipt is up, on the live board only.
  const unlocks = showGrowth ? growth.receipt() : null;
  $('battle').dataset.reveal = unlocks ? unlocks.rows.map((row: GrowthRow) => row.creature).join(' ') : '';
  // The receipt floats over the top of the field: crests start below it rather than behind it.
  const receiptBox = $('growth-receipt').getBoundingClientRect(), stageBox = document.querySelector('#phaser-mount canvas')?.getBoundingClientRect();
  const inset = unlocks && stageBox && receiptBox.height ? Math.max(0, receiptBox.bottom - stageBox.top + 4) : 0;
  if (unlocks) frame = { ...frame, reveal: { round: unlocks.round, inset, markers: unlocks.rows.map((row: GrowthRow) => ({
    id: String(row.creature), mutation: row.mutation, stage: row.stage, colour: row.colour, gains: row.spells,
    fallback: row.passives.length ? 'New passive' : row.initiative ? `${row.initiative > 0 ? '+' : ''}${row.initiative} initiative` : 'No new spell' })) } };
  scene.present(frame, reduced(), playbackState.rate);
  const reveal = growth.receipt();
  if (!reveal) lastMutationReveal = null;
  else if (lastMutationReveal !== reveal.sequence && reveal.round === board.round && visible() && !replay.active && !playbackState.active && !plan.opened) {
    lastMutationReveal = reveal.sequence;
    if (!reduced()) scene.revealMutations(reveal.rows.map((row: {creature: number}) => String(row.creature)));
  }
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, canAuto() && !upkeep.model().pending);
  helpUI.render(guide, canAuto() && !upkeep.model().pending && !orderReveal.model().pending);
  // The mutation phase opens its tree by itself once the board is unobstructed: after the upkeep receipt, the
  // turn order and a first-use tip, never in the waiting room or over another dialog or playback. Once per asking: a player who
  // closes it reopens it from the battlefield or the dock (ADR 0138).
  const asked = planning.model();
  if (asked.opened) mutationOpened = asked.question;
  else if (asked.available && asked.latest?.waitingFor === 'Evolution' && !asked.latest.waiting && mutationOpened !== asked.question
    && canAuto() && !upkeep.model().pending && !orderReveal.model().pending && !helpUI.pending()) { mutationOpened = asked.question; planning.open(); }
  // The player's spell choice opens by itself in the same way, docked beside the field (ADR 0149): once per asking,
  // after whatever the board still had to show. Closing it leaves Choose spell in the dock.
  const fight = combat.model();
  if (fight.opened) spellOpened = fight.question;
  // A refusal the host gave is read first, in the dock: opening the choice would clear it.
  else if (fight.available && !fight.error && ['Action', 'Intent', 'Target'].includes(fight.waitingFor) && spellOpened !== fight.question
    && canAuto() && !upkeep.model().pending && !orderReveal.model().pending && !helpUI.pending()) { spellOpened = fight.question; combat.open(); }
  // The match's end is one moment: once its last result is read, the result opens by itself, once per seat.
  const seatKey = selected ? `${selected.table.id}:${selected.seat}` : '';
  if (view?.over && seatKey && matchShown !== seatKey && !replay.active && !match.model().active && canAuto()) { matchShown = seatKey; matchUI.open(); }
  // An opponent's action that arrived under a dialog waited, unplayed; once the field is in view it plays.
  if (playbackState.active && playbackState.sequence === null && visible() && !combat.model().target && !planning.model().opened && !match.model().active)
    queueMicrotask(() => playback.resume());
}

// Health and energy as chips beside the badges, a heart and a bolt before each number. The units and the
// separator stay in the text, unseen, so the caption still reads "20/20 HP · 5 EN" to a screen reader.
function statChips(stats: HTMLElement, health: number, maxHealth: number, energy: number) {
  const key = `${health}/${maxHealth}/${energy}`;
  if (stats.dataset.value === key) return;
  stats.dataset.value = key;
  const unseen = (value: string) => { const span = document.createElement('span'); span.className = 'unseen'; span.textContent = value; return span; };
  const chip = (kind: string, icon: string, value: string, unit: string) => {
    const span = document.createElement('span'); span.className = `stat stat-${kind}`;
    span.append(iconElement(icon), document.createTextNode(value), unseen(unit)); return span;
  };
  stats.replaceChildren(chip('health', 'health-gain', `${health}/${maxHealth}`, ' HP'), unseen(' · '), chip('energy', 'energy-gain', String(energy), ' EN'));
}

// An action's outcomes read as chips beside the creature each landed on; its words stay for screen readers.
function readingDetail(reading: { detail: string; groups?: { target: string; chips: EffectChip[] }[] } | null) {
  const detail = $('reading-detail'), key = JSON.stringify(reading ? [reading.detail, reading.groups] : null);
  if (detail.dataset.key === key) return;
  detail.dataset.key = key;
  if (!reading?.groups?.length) { detail.textContent = reading?.detail ?? ''; return; }
  const unseen = document.createElement('span'); unseen.className = 'unseen'; unseen.textContent = reading.detail;
  detail.replaceChildren(unseen, ...reading.groups.map(group => {
    const row = document.createElement('span'); row.className = 'reading-group'; row.setAttribute('aria-hidden', 'true');
    const target = document.createElement('span'); target.className = 'reading-target'; target.textContent = group.target;
    row.append(...group.chips.map(chip => chipElement(chip, catalogue?.effects ?? [])), target);
    return row;
  }));
}

function effectChips(node: HTMLElement, chips: EffectChip[]) {
  // Above the caption, which is centred under the figure: the hit area around it is taller than the creature.
  let holder = node.querySelector<HTMLElement>('.creature-effect');
  if (!holder) { holder = document.createElement('span'); holder.className = 'creature-effect'; node.querySelector('.caption')!.prepend(holder); }
  const key = JSON.stringify(chips);
  holder.hidden = chips.length === 0;
  if (holder.dataset.chips === key) return;
  holder.dataset.chips = key;
  holder.replaceChildren(...chips.map(chip => chipElement(chip, catalogue?.effects ?? [])));
}

// Who the player is up against: the other seat as the table names it, its place on the ladder and whether it is a
// friend. Read when a seat is chosen, again when the opponent takes an open seat, and whenever its card opens.
type Ladder = Parameters<typeof opponentCard>[1];
type FriendLists = Parameters<typeof opponentCard>[2];
let opponent: { seat: ReturnType<typeof opponentOf>; ladder: Ladder; friends: FriendLists } = { seat: null, ladder: null, friends: null };
async function readJson(path: string) {
  try { const response = await request(path); return response.ok ? await response.json() : null; } catch { return null; }
}
// Loads overlap (the card opening while the match begins), so the seats a refresh reads are kept on the choice as
// soon as they arrive, each load draws from the choice as it stands when it ends, and only the latest load draws.
let opponentLoad = 0;
async function loadOpponent(refreshSeats = false) {
  const choice = selected, load = ++opponentLoad;
  if (!choice) return;
  if (refreshSeats) {
    try {
      const response = await request('/api/lobby/resume', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ tokens: [choice.token] }) });
      const answer = response.ok ? await response.json() : null;
      const table = answer?.tables?.find((one: { table: Choice['table'] }) => one.table.id === choice.table.id)?.table;
      if (table) { choice.table = table; if (selected === choice) { opponent = { ...opponent, seat: opponentOf(table, choice.seat) }; drawOpponent(); } }
    } catch { /* The seats the page opened with still name the opponent. */ }
  }
  const [ladder, friends] = await Promise.all([readJson('/api/leaderboard'), readJson('/api/friends')]);
  if (selected !== choice || load !== opponentLoad) return;
  opponent = { seat: opponentOf(choice.table, choice.seat), ladder, friends };
  drawOpponent();
}
const avatarOf = (card: ReturnType<typeof opponentCard>) => card.kind === 'bot' ? '⚙' : card.kind === 'waiting' ? '…' : (card.name.trim()[0] ?? '?').toUpperCase();
function opponentLabel() {
  const card = opponentCard(opponent.seat, opponent.ladder, opponent.friends);
  return card.kind === 'person' || card.kind === 'bot' ? `OPPONENT · ${card.name.toUpperCase()}` : 'OPPONENT';
}
function drawOpponent() {
  const card = opponentCard(opponent.seat, opponent.ladder, opponent.friends);
  const chip = $<HTMLButtonElement>('opponent-open');
  chip.hidden = !selected || card.kind === 'unknown';
  chip.dataset.kind = card.kind;
  text('opponent-avatar', avatarOf(card)); text('opponent-name', card.name);
  $('opponent-badge').hidden = !card.badge; text('opponent-badge', card.badge);
  $('opponent-heart').hidden = card.friend !== 'friend';
  chip.setAttribute('aria-label', `Opponent: ${card.name}${card.badge ? `, ${card.badge === 'BOT' ? 'a bot' : `rank ${card.badge}`}` : ''}${card.friend === 'friend' ? ', your friend' : ''}. Open details.`);
  text('enemy-label', view ? opponentLabel() : '');
  // The card, open or not, says the same.
  $('opponent-dialog').dataset.kind = card.kind;
  text('opponent-dialog-avatar', avatarOf(card)); text('opponent-title', card.name);
  text('opponent-kind', card.kind === 'bot' ? 'OPPONENT · BOT' : card.kind === 'waiting' ? 'OPPONENT · NOT SEATED' : 'OPPONENT · PLAYER');
  text('opponent-rank', card.rankText); $('opponent-rank').hidden = !card.rankText;
  const action = friendAction(card.friend);
  $('opponent-note').hidden = !action?.note; text('opponent-note', action?.note ?? '');
  $('opponent-friend').hidden = !action?.label; text('opponent-friend', action?.label ?? '');
}
$('opponent-open').addEventListener('click', () => {
  $('opponent-problem').hidden = true;
  $<HTMLDialogElement>('opponent-dialog').showModal(); render(); void loadOpponent();
});
$('opponent-close').addEventListener('click', () => $<HTMLDialogElement>('opponent-dialog').close());
$('opponent-dialog').addEventListener('close', () => { render(); $('opponent-open').focus({ preventScroll: true }); });
// Asking, or accepting by asking back (ADR 0129): the same request, by the opponent's player name.
$('opponent-friend').addEventListener('click', async () => {
  const button = $<HTMLButtonElement>('opponent-friend'), name = opponent.seat?.name;
  if (!name) return;
  button.disabled = true; $('opponent-problem').hidden = true;
  try {
    const response = await request('/api/friends/requests', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username: name }) });
    if (!response.ok) throw new Error((await response.json().catch(() => null))?.message ?? 'The host did not answer. Try again.');
    await loadOpponent();
  } catch (error) {
    text('opponent-problem', (error as Error).message); $('opponent-problem').hidden = false;
  } finally { button.disabled = false; }
});

function choose(choice: Choice) {
  audio.stop();
  if (rendererFailed) return;
  discovering++;
  selected = choice;
  $('loading').hidden = true;
  $('seat-choices').replaceChildren(); $('retry').hidden = true;
  text('perspective', seatName(choice.seat).toUpperCase());
  seenGame = `${choice.table.id}:${choice.seat}`; seenKept = null;
  opponent = { seat: opponentOf(choice.table, choice.seat), ladder: null, friends: null }; drawOpponent(); void loadOpponent();
  upkeep.reset(`${choice.table.id}:${choice.seat}`); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(choice.seat); planning.reset(choice.seat); match.reset(choice.seat);
  helpUI.setGame(choice.table.id); applySettings();
  const transport = httpTransport(choice.seat, choice.token, request);
  connection.start(transport, catalogue);
  services.reset(transport, heldSeats.filter(one => one.table.id === choice.table.id).map(one => httpTransport(one.seat, one.token, request)), `${choice.table.id}:${choice.seat}`);
}

async function discover(autoResume = true) {
  audio.stop();
  if (rendererFailed) return;
  const ticket = ++discovering;
  $('loading').hidden = true;
  connection.stop(); services.reset(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset(); selected = null; seenGame = null; opponent = { seat: null, ladder: null, friends: null }; drawOpponent(); helpUI.setGame(null); view = null; catalogue = null; render();
  $('retry').hidden = true; $('seat-choices').replaceChildren();
  text('perspective', 'YOUR TEAM'); text('connection', 'Connecting…');
  text('load-title', 'Opening your battle'); text('load-detail', 'Verifying the seats saved in this browser…');
  text('access-link', 'Open tables'); $<HTMLAnchorElement>('access-link').href = '/';
  const recorded = replayOf(location.search);
  if (recorded) {
    text('perspective', `${seatName(recorded.seat).toUpperCase()} · RECORDED MATCH`);
    match.reset(recorded.seat);
    connection.start(replayTransport(recorded.id, recorded.seat, operatorToken(storage), request));
    return;
  }
  try {
    const result = await battleSeats(location.search, storage, request);
    if (ticket !== discovering) return;
    heldSeats = result.held;
    if (autoResume && result.choices.length === 1 && !result.invitations.length && result.held.filter((one: Choice) => one.table.id === result.choices[0].table.id).length === 1) { choose(result.choices[0]); return; }
    $('loading').hidden = false;
    text('load-title', result.choices.length ? 'Choose your seat' : 'No seat available');
    text('connection', result.choices.length ? 'Choose a seat' : 'No active seat');
    text('load-detail', result.choices.length ? 'Only the selected seat will be loaded. Choose who is holding this device.' : 'No accepted seat was found for this link or browser.');
    for (const choice of result.choices as Choice[]) {
      const button = document.createElement('button'); button.type = 'button';
      const players = choice.table.seats?.map(one => one.name).join(' vs ') ?? choice.table.format;
      button.textContent = `${seatName(choice.seat)} · ${players} · ${choice.table.id}`;
      button.addEventListener('click', () => choose(choice)); $('seat-choices').append(button);
    }
    if (result.invitations.length) {
      text('load-title', 'Review your invitation');
      text('load-detail', 'This link is an invitation. Accept it at the table before opening its live view.');
      text('access-link', 'Review invitation'); $<HTMLAnchorElement>('access-link').href = `/play${location.search}`;
    }
    $('retry').hidden = result.choices.length > 0;
  } catch (error) {
    if (ticket !== discovering) return;
    $('loading').hidden = false;
    if ((error as { status?: number }).status === 401) status('signin');
    else { text('load-title', 'Connection interrupted'); text('load-detail', 'Could not verify saved seats. Check the connection and try again.'); $('retry').hidden = false; }
  }
}

scene.onLayout = positions => placeHitTargets(creatures.map(one => nodes.get(one.id)!), positions);
scene.onReady = () => { ready = true; $('battle').dataset.ready = 'true'; $('battle').dataset.assetFallback = String(scene.assetFailed); render(); };
function failRenderer() {
  audio.stop();
  discovering++; rendererFailed = true; ready = false; view = null; connection.stop(); services.reset(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset();
  render(); $('loading').hidden = false; text('load-title', 'The renderer was interrupted'); text('load-detail', 'Reload to restore the scene, or continue playing at the table.');
  // The match goes on without the scene: the same seat continues at the table, not the lobby.
  if (selected) {
    text('access-link', 'Continue at table');
    $<HTMLAnchorElement>('access-link').href = seatPath('/play', selected, storage);
  }
  text('retry', 'Reload scene'); $('retry').hidden = false;
}
$('retry').addEventListener('click', () => { if (rendererFailed) location.reload(); else if (selected) void connection.refresh(); else void discover(); });
scene.onCancel = () => audio.silence();
scene.onImpact = () => {
  if (playback.model().active && !document.hidden && !match.model().active) audio.play(frame.cue?.visual);
};
soundControl(audio, $<HTMLInputElement>('sound'));
// The player's settings start every game (ADR 0140): the account's, or this browser's when signed out. The
// first time a game is opened they are copied into it, so changing them later starts new games differently
// and leaves this one as it was; what this page's settings change, they change for this game alone.
let settings = localSettings(storage);
let settingsKnown = false;
const gameOf = () => selected?.table.id ?? null;
function applySettings() {
  const game = gameOf(), kept = gameOverride(storage, game);
  const chosen = effectiveSettings(settings, kept);
  if (game && settingsKnown && Object.keys(kept).length < Object.keys(chosen).length) saveGameOverride(storage, game, chosen);
  $<HTMLSelectElement>('playback-preference').value = chosen.pace; playback.setMode(chosen.pace);
  $<HTMLSelectElement>('playback-speed').value = String(chosen.speed); playback.setRate(chosen.speed);
  $<HTMLInputElement>('reduce-motion').checked = chosen.reducedMotion; playback.setReduced(reduced()); $('battle').dataset.reducedMotion = String(reduced());
  helpUI.setHelp(chosen.help);
}
function keepForGame(change: object) { const game = gameOf(); if (game) saveGameOverride(storage, game, change); }
void loadSettings(storage).then(answer => { settings = answer.settings; settingsKnown = true; applySettings(); render(); });
$('playback-speed').addEventListener('change', () => {
  const speed = Number($<HTMLSelectElement>('playback-speed').value);
  playback.setRate(speed); keepForGame({ speed });
});
$('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
function motionChanged() { playback.setReduced(reduced()); $('battle').dataset.reducedMotion = String(reduced()); render(); }
$('reduce-motion').addEventListener('change', () => { keepForGame({ reducedMotion: $<HTMLInputElement>('reduce-motion').checked }); motionChanged(); });
motion.addEventListener('change', motionChanged);
applySettings();
$('playback-preference').addEventListener('change', () => {
  const pace = $<HTMLSelectElement>('playback-preference').value;
  playback.setMode(pace); keepForGame({ pace });
});
$('playback-next').addEventListener('click', () => {
  playback.advance();
  if (!playback.model().active && !document.querySelector('dialog[open]'))
    (['planning-open', 'combat-open', 'match-open'].map(id => $(id)).find(node => !node.hidden) ?? $('settings')).focus({ preventScroll: true });
});
$('playback-skip').addEventListener('click', () => playback.skip({ dismissUpkeep: true }));
$('playback-result').addEventListener('click', () => $<HTMLDialogElement>('result-dialog').showModal());
$('waiting-last').addEventListener('click', () => $<HTMLDialogElement>('result-dialog').showModal());
$('reading-open').addEventListener('click', () => {
  if (orderUI.isReading()) orderUI.details();
  else if (playback.model().summary) $<HTMLDialogElement>('result-dialog').showModal();
});
$('order-continue').addEventListener('click', () => orderUI.finish());
// A docked choice opening or closing beside the field resizes it; that re-lays out the cast being shown rather than
// passing over it, which a rotation or a resized window still does.
scene.onResizeDuringPlayback = () => { if (dockMoved) { dockMoved = false; return; } playback.skip(); };
// A held Step or a text receipt has no running Phaser tween, but rotation still cancels it. A resize that
// keeps the orientation (a browser bar, a dragged window) re-lays out the held frame and keeps it.
matchMedia('(orientation: portrait)').addEventListener('change', () => { if (playback.model().active) playback.skip(); });
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

for (const id of ['inspect-dialog', 'settings-dialog', 'result-dialog', 'match-dialog', 'concede-dialog']) {
  $(id).addEventListener('close', () => render());
}
// A receipt that became current under the inspection removed the creature it opened from: focus goes to the
// receipt being read, or to that creature again once it is back, never to the page itself. A tap focuses no
// opener, so focus can still sit inside the closed dialog.
$('inspect-dialog').addEventListener('close', () => {
  const focused = document.activeElement;
  if (focused && focused !== document.body && !$('inspect-dialog').contains(focused)) return;
  const node = inspected === null ? undefined : nodes.get(inspected);
  (node ?? (!$('playback-pacing').hidden ? $('reading-open') : undefined))?.focus({ preventScroll: true });
});
