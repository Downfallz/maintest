import { pictureElement } from '../../table/picture.js';

// The optional art name comes from a spell's or a capstone's visual in the served catalogue; the picture is the
// kit's `spell-<name>`, declared for the battle page (docs/art/page-pictures.json, ADR 0198) and sent at the size it is drawn. A name the
// kit does not hold draws nothing. A picture that fails to load keeps its place, hidden, so the card's text does not
// move after it is drawn.
export function spellPortrait(name?: string | null, drawn = 40) {
  if (!name || !/^[a-z0-9-]+$/.test(name)) return null;
  let image: HTMLImageElement;
  try {
    image = pictureElement(document, `spell-${name}`, { page: 'table/battle', class: 'spell-portrait', sizes: `${drawn}px` });
  } catch {
    return null;
  }
  image.addEventListener('error', () => image.classList.add('missing'), { once: true });
  return image;
}
