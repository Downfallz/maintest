import { buildRecap, healthChangeText } from '../../table/battle-recap.js';

type Line = { unseen?: boolean };
// The shapes buildRecap returns: its arrays start empty, which leaves their items untyped to TypeScript.
type Side = { standing: number; total: number; health: number };
type Creature = { label: string; health: number; maxHealth: number; energy: number; defeated: boolean; fellWhileAway: boolean; change: number | null };
type Round = { round: number; state: string; folded: boolean; recorded: boolean; unseen: boolean;
  mutations: ({ creature: string; mutation: string } & Line)[];
  casts: ({ caster: string; spell: string; status: string; side: string; effects: { text: string }[] } & Line)[];
  skipped: ({ text: string } & Line)[]; defeated: ({ creature: string } & Line)[]; result: ({ text: string } & Line) | null };
type Recap = Omit<ReturnType<typeof buildRecap>, 'rounds' | 'teams' | 'chosen' | 'asked' | 'where'> & {
  where: Omit<ReturnType<typeof buildRecap>['where'], 'sides'> & { sides: { you: Side; opponent: Side } };
  teams: { you: Creature[]; opponent: Creature[] }; rounds: Round[];
  chosen: { tempos: { creature: string; tempo: string }[]; spells: { creature: string; spell: string }[];
    mutations: { round: number; picks: { creature: string; mutation: string }[] } | null };
  asked: { question: string | null; reactions: ({ glyph: string; label: string; ago: string } & Line)[] } };
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function node(tag: string, text = '', className = '') {
  const one = document.createElement(tag); if (text) one.textContent = text; if (className) one.className = className; return one;
}
// What the seat had not seen at its last visit carries the word New, read aloud as part of its line.
function line(tag: string, parts: (string | Node)[], item: Line, className = '') {
  const one = node(tag, '', className);
  if (item.unseen) { one.dataset.unseen = 'true'; one.append(node('span', 'New', 'recap-new')); }
  one.append(...parts.map(part => typeof part === 'string' ? document.createTextNode(part) : part));
  return one;
}
const STATUS: Record<string, string> = { Critical: 'critical', Fizzled: 'fizzled' };

/**
 * The recap's sheet, **Where you are** (docs/tabletop/async-recap-plan.md, R3): where the game stands, the two teams
 * now, what happened recently, what the seat had chosen and what it is asked, drawn from `buildRecap`. Two buttons
 * close it: **Go to my turn** settles the board on the present, **Replay step by step** hands what was not seen to
 * the playback, offered only when it holds something to play. Escape is Go to my turn, the sheet's default.
 */
export function recapControls({ onGo, onReplay, onClose }: { onGo: () => void; onReplay: () => void; onClose: () => void }) {
  const dialog = el<HTMLDialogElement>('recap-dialog');
  let chosen: 'go' | 'replay' | 'quiet' | null = null, opener: HTMLElement | null = null;
  el('recap-go').addEventListener('click', () => { chosen = 'go'; dialog.close(); });
  el('recap-replay').addEventListener('click', () => { chosen = 'replay'; dialog.close(); });
  dialog.addEventListener('close', () => {
    const choice = chosen; chosen = null;
    if (choice === 'quiet') return;
    if (choice === 'replay') onReplay(); else onGo();
    onClose();
    if (opener?.isConnected && !opener.hidden && opener.getClientRects().length) opener.focus({ preventScroll: true });
    opener = null;
  });

  function where(recap: Recap) {
    const { where } = recap;
    el('recap-context').textContent = [where.opponent ? `Against ${where.opponent}` : '', where.round ? `Round ${where.round}` : '', where.over ? '' : where.step ?? '']
      .filter(Boolean).join(' · ');
    el('recap-timing').textContent = [where.lastMove ? `Last move ${where.lastMove.ago}` : '', where.timeLeft ? `${where.timeLeft} left to answer` : ''].filter(Boolean).join(' · ');
    el('recap-timing').hidden = !el('recap-timing').textContent;
    el('recap-result').textContent = where.result ?? ''; el('recap-result').hidden = !where.result;
    const side = (who: string, one: Recap['where']['sides']['you'], key: string) => {
      const cell = node('div', '', 'recap-side'); cell.dataset.side = key;
      cell.append(node('strong', who), node('span', `${one.standing}/${one.total} standing`), node('span', `${one.health} HP`));
      return cell;
    };
    const them = where.opponent ?? 'Opponent';
    el('recap-sides').replaceChildren(side('You', where.sides.you, 'you'), side(them, where.sides.opponent, 'opponent'));
    el('recap-leader').textContent = where.leader === 'even' ? 'Even: as many creatures standing, as much health'
      : where.leader === 'you' ? 'You lead' : `${them} leads`;
    el('recap-leader').dataset.leader = where.leader;
  }

  function team(id: string, creatures: Recap['teams']['you']) {
    el(id).replaceChildren(...creatures.map(one => {
      const item = node('li', '', 'recap-creature');
      item.dataset.defeated = String(one.defeated);
      const change = healthChangeText(one.change);
      item.append(node('strong', one.label),
        node('span', one.defeated ? 'Defeated' : `${one.health}/${one.maxHealth} HP · ${one.energy} EN`, 'recap-stats'),
        ...(change ? [node('span', change, `recap-change ${(one.change ?? 0) < 0 ? 'loss' : 'gain'}`)] : []));
      if (one.fellWhileAway) item.prepend(node('span', 'New', 'recap-new'));
      if (one.fellWhileAway) item.dataset.unseen = 'true';
      return item;
    }));
  }

  function round(one: Round) {
    const card = document.createElement('details'); card.className = 'recap-round'; card.open = !one.folded;
    card.dataset.round = String(one.round); card.dataset.unseen = String(one.unseen);
    const summary = node('summary');
    summary.append(node('strong', `Round ${one.round}`), node('span', one.state === 'completed' ? 'completed' : 'under way', 'recap-round-state'),
      ...(one.unseen ? [node('span', 'New', 'recap-new')] : []));
    card.append(summary);
    const list = node('ul', '', 'recap-lines');
    if (!one.recorded) list.append(node('li', 'Nothing of this round is in the record this device loaded.', 'recap-quiet'));
    for (const pick of one.mutations) list.append(line('li', [node('span', pick.creature, 'recap-who'), ` mutated: ${pick.mutation || 'a mutation'}`], pick));
    for (const cast of one.casts) {
      const item = line('li', [node('span', cast.caster, 'recap-who'), ` cast ${cast.spell}`, ...(STATUS[cast.status] ? [node('span', `, ${STATUS[cast.status]}`, 'recap-status')] : [])], cast, 'recap-cast');
      item.dataset.side = cast.side;
      if (cast.effects.length) item.append(node('small', cast.effects.slice(0, 3).map(effect => effect.text).join(' · ')));
      list.append(item);
    }
    for (const skipped of one.skipped) list.append(line('li', [skipped.text], skipped, 'recap-quiet'));
    for (const down of one.defeated) list.append(line('li', [node('span', down.creature, 'recap-who'), ' defeated'], down, 'recap-defeated'));
    if (one.result) list.append(line('li', [one.result.text], one.result, 'recap-result-line'));
    if (one.recorded && !list.children.length) list.append(node('li', one.state === 'completed' ? 'No spells cast this round.' : 'No spells cast yet this round.', 'recap-quiet'));
    card.append(list);
    return card;
  }

  function chosenBy(recap: Recap) {
    const { chosen: mine } = recap;
    const items = [
      ...mine.tempos.map(one => node('li', `${one.creature}: ${one.tempo}`)),
      ...mine.spells.map(one => node('li', `${one.creature} declared ${one.spell}`)),
      ...(mine.mutations ? mine.mutations.picks.map(one => node('li', `${one.creature} took ${one.mutation} (round ${mine.mutations!.round})`)) : []),
    ];
    el('recap-chosen').replaceChildren(...(items.length ? items : [node('li', 'Nothing chosen yet this round.', 'recap-quiet')]));
  }

  function asked(recap: Recap, waitingOn: string) {
    const { asked: now } = recap;
    el('recap-question').textContent = recap.where.over ? 'The match is over.' : now.question ?? `Nothing for now: ${waitingOn}.`;
    el('recap-question').dataset.asked = String(Boolean(now.question));
    el('recap-reactions').replaceChildren(...now.reactions.map(one => {
      const glyph = node('span', one.glyph, 'reaction-glyph'); glyph.setAttribute('aria-hidden', 'true');
      return line('li', [glyph, ` ${one.label}`, ...(one.ago ? [node('small', ` ${one.ago}`)] : [])], one);
    }));
    el('recap-reactions').hidden = !now.reactions.length;
  }

  /** Draws the sheet; `replay` says whether the playback holds something to play step by step. */
  function render(built: ReturnType<typeof buildRecap>, { replay, waitingOn }: { replay: boolean; waitingOn: string }) {
    const recap = built as unknown as Recap;
    where(recap);
    team('recap-yours', recap.teams.you); team('recap-theirs', recap.teams.opponent);
    el('recap-theirs-title').textContent = recap.where.opponent ?? 'Opponent';
    el('recap-rounds').replaceChildren(...recap.rounds.map(round));
    el('recap-unseen').textContent = recap.unseen ? `${recap.unseen} new since your last visit` : 'Nothing new since your last visit';
    chosenBy(recap); asked(recap, waitingOn);
    el('recap-replay').hidden = !replay;
    // A match that ended while the seat was away has no turn to go to: the button leads to its result.
    el('recap-go').textContent = recap.where.over ? 'See the result' : 'Go to my turn';
    dialog.dataset.unseen = String(recap.unseen);
  }
  function open(source: HTMLElement | null) {
    if (dialog.open) return;
    opener = source; dialog.showModal();
    el('recap-body').scrollTop = 0;
    el<HTMLButtonElement>('recap-go').focus({ preventScroll: true });
  }
  return { render, open, isOpen: () => dialog.open, // A seat changed or the page paused: the sheet goes without settling anything.
    dismiss: () => { if (dialog.open) { chosen = 'quiet'; opener = null; dialog.close(); } } };
}
