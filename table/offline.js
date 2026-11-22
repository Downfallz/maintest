// The offline page's plate (docs/art/journeys/pages.md, the offline page): the Parasite under its slate, drawn into the
// room the page holds for it. The service worker keeps this script, the picture kit and the plate's two files
// (table/sw.js); when one of them is missing, the room goes and the page is its words.
import { sceneElement } from './picture.js';

export const PLATE_ALT = 'A Parasite hidden under a slab of slate, one claw in the light, its teeth in the dark.';

/** Draws the plate into `room`, or removes the room when the painting cannot be drawn. */
export function drawPlate(doc, room) {
  const plate = sceneElement(doc, 'parasite-offline', { page: 'table/offline', form: 'plate', alt: PLATE_ALT, sizes: '(min-width: 390px) 358px, calc(100vw - 32px)' });
  plate.querySelector('img').addEventListener('error', () => room.remove(), { once: true });
  room.replaceChildren(plate);
}

if (typeof document !== 'undefined') {
  const room = document.getElementById('offline-plate');
  if (room) drawPlate(document, room);
}
