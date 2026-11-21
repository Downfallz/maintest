// The contrast of words on a painting (docs/art/scene-paintings-plan.md, S6): each box of text on a scene is read
// against the lightest pixel under it once its words are hidden, on the walk's own screenshot. These hold the
// arithmetic; studio/browser/picture-kit.spec.js holds the reading in a browser.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CONTRAST, SCENE_TEXT, contrastRatio, contrastMeasure, cutBox, lowestContrast, luminance } from '../scripts/ux/contrast.mjs';

test('luminance and ratio are WCAG 2\'s: black on white is 21:1, a colour on itself 1:1', () => {
  assert.equal(luminance([0, 0, 0]), 0);
  assert.equal(luminance([255, 255, 255]), 1);
  assert.equal(contrastRatio([0, 0, 0], [255, 255, 255]), 21);
  assert.equal(contrastRatio([255, 255, 255], [0, 0, 0]), 21);
  assert.equal(contrastRatio([118, 118, 118], [118, 118, 118]), 1);
  // #767676 on white is the classic 4.54:1.
  assert.ok(Math.abs(contrastRatio([118, 118, 118], [255, 255, 255]) - 4.54) < 0.01);
  assert.equal(CONTRAST, 4.5);
});

test('a box reads against its worst pixel: one light pixel under bone text is the one that counts', () => {
  const bone = [236, 233, 220];
  const soot = [7, 13, 16, 255], light = [200, 200, 200, 255];
  assert.ok(lowestContrast(bone, [...soot, ...soot, ...soot]) > 15);
  const worst = lowestContrast(bone, [...soot, ...light, ...soot]);
  assert.equal(worst, contrastRatio(bone, light.slice(0, 3)));
  // Dark text reads against the darkest pixel the same way.
  assert.equal(lowestContrast([0, 0, 0], [...[255, 255, 255, 255], ...[90, 90, 90, 255]]), contrastRatio([0, 0, 0], [90, 90, 90]));
  // A pixel the screenshot left transparent is not a pixel under the text.
  assert.equal(lowestContrast(bone, [...soot, 255, 255, 255, 0]), contrastRatio(bone, soot.slice(0, 3)));
});

test('a screen\'s measure counts the boxes of text on a painting, the lowest ratio and those under 4.5:1', () => {
  assert.equal(contrastMeasure([]), null, 'a screen with no words on a painting measures nothing, and its line is unchanged');
  assert.deepEqual(contrastMeasure([7.214, 4.49, 12]), { texts: 3, lowest: 4.49, under: 1 });
  assert.deepEqual(contrastMeasure([9.876]), { texts: 1, lowest: 9.87, under: 0 }, 'rounded down, so a ratio never reads higher than it is');
});

test('a text not quite opaque is blended over the pixel under it before it is read', () => {
  const white = [255, 255, 255, 255];
  assert.equal(lowestContrast([0, 0, 0, 1], white), 21);
  assert.equal(lowestContrast([0, 0, 0, 0], white), 1, 'a text that is not drawn reads as the painting itself');
  const half = lowestContrast([0, 0, 0, 0.5], white);
  assert.ok(half > 1 && half < 5, String(half));
  assert.equal(lowestContrast([0, 0, 0], white), 21, 'opaque when the alpha is left out');
});

test('a box is cut to its room, and what runs in the page carries the helpers it calls', () => {
  assert.deepEqual(cutBox({ left: -10, top: 5, right: 50, bottom: 30 }, { left: 0, top: 10, right: 40, bottom: 100 }), { left: 0, top: 10, width: 40, height: 20 });
  assert.ok(cutBox({ left: 0, top: 200, right: 10, bottom: 210 }, { left: 0, top: 0, right: 40, bottom: 100 }).height < 0, 'a box out of its room has no height');
  for (const helper of ['cutBox', 'colourBytesInPage', 'wordBoxesInPage']) assert.match(SCENE_TEXT, new RegExp(`${helper}: function ${helper}\\(`), helper);
});
