// The record of the promotion's media (docs/art/promotion-plan.md, P2; ADR 0206): scripts/promo/media.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { MEDIA_FORMAT, readMedia } from '../scripts/promo/media.mjs';

const record = fileURLToPath(new URL('../promo/media.json', import.meta.url));
const script = fileURLToPath(new URL('../scripts/promo/media.mjs', import.meta.url));
const hash = 'a'.repeat(64);
const film = { shot: 'brute-pummel-round-3', ratio: '9:16', file: 'film-9x16.mp4', sha256: hash, bytes: 1234, blob: `brute-pummel-round-3/${hash}.mp4` };

// A record written to a folder of its own, read back.
function withMedia(media) {
  const folder = mkdtempSync(join(tmpdir(), 'promo-media-'));
  try {
    const path = join(folder, 'media.json');
    writeFileSync(path, typeof media === 'string' ? media : JSON.stringify(media));
    return readMedia(path);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

test('the repository\'s record reads, and the command says how many files it holds', () => {
  assert.ok(Array.isArray(readMedia(record)));
  const run = spawnSync(process.execPath, [script], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stderr);
  assert.match(run.stdout, /promo\/media\.json: \d+ file\(s\)\./);
});

test('a film and its sheet read, each by its hash under its shot', () => {
  const sheet = { ...film, file: 'sheet-9x16.png', blob: `brute-pummel-round-3/${hash}.png` };
  assert.deepEqual(withMedia({ format: MEDIA_FORMAT, files: [film, sheet] }), [film, sheet]);
});

test('a file of a missing shot, another ratio, another name, a short hash, no size or another blob is refused', () => {
  const refused = (change, why) => assert.throws(() => withMedia({ format: MEDIA_FORMAT, files: [{ ...film, ...change }] }), why);
  refused({ shot: 'no-such-shot' }, /promo\/shots\/ does not hold/);
  refused({ ratio: '4:3' }, /names the ratio "4:3"/);
  refused({ file: 'film-16x9.mp4' }, /film-9x16\.mp4 or sheet-9x16\.png/);
  refused({ sha256: 'abc' }, /64 hexadecimal digits/);
  refused({ bytes: 0 }, /size in bytes/);
  refused({ blob: 'elsewhere.mp4' }, /its hash under its shot/);
  assert.throws(() => withMedia({ format: MEDIA_FORMAT, files: [{ ...film, link: 'x' }] }), /has exactly/);
  assert.throws(() => withMedia({ format: MEDIA_FORMAT, files: [film, film] }), /twice/);
  assert.throws(() => withMedia({ files: [] }), /"format"/);
  assert.throws(() => withMedia('{'), /not the media's JSON/);
});
