import { mutationChoices } from '../../table/arena-mutations.js';
import { packageForest, passiveOf, requirementLine } from '../../table/mat.js';
import { cardsById, cardLines } from '../../table/card.js';
import { stageLabel } from '../../table/terms.js';
import type { Catalogue, Package } from './planning';

type Choices = ReturnType<typeof mutationChoices>;
const text = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
export function mutationCards(model: Choices, catalogue: Catalogue | null, choose: (id: string) => void) {
  const grid = document.createElement('div'); grid.className = 'mutation-cards';
  for (const face of model.faces) {
    const card = document.createElement('article'); card.className = 'mutation-card'; card.style.setProperty('--mutation-colour', face.colour);
    const select = document.createElement('button'); select.type = 'button'; select.className = 'mutation-select';
    select.dataset.mutation = face.id; select.dataset.offered = 'true';
    select.addEventListener('click', () => choose(face.id));
    select.append(text('span', `${face.family} · ${face.stage == null ? 'Stage unavailable' : stageLabel(face.stage)}`, 'mutation-family'),
      text('strong', face.name, 'mutation-name'), text('span', face.initiative == null ? 'Initiative unavailable' : `${face.initiative >= 0 ? '+' : ''}${face.initiative} initiative`, 'mutation-initiative'));
    if (face.tags.length) {
      const tags = text('span', '', 'mutation-tags'); tags.append(...face.tags.map((tag: string) => text('span', tag))); select.append(tags);
    }
    for (const spell of face.spells) {
      const preview = text('span', '', 'mutation-spell'); preview.append(text('strong', spell.name), text('span', spell.preview || 'Spell details unavailable'));
      select.append(preview);
    }
    for (const passive of face.passives) select.append(text('span', passive, 'mutation-passive'));
    if (face.missing) select.append(text('span', 'Catalogue details unavailable. This mutation is offered by the host.', 'mutation-passive'));
    select.append(text('span', 'Select mutation', 'mutation-select-label')); card.append(select);
    const details = document.createElement('details'); details.className = 'mutation-details';
    details.append(text('summary', `Details · ${face.name}`));
    if (face.requirement) details.append(text('p', face.requirement));
    for (const spell of face.spells) {
      details.append(text('h4', `${spell.name}${spell.cost == null ? '' : ` · ${spell.cost} energy`}`));
      details.append(...(spell.lines.length ? spell.lines : ['Spell details unavailable.']).map((line: string) => text('p', line)));
    }
    if (face.passives.length) details.append(text('h4', 'Passive · while held'), ...face.passives.map((line: string) => text('p', line)));
    if (!face.spells.length && !face.missing) details.append(text('p', 'This mutation adds no spells.'));
    card.append(details); grid.append(card);
  }
  if (!model.faces.length) grid.append(text('p', 'No mutation offered for this creature. Choose another creature or pass all remaining picks.', 'mutation-empty'));
  const tree = document.createElement('details'); tree.id = 'mutation-tree';
  tree.append(text('summary', 'Explore Mutation Tree · reference'), text('p', 'Acquired and picked mutations are marked below. Select an offered card above to spend a pick.'));
  const cards = cardsById(catalogue), byId = new Map<string, Package & { status: string }>(model.packages.map((pack: Package & { status: string }) => [pack.id, pack]));
  const statuses: Record<string, string> = { known: 'Acquired', picked: 'Picked · hidden until reveal', available: 'Available', future: 'Not offered this pick' };
  function branch(node: Package & { children: typeof node[] }): HTMLDetailsElement {
    const pack = byId.get(node.id)!;
    const detail = document.createElement('details'); detail.className = 'mutation-branch'; detail.open = node.level === 1;
    detail.append(text('summary', `${pack.name} · ${stageLabel(pack.level)} · ${statuses[pack.status]}`),
      text('p', requirementLine(pack, (id: string) => byId.get(id)?.name ?? id)),
      ...passiveOf(pack).map((line: string) => text('p', line)));
    for (const id of pack.spells ?? []) {
      const spell = document.createElement('details'); spell.append(text('summary', cards.get(id)?.name ?? id), text('p', cardLines(cards.get(id)).join(' · '))); detail.append(spell);
    }
    detail.append(...node.children.map(branch)); return detail;
  }
  tree.append(...packageForest(catalogue).map(branch));
  return [grid, tree];
}
