import { test, expect } from '@playwright/test';
import { join } from 'node:path';
import { onHost, readShot, root, shootStill } from '../../scripts/promo/shoot.mjs';
import { startHost } from '../../scripts/ux/set.mjs';

// The promotion's still on the real engine (docs/art/promotion-plan.md, P1; ADR 0204): a shot of the repository
// opened on a real host gives the same bytes twice, and a picture of the size its ratio asks. The still sets its
// own size, so one project plays it.
test.describe.configure({ mode: 'default' });
const shot = readShot(join(root, 'promo/shots/brute-mirror-round-3.json'));

// The width and height a PNG says in its header.
const dimensions = png => [png.readUInt32BE(16), png.readUInt32BE(20)];

test('two stills of one shot give the same bytes, at the size each ratio asks', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The still sets its own size: one project plays it.');
  test.setTimeout(180000);
  const still = ratio => onHost(startHost, origin => shootStill(browser, origin, shot, ratio));
  const first = await still('9:16'), second = await still('9:16');
  expect(dimensions(first)).toEqual([1080, 1920]);
  expect(first.equals(second)).toBe(true);
  expect(dimensions(await still('16:9'))).toEqual([1920, 1080]);
});
