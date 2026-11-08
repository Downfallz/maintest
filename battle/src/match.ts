import { createBattleMatch } from '../../table/battle-match.js';
import { rematchPlan } from '../../table/outcome.js';
import { activateSeats } from '../../table/session.js';
import { presentationPath } from '../../table/battle-preferences.js';

type Controller = ReturnType<typeof createBattleMatch>;
type Rematch = ReturnType<typeof rematchPlan>;
interface Team { label: string; text: string; standing: number | null; total: number | null; hp: number | null; maxHp: number | null }
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
const node = (tag: string, className: string, value = '') => { const one = document.createElement(tag); one.className = className; one.textContent = value; return one; };

// One team's card: how many stand, big, and the health they kept as a bar. The whole line is its accessible name,
// so the drawn figures are not read twice.
function teamCard(team: Team, side: string) {
  const card = document.createElement('section'); card.className = 'match-team'; card.dataset.side = side;
  card.setAttribute('role', 'group'); card.setAttribute('aria-label', `${team.label}: ${team.text}`);
  const figures = node('div', 'match-team-figures'); figures.setAttribute('aria-hidden', 'true');
  const standing = node('p', 'match-team-standing');
  standing.append(node('strong', '', team.standing == null ? '–' : String(team.standing)), node('span', '', team.total == null ? 'standing' : `/${team.total} standing`));
  const bar = node('div', 'match-team-bar'), fill = node('span', '');
  fill.style.width = `${team.hp != null && team.maxHp ? Math.round(Math.min(1, team.hp / team.maxHp) * 100) : 0}%`; bar.append(fill);
  const hp = node('p', 'match-team-hp', team.hp == null ? 'HP unavailable' : team.maxHp ? `${team.hp} / ${team.maxHp} HP` : `${team.hp} HP`);
  figures.append(standing, bar, hp);
  card.append(node('h3', '', team.label), figures);
  return card;
}

// A rematch the host opens at once (a bot, a shared device, a friend kept a seat) is entered like the lobby enters
// a table: its seats saved, the battle opened on them. One the lobby must finish (a player who is not a friend) opens it.
async function playAgain(plan: NonNullable<Rematch>) {
  if (plan.link) { location.assign(plan.link); return; }
  const response = await fetch('/api/lobby/tables', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(plan.request), signal: AbortSignal.timeout(15000) });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.message || `The host answered ${response.status}. Try again.`);
  const seats = (body.seats ?? []) as { seat: string; token: string }[];
  try { activateSeats(seats, localStorage); } catch { /* The link below still carries the seats. */ }
  location.assign(`${presentationPath('battle')}?${new URLSearchParams(seats.map(one => [one.seat, one.token]))}`);
}

export function matchControls(controller: Controller, beforeOpen: () => void, rematch: () => Rematch = () => null) {
  const result = el<HTMLDialogElement>('match-dialog'), concede = el<HTMLDialogElement>('concede-dialog');
  let roundsKey = '', resultKey = '', starting = false, rematchError = '';
  el('match-rematch').addEventListener('click', async () => {
    const plan = rematch();
    if (!plan || starting) return;
    starting = true; rematchError = ''; render();
    try { await playAgain(plan); } catch (error) { rematchError = (error as Error).message || 'Could not start the rematch. Try again.'; }
    finally { starting = false; render(); }
  });
  el('match-open').addEventListener('click', () => { beforeOpen(); result.showModal(); });
  el('match-replay').addEventListener('click', async () => { beforeOpen(); await controller.replay(); if (controller.model().active) { result.close(); el('replay-next').focus(); } });
  el('replay-previous').addEventListener('click', () => controller.move(-1));
  el('replay-next').addEventListener('click', () => controller.move(1));
  el('replay-close').addEventListener('click', () => { controller.closeReplay(); result.showModal(); });
  el('replay-round').addEventListener('change', () => controller.jump(Number(el<HTMLSelectElement>('replay-round').value)));
  el('concede-open').addEventListener('click', () => { el<HTMLDialogElement>('settings-dialog').close(); beforeOpen(); concede.showModal(); });
  el('concede-confirm').addEventListener('click', () => { void controller.concede(); });
  concede.addEventListener('cancel', event => { if (controller.model().busy) event.preventDefault(); });
  function render() {
    const model = controller.model();
    el('match-open').hidden = !model.over || model.active;
    el('concede-open').hidden = !model.canConcede;
    el<HTMLButtonElement>('concede-confirm').disabled = model.busy || !model.canConcede || el('battle').dataset.connection !== 'live';
    el<HTMLButtonElement>('concede-cancel').disabled = model.busy;
    if (!model.canConcede) concede.close();
    el<HTMLButtonElement>('match-replay').disabled = model.busy;
    if (!model.over) result.close();
    text('match-title', model.result?.title ?? 'Match complete');
    text('match-eyebrow', Number.isInteger(model.result?.round) ? `MATCH COMPLETE · ROUND ${model.result.round}` : 'MATCH COMPLETE');
    result.dataset.tone = model.result?.tone ?? 'unknown';
    text('match-replay-error', rematchError || model.error);
    const plan = model.over ? rematch() : null, again = el<HTMLButtonElement>('match-rematch');
    again.hidden = !plan; again.disabled = starting || model.busy;
    text('match-rematch', starting ? 'Starting…' : plan?.label ?? 'Rematch');
    const resultData = JSON.stringify(model.result);
    if (resultData !== resultKey) {
      resultKey = resultData;
      el('match-stats').replaceChildren(...(model.result?.teams ?? []).map((team: Team, index: number) => teamCard(team, index === 0 ? 'ally' : 'enemy')));
    }
    text('match-outcome', model.outcome); text('concede-error', model.error); text('match-error', model.error);
    el('match-error').hidden = !model.error; el('replay-controls').hidden = !model.active;
    el('battle').dataset.replay = String(model.active);
    text('replay-text', model.summary); text('replay-note', model.note);
    text('replay-position', `Round ${model.round} · ${model.index + 1} of ${model.count}`);
    el<HTMLButtonElement>('replay-previous').disabled = !model.previous;
    el<HTMLButtonElement>('replay-next').disabled = !model.next;
    const rounds = JSON.stringify(model.rounds);
    if (rounds !== roundsKey) {
      roundsKey = rounds;
      el('replay-round').replaceChildren(...model.rounds.map((round: number) => { const option = document.createElement('option'); option.value = String(round); option.textContent = `Round ${round}`; return option; }));
    }
    el<HTMLSelectElement>('replay-round').value = String(model.round);
  }
  // The result, once: when the match ends and its last result has been read.
  function open() { if (controller.model().over && !result.open) result.showModal(); }
  return { render, open };
}
