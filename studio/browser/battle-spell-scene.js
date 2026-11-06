import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { expect } from '@playwright/test';
const require = createRequire(new URL('../../battle/package.json', import.meta.url));
const { build } = require('esbuild');
let bundle;

// Exercise the production scene directly; no debug globals or fictional actions enter the live bundle.
export async function mountSpellScene(page) {
  bundle ??= build({ stdin: { contents: `
    import { BattleScene } from './src/scene';
    import { bootScene } from './src/boot';
    const scene = new BattleScene('/table/battle-art/');
    window.artScene = scene;
    scene.onReady = () => { window.artReady = true; };
    scene.onResizeDuringPlayback = () => scene.present({ ...window.artFrame, stage: 'decision' }, false, 1);
    bootScene(scene, document.body, { onContextLost() {}, onNeverReady() {}, isReady: () => window.artReady });
  `, resolveDir: fileURLToPath(new URL('../../battle/', import.meta.url)), loader: 'ts' }, bundle: true, write: false, format: 'iife', minify: true });
  await page.clock.install();
  await page.route('**/art4-scene.html', route => route.fulfill({ contentType: 'text/html', body:
    '<!doctype html><html><head><style>html,body{margin:0;width:100%;height:100%;overflow:hidden}</style></head><body></body></html>' }));
  await page.goto('/art4-scene.html');
  await page.addScriptTag({ content: (await bundle).outputFiles[0].text });
  await expect.poll(() => page.evaluate(() => Boolean(window.artReady))).toBe(true);
  // The browser's simulated clock can lead the runner's wall clock under load.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 10000));
}

export const spellFrame = cue => ({ stage: 'decision', actor: '1', target: cue.contact[0] ?? null, targets: [], cue,
  creatures: ['brute', 'predator', 'warped', 'brute', 'predator', 'warped'].map((form, index) =>
    ({ id: String(index + 1), form, isAlive: true, enemy: index > 2 })) });
export async function presentSpell(page, frame, stage, reduced = false, rate = 1) {
  await page.evaluate(({ frame, stage, reduced, rate }) => {
    window.artFrame = { ...frame, stage };
    window.artScene.present(window.artFrame, reduced, rate);
  }, { frame, stage, reduced, rate });
}
export const renderedEffects = page => page.evaluate(() => window.artScene.children.list
  .filter(child => child.name === 'spell-vfx').map(child => ({ ...child.data.getAll(), alpha: child.alpha })));
export const actorPosition = page => page.evaluate(() => {
  const actor = window.artScene.figures.get('1');
  return { x: actor.image.x, y: actor.image.y, homeX: actor.home.x, homeY: actor.home.y };
});
