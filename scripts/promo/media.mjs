// The record of the promotion's media (docs/art/promotion-plan.md, P2; ADR 0206): the films and pictures live in the
// promo container of the table's storage account, and the repository keeps what each file is, by its hash. A line
// is copied from the summary of `.github/workflows/promo-film.yml` after a run.
//
//   node scripts/promo/media.mjs            # checks promo/media.json, exit 1 on a line it refuses

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RATIOS, root } from './shoot.mjs';

export const MEDIA_FORMAT = 'downfall.promo.media.v1';
const FILE_KEYS = ['shot', 'ratio', 'file', 'sha256', 'bytes', 'blob'];
const KINDS = { mp4: 'film', png: 'sheet' };

/**
 * The record read and checked: each file names a shot of `promo/shots/`, one of its ratios' film or sheet, its
 * SHA-256 and size, and the blob it was written to, which is the shot's name and the hash. Returns the files.
 */
export function readMedia(path, { shots = join(root, 'promo/shots') } = {}) {
  let media;
  try {
    media = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path}: not the media's JSON (${error.message}).`);
  }
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  if (media?.format !== MEDIA_FORMAT || !Array.isArray(media.files)) refuse(`the record is {"format": "${MEDIA_FORMAT}", "files": [...]}.`);
  const seen = new Set();
  media.files.forEach((file, index) => {
    const at = `file ${index + 1}`;
    if (file === null || typeof file !== 'object' || Object.keys(file).sort((a, b) => a.localeCompare(b)).join() !== [...FILE_KEYS].sort((a, b) => a.localeCompare(b)).join()) refuse(`${at} has exactly ${FILE_KEYS.join(', ')}.`);
    if (!/^[a-z0-9][a-z0-9-]*$/.test(file.shot) || !existsSync(join(shots, `${file.shot}.json`))) refuse(`${at} names the shot ${JSON.stringify(file.shot)}, which promo/shots/ does not hold.`);
    if (!Object.hasOwn(RATIOS, file.ratio)) refuse(`${at} names the ratio ${JSON.stringify(file.ratio)}.`);
    const tag = String(file.ratio).replace(':', 'x');
    const extension = String(file.file).split('.').pop();
    if (file.file !== `${KINDS[extension]}-${tag}.${extension}`) refuse(`${at} is film-${tag}.mp4 or sheet-${tag}.png, not ${JSON.stringify(file.file)}.`);
    if (!/^[0-9a-f]{64}$/.test(file.sha256)) refuse(`${at} gives its SHA-256 as 64 hexadecimal digits.`);
    if (!Number.isInteger(file.bytes) || file.bytes <= 0) refuse(`${at} gives its size in bytes.`);
    if (file.blob !== `${file.shot}/${file.sha256}.${extension}`) refuse(`${at} was written to ${file.shot}/${file.sha256}.${extension}, its hash under its shot.`);
    if (seen.has(file.blob)) refuse(`${at} records ${file.blob} twice.`);
    seen.add(file.blob);
  });
  return media.files;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const files = readMedia(join(root, 'promo/media.json'));
    console.log(`promo/media.json: ${files.length} file(s).`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
