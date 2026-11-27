import { stageLabel, stageSpoken } from '../../table/terms.js';
import { mutationReference, createBattleGrowth, mutationSchedule, mutationRoundLabel, afterRevealLabel } from '../../table/battle-growth.js';
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

// The families a creature holds, each at its highest Stage, as the atlas's progress line reads them.
function heldLine(families: Reference['families']) {
  if (!families.length) return 'No revealed mutations yet';
  const prefix = families.length > 1 ? 'Multiclass · ' : '';
  return prefix + families.map(one => one.name + ' · ' + stageLabel(one.stage)).join(' / ');
}

// One reveal as the status line speaks it: who, what it became, its Stage and the spells it learned.
function spokenReveal(row: Row) {
  const parts = [row.name, (row.enemy ? 'opponent' : 'your team') + ': ' + row.mutation];
  if (row.stage != null) parts.push(stageSpoken(row.stage));
  if (row.spells.length) parts.push('new ' + row.spells.join(', '));
  return parts.join(', ');
}
const signed = (value: number) => `${value > 0 ? '+' : ''}${value}`;

// One card per creature that mutated: who, what it became, the stage it reached and what it bought.
function revealCard(row: Row, open: (creature: number, tier: string, source: HTMLElement) => void) {
  const card = document.createElement('li'); card.className = 'reveal-card'; card.style.setProperty('--mutation-colour', row.colour);
  const head = document.createElement('div'); head.className = 'reveal-head';
  head.append(text('span', row.name, 'reveal-creature'), text('span', row.stage == null ? '' : stageLabel(row.stage), 'reveal-stage'));
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
    // When the next mutation round comes, in the words Help uses (table/battle-growth.js): the host's rounds, never a
    // schedule worked out here.
    const next = model.over ? 'Match complete · no more mutation rounds' : mutationRoundLabel(mutationSchedule(view, catalogue));
    const held = heldLine(model.families);
    return { title: subject ? `${subject.name}${subject.enemy ? ' · Opponent' : ''}` : 'Mutation Tree', eyebrow: 'MUTATION TREE · PUBLIC GROWTH',
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
    // When the next mutation round comes (F3): the round after the reveal's own, as the host names it.
    const upcoming = receipt ? afterRevealLabel(mutationSchedule(view, catalogue), receipt.round) : '';
    const nextReceiptKey = JSON.stringify([receipt, upcoming]);
    if (nextReceiptKey !== receiptKey) {
      // Who mutated, then when the next mutation round comes; the round of the reveal is the one being played, and
      // the reveal's sheet restates it. A narrow receipt keeps the next round and leaves the counts to the crests.
      const counts = receipt ? [receipt.counts.ours ? `You ${receipt.counts.ours}` : '', receipt.counts.theirs ? `Opponent ${receipt.counts.theirs}` : ''].filter(Boolean) : [];
      el('growth-receipt-summary').textContent = counts.join(' · ');
      for (const id of ['growth-receipt-next', 'growth-next']) { el(id).textContent = upcoming; el(id).hidden = !upcoming; }
      // The status line speaks every reveal in full; the visible receipt stays two readable lines.
      el('growth-announcement').textContent = receipt ? `Round ${receipt.round} mutations revealed. ${receipt.rows.map(spokenReveal).join('. ')}.${upcoming ? ` ${upcoming}.` : ''}` : '';
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
