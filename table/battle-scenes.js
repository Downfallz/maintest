// The scenes the match's windows wear (docs/art/scene-paintings-plan.md, S11; docs/art/journeys/windows.md): the
// Mutation Tree opens on the Brute's moult, each of its family columns on the scene of that family, and the match's
// result on the ground the last blow left. Each is a head (table/picture-form.js): a decoration under the window's
// own words, declared for the battle page in docs/art/page-pictures.json and drawn from the battle's own pictures,
// which the bundle carries instead of every page's (#1016).
import { BATTLE_PAGE_ART } from './battle-page-art.js';
import { wearScene } from './picture-form.js';

export const BATTLE_PAGE = 'table/battle';

/** The tree's band, across the whole window: the screen on a phone, at most 1240 px wide. Its words on the dark left. */
export const TREE_HEAD = { name: 'three-trails-in-ash', side: 'start', focus: '50% 60%', sizes: '(min-width: 1264px) 1240px, (min-width: 820px) calc(100vw - 24px), 100vw' };

/**
 * Each family's column opens on a band of 48 px, cut on what shows the family: the Ironhide's eye under its plates,
 * the Blightweaver's sac, the Warped listening over the water. A family the list does not name keeps its plain head.
 */
export const FAMILY_HEADS = {
  brute: { name: 'ironhide-scar', focus: '50% 75%' },
  predator: { name: 'blightweaver-pressure-sac', focus: '50% 50%' },
  warped: { name: 'cataclysm-eye', focus: '50% 50%' },
};
export const FAMILY_HEAD_SIZES = '(max-width: 819px) min(94vw, 400px), 400px';

/**
 * The result's band, above the verdict: the slab and the three marks on the left, the words on the dark right. The
 * window is `min(620px, 100vw - 24px)`, held under the browser's own `max-width: calc(100% - 2em - 6px)` for a dialog:
 * 352 px on a 390 px phone.
 */
export const RESULT_HEAD = { name: 'crushing-stomp-aftermath', side: 'end', sizes: '(min-width: 658px) 620px, calc(100vw - 38px)' };

const worn = (doc, element, { name, ...options }, words) =>
  wearScene(doc, element, name, { page: BATTLE_PAGE, art: BATTLE_PAGE_ART, form: 'head', side: 'start', words, ...options });

/** The Mutation Tree's band: `.atlas-band` holds the eyebrow, the title and the progress line, and the Close. */
export function wearTreeHead(doc) {
  const band = doc.querySelector('#atlas-dialog .atlas-band');
  if (band && !band.classList.contains('scene')) worn(doc, band, TREE_HEAD, band.querySelector(':scope > .atlas-words'));
}

/** A family column's head, built by the tree: `words` holds its sigil, its name and how far the creature is. */
export function wearFamilyHead(doc, head, family, words) {
  const scene = FAMILY_HEADS[family];
  if (scene) worn(doc, head, { ...scene, sizes: FAMILY_HEAD_SIZES }, words);
}

/** The match's result: its header keeps its verdict and its close, and the band heads the verdict. */
export function wearResultHead(doc) {
  const header = doc.querySelector('#match-dialog > .match-header');
  if (header && !header.classList.contains('scene')) worn(doc, header, RESULT_HEAD, header.querySelector(':scope > div'));
}
