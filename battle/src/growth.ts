import { mutationReference, createBattleGrowth } from '../../table/battle-growth.js';
import type { Catalogue } from './planning';
import type { atlasControls, AtlasView } from './mutation-atlas';

type Atlas = ReturnType<typeof atlasControls>;

type Reference = ReturnType<typeof mutationReference>;
type Growth = ReturnType<typeof createBattleGrowth>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (tag: string, value: string, className = '') => { const node = document.createElement(tag); node.textContent = value; if (className) node.className = className; return node; };
type Row = { creature: number; tier: string; name: string; enemy: boolean; mutation: string; spells: string[]; stage: number | null; family: string | null;
  colour: string; initiative: number | null; passives: string[]; details: { name: string; cost: number | null; targeting: string | null }[] };
type Receipt = { round: number; rows: Row[]; counts: { ours: number; theirs: number } };
const signed = (value: number) => `${value > 0 ? '+' : ''}${value}`;

// One card per creature that mutated: who, what it became, the stage it reached and what it bought.
function revealCard(row: Row, open: (creature: number, tier: string, source: HTMLElement) => void) {
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
  path.addEventListener('click', () => open(row.creature, row.tier, path));
  card.append(path);
  return card;
}

export function growthControls(growth: Growth, beforeOpen: () => void, onVisibility: () => void, atlas: Atlas) {
  const dialog = el<HTMLDialogElement>('growth-dialog');
  const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-growth-open]'));
  let view: Parameters<typeof mutationReference>[0] = null, catalogue: Catalogue | null = null;
  let selected: string | number | null = null, opener: HTMLElement | null = null, receiptKey = '';
  // The public tree of one creature, for the full-screen mutation tree: what it holds, what comes next.
  function reference(creature: string | number | null, focus: string | null = null): AtlasView | null {
    const model: Reference = mutationReference(view, catalogue, creature);
    if (!model.available) return null;
    selected = model.selected;
    const subject = model.roster.find(one => one.id === model.selected);
    const next = model.over ? 'Match complete · no further mutation opportunities'
      : model.next == null ? '' : model.next === model.current ? `Mutation opportunity this round · Round ${model.next}` : `Next mutation opportunity · Round ${model.next}`;
    const held = model.families.length ? `${model.families.length > 1 ? 'Multiclass · ' : ''}${model.families.map(one => `${one.name} · Stage ${one.stage}`).join(' / ')}` : 'No revealed mutations yet';
    return { title: subject ? `${subject.name}${subject.enemy ? ' · Opponent' : ''}` : 'Mutation tree', eyebrow: 'MUTATION TREE · PUBLIC GROWTH',
      progress: [held, next].filter(Boolean).join(' · '), packages: model.packages, creature: String(model.selected), focus,
      roster: model.roster.map(one => ({ id: String(one.id), name: `${one.enemy ? 'Opponent' : 'Your team'} · ${one.name}${one.alive ? '' : ' · Defeated'}` })) };
  }
  // A revealed mutation's path, or a creature's whole tree: the full-screen mutation tree, never this sheet.
  function explore(creature: number | null, tier: string | null = null, source?: HTMLElement | null) {
    const tree = reference(creature ?? selected, tier);
    if (!tree) return;
    if (dialog.open) { opener = null; dialog.close(); }
    atlas.open(tree, catalogue, source, next => reference(next));
  }
  function open(source: HTMLButtonElement) {
    if (!mutationReference(view, catalogue, selected).available || dialog.open) return;
    if (source.dataset.growthMode !== 'reveal' || !growth.receipt()) { beforeOpen(); explore(Number(source.dataset.growthCreature ?? selected) || null, null, source); return; }
    opener = source; beforeOpen();
    const receipt = growth.receipt() as Receipt | null;
    el('growth-eyebrow').textContent = `PUBLIC REVEAL · ROUND ${receipt?.round ?? ''}`;
    dialog.showModal(); onVisibility(); el('growth-reveal').scrollTop = 0;
  }
  buttons.forEach(button => button.addEventListener('click', () => open(button)));
  el('growth-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.close(); });
  dialog.addEventListener('close', () => {
    onVisibility();
    if (opener?.isConnected && opener.getClientRects().length && !opener.matches(':disabled')) opener.focus({ preventScroll: true });
    opener = null;
  });
  el('growth-dismiss').addEventListener('click', () => { growth.dismiss(); onVisibility(); });
  el('growth-explore').addEventListener('click', () => {
    const receipt = growth.receipt() as Receipt | null, row = receipt?.rows.find(one => !one.enemy) ?? receipt?.rows[0];
    explore(row?.creature ?? null, row?.tier ?? null, el('growth-receipt').querySelector<HTMLElement>('[data-growth-open]'));
  });
  function render(next: typeof view, content: Catalogue | null, showReceipt: boolean) {
    view = next; catalogue = content;
    const available = mutationReference(view, catalogue, selected).available;
    buttons.forEach(button => { button.disabled = !available; });
    if (!available) { opener = null; selected = null; if (dialog.open) dialog.close(); }
    const receipt = growth.receipt() as Receipt | null;
    el('growth-receipt').hidden = !showReceipt || !receipt;
    if (dialog.open && !receipt) dialog.close();
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
        list.append(...rows.map(row => revealCard(row, (creature, tier, source) => explore(creature, tier, source))));
        group.append(text('h3', label), list); return group;
      }));
      receiptKey = nextReceiptKey;
    }
  }
  return { render };
}
