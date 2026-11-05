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
// holdResume keeps the context's resume pending until the test calls window.releaseAudio().
export async function probeAudio(page, { holdResume = false } = {}) {
  await page.addInitScript(hold => {
    window.audioProbe = { contexts: 0, starts: 0, stops: 0 };
    const Original = window.AudioContext;
    window.AudioContext = class extends Original {
      constructor(...args) { super(...args); window.audioProbe.contexts++; }
      resume() {
        if (!hold) return super.resume();
        return new Promise(done => { window.releaseAudio = () => super.resume().then(done); });
      }
      createOscillator() {
        const node = super.createOscillator(), start = node.start.bind(node), stop = node.stop.bind(node);
        node.start = (...args) => { window.audioProbe.starts++; start(...args); };
        // A stop scheduled at start is not forwarded: real audio time does not follow the page clock, so a short
        // tone could end on its own before the test acts, and a cut would be invisible.
        node.stop = (...args) => { if (args.length) return; window.audioProbe.stops++; stop(); };
        return node;
      }
    };
  }, holdResume);
}
