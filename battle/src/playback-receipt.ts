import type { createBattlePlayback } from '../../table/battle-playback.js';

type PlaybackModel = ReturnType<ReturnType<typeof createBattlePlayback>['model']>;
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
let receiptKey = '', receiptRound: number | null = null;

// The match result covers the field; a paced turnover reads on it instead (live.ts), so this draws the result.
export function renderReceipt(receipt: PlaybackModel['receipt'], round: number | null) {
  $('playback-receipt').hidden = !receipt;
  const key = JSON.stringify([round, receipt]);
  if (key === receiptKey) return;
  const scroll = receiptRound === round ? $('playback-receipt').scrollTop : 0;
  receiptKey = key; receiptRound = round;
  $('receipt-title').textContent = receipt?.title ?? '';
  $('receipt-sections').replaceChildren(...(receipt?.sections ?? []).map((section: { title: string; items: { text: string; tone: string }[] }) => {
    const group = document.createElement('section'), title = document.createElement('h3');
    title.textContent = section.title; group.append(title);
    for (const row of section.items) { const line = document.createElement('p'); line.textContent = row.text; line.dataset.tone = row.tone; group.append(line); }
    return group;
  }));
  $('playback-receipt').scrollTop = scroll;
}
