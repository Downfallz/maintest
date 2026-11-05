import { mutationChoices } from '../../table/arena-mutations.js';
import { packageForest, passiveOf, requirementLine, talentPalette, classColour, familyRoot } from '../../table/mat.js';
import { cardsById, cardLines } from '../../table/card.js';
import { stageLabel } from '../../table/terms.js';
import type { Catalogue, Package } from './planning';

type Choices = ReturnType<typeof mutationChoices>;
const text = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
export function mutationCards(model: Choices, catalogue: Catalogue | null, choose: (id: string) => void) {
  const grid = document.createElement('div'); grid.className = 'mutation-cards';
  model.faces.forEach((face: Choices['faces'][number], index: number) => {
    const id = (part: string) => `mutation-card-${index}-${part}`;
    const card = document.createElement('article'); card.className = 'mutation-card'; card.style.setProperty('--mutation-colour', face.colour);
    const select = document.createElement('button'); select.type = 'button'; select.className = 'mutation-select';
    select.dataset.mutation = face.id; select.dataset.offered = 'true';
    select.addEventListener('click', () => choose(face.id));
    const family = text('span', `${face.family} · ${face.stage == null ? 'Stage unavailable' : stageLabel(face.stage)}`, 'mutation-family');
    const name = text('strong', face.name, 'mutation-name'); name.id = id('name'); family.id = id('family');
    select.append(family, name, text('span', face.initiative == null ? 'Initiative unavailable' : `${face.initiative >= 0 ? '+' : ''}${face.initiative} initiative`, 'mutation-initiative'));
    if (face.tags.length) {
      const tags = text('span', '', 'mutation-tags'); tags.append(...face.tags.map((tag: string) => text('span', tag))); select.append(tags);
    }
    for (const spell of face.spells) {
      const preview = text('span', '', 'mutation-spell'); preview.append(text('strong', spell.name), text('span', spell.preview || 'Spell details unavailable'));
      select.append(preview);
    }
    for (const passive of face.passives) select.append(text('span', passive, 'mutation-passive'));
    if (face.missing) select.append(text('span', 'Catalogue details unavailable. This mutation is offered by the host.', 'mutation-passive'));
    // The whole card would otherwise be the button's name, and it would change on selection: aria-pressed says that.
    const described = Array.from(select.children).filter(node => node !== family && node !== name);
    described.forEach((node, part) => { node.id = id(`about-${part}`); });
    select.setAttribute('aria-labelledby', `${name.id} ${family.id}`);
    select.setAttribute('aria-describedby', described.map(node => node.id).join(' '));
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
  });
  if (!model.faces.length) grid.append(text('p', 'No mutation offered for this creature. Choose another creature or pass all remaining picks.', 'mutation-empty'));
  const tree = document.createElement('details'); tree.id = 'mutation-tree';
  tree.append(text('summary', 'Explore Mutation Tree · reference'), text('p', 'Acquired and picked mutations are marked below. Select an offered card above to spend a pick.'));
  tree.append(...mutationBranches(model.packages, catalogue));
  return [grid, tree];
}

// The command and public reference share exactly the authored prerequisite graph and card details.
export function mutationBranches(packages: (Package & { status: string })[], catalogue: Catalogue | null) {
  const cards = cardsById(catalogue), byId = new Map<string, Package & { status: string }>(packages.map((pack: Package & { status: string }) => [pack.id, pack]));
  const palette = talentPalette(catalogue, cards);
  const statuses: Record<string, string> = { known: 'Acquired', picked: 'Picked · hidden until reveal', available: 'Available', future: 'Not offered this pick' };
  function branch(node: Package & { children: typeof node[] }, parents: string[]): HTMLDetailsElement {
    const pack = byId.get(node.id)!, path = [...parents, node.id];
    const detail = document.createElement('details'); detail.dataset.package = pack.id; detail.dataset.path = JSON.stringify(path); detail.dataset.status = pack.status; detail.className = 'mutation-branch'; detail.open = node.level === 1;
    detail.style.setProperty('--mutation-colour', classColour(familyRoot(pack, byId).id, palette));
    detail.append(text('summary', `${pack.name} · ${stageLabel(pack.level)} · ${statuses[pack.status]}`),
      text('p', requirementLine(pack, (id: string) => byId.get(id)?.name ?? id)),
      text('p', Number.isInteger(pack.initiativeBonus) ? `${pack.initiativeBonus >= 0 ? '+' : ''}${pack.initiativeBonus} initiative` : 'Initiative unavailable'),
      ...(passiveOf(pack).length ? [text('h4', 'Passive · while held'), ...passiveOf(pack).map((line: string) => text('p', line))] : []));
    for (const id of pack.spells ?? []) {
      const card = cards.get(id);
      const spell = document.createElement('details'); spell.dataset.path = JSON.stringify([...path, 'spell', id]);
      spell.append(text('summary', card?.name ?? id), text('p', Number.isInteger(card?.cost) ? `${card.cost} energy` : 'Cost unavailable'),
        text('p', cardLines(card).join(' · ') || 'Spell details unavailable.')); detail.append(spell);
    }
    detail.append(...node.children.map(child => branch(child, path))); return detail;
  }
  return packageForest(catalogue).map((node: Package & { children: typeof node[] }) => branch(node, []));
}
