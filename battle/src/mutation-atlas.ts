import { cardsById } from '../../table/card.js';
import { classColour, familyRoot, requirementLine, talentPalette } from '../../table/mat.js';
import { stageLabel } from '../../table/terms.js';
import { iconElement } from './condition-icons';
import { onNextFrame } from './next-frame';
import { spellCard } from './spell-card';
import type { Catalogue, Package } from './planning';

export type AtlasPack = Package & { status: string };
export interface AtlasView {
  title: string;
  eyebrow?: string;
  // A line under the title: what the creature holds, when the next opportunity comes.
  progress?: string;
  packages: AtlasPack[];
  // The creatures the atlas can show, and the one shown; changing it asks the caller for that creature's view.
  roster?: { id: string; name: string }[];
  creature?: string | null;
  // The mutation the atlas opens on; its path from the family root is lit.
  focus?: string | null;
  // Offered mutations can be chosen from their card; the atlas never spends a pick by itself.
  choose?: (tier: string) => void;
}

const svgNS = 'http://www.w3.org/2000/svg';
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const node = (tag: string, text = '', cls = '') => { const n = document.createElement(tag); if (text) n.textContent = text; if (cls) n.className = cls; return n; };
const key = (one: AtlasView) => JSON.stringify({ ...one, choose: Boolean(one.choose) });
const parents = (pack?: Package) => [...(pack?.prerequisites ?? []), ...(pack?.anyOf ?? [])];
const STATUS: Record<string, string> = { known: 'Acquired', picked: 'Picked · hidden until reveal', available: 'Offered now', future: 'Not acquired' };
const sigils: Record<string, string> = {
  brute: 'M3 4 L8 7 L12 3 L16 7 L21 4 L19 15 L12 21 L5 15 Z M8 10 L12 14 L16 10',
  predator: 'M4 18 L9 6 L12 4 L8 15 Z M11 19 L16 7 L19 5 L15 16 Z M17 19 L21 10',
  warped: 'M2 12 Q12 0 22 12 Q12 24 2 12 Z M12 6 L15 12 L12 18 L9 12 Z',
};
export function sigil(family: string) {
  const svg = document.createElementNS(svgNS, 'svg'); svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('aria-hidden', 'true'); svg.classList.add('atlas-sigil');
  const path = document.createElementNS(svgNS, 'path'); path.setAttribute('d', sigils[family] ?? 'M12 3 L21 12 L12 21 L3 12 Z');
  svg.append(path); return svg;
}

// The whole mutation tree, in one full-screen view: a column per family, a row per stage, every authored
// prerequisite drawn as a link, and a card for the mutation in hand with its spells. One component for every
// entry: the private chooser (offers can be chosen from their card), the public reference, and a revealed
// mutation's path, lit from its family's root. Reading it never spends a pick.
export function atlasControls(onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('atlas-dialog');
  let view: AtlasView | null = null, catalogue: Catalogue | null = null, selected: string | null = null, opener: HTMLElement | null = null;
  let anchors = new Map<string, HTMLElement>(), columns: HTMLElement[] = [];
  let request: ((creature: string) => AtlasView | null) | null = null;
  const redraw = onNextFrame(drawLinks);
  const observer = new ResizeObserver(redraw);

  el('atlas-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', event => { event.preventDefault(); if (selected && !el('atlas-detail').hidden && matchMedia('(max-width: 819px)').matches) show(null); else dialog.close(); });
  dialog.addEventListener('close', () => {
    // Nothing stays behind a closed atlas: a seat that lost access keeps no creature's tree in the page.
    observer.disconnect(); redraw.cancel(); view = null; request = null; selected = null; anchors = new Map(); columns = [];
    el('atlas-board').replaceChildren(); el('atlas-detail-body').replaceChildren(); el('atlas-detail').hidden = true; onVisibility();
    // A choice re-renders the chooser under the atlas: its opener comes back as a new node with the same id.
    const back = opener?.isConnected ? opener : opener?.id ? document.getElementById(opener.id) : null;
    if (back?.getClientRects().length && !back.matches(':disabled')) back.focus({ preventScroll: true });
    opener = null;
  });
  el<HTMLSelectElement>('atlas-creature').addEventListener('change', event => {
    const next = request?.((event.target as HTMLSelectElement).value);
    if (next) { view = { ...next }; selected = next.focus ?? null; draw(); el('atlas-board').scrollTo({ left: 0, top: 0 }); }
  });
  el('atlas-detail-close').addEventListener('click', () => show(null));

  // The family root's path to a mutation, through the ones the creature holds where there is a choice.
  function pathTo(id: string | null | undefined, byId: Map<string, AtlasPack>) {
    const path = new Set<string>();
    const walk = (current: string | undefined) => {
      if (!current || path.has(current)) return;
      path.add(current);
      const options = parents(byId.get(current));
      const held = options.filter(one => ['known', 'picked'].includes(byId.get(one)?.status ?? ''));
      for (const parent of (held.length ? held : options.slice(0, 1))) walk(parent);
    };
    walk(id ?? undefined); return path;
  }

  function draw() {
    if (!view) return;
    const packages = view.packages, byId = new Map(packages.map(pack => [pack.id, pack]));
    const palette = talentPalette(catalogue, cardsById(catalogue));
    el('atlas-eyebrow').textContent = view.eyebrow ?? 'MUTATION TREE';
    el('atlas-title').textContent = view.title;
    el('atlas-progress').textContent = view.progress ?? ''; el('atlas-progress').hidden = !view.progress;
    const select = el<HTMLSelectElement>('atlas-creature');
    select.hidden = !view.roster?.length;
    select.replaceChildren(...(view.roster ?? []).map(one => { const option = node('option', one.name) as HTMLOptionElement; option.value = one.id; return option; }));
    if (view.creature != null) select.value = String(view.creature);
    const lit = pathTo(view.focus, byId);
    observer.disconnect(); anchors = new Map(); columns = [];
    const families = new Map<string, AtlasPack[]>();
    for (const pack of packages) {
      const root = familyRoot(pack, byId);
      families.set(root.id, [...(families.get(root.id) ?? []), pack]);
    }
    const board = el('atlas-board');
    board.replaceChildren(...[...families].map(([rootId, members]) => {
      const root = byId.get(rootId)!, colour = classColour(rootId, palette);
      const column = node('section', '', 'atlas-family'); column.style.setProperty('--mutation-colour', colour);
      column.dataset.family = rootId; columns.push(column);
      const held = members.filter(pack => pack.status === 'known').reduce((top, pack) => Math.max(top, pack.level), 0);
      const head = node('header', '', 'atlas-family-head');
      head.append(sigil(root.visual?.family ?? root.name.toLowerCase()), node('strong', root.name), node('small', held ? `Stage ${held} held` : 'Not started'));
      const links = document.createElementNS(svgNS, 'svg'); links.classList.add('atlas-links'); links.setAttribute('aria-hidden', 'true');
      column.append(links, head);
      const levels = [...new Set(members.map(pack => pack.level))].sort((a, b) => a - b);
      for (const level of levels) {
        const row = node('div', '', 'atlas-stage'); row.dataset.stage = String(level);
        for (const pack of members.filter(one => one.level === level).sort((a, b) => a.id.localeCompare(b.id))) {
          const tile = node('button', '', 'atlas-node') as HTMLButtonElement; tile.type = 'button';
          tile.dataset.package = pack.id; tile.dataset.status = pack.status;
          if (lit.has(pack.id)) tile.dataset.path = 'true';
          tile.setAttribute('aria-label', `${pack.name} · ${stageLabel(pack.level)} · ${STATUS[pack.status] ?? pack.status}`);
          tile.append(node('span', String(pack.level), 'atlas-node-stage'), node('span', pack.name, 'atlas-node-name'));
          if (pack.status === 'available') tile.append(node('span', 'Offer', 'atlas-node-flag'));
          if (pack.status === 'picked') tile.append(node('span', 'Picked', 'atlas-node-flag'));
          tile.addEventListener('click', () => show(pack.id));
          row.append(tile); anchors.set(pack.id, tile);
        }
        column.append(row);
      }
      return column;
    }));
    if (!packages.length) board.append(node('p', 'Mutation catalogue unavailable.', 'atlas-empty'));
    columns.forEach(column => observer.observe(column));
    show(selected, false);
    redraw();
  }

  function drawLinks() {
    if (!view) return;
    const byId = new Map(view.packages.map(pack => [pack.id, pack]));
    const lit = pathTo(selected ?? view.focus, byId);
    for (const column of columns) {
      const links = column.querySelector<SVGSVGElement>('svg.atlas-links')!, box = column.getBoundingClientRect();
      links.setAttribute('viewBox', `0 0 ${box.width} ${box.height}`);
      const paths: SVGPathElement[] = [];
      for (const tile of column.querySelectorAll<HTMLElement>('.atlas-node')) {
        const id = tile.dataset.package!;
        for (const parent of parents(byId.get(id))) {
          const from = anchors.get(parent); if (!from || !column.contains(from)) continue;
          const a = from.getBoundingClientRect(), b = tile.getBoundingClientRect();
          const x = a.left + a.width / 2 - box.left, y = a.bottom - box.top, endX = b.left + b.width / 2 - box.left, endY = b.top - box.top, mid = (y + endY) / 2;
          const path = document.createElementNS(svgNS, 'path');
          path.setAttribute('d', `M${x},${y} C${x},${mid} ${endX},${mid} ${endX},${endY}`);
          if (lit.has(id) && lit.has(parent)) path.classList.add('lit');
          else if (['known', 'picked'].includes(byId.get(id)?.status ?? '')) path.classList.add('held');
          paths.push(path);
        }
      }
      links.replaceChildren(...paths);
    }
  }

  // The card of one mutation: what it costs to reach, what it gives, its spells in full, and what it opens.
  function show(id: string | null, scroll = true) {
    selected = id;
    for (const tile of anchors.values()) tile.setAttribute('aria-pressed', String(tile.dataset.package === id));
    const detail = el('atlas-detail'), body = el('atlas-detail-body');
    const pack = id ? view?.packages.find(one => one.id === id) : null;
    detail.hidden = !pack; dialog.dataset.detail = String(Boolean(pack));
    if (!pack || !view) { body.replaceChildren(); redraw(); return; }
    const byId = new Map(view.packages.map(one => [one.id, one]));
    const colour = classColour(familyRoot(pack, byId).id, talentPalette(catalogue, cardsById(catalogue)));
    detail.style.setProperty('--mutation-colour', colour);
    const head = node('header', '', 'atlas-card-head');
    head.append(node('p', `${familyRoot(pack, byId).name} · ${stageLabel(pack.level)}`, 'atlas-card-family'), node('h3', pack.name, 'atlas-card-name'));
    const chips = node('div', '', 'atlas-card-chips');
    chips.append(node('span', STATUS[pack.status] ?? pack.status, `atlas-chip atlas-chip-status`));
    if (Number.isInteger(pack.initiativeBonus) && pack.initiativeBonus !== 0) {
      const init = node('span', '', 'atlas-chip atlas-chip-init'); init.append(iconElement('initiative-gain'), document.createTextNode(`${pack.initiativeBonus > 0 ? '+' : ''}${pack.initiativeBonus} initiative`)); chips.append(init);
    }
    head.append(chips);
    const parts: HTMLElement[] = [head];
    if (parents(pack).length) parts.push(node('p', requirementLine(pack, (one: string) => byId.get(one)?.name ?? one), 'atlas-card-requirement'));
    if (pack.passiveLines?.length) {
      const passives = node('ul', '', 'atlas-card-passives');
      passives.append(...pack.passiveLines.map(line => node('li', line)));
      parts.push(node('h4', 'Passive · while held'), passives);
    }
    const cards = cardsById(catalogue);
    if (pack.spells?.length) parts.push(node('h4', pack.spells.length > 1 ? 'Spells' : 'Spell'), ...pack.spells.map(spell => spellCard(cards.get(spell), spell, catalogue?.effects ?? [])));
    const next = view.packages.filter(one => parents(one).includes(pack.id));
    if (next.length) {
      const further = node('div', '', 'atlas-card-next');
      further.append(node('h4', 'Opens'), ...next.map(one => {
        const link = node('button', one.name, 'atlas-next') as HTMLButtonElement; link.type = 'button';
        link.addEventListener('click', () => show(one.id)); return link;
      }));
      parts.push(further);
    }
    if (pack.status === 'available' && view.choose) {
      const choose = node('button', `Choose ${pack.name}`, 'primary atlas-choose') as HTMLButtonElement; choose.type = 'button';
      // The atlas covers the field, which holds every choice: it closes first, then chooses on the uncovered page.
      choose.addEventListener('click', () => {
        const pick = view?.choose, back = opener?.id;
        dialog.close(); pick?.(pack.id);
        if (back && !document.querySelector('dialog:modal')) document.getElementById(back)?.focus({ preventScroll: true });
      });
      parts.push(choose);
    }
    body.replaceChildren(...parts); body.scrollTop = 0;
    if (scroll) anchors.get(pack.id)?.scrollIntoView({ block: 'nearest', inline: 'center' });
    redraw();
  }

  return {
    open(next: AtlasView, content: Catalogue | null, source?: HTMLElement | null, creatureView?: (creature: string) => AtlasView | null) {
      view = next; catalogue = content; selected = next.focus ?? null; request = creatureView ?? null;
      opener = source ?? (document.activeElement as HTMLElement | null);
      if (!dialog.open) dialog.showModal();
      draw(); onVisibility();
      if (selected) anchors.get(selected)?.scrollIntoView({ block: 'center', inline: 'center' });
    },
    // A poll asks whoever opened the atlas for the same creature's newer view: the mutation in hand and the
    // focused node stay; a view that no longer exists (access lost, the question gone) closes the atlas.
    refresh(content: Catalogue | null) {
      if (!dialog.open || !view) return;
      const next = request?.(String(view.creature ?? ''));
      if (!next) { dialog.close(); return; }
      const kept = { ...next, focus: view.focus };
      if (key(kept) === key(view) && content === catalogue) { view = kept; return; }
      catalogue = content;
      const focused = (document.activeElement as HTMLElement | null)?.closest<HTMLElement>('.atlas-node')?.dataset.package;
      const keep = selected; view = kept; selected = keep; draw();
      if (focused) anchors.get(focused)?.focus({ preventScroll: true });
    },
    close() { if (dialog.open) dialog.close(); },
    isOpen: () => dialog.open,
  };
}
