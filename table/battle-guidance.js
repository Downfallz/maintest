import { phaseLabel, TEMPO_RULE } from './terms.js';
import { activeTurn } from './presentation.js';
import { creatureLabel } from './battle-live.js';

export const HELP_PREFERENCE_KEY = 'downfall.arena.help.v1';
const TOPICS = ['mutation', 'tempo', 'ties', 'action', 'intent', 'target'];
const topicOf = { Evolution: 'mutation', Speed: 'tempo', TieOrder: 'ties', Action: 'action', Intent: 'intent', Target: 'target' };
const count = (n, noun) => `${n} ${noun}${n === 1 ? '' : 's'}`;

// Only generic instructional preferences cross sessions. No match, seat, question, token or choice is saved.
export function helpPreferences(storage) {
  let muted = false, seen = new Set();
  try {
    const value = JSON.parse(storage?.getItem(HELP_PREFERENCE_KEY) ?? 'null');
    if (value?.version === 1) {
      muted = value.muted === true;
      seen = new Set(Array.isArray(value.seen) ? value.seen.filter(topic => TOPICS.includes(topic)) : []);
    }
  } catch { /* Help works in memory when storage is unavailable or malformed. */ }
  function save() {
    try { storage?.setItem(HELP_PREFERENCE_KEY, JSON.stringify({ version: 1, muted, seen: [...seen] })); } catch { /* Optional preference. */ }
  }
  return {
    muted: () => muted,
    shouldShow: topic => TOPICS.includes(topic) && !muted && !seen.has(topic),
    markSeen(topic) { if (TOPICS.includes(topic) && !seen.has(topic)) { seen.add(topic); save(); } },
    setMuted(value) { muted = value === true; save(); },
  };
}

// The rule-set stamp names its declaration only when it is not the hidden intent (ArtifactJson, ADR 0110), so a
// catalogue whose rules say nothing plays hidden intents, the way /play reads it. Without any rules at all, the
// current question can still explain itself.
function declaration(view, catalogue) {
  const mode = catalogue?.rules?.combatDeclaration;
  if (catalogue?.rules) return mode === 'AtActivation' ? 'AtActivation' : 'HiddenIntent';
  if (view.waitingFor === 'Action') return 'AtActivation';
  if (['Intent', 'Target'].includes(view.waitingFor)) return 'HiddenIntent';
  return null;
}
function roundFlow(mode) {
  const combat = mode === 'AtActivation'
    ? 'In turn order, choose a spell and its targets when your creature acts. Confirm to cast.'
    : mode === 'HiddenIntent'
      ? 'After turn order is set, declare hidden spells. When each spell is revealed, choose its targets and confirm.'
      : 'Follow the current spell or target question. This catalogue does not supply the declaration mode.';
  return [
    { phase: 'StartOfRound', title: phaseLabel('StartOfRound'), text: 'Energy and ongoing effects resolve automatically. History keeps the recorded events.' },
    { phase: 'Planning', title: phaseLabel('Planning'), text: `On mutation rounds, spend shared team picks. Then choose tempos and order your tied creatures. ${TEMPO_RULE}` },
    { phase: 'Combat', title: phaseLabel('Combat'), text: combat },
    { phase: 'EndOfRound', title: phaseLabel('EndOfRound'), text: 'Temporary conditions count down or expire according to their duration. The next round begins if the match continues.' },
  ];
}
const tips = {
  mutation: { title: 'What is a mutation?', lines: [
    'A mutation develops your creature: new spells or a passive, plus initiative. Its branch explains what the creature gains.',
    'Picks are shared across your team. Each creature can acquire at most one mutation this opportunity. Both teams choose privately, then reveal together.',
    'Choose an eligible creature on the battlefield or open Mutation tree. Preview a branch, then confirm the named mutation and recipient. Pass remaining picks gives up all your unused picks for this round.',
  ] },
  tempo: { title: 'Choose how your team acts', lines: [TEMPO_RULE,
    'Open Team tempos and draft a tempo in every creature tab. Return to any tab to edit, then Confirm tempos once. All tempos reveal together.',
    'Check the creature’s spells and energy when deciding. Choosing a tempo does not choose a spell.',
  ] },
  ties: { title: 'Order your tied creatures', lines: [
    'The dice settle places between teams. You decide the order within your own tied groups.',
    'Open Order ties, choose an order or keep the current order, then Confirm choice. Both teams’ orders reveal together.',
  ] },
  action: { title: 'Choose a spell on your turn', lines: [
    'Open Choose spell and select an offered spell. Its legal targets appear on the battlefield.',
    'Select targets, then Confirm cast. A spell without targets still needs confirmation. No hidden spell declaration is required in this mode.',
    'History explains the applied result after the action resolves; a spell preview is not a guaranteed outcome.',
  ] },
  intent: { title: 'Declare a hidden spell', lines: [
    'Open Choose spell, select a spell, then use Declare to confirm your intent. The opponent cannot see it before its reveal.',
    'You choose targets later, when this creature’s spell is revealed. A plan that cannot be paid for or resolved at that time may fizzle.',
  ] },
  target: { title: 'Choose targets for the revealed spell', lines: [
    'Your spell is already declared. Open Choose targets, select legal creatures on the field, then Confirm cast.',
    'Tap a selected creature again to deselect it. Follow the offered target count; a targetless spell still needs confirmation.',
  ] },
};
function question(view) {
  if (view.playedByBot || !Number.isInteger(view.waitingAsked)) return null;
  const topic = Object.hasOwn(topicOf, view.waitingFor) ? topicOf[view.waitingFor] : null;
  return topic ?? null;
}
function objective(view, topic) {
  const actor = creatureLabel(view.board, view.waitingCreature);
  switch (topic) {
    case 'mutation': {
      const offer = view.options?.evolution;
      if (offer?.remainingPicks === 0) return 'No team picks remain. Review your command.';
      if (offer?.creatures?.length && !offer.creatures.some(one => one.availableTiers?.length)) return 'No mutations offered. Open Mutation tree to pass.';
      const budget = Number.isInteger(offer?.remainingPicks) ? ` · ${count(offer.remainingPicks, 'team pick')} left` : '';
      return `Acquire a mutation${budget}.`;
    }
    case 'tempo': return 'Draft every eligible creature’s tempo, then confirm the team.';
    case 'ties': return 'Order your tied creatures, then confirm.';
    case 'action': return `${actor}: choose a spell, then targets. Confirm to cast.`;
    case 'intent': return `${actor}: declare a spell. Choose targets later.`;
    case 'target': return `${actor}: choose targets for the revealed spell.`;
    default: {
      if (view.playedByBot) return 'Your team is controlled by a bot. Follow its actions in History.';
      const turn = view.board.subPhase === 'Activation' ? activeTurn(view.board) : null;
      if (turn && turn.slot.owner !== view.board.slot) return `${creatureLabel(view.board, turn.slot.creature)} (opponent) is acting. Follow History.`;
      return 'Waiting for the next decision or the other team. History stays available.';
    }
  }
}

// Read only the selected seat's public context and current offer. No draft, hidden opponent plan or schedule arithmetic.
export function battleGuidance(view, catalogue, { accepted = false, targeting = false, historical = false, online = true } = {}) {
  if (!view?.board) return { available: false, topic: null, objective: 'Choose a viewpoint to play.', title: '', lines: [], round: '', context: '', next: '', phase: '', flow: [] };
  const board = view.board, waiting = Boolean(view.waiting && !view.over), mode = declaration(view, catalogue);
  const round = `Round ${board.roundNumber ?? '—'}${Number.isInteger(catalogue?.rules?.roundCap) ? ` of ${catalogue.rules.roundCap}` : ''}`;
  let topic = waiting || view.over || historical || accepted ? null : question(view);
  let current = objective(view, topic);
  if (topic === 'mutation' && view.options?.evolution?.remainingPicks === 0) topic = null;
  if (targeting) current = `${creatureLabel(board, view.waitingCreature)}: select targets, then Confirm cast.`;
  if (accepted) current = 'Choice sent. Waiting for the next decision or the other team.';
  if (!online) current = 'Connection interrupted. Reconnect before choosing.';
  if (view.over) current = 'Match complete. Open Match result or History.';
  if (historical) current = 'Reading recorded results. Return to the current board to choose.';
  if (waiting) current = 'Waiting for players. The match begins when everyone is seated.';
  const tip = topic ? tips[topic] : null;
  const actor = topic && topic !== 'mutation' && topic !== 'ties' ? creatureLabel(board, view.waitingCreature) : '';
  const next = !waiting && !view.over && !historical && Number.isInteger(board.nextEvolutionRound)
    ? board.nextEvolutionRound > board.roundNumber ? `Next mutation opportunity: round ${board.nextEvolutionRound}.`
      : board.nextEvolutionRound === board.roundNumber ? 'Mutation opportunity this round.' : '' : '';
  return { available: true, topic, objective: current, title: tip?.title ?? 'Round guide', lines: tip?.lines ?? [],
    round: waiting ? 'Match not started' : historical ? 'Recorded results' : round,
    context: waiting || historical ? '' : [phaseLabel(board.phase), actor].filter(Boolean).join(' · '),
    next, phase: waiting || historical || view.over ? '' : board.phase, flow: roundFlow(mode) };
}
