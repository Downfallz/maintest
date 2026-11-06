import { packageForest, passiveOf, requirementLine, talentPalette, classColour, familyRoot } from '../../table/mat.js';
import { cardsById, cardLines } from '../../table/card.js';
import { stageLabel } from '../../table/terms.js';
import type { Catalogue, Package } from './planning';


const text = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
// The command and public reference share exactly the authored prerequisite graph and card details.
// `labels` lets the public reference speak of ownership; the private chooser speaks of this pick's offer.
export function mutationBranches(packages: (Package & { status: string })[], catalogue: Catalogue | null, labels: Record<string, string> = {}) {
  const cards = cardsById(catalogue), byId = new Map<string, Package & { status: string }>(packages.map((pack: Package & { status: string }) => [pack.id, pack]));
  const palette = talentPalette(catalogue, cards);
  const statuses: Record<string, string> = { known: 'Acquired', picked: 'Picked · hidden until reveal', available: 'Available', future: 'Not offered this pick', ...labels };
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
