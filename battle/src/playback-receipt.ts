import type { createBattlePlayback } from '../../table/battle-playback.js';
import { receiptDetails } from './receipt-details';

type PlaybackModel = ReturnType<ReturnType<typeof createBattlePlayback>['model']>;
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const pacedDetails = $<HTMLButtonElement>('paced-details');
const details = receiptDetails(pacedDetails, $('receipt-sections'));
let receiptKey = '', receiptRound: number | null = null;

export function renderReceipt(receipt: PlaybackModel['receipt'], round: number | null, summary: string | undefined) {
  $('playback-receipt').hidden = !receipt;
  const key = JSON.stringify([round, receipt, summary]);
  if (key === receiptKey) return;
  const scroll = receiptRound === round ? $('playback-receipt').scrollTop : 0;
  const paced = summary !== undefined;
  if (!paced || receiptRound !== round || pacedDetails.hidden) details.expand(!paced);
  $('playback-receipt').classList.toggle('paced-upkeep', paced);
  pacedDetails.hidden = !paced;
  $('paced-summary').hidden = !paced;
  $('paced-summary').textContent = summary ?? '';
  receiptKey = key; receiptRound = round;
  $('receipt-title').textContent = paced ? 'Upkeep' : receipt?.title ?? '';
  $('receipt-sections').replaceChildren(...(receipt?.sections ?? []).map((section: { title: string; items: { text: string; tone: string }[] }) => {
    const group = document.createElement('section'), title = document.createElement('h3');
    title.textContent = section.title; group.append(title);
    for (const row of section.items) { const line = document.createElement('p'); line.textContent = row.text; line.dataset.tone = row.tone; group.append(line); }
    return group;
  }));
  $('playback-receipt').scrollTop = scroll;
}
