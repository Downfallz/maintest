import { mutationChoices } from '../../table/battle-mutations.js';
import { cardsById } from '../../table/card.js';
import { classColour, familyRoot, requirementLine, talentPalette } from '../../table/mat.js';
import { stageLabel } from '../../table/terms.js';
import { mutationBranches } from './mutations';
import type { Catalogue, Package } from './planning';

type Model = ReturnType<typeof mutationChoices>;
type Face = Model['faces'][number];
type Pack = Package & { status: string };
const svgNS = 'http://www.w3.org/2000/svg';
const node = (tag: string, text: string, cls: string) => {
  const el = document.createElement(tag); el.className = cls; el.textContent = text; return el;
};
const parents = (pack?: Package) => [...(pack?.prerequisites ?? []), ...(pack?.anyOf ?? [])];
const sigils: Record<string, string> = {
  brute: 'M3 4 L8 7 L12 3 L16 7 L21 4 L19 15 L12 21 L5 15 Z M8 10 L12 14 L16 10',
  predator: 'M4 18 L9 6 L12 4 L8 15 Z M11 19 L16 7 L19 5 L15 16 Z M17 19 L21 10',
  warped: 'M2 12 Q12 0 22 12 Q12 24 2 12 Z M12 6 L15 12 L12 18 L9 12 Z',
};
function sigil(family: string) {
  const svg = document.createElementNS(svgNS, 'svg'); svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(svgNS, 'path'); path.setAttribute('d', sigils[family] ?? 'M12 3 L21 12 L12 21 L3 12 Z');
  svg.append(path); return svg;
}

function preview(face: Face, packages: Pack[], recipient: string) {
  const section = node('section', '', 'mutation-preview');
  section.style.setProperty('--mutation-colour', face.colour);
  section.append(node('div', `${recipient} · ${face.name}`, 'preview-name'), node('span', face.initiative == null ? 'Initiative unavailable' : `${face.initiative >= 0 ? '+' : ''}${face.initiative} initiative`, 'mutation-initiative'));
  for (const spell of face.spells) {
    const row = node('div', '', 'mutation-spell');
    row.append(node('strong', spell.name, ''), node('span', spell.preview || 'Spell details unavailable.', '')); section.append(row);
  }
  for (const passive of face.passives) section.append(node('p', passive, 'mutation-passive'));
  if (face.missing) section.append(node('p', 'Catalogue details unavailable for this offer.', 'mutation-passive'));
  const details = document.createElement('details'); details.className = 'mutation-details';
  details.append(node('summary', 'Spell & path details', ''));
  if (face.requirement) details.append(node('p', face.requirement, ''));
  for (const spell of face.spells) {
    details.append(node('h4', `${spell.name} · ${spell.cost ?? 'Unknown'} energy`, ''), ...spell.lines.map((line: string) => node('p', line, '')));
  }
  const next = packages.filter(pack => parents(pack).includes(face.id));
  if (next.length) {
    section.append(node('small', `Further paths · ${next.map(pack => pack.name).join(' / ')}`, 'mutation-future'));
    details.append(node('h4', 'Further mutations', ''), ...next.map(pack => node('p',
      `${pack.name} · ${requirementLine(pack, id => packages.find(one => one.id === id)?.name ?? id)}`, '')));
  }
  section.append(details); return section;
}

// Only offers become buttons. Existing paths and connectors come from the authored prerequisites,
// including every visible parent of a shared capstone. The complete reference remains read-only.
export function mutationTree(model: Model, catalogue: Catalogue | null, choose: (tier: string) => void) {
  const graph = node('section', '', 'mutation-graph'), subject = model.roster.find((one: {id: number; name: string}) => one.id === model.selected);
  const root = node('div', '◇', 'mutation-origin');
  graph.setAttribute('aria-label', `Mutation paths for ${subject?.name ?? 'creature'}`);
  const svg = document.createElementNS(svgNS, 'svg'); svg.classList.add('mutation-links'); svg.setAttribute('aria-hidden', 'true');
  graph.append(svg, root);
  const packages: Pack[] = model.packages;
  const byId = new Map(packages.map(pack => [pack.id, pack]));
  const palette = talentPalette(catalogue, cardsById(catalogue));
  const faces = new Map<string, Face>(model.faces.map((face: Face) => [face.id, face]));
  const ids = [...new Set([...packages.filter(pack => ['known', 'picked'].includes(pack.status)).map(pack => pack.id), ...faces.keys()])];
  const stages = new Map<number, HTMLElement>(), anchors = new Map<string, HTMLElement>();
  for (const id of ids.sort((a, b) => (byId.get(a)?.level ?? 1) - (byId.get(b)?.level ?? 1))) {
    const pack = byId.get(id), face = faces.get(id), stage = pack?.level ?? 1;
    let row = stages.get(stage);
    if (!row) { row = node('div', '', 'mutation-tier'); stages.set(stage, row); graph.append(row); }
    const tile = node('div', '', 'mutation-node');
    const family = pack ? familyRoot(pack, byId) : null;
    const colour = face?.colour ?? classColour(family?.id ?? id, palette);
    tile.style.setProperty('--mutation-colour', colour); tile.dataset.status = face ? 'available' : pack!.status;
    const label = node(face ? 'button' : 'div', '', 'mutation-select');
    label.append(sigil(family?.visual?.family ?? family?.name?.toLowerCase() ?? ''), node('strong', face?.name ?? pack!.name, 'mutation-name'));
    if (face) {
      const button = label as HTMLButtonElement; button.type = 'button'; button.dataset.mutation = id;
      button.setAttribute('aria-label', `${face.name} · ${face.family} · ${face.stage == null ? 'Stage unavailable' : stageLabel(face.stage)}`);
      button.setAttribute('aria-description', [face.tags.join(' · '), ...face.spells.map((spell: {name: string}) => spell.name)].filter(Boolean).join('. '));
      label.append(node('small', face.tags.join(' · ') || face.spells[0]?.name || (face.missing ? 'Details unavailable' : 'Passive'), 'mutation-tags'));
      button.addEventListener('click', () => choose(id));
    } else label.append(node('small', pack!.status === 'picked' ? 'Picked privately' : 'Acquired', ''));
    tile.append(label); row.append(tile); anchors.set(id, tile);
  }
  if (!faces.size) graph.append(node('p', 'No mutation offered. Choose another creature or pass all remaining picks.', 'mutation-empty'));
  const pane = node('div', '', 'mutation-preview-slot');
  const tree = document.createElement('details'); tree.id = 'mutation-tree';
  tree.append(node('summary', 'Full mutation tree', ''));
  tree.addEventListener('toggle', () => {
    if (tree.open && tree.children.length === 1) tree.append(...mutationBranches(packages, catalogue));
  });
  let active: string | null = null;
  function draw() {
    const bounds = graph.getBoundingClientRect();
    if (!bounds.width || !bounds.height) return;
    const paths: SVGPathElement[] = [];
    for (const [id, target] of anchors) {
      const sourceIds = parents(byId.get(id));
      const sources = sourceIds.length ? sourceIds.flatMap(parent => anchors.get(parent) ?? []) : [root];
      for (const source of sources) {
        const from = source.getBoundingClientRect(), to = target.getBoundingClientRect();
        const x = from.left + from.width / 2 - bounds.left, y = from.bottom - bounds.top;
        const endX = to.left + to.width / 2 - bounds.left, endY = to.top - bounds.top, mid = (y + endY) / 2;
        const path = document.createElementNS(svgNS, 'path');
        path.setAttribute('d', `M${x},${y} C${x},${mid} ${endX},${mid} ${endX},${endY}`);
        if (id === active || !faces.has(id)) path.classList.add('lit');
        paths.push(path);
      }
    }
    svg.replaceChildren(...paths);
  }
  const observer = new ResizeObserver(draw); observer.observe(graph);
  anchors.forEach(el => observer.observe(el));
  return { graph, pane, tree, dispose: () => observer.disconnect(), select(id: string | null) {
    if (active === id) return;
    active = id; const face = id ? faces.get(id) : null;
    pane.replaceChildren(...(face ? [preview(face, packages, subject?.name ?? 'Creature')] : [])); draw();
    // Opening the preview takes space from the graph. Keep the chosen node in its own scroll
    // region without moving the page, and leave a player's subsequent scroll alone on polls.
    const selected = id && anchors.get(id), scroller = graph.parentElement;
    if (selected && scroller) {
      const tile = selected.getBoundingClientRect(), area = scroller.getBoundingClientRect();
      if (tile.bottom > area.bottom) scroller.scrollTop += tile.bottom - area.bottom;
      else if (tile.top < area.top) scroller.scrollTop += tile.top - area.top;
    }
  } };
}
