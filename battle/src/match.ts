import { createBattleMatch } from '../../table/battle-match.js';
import { rematchPlan } from '../../table/outcome.js';
import { activateSeats } from '../../table/session.js';
import { presentationPath } from '../../table/battle-preferences.js';
import { resultCardElement, type ResultCard } from './result-card';

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

interface Fighter { id: number; name: string; side: string; alive: boolean; form: string; dealt: number; bleed: number; healed: number; regen: number; taken: number; blocked: number; kills: number; casts: number; crits: number }
interface Report { sides: { side: string; creatures: Fighter[]; totals: Record<string, number> }[]; mvp: number | null; top: Record<string, number> }
const portrait = (form: string) => {
  const image = document.createElement('img'); image.className = 'match-portrait'; image.alt = ''; image.decoding = 'async'; image.loading = 'lazy';
  // Beside the page, as the scene loads its figures: /battle-art under /battle, and under /table/ where the suite serves it.
  image.src = `battle-art/${form || 'base-creature'}.webp`; return image;
};
const figure = (value: number, small = '') => {
  const cell = node('td', 'match-figure', String(value));
  if (!value) cell.dataset.zero = 'true';
  if (small) cell.append(node('small', '', small));
  return cell;
};

// The creature of the match: who dealt the most, drawn large in its side's colour.
function mvpCard(report: Report) {
  const fighter = report.sides.flatMap(side => side.creatures).find(one => one.id === report.mvp);
  if (!fighter) return [];
  const card = node('div', 'match-mvp'); card.dataset.side = fighter.side;
  const words = node('div', 'match-mvp-words');
  words.append(node('p', 'match-mvp-eyebrow', 'Creature of the match'), node('strong', '', fighter.name),
    node('p', 'match-mvp-line', [`${fighter.dealt} damage`, fighter.kills ? `${fighter.kills} KO` : '', fighter.healed ? `${fighter.healed} healed` : ''].filter(Boolean).join(' · ')));
  card.setAttribute('aria-label', `Creature of the match: ${fighter.name}, ${words.lastChild?.textContent ?? ''}`);
  card.append(portrait(fighter.form), words);
  return [card];
}

// One side's table: each creature's damage (its bleeds among it), healing (its regenerations among it), what it took,
// what its defense blocked and its knock-outs; the damage drawn as a bar against the match's top damage.
function sideTable(side: Report['sides'][number], label: string, report: Report, index: number) {
  const section = node('section', 'match-report-side'); section.dataset.side = side.side;
  const heading = node('h4', 'match-report-heading', label); heading.id = `match-report-${index}`;
  const scroll = node('div', 'match-report-scroll');
  const table = document.createElement('table'); table.className = 'match-report-table'; table.dataset.side = side.side;
  table.setAttribute('aria-labelledby', heading.id);
  const head = document.createElement('thead'), titles = document.createElement('tr');
  for (const [title, short, hint] of [['Creature', 'Creature', ''], ['Damage', 'DMG', 'Damage dealt, bleeds included'], ['Healing', 'Heal', 'Healing given, regenerations included'],
    ['Taken', 'Hit', 'Damage taken'], ['Blocked', 'Blk', 'Damage its defense blocked'], ['KO', 'KO', 'Enemies knocked out']]) {
    const th = node('th', title === 'Creature' ? 'match-who' : 'match-figure'); th.setAttribute('scope', 'col');
    th.append(node('span', 'match-long', title), node('span', 'match-short', short));
    if (hint) th.title = hint;
    titles.append(th);
  }
  head.append(titles);
  const body = document.createElement('tbody');
  for (const fighter of side.creatures) {
    const row = document.createElement('tr'); row.dataset.reportCreature = String(fighter.id);
    if (!fighter.alive) row.dataset.fallen = 'true';
    if (fighter.id === report.mvp) row.dataset.mvp = 'true';
    const who = node('th', 'match-who'); who.setAttribute('scope', 'row');
    const name = node('span', 'match-name', fighter.name);
    if (!fighter.alive) name.append(node('small', 'match-fallen', 'fallen'));
    const inner = node('span', 'match-who-inner'); inner.append(portrait(fighter.form), name); who.append(inner);
    const dealt = figure(fighter.dealt, fighter.bleed ? `${fighter.bleed} bleed` : '');
    const bar = node('span', 'match-share'); bar.style.setProperty('--share', String(report.top.dealt ? fighter.dealt / report.top.dealt : 0)); dealt.prepend(bar);
    row.append(who, dealt, figure(fighter.healed, fighter.regen ? `${fighter.regen} regen` : ''), figure(fighter.taken), figure(fighter.blocked), figure(fighter.kills));
    body.append(row);
  }
  const foot = document.createElement('tfoot'), total = document.createElement('tr');
  const all = node('th', 'match-who', 'Team'); all.setAttribute('scope', 'row');
  total.append(all, ...['dealt', 'healed', 'taken', 'blocked', 'kills'].map(stat => figure(side.totals[stat] ?? 0)));
  foot.append(total);
  table.append(head, body, foot);
  scroll.append(table);
  section.append(heading, scroll);
  return section;
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

export function matchControls(controller: Controller, beforeOpen: () => void, rematch: () => Rematch = () => null,
  cues: () => Parameters<typeof resultCardElement>[1] = () => []) {
  const result = el<HTMLDialogElement>('match-dialog'), concede = el<HTMLDialogElement>('concede-dialog');
  let roundsKey = '', resultKey = '', reportKey = '', cardKey = '', starting = false, rematchError = '';
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
    const reportData = JSON.stringify([model.report, model.result?.teams?.map((team: Team) => team.label)]);
    if (reportData !== reportKey) {
      reportKey = reportData;
      const report = model.report as Report | null;
      el('match-report').hidden = !report?.mvp && !report?.sides.some(side => side.creatures.some(one => one.casts));
      el('match-mvp').replaceChildren(...(report ? mvpCard(report) : []));
      el('match-report-sides').replaceChildren(...(report?.sides ?? []).map((side, index) =>
        sideTable(side, model.result?.teams?.[index]?.label ?? (index === 0 ? 'Your team' : 'Opponent'), report as Report, index)));
    }
    text('match-outcome', model.outcome); text('concede-error', model.error); text('match-error', model.error);
    el('match-error').hidden = !model.error; el('replay-controls').hidden = !model.active;
    el('battle').dataset.replay = String(model.active);
    text('replay-text', model.summary); text('replay-note', model.note);
    text('replay-position', `Round ${model.round} · ${model.index + 1} of ${model.count}`);
    // Where the replay stands in the match, as a thin bar, and the step itself drawn as the last result is.
    el('replay-progress').style.width = model.count ? `${Math.round(((model.index + 1) / model.count) * 100)}%` : '0%';
    const card = (model.card ?? null) as ResultCard | null, nextCard = JSON.stringify(card);
    if (nextCard !== cardKey) { cardKey = nextCard; el('replay-card').replaceChildren(...(card ? [resultCardElement(card, cues())] : [])); }
    el('replay-controls').dataset.stage = model.stage ?? '';
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
