import { createBattleServices } from '../../table/battle-services.js';
import { serviceControls } from './services';
import { createBattleReactions } from '../../table/battle-reactions.js';
import { reactionControls } from './reactions';
import { creatureInspection, creatureCues, spokenCues } from '../../table/battle-inspection.js';
import { chipElement, conditionIcons, iconElement, type EffectChip } from './condition-icons';
import { inspectionControls } from './inspection';
import { battleTargetState, battleTargetTag } from '../../table/battle-targets.js';
import { battleOrder, createBattleOrderReveal, rollOffDice, tieMarks } from '../../table/battle-order.js';
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
import { resultCardElement, type ResultCard } from './result-card';
import { rematchPlan } from '../../table/outcome.js';
import { createBattlePlanning } from '../../table/battle-planning.js';
import { createBattleMatch } from '../../table/battle-match.js';
import { replayOf, operatorToken, replayMessage } from '../../table/session.js';
import { httpTransport, replayTransport } from '../../table/transport.js';
import { battleSeats, seatPath } from '../../table/battle-seats.js';
import { createLiveConnection } from '../../table/battle-live.js';
import { signInPath } from '../../table/account.js';
import { seatLabel } from '../../table/invitation.js';
import { phaseLabel, stepLabel, tempoLabel } from '../../table/terms.js';

interface Choice { seat: string; token: string; table: { id: string; format: string; round?: number; seats?: { seat: string; name: string; kind?: string; account?: string }[] } }
interface Creature { form: string; description: string; id: string; name: string; label: string; enemy: boolean; health: number; maxHealth: number; energy: number;
  totalDefense: number; currentInitiative: number; isAlive: boolean; isStunned: boolean; isStunImmune: boolean;
  knownSpells?: string[]; conditions: { effect: { kind?: string; $type?: string; amount?: number }; remainingRounds: number | null }[];
  evolution?: { artworkId: string | null; artworkFile: string | null; capstoneEffectIds: string[] } }
interface View { board: object; over: boolean; waiting: unknown; waitingFor: string | null;
  outcome?: { winner?: string; reason?: string }; answerBy?: string | null }
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
let frame: SceneFrame = { round: null, stage: 'decision', target: null, actor: null, targets: [], creatures: [] };
let discovering = 0;
let heldSeats: Choice[] = [];
let rendererFailed = false;
// The seat being shown, as this browser remembers what it saw of it, and the last mark kept for it.
let seenGame: string | null = null, seenKept: number | null = null;
const nodes = new Map<string, HTMLButtonElement>();
// What the scene drew for a creature whose button the page has not made yet.
const shownArt = new Map<string, string>();
const name = (id: unknown) => creatures.find(one => one.id === String(id))?.label ?? `Creature ${id}`;
const seatName = (seat: string): string => seatLabel(seat);
const connection = createLiveConnection({ onState: status, onView: (next: View | null, content: Catalogue | null) => {
  receiving = true;
  // The match begins once everyone is seated: an opponent who took an open seat is named from then on.
  if (view?.waiting && next && !next.waiting) void loadOpponent(true);
  try {
    view = next; catalogue = content ?? catalogue;
    if (next) services.receive(next);
    reactions.receive(next);
    history.receive(next, catalogue); upkeep.receive(next); orderReveal.receive(next, { since: seenGame ? seenSince(storage, seenGame) : null }); growth.receive(next, catalogue);
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
  !['inspect-dialog', 'settings-dialog', 'result-dialog', 'match-dialog', 'concede-dialog', 'history-dialog', 'help-dialog', 'upkeep-dialog', 'order-dialog', 'growth-dialog', 'atlas-dialog', 'opponent-dialog', 'reactions-dialog'].some(id => $<HTMLDialogElement>(id).open) &&
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
// Opening the team's tempos by hand reads the round's reveal, as Continue does: its receipt and crests leave the
// field to the tempos.
const beforePlanning = () => {
  passOver();
  const receipt = growth.receipt() as { sequence: number } | null;
  if (receipt && planning.model().latest?.waitingFor === 'Speed') { revealRead = receipt.sequence; growth.dismiss(); }
};
const planningUI = planningControls(planning, name, beforePlanning, inspectCreature, atlasUI);
// The round review is read-only: it covers a draft without dropping it, the way the inspection does.
// The end card plays the same game again (ADR 0153): against this table's opponent, with the seats this browser holds
// at it. A recording is nobody's game to play again.
const rematchOf = () => {
  if (!selected || (view as { replay?: unknown } | null)?.replay) return null;
  const held = heldSeats.filter(one => one.table.id === selected!.table.id).map(one => one.seat);
  return rematchPlan(selected.table, held.length ? held : [selected.seat], opponentCard(opponent.seat, opponent.ladder, opponent.friends).friend);
};
const matchUI = matchControls(match, () => { passOver(); combat.close(); planning.close(); }, rematchOf, () => catalogue?.effects ?? []);
const services = createBattleServices({ onChange: () => servicesUI.render() });
const servicesUI = serviceControls(services, storage);
// Reactions (ADR 0173): the set of short messages the two players send each other, and the bubbles that show them.
const reactions = createBattleReactions({ onChange: () => reactionsUI.render(), storage });
const reactionsUI = reactionControls(reactions, {
  opponentName: () => opponentCard(opponent.seat, opponent.ladder, opponent.friends).name || 'Opponent',
  onShown: shown => { keepForGame({ reactions: shown }); reactions.setShown(shown); },
});
const inspectionUI = inspectionControls();
const historyUI = historyControls(history, () => playback.skip(), () => render());
// The mutation reveal last read: opened with See and closed, or set aside with Continue.
let revealRead: number | null = null;
const revealUnread = () => { const receipt = growth.receipt() as { sequence: number } | null; return Boolean(receipt) && receipt!.sequence !== revealRead; };
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
  // A seat that can no longer play: signed out, no longer this page's, or a match voided by an update (ADR 0163).
  const closed = state === 'signin' || state === 'revoked' || state === 'ended';
  if (state === 'stale' || closed) audio.stop();
  if (closed || (state === 'stale' && !view)) $('loading').hidden = false;
  if (closed) services.reset();
  services.setOnline(state === 'live');
  if (state === 'stale') playback.skip();
  const messages: Record<string, string> = { live: 'LIVE', connecting: 'Connecting…',
    stale: 'Connection interrupted · retrying', signin: 'Sign in to continue', revoked: 'Seat access is no longer valid',
    ended: 'This match was ended' };
  text('connection', messages[state] ?? state);
  if (state === 'ended') {
    selected = null;
    text('perspective', 'YOUR TEAM');
    text('load-title', messages.ended);
    text('load-detail', 'The game was updated and this match could no longer be played on. It ended without a result and does not count.');
    $<HTMLAnchorElement>('access-link').href = '/';
    text('access-link', 'Lobby');
    $('retry').hidden = true;
  } else if ((state === 'signin' || state === 'revoked') && replayOf(location.search)) {
    // A recording is the operator's to read with the panel's token, or a player's own game read by their account
    // (ADR 0156): signed out is asked to sign in, and anybody else is told whose it is.
    const replay = replayMessage(state, Boolean(operatorToken(storage)));
    text('load-title', replay.title); text('load-detail', replay.detail);
    $<HTMLAnchorElement>('access-link').href = replay.link === 'signin' ? signInPath(location.pathname + location.search) : replay.link;
    text('access-link', replay.label);
    $('retry').hidden = false;
  } else if (state === 'signin' || state === 'revoked') {
    selected = null;
    text('perspective', 'YOUR TEAM');
    text('load-title', messages[state]);
    text('load-detail', state === 'signin' ? 'Sign in with the account that holds this seat.' : 'Return to the lobby to find a current seat.');
    $<HTMLAnchorElement>('access-link').href = state === 'signin' ? signInPath(location.pathname + location.search) : '/';
    text('access-link', state === 'signin' ? 'Sign in' : 'Lobby');
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
// What the field marks in blue and numbers, by creature id (the strip's lit creature, or the ties being ordered).
type FieldMark = { badge: string; state: string };
let fieldMarks = new Map<string, FieldMark>();

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
  resultCard(playbackState.result as ResultCard | null);
  text('playback-announcement', playbackState.stage === 'windup' ? '' : playbackState.summary);
  $('playback-result').setAttribute('aria-label', `Action result: ${playbackState.summary}`);
  text('result-label', playbackState.active ? 'Confirmed result' : 'Last result');
  if (!playbackState.summary) $<HTMLDialogElement>('result-dialog').close();
  if (targeting) frame = { ...frame, stage: 'targeting', actor: String(action.actor),
    targets: action.emptyCast ? [] : action.target.legalTargets.candidates.map(String), selected: action.picked.map(String) };
  // The docked spell and tempo choices leave the field in view, so a reveal stays on it while they are made; targets
  // are chosen on the field itself, and the mutation sheet lights its own crests there, which the reveal leaves to them.
  const sheet = planning.model();
  const showGrowth = visible() && !playbackState.active && !replay.active && !action.target && !(sheet.opened && sheet.latest?.waitingFor !== 'Speed');
  growthUI.render(view, catalogue, showGrowth);
  // The docked spell choice steps aside, its draft kept, for whatever the field has to show first: results being read,
  // a recorded replay, an upkeep receipt still to open, and a turn order being revealed.
  const spellsHeld = playbackState.active || replay.active || upkeep.model().pending || orderUI.isReading() || orderUI.isOpening();
  controls.render(action, catalogue, growth.highlights(action, visible() && !playbackState.active && !replay.active), spellsHeld);
  const shape = `${$<HTMLDialogElement>('combat-dialog').open}|${$<HTMLDialogElement>('planning-dialog').open && ['Evolution', 'Speed', 'TieOrder'].includes($('planning-dialog').dataset.kind ?? '')}`;
  if (shape !== dockShape) { dockShape = shape; dockMoved = playbackState.active; }
  else if (!playbackState.active) dockMoved = false; planningUI.render(planning.model(), catalogue); atlasUI.refresh(catalogue); matchUI.render(); servicesUI.render(); reactionsUI.render(); historyUI.render(history.model());
  // No command is offered while results are still to be read: the next choice appears once the player has
  // seen what led to it (ADR 0139). Skip all remains the deliberate way out, except past an opponent's action.
  if (playbackState.active) for (const id of ['combat-open', 'planning-open', 'match-open']) $(id).hidden = true;
  const plan = planning.model();
  const mutation = mutationScene(plan, catalogue, planningUI.recipient(), visible() && !replay.active && !playbackState.active && !action.opened, frame, playback.purchases());
  frame = mutation.frame;
  // The tempo sheet says on the field which creature it is about: the one in hand ringed in gold, the others dimmed,
  // and each creature still to draft tagged with the tempo drafted for it.
  const tempoSheet = plan.opened && plan.latest?.waitingFor === 'Speed' && plan.tempo ? plan.tempo : null;
  const tempoFocus = tempoSheet?.focus != null ? String(tempoSheet.focus) : null;
  if (tempoFocus) frame = { ...frame, mutationFocus: tempoFocus };
  const tempoDrafts = new Map<string, string>((tempoSheet?.drafts ?? []).map(([id, speed]: [number, string]) => [String(id), speed]));
  const tempoTag = (id: string) => {
    if (!tempoSheet?.creatures.map(String).includes(id)) return null;
    const drafted = tempoDrafts.get(id);
    if (drafted) return `${drafted === 'Quick' ? '⚡' : '✦'} ${tempoLabel(drafted).toUpperCase()}`;
    return id === tempoFocus ? 'CHOOSING TEMPO' : 'TEMPO ?';
  };
  // The field says in blue what the strip or the tie sheet is about: the creature the strip lit, or the seat's tied
  // creatures with the place each takes while it orders them, the opponent's places in those ties muted beside them.
  const tieSheet = plan.opened && plan.latest?.waitingFor === 'TieOrder' && !playbackState.active && !replay.active
    ? tieMarks(plan.latest, plan.tapped) as Map<string, FieldMark> : null;
  const stripFocus = orderUI.focused();
  fieldMarks = tieSheet ?? new Map(stripFocus ? [[stripFocus, { badge: '', state: 'focus' }]] : []);
  const focusIds = [...fieldMarks].filter(([, mark]) => mark.state !== 'opponent').map(([id]) => id);
  if (focusIds.length && !targeting) frame = { ...frame, focus: focusIds };
  const tieTag = (id: string) => {
    const mark = tieSheet?.get(id);
    if (!mark) return null;
    return mark.state === 'pending' ? 'TAP TO PLACE' : `PLACE ${mark.badge}`;
  };
  // A sheet docked under a phone's field shortens it: the captions go compact so the art stays in view.
  $('battle').dataset.docked = String($<HTMLDialogElement>('combat-dialog').open || ($<HTMLDialogElement>('planning-dialog').open && ['Evolution', 'Speed', 'TieOrder'].includes($('planning-dialog').dataset.kind ?? '')));
  $('battle').classList.toggle('mutation-entry', mutation.available && !plan.opened);
  $('actor-layer').setAttribute('aria-label', targeting ? 'Select targets' : mutation.available ? 'Choose a creature to mutate' : 'Inspect creatures');
  if ($('battle').dataset.connection === 'live') text('connection', playbackState.active ? 'READING RESULTS' : connectionText(replay.active, action.opened));
  const ids = new Set(creatures.map(one => one.id));
  for (const [id, node] of nodes) if (!ids.has(id)) { node.remove(); nodes.delete(id); }
  for (const one of creatures) {
    let node = nodes.get(one.id);
    if (!node) {
      node = creatureButton(one.id);
      if (shownArt.has(one.id)) { node.dataset.artworkShown = shownArt.get(one.id)!; shownArt.delete(one.id); }
      node.addEventListener('click', event => {
        if (controls.target(one.id)) return;
        // While the seat orders its ties, a tied creature of its own is placed by tapping it on the field too.
        if (['pending', 'placed'].includes(fieldMarks.get(one.id)?.state ?? '')) { planning.order(Number(one.id)); return; }
        if ((event.currentTarget as HTMLElement).dataset.mutationRecipient === 'true') planningUI.openCreature(Number(one.id));
        else inspectCreature(one.id);
      });
      $('actor-layer').append(node); nodes.set(one.id, node);
    }
    const offered = mutation.eligible.has(one.id);
    node.dataset.mutationRecipient = String(offered);
    node.dataset.mutationFocus = String(mutation.recipient === one.id);
    node.dataset.sheetFocus = String(tempoFocus === one.id);
    if (tempoDrafts.has(one.id)) node.dataset.tempo = tempoDrafts.get(one.id)!; else delete node.dataset.tempo;
    const mark = fieldMarks.get(one.id);
    if (mark) node.dataset.focus = mark.state; else delete node.dataset.focus;
    let badge = node.querySelector<HTMLElement>('.focus-badge');
    if (mark?.badge && !badge) { badge = document.createElement('span'); badge.className = 'focus-badge'; badge.setAttribute('aria-hidden', 'true'); node.append(badge); }
    if (badge) { badge.hidden = !mark?.badge; badge.textContent = mark?.badge ?? ''; }
    if (mutation.recipient === one.id && mutation.previewForm) node.dataset.previewForm = mutation.previewForm;
    else delete node.dataset.previewForm;
    node.dataset.form = one.form;
    // The illustration and capstone effects the creature's purchases resolve to (ADR 0151); the scene says which
    // image it actually draws in data-artwork-shown, the audited sprite until the illustration has loaded.
    node.dataset.artwork = one.evolution?.artworkId ?? '';
    node.dataset.capstones = (one.evolution?.capstoneEffectIds ?? []).join(' ');
    node.className = `creature ${one.enemy ? 'enemy' : 'ally'}${one.isAlive ? '' : ' is-defeated'}`;
    const targetState = battleTargetState(action, one.id);
    const targetable = Boolean(targetState?.selectable);
    if (targetState) node.dataset.targetState = targetState.state; else delete node.dataset.targetState;
    if (targetable) node.classList.add('is-targetable');
    node.disabled = !ready || (targeting && (!targetable || !action.canEdit));
    if (targeting) node.setAttribute('aria-pressed', String(frame.selected?.includes(one.id) === true));
    else node.removeAttribute('aria-pressed');
    const targetTag = battleTargetTag(targetState, one);
    const condition = mutation.tag(one.id) ?? tempoTag(one.id) ?? tieTag(one.id) ?? targetTag?.tag ?? tagOf(one);
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
  // Before both players are seated there is no phase yet: the header says the table is waiting.
  text('round-phase', view?.waiting && !view.over ? 'WAITING' : phaseLabel(board.phase).toUpperCase());
  text('ally-label', view ? 'YOUR TEAM' : ''); text('enemy-label', view ? opponentLabel() : '');
  const order = battleOrder(view?.waiting && !view.over ? null : board);
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, false, view?.waiting && !view.over ? null : board);
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
  // A turn order not yet read holds the mark below it, so a seat that leaves before Continue is shown it on return.
  const unreadOrder = orderReveal.model().unread;
  const watermark = playbackState.watermark === null ? null : Number.isInteger(unreadOrder) ? Math.min(playbackState.watermark, unreadOrder - 1) : playbackState.watermark;
  if (seenGame && view && !replay.active && watermark !== null && watermark !== seenKept) {
    seenKept = watermark; saveSeen(storage, seenGame, seenKept);
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
  // The d20s beside the creatures that rolled them (ADR 0154): each roll-off's last throw while the revealed order is
  // read, and a caster's critical roll as its action lands.
  type Die = NonNullable<SceneFrame['dice']>[number];
  const dice: Die[] = [];
  // A roll-off's dice say which tie they broke (its group) and where each creature finished in it.
  if (orderReading) for (const die of rollOffDice(view?.board)) {
    dice.push({ id: die.id, value: die.value, tone: 'roll', key: `order:${board.round}:${die.id}:${die.rolls.join('-')}`, group: die.group, place: die.place, rivals: die.rivals });
  }
  const critical = impact ? (playbackState.cue as { roll?: { value: number; critical: boolean; needed?: number | null } | null } | null)?.roll : null;
  if (critical && frame.actor) dice.push({ id: frame.actor, value: critical.value, tone: critical.critical ? 'crit' : 'miss', key: `crit:${playbackState.sequence}`, needed: critical.needed ?? null });
  frame = { ...frame, dice };
  scene.present(frame, reduced(), playbackState.rate, replay.active);
  const reveal = growth.receipt();
  if (!reveal) lastMutationReveal = null;
  else if (lastMutationReveal !== reveal.sequence && reveal.round === board.round && visible() && !replay.active && !playbackState.active && !plan.opened) {
    lastMutationReveal = reveal.sequence;
    if (!reduced()) scene.revealMutations(reveal.rows.map((row: {creature: number}) => String(row.creature)));
  }
  orderUI.render(order, Boolean(view) && !view?.waiting && !rendererFailed, canAuto() && !upkeep.model().pending, view?.waiting && !view.over ? null : board);
  helpUI.render(guide, canAuto() && !upkeep.model().pending && !orderReveal.model().pending);
  // The mutation phase opens its tree by itself once the board is unobstructed: after the upkeep receipt, the
  // turn order and a first-use tip, never in the waiting room or over another dialog or playback. Once per asking: a player who
  // closes it reopens it from the battlefield or the dock (ADR 0138).
  const asked = planning.model();
  if (asked.opened) mutationOpened = asked.question;
  // The team's tempos open the same way, docked as well (ADR 0150).
  // The tempos wait for a fresh mutation reveal to be read first (See, then closed, or Continue): the field shows who
  // mutated into what, which the docked sheet would crowd. The dock's Team tempos opens them at any time.
  else if (asked.available && ['Evolution', 'Speed'].includes(asked.latest?.waitingFor) && !asked.latest.waiting && mutationOpened !== asked.question
    && (asked.latest.waitingFor !== 'Speed' || !revealUnread()) && canAuto() && !upkeep.model().pending && !orderReveal.model().pending && !helpUI.pending()) { mutationOpened = asked.question; planning.open(); }
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
// The last result sheet draws the result as the journal draws a cast (result-card.ts), redrawn only when it changes.
function resultCard(card: ResultCard | null) {
  const holder = $('result-card'), key = JSON.stringify(card);
  if (holder.dataset.key === key) return;
  holder.dataset.key = key;
  holder.replaceChildren(...(card ? [resultCardElement(card, catalogue?.effects ?? [])] : []));
}

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
  // Above the creature's head, where the hit area begins: never over its body, which the caption already shares.
  let holder = node.querySelector<HTMLElement>('.creature-effect');
  if (!holder) { holder = document.createElement('span'); holder.className = 'creature-effect'; node.prepend(holder); }
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
  // A device holding both seats is two people side by side: they have no use for reactions.
  reactions.reset(transport, `${choice.table.id}:${choice.seat}`, choice.seat, { hotseat: heldSeats.filter(one => one.table.id === choice.table.id).length > 1 });
  reactions.setShown(effectiveSettings(settings, gameOverride(storage, choice.table.id)).reactions);
}

async function discover(autoResume = true) {
  audio.stop();
  if (rendererFailed) return;
  const ticket = ++discovering;
  $('loading').hidden = true;
  connection.stop(); services.reset(); reactions.reset(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset(); selected = null; seenGame = null; opponent = { seat: null, ladder: null, friends: null }; drawOpponent(); helpUI.setGame(null); view = null; catalogue = null; render();
  $('retry').hidden = true; $('seat-choices').replaceChildren();
  text('perspective', 'YOUR TEAM'); text('connection', 'Connecting…');
  text('load-title', 'Opening your battle'); text('load-detail', 'Verifying the seats saved in this browser…');
  text('access-link', 'Lobby'); $<HTMLAnchorElement>('access-link').href = '/';
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
    // The seat this link or browser held belongs to a match voided by an update (ADR 0163): say why it ended.
    if (!result.choices.length && !result.invitations.length && result.ended.length) { status('ended'); return; }
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

scene.onLayout = positions => { placeHitTargets(creatures.map(one => nodes.get(one.id)!), positions); orderUI.place(); };
scene.onDice = shown => { if (shown) $('battle').dataset.dice = shown; else delete $('battle').dataset.dice; };
scene.onHalo = id => { if (id) $('battle').dataset.halo = id; else delete $('battle').dataset.halo; };
scene.onFocus = ids => { if (ids) $('battle').dataset.focusLit = ids; else delete $('battle').dataset.focusLit; };
scene.onArtwork = (id, shown) => { const node = nodes.get(id); if (node) node.dataset.artworkShown = shown; else shownArt.set(id, shown); };
scene.onReady = () => { ready = true; $('battle').dataset.ready = 'true'; $('battle').dataset.assetFallback = String(scene.assetFailed); render(); };
function failRenderer() {
  audio.stop();
  discovering++; rendererFailed = true; ready = false; view = null; connection.stop(); services.reset(); upkeep.clear(); history.reset(); orderReveal.reset(); growth.reset(); playback.reset(); combat.reset(); planning.reset(); match.reset();
  render(); $('loading').hidden = false; text('load-title', 'The renderer was interrupted'); text('load-detail', 'Reload to restore the scene: your seat and the match wait for you.');
  // The classic table is archived (ADR 0157): reloading the scene is the way back, and the lobby lists the game.
  text('access-link', 'Lobby'); $<HTMLAnchorElement>('access-link').href = '/';
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
  reactions.setShown(chosen.reactions);
}
function keepForGame(change: object) { const game = gameOf(); if (game) saveGameOverride(storage, game, change); }
void loadSettings(storage).then(answer => { settings = answer.settings; settingsKnown = true; applySettings(); render(); });
// A time left to answer counts down on its own (ADR 0169): drawn again each minute while the game has one, with
// nothing read from the host.
globalThis.setInterval(() => { if (view?.answerBy && !view.over) render(); }, 60000);
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
// A mutation reveal is read once its sheet closes; Continue sets it aside, which reads it too.
$('growth-dialog').addEventListener('close', () => { revealRead = (growth.receipt() as { sequence: number } | null)?.sequence ?? revealRead; render(); });
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
// Back online, the stream waiting to open again opens now (ADR 0164); its `ready` reads the seat.
window.addEventListener('online', () => connection.reconnect());
// A notice the service worker received for this browser is a second word that something changed, beside the stream.
navigator.serviceWorker?.addEventListener?.('message', event => { if ((event as MessageEvent).data?.kind === 'notice' && selected && !document.hidden) void connection.refresh(); });
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
