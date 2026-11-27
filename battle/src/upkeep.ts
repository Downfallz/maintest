import { createBattleUpkeep, upkeepDisplay } from '../../table/battle-upkeep.js';
import { roundStartLine, mutationSchedule } from '../../table/battle-growth.js';
import { ROUND_START, inRound } from '../../table/terms.js';
import { receiptDetails } from './receipt-details';
import { onNextFrame } from './next-frame';

type Controller = ReturnType<typeof createBattleUpkeep>;
type Schedule = ReturnType<typeof mutationSchedule>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const node = (tag: string, text: string, className = '') => {
  const result = document.createElement(tag); result.textContent = text; result.className = className; return result;
};

export function upkeepControls(controller: Controller, inspect: (id: string) => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('upkeep-dialog');
  let key = '', renderedRound = 0, opening = false, opener: HTMLElement | null = null;
  const details = el<HTMLButtonElement>('upkeep-details');
  const body = el('upkeep-body');
  // Reserve only the compact dock. Opening Details may cover more of the scene on request.
  const header = dialog.querySelector<HTMLElement>('.upkeep-header')!;
  const tools = dialog.querySelector<HTMLElement>('.upkeep-tools')!;
  const reserve = new ResizeObserver(onNextFrame(() => {
    if (dialog.open) el('battle').style.setProperty('--upkeep-dock-height', `${header.offsetHeight + tools.offsetHeight + 12}px`);
  }));
  reserve.observe(header); reserve.observe(tools);
  let display: ReturnType<typeof upkeepDisplay> | null = null;
  const { expand } = receiptDetails(details, body);
  el('upkeep-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => {
    // Restore focus before rendering: the next phase tip may open and own focus in that render.
    if (opener?.isConnected && opener.getClientRects().length && !opener.matches(':disabled')) opener.focus({ preventScroll: true });
    else if (controller.model().available) el('help-open').focus({ preventScroll: true });
    opener = null; onVisibility();
  });
  // A mutation round, or the round before one, said under what the start gave (F3): only on the receipt of the
  // round the board is at, since the schedule is the live board's and says nothing of a round already past.
  function mutationLine(round: number, schedule: Schedule | null) {
    const line = schedule && schedule.round === round ? roundStartLine(schedule) : '';
    const node = el('upkeep-mutation');
    if (node.textContent !== line) node.textContent = line;
    node.hidden = !line;
  }
  function render(canAuto: boolean, schedule: Schedule | null = null) {
    const model = controller.model();
    if (!model.available || !model.receipt) {
      opener = null;
      if (dialog.open) dialog.close();
      display = null; expand(false); el('upkeep-intro').textContent = ''; mutationLine(0, null); body.replaceChildren(); el('upkeep-title').textContent = ''; key = ''; renderedRound = 0; return;
    }
    const receipt = model.receipt;
    // Do not replace the round being read under History/inspection. A newer pending receipt follows on close.
    if (dialog.open && renderedRound !== receipt.round) return;
    mutationLine(receipt.round, schedule);
    const nextKey = JSON.stringify(receipt);
    if (nextKey !== key) {
      display = upkeepDisplay(receipt.energy, receipt.ongoing, receipt.energyNote);
      el('upkeep-intro').textContent = display.summary;
      el('upkeep-title').textContent = inRound(receipt.round, ROUND_START);
      const sections = [
        { title: 'Round energy', rows: receipt.energy, empty: receipt.energyNote },
        { title: 'Ongoing effects', rows: receipt.ongoing, empty: receipt.ongoingNote },
      ].map(group => {
        const section = document.createElement('section'); section.append(node('h3', group.title));
        const list = document.createElement('ul'); list.className = 'upkeep-rows';
        for (const row of group.rows) {
          const item = document.createElement('li'); item.dataset.tone = row.tone;
          const button = document.createElement('button'); button.type = 'button'; button.textContent = row.label;
          button.disabled = !row.inspectable; button.setAttribute('aria-label', `Inspect ${row.label} · ${row.side}`);
          button.addEventListener('click', () => inspect(String(row.creature)));
          item.append(node('small', row.side), button, node('p', row.text)); list.append(item);
        }
        section.append(group.rows.length ? list : node('p', group.empty, 'upkeep-empty')); return section;
      });
      el('upkeep-body').replaceChildren(...sections); key = nextKey; renderedRound = receipt.round;
    }
    if (model.pending && canAuto && !dialog.open && !opening) {
      opening = true; opener = document.activeElement as HTMLElement;
      expand(false); controller.presented(); dialog.showModal(); opening = false; onVisibility();
    }
  }
  const shown = (round: number | null) => dialog.open && round === renderedRound ? display : null;
  return { render, markers: (round: number | null) => shown(round)?.markers, bar: (round: number | null) => shown(round)?.bar ?? null };
}
