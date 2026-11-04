import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
const require = createRequire(new URL('../../arena/package.json', import.meta.url));
const { build } = require('esbuild');
let bundle;
export async function audioModule(page) {
  bundle ??= build({ stdin: { contents: "import { ArenaAudio } from './src/audio'; window.TestArenaAudio = ArenaAudio;",
    resolveDir: fileURLToPath(new URL('../../arena/', import.meta.url)), loader: 'ts' }, bundle: true, write: false, format: 'iife' });
  await page.addScriptTag({ content: (await bundle).outputFiles[0].text });
}
export async function probeAudio(page) {
  await page.addInitScript(() => {
    window.audioProbe = { contexts: 0, starts: 0, stops: 0 };
    const Original = window.AudioContext;
    window.AudioContext = class extends Original {
      constructor(...args) { super(...args); window.audioProbe.contexts++; }
      createOscillator() {
        const node = super.createOscillator(), start = node.start.bind(node), stop = node.stop.bind(node);
        node.start = (...args) => { window.audioProbe.starts++; start(...args); };
        node.stop = (...args) => { if (!args.length) { window.audioProbe.stops++; } stop(...args); };
        return node;
      }
    };
  });
}
