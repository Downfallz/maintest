import { mutationReference, createBattleGrowth } from '../../table/battle-growth.js';
import { mutationBranches } from './mutations';
import type { Catalogue } from './planning';

type Reference = ReturnType<typeof mutationReference>;
type Growth = ReturnType<typeof createBattleGrowth>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (tag: string, value: string, className = '') => { const node = document.createElement(tag); node.textContent = value; if (className) node.className = className; return node; };
type Row = { creature: number; name: string; enemy: boolean; mutation: string; spells: string[]; stage: number | null; family: string | null;
  colour: string; initiative: number | null; passives: string[]; details: { name: string; cost: number | null; targeting: string | null }[] };
type Receipt = { round: number; rows: Row[]; counts: { ours: number; theirs: number } };
const signed = (value: number) => `${value > 0 ? '+' : ''}${value}`;

// One card per creature that mutated: who, what it became, the stage it reached and what it bought.
function revealCard(row: Row, open: (creature: number) => void) {
  const card = document.createElement('li'); card.className = 'reveal-card'; card.style.setProperty('--mutation-colour', row.colour);
  const head = document.createElement('div'); head.className = 'reveal-head';
  head.append(text('span', row.name, 'reveal-creature'), text('span', row.stage == null ? '' : `Stage ${row.stage}`, 'reveal-stage'));
  const meta = [row.family && row.family !== row.mutation ? `${row.family} path` : '', row.initiative ? `${signed(row.initiative)} initiative` : ''].filter(Boolean).join(' · ');
  card.append(head, text('p', row.mutation, 'reveal-mutation'));
  if (meta) card.append(text('p', meta, 'reveal-meta'));
  if (row.details.length) {
    const spells = document.createElement('ul'); spells.className = 'reveal-spells';
    spells.append(...row.details.map(spell => {
      const item = document.createElement('li');
      item.append(text('strong', `+ ${spell.name}`), text('span', [spell.cost == null ? '' : `${spell.cost} energy`, spell.targeting ?? ''].filter(Boolean).join(' · ')));
      return item;
    }));
    card.append(spells);
  }
  for (const passive of row.passives) card.append(text('p', passive, 'reveal-passive'));
  if (!row.details.length && !row.passives.length && !row.initiative) card.append(text('p', 'No new spell this time.', 'reveal-passive'));
  const path = text('button', 'See its path', 'reveal-path') as HTMLButtonElement; path.type = 'button';
  path.setAttribute('aria-label', `See ${row.name}'s mutation path`);
  path.addEventListener('click', () => open(row.creature));
  card.append(path);
  return card;
}

export function growthControls(growth: Growth, beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('growth-dialog');
  const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-growth-open]'));
  let view: Parameters<typeof mutationReference>[0] = null, catalogue: Catalogue | null = null;
  let selected: string | number | null = null, opener: HTMLElement | null = null, key = '', lastCatalogue: Catalogue | null = null;
  let receiptKey = '', mode: 'reveal' | 'tree' = 'tree', drawnFor: string | number | null = null;
  // The reveal reads first when the receipt opens the dialog; every other entry is the reference tree.
  function setMode(next: 'reveal' | 'tree') {
    mode = next === 'reveal' && growth.receipt() ? 'reveal' : 'tree';
    dialog.dataset.mode = mode;
    const receipt = growth.receipt() as Receipt | null;
    el('growth-eyebrow').textContent = mode === 'reveal' ? `PUBLIC REVEAL · ROUND ${receipt?.round ?? ''}` : 'PUBLIC GROWTH · REFERENCE';
    el('growth-title').textContent = mode === 'reveal' ? 'Mutations revealed' : 'Mutation Tree';
  }
  function explore(creature: number | null) {
    if (creature != null) selected = creature;
    setMode('tree'); key = ''; draw(); el('growth-body').scrollTop = 0;
    el('growth-creature').focus({ preventScroll: true });
  }
  function draw() {
    const model: Reference = mutationReference(view, catalogue, selected); selected = model.selected;
    const nextKey = JSON.stringify(model);
    if (nextKey === key && lastCatalogue === catalogue) return;
    const focused = dialog.contains(document.activeElement) ? document.activeElement as HTMLElement : null;
    const focusPath = focused?.closest('details[data-path]')?.getAttribute('data-path');
    // Disclosures survive a poll of the same creature; another creature starts from its own path.
    const disclosures = new Map(drawnFor === model.selected ? Array.from(el('growth-tree').querySelectorAll<HTMLDetailsElement>('details[data-path]')).map(node => [node.dataset.path, node.open]) : []);
    drawnFor = model.selected;
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
    // A creature's own path reads first: families it holds stay open, the others fold away until asked for.
    const families = Array.from(el('growth-tree').querySelectorAll<HTMLDetailsElement>(':scope > details'));
    const held = families.filter(node => node.dataset.status === 'known' || node.querySelector('[data-status="known"]'));
    if (held.length) for (const node of families) node.open = held.includes(node);
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
    setMode(source.dataset.growthMode === 'reveal' ? 'reveal' : 'tree');
    beforeOpen(); draw(); dialog.showModal(); onVisibility();
    if (mode === 'reveal') el('growth-reveal').scrollTop = 0;
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
  el('growth-explore').addEventListener('click', () => {
    const receipt = growth.receipt() as Receipt | null;
    explore(receipt?.rows.find(row => !row.enemy)?.creature ?? receipt?.rows[0]?.creature ?? null);
  });
  function render(next: typeof view, content: Catalogue | null, showReceipt: boolean) {
    view = next; catalogue = content;
    const available = mutationReference(view, catalogue, selected).available;
    buttons.forEach(button => { button.disabled = !available; });
    if (!available) {
      opener = null; selected = null; key = ''; lastCatalogue = null;
      if (dialog.open) dialog.close();
      for (const id of ['growth-creature', 'growth-progress', 'growth-next', 'growth-tree']) el(id).replaceChildren();
    } else if (dialog.open) draw();
    const receipt = growth.receipt() as Receipt | null;
    el('growth-receipt').hidden = !showReceipt || !receipt;
    if (dialog.open && mode === 'reveal' && !receipt) setMode('tree');
    const nextReceiptKey = JSON.stringify(receipt);
    if (nextReceiptKey !== receiptKey) {
      // Counts first: a narrow receipt cuts the round, which the reveal sheet restates, not who mutated.
      const counts = receipt ? [receipt.counts.ours ? `You ${receipt.counts.ours}` : '', receipt.counts.theirs ? `Opponent ${receipt.counts.theirs}` : ''].filter(Boolean) : [];
      el('growth-receipt-summary').textContent = receipt ? [...counts, `Round ${receipt.round}`].join(' · ') : '';
      // The status line speaks every reveal in full; the visible receipt stays one readable line.
      el('growth-announcement').textContent = receipt ? `Round ${receipt.round} mutations revealed. ${receipt.rows.map(row =>
        `${row.name}, ${row.enemy ? 'opponent' : 'your team'}: ${row.mutation}${row.stage == null ? '' : `, stage ${row.stage}`}${row.spells.length ? `, new ${row.spells.join(', ')}` : ''}`).join('. ')}.` : '';
      const groups = receipt ? [['Your team', receipt.rows.filter(row => !row.enemy)], ['Opponent', receipt.rows.filter(row => row.enemy)]] as const : [];
      el('growth-reveals').replaceChildren(...groups.filter(([, rows]) => rows.length).map(([label, rows]) => {
        const group = document.createElement('section'); group.className = 'reveal-group';
        const list = document.createElement('ul'); list.className = 'reveal-list';
        list.append(...rows.map(row => revealCard(row, explore)));
        group.append(text('h3', label), list); return group;
      }));
      receiptKey = nextReceiptKey;
    }
  }
  return { render };
}
