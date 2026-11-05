import { mutationReference, createArenaGrowth } from '../../table/arena-growth.js';
import { mutationBranches } from './mutations';
import type { Catalogue } from './planning';

type Reference = ReturnType<typeof mutationReference>;
type Growth = ReturnType<typeof createArenaGrowth>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (tag: string, value: string) => { const node = document.createElement(tag); node.textContent = value; return node; };

export function growthControls(growth: Growth, beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('growth-dialog');
  const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-growth-open]'));
  let view: Parameters<typeof mutationReference>[0] = null, catalogue: Catalogue | null = null;
  let selected: string | number | null = null, opener: HTMLElement | null = null, key = '', lastCatalogue: Catalogue | null = null;
  let receiptKey = '';
  function draw() {
    const model: Reference = mutationReference(view, catalogue, selected); selected = model.selected;
    const nextKey = JSON.stringify(model);
    if (nextKey === key && lastCatalogue === catalogue) return;
    const focused = dialog.contains(document.activeElement) ? document.activeElement as HTMLElement : null;
    const focusPath = focused?.closest('details[data-path]')?.getAttribute('data-path');
    const disclosures = new Map(Array.from(el('growth-tree').querySelectorAll<HTMLDetailsElement>('details[data-path]')).map(node => [node.dataset.path, node.open]));
    const scroll = el('growth-body').scrollTop;
    const select = el<HTMLSelectElement>('growth-creature');
    select.replaceChildren(...model.roster.map(one => {
      const option = document.createElement('option'); option.value = String(one.id);
      option.textContent = `${one.enemy ? 'Opponent' : 'Your team'} · ${one.name}${one.alive ? '' : ' · Defeated'}`; return option;
    }));
    select.value = String(selected);
    el('growth-progress').textContent = model.families.length ? `${model.families.length > 1 ? 'Multiclass · ' : ''}${model.families.map(one => `${one.name} · Stage ${one.stage}`).join(' / ')}` : 'No revealed mutations yet';
    el('growth-next').textContent = model.over ? 'Match complete · no further mutation opportunities'
      : model.next == null ? 'Next mutation opportunity unavailable'
        : model.next === model.current ? `Mutation opportunity this round · Round ${model.next}` : `Next mutation opportunity · Round ${model.next}`;
    // Public: the client never knows the other side's offer, so a tier it does not hold is only "not acquired".
    el('growth-tree').replaceChildren(...mutationBranches(model.packages, catalogue, { future: 'Not acquired' }));
    if (!model.packages.length) el('growth-tree').append(text('p', 'Mutation catalogue unavailable.'));
    for (const node of el('growth-tree').querySelectorAll<HTMLDetailsElement>('details[data-path]')) {
      if (disclosures.has(node.dataset.path)) node.open = disclosures.get(node.dataset.path)!;
      if (focusPath === node.dataset.path && !focused?.isConnected) node.querySelector('summary')?.focus({ preventScroll: true });
    }
    el('growth-body').scrollTop = scroll;
    key = nextKey; lastCatalogue = catalogue;
  }
  function open(source: HTMLElement) {
    if (!mutationReference(view, catalogue, selected).available || dialog.open) return;
    opener = source;
    selected = source.dataset.growthCreature ?? selected;
    beforeOpen(); draw(); dialog.showModal(); onVisibility();
  }
  buttons.forEach(button => button.addEventListener('click', () => open(button)));
  el('growth-creature').addEventListener('change', event => { selected = (event.target as HTMLSelectElement).value; draw(); el('growth-body').scrollTop = 0; });
  el('growth-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.close(); });
  dialog.addEventListener('close', () => {
    onVisibility();
    if (opener?.isConnected && opener.getClientRects().length && !opener.matches(':disabled')) opener.focus({ preventScroll: true });
    opener = null;
  });
  el('growth-dismiss').addEventListener('click', () => { growth.dismiss(); onVisibility(); });
  function render(next: typeof view, content: Catalogue | null, showReceipt: boolean) {
    view = next; catalogue = content;
    const available = mutationReference(view, catalogue, selected).available;
    buttons.forEach(button => { button.disabled = !available; });
    if (!available) {
      opener = null; selected = null; key = ''; lastCatalogue = null;
      if (dialog.open) dialog.close();
      for (const id of ['growth-creature', 'growth-progress', 'growth-next', 'growth-tree']) el(id).replaceChildren();
    } else if (dialog.open) draw();
    const receipt = growth.receipt();
    el('growth-receipt').hidden = !showReceipt || !receipt;
    const nextReceiptKey = JSON.stringify(receipt);
    if (nextReceiptKey !== receiptKey) {
      el('growth-announcement').textContent = receipt ? `Round ${receipt.round} · ${receipt.rows.map((row: { name: string; mutation: string; spells: string[] }) => `${row.name}: ${row.spells.length ? row.spells.join(', ') : row.mutation}`).join(' · ')}` : '';
      el('growth-announcement').title = el('growth-announcement').textContent ?? '';
      el('growth-reveals').replaceChildren(...(receipt?.rows ?? []).map((row: { name: string; enemy: boolean; mutation: string; spells: string[] }) => text('li', `${row.name} (${row.enemy ? 'Opponent' : 'Your team'}) · ${row.mutation}${row.spells.length ? ` · New: ${row.spells.join(', ')}` : ''}`)));
      receiptKey = nextReceiptKey;
    }
    el('growth-reveal-details').hidden = !receipt;
  }
  return { render };
}
