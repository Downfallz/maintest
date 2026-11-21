import { pictureElement } from '../../table/picture.js';

// The optional art name comes from a spell's or a package's visual in the served catalogue. The picture is the
// kit's `spell-<name>`, declared for the battle page (docs/art/page-pictures.json, ADR 0198) and sent at the size
// it is drawn. A name the kit does not hold draws nothing. A picture that fails to load keeps its place, hidden, so
// the card's text does not move after it is drawn; beside a title, it is not drawn at all. `drawn` is the width,
// or a `sizes` list when the screen decides it (the spellbook's 24 and 40 px).
export function spellPortrait(name?: string | null, drawn: number | string = 40) {
  if (!name || !/^[a-z0-9-]+$/.test(name)) return null;
  let image: HTMLImageElement;
  try {
    image = pictureElement(document, `spell-${name}`, { page: 'table/battle', class: 'spell-portrait', sizes: typeof drawn === 'number' ? `${drawn}px` : drawn });
  } catch {
    return null;
  }
  image.addEventListener('error', () => image.classList.add('missing'), { once: true });
  return image;
}
