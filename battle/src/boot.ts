import Phaser from 'phaser';

// Limit supersampling on large displays. A phone-sized battlefield can use its native 2×–3× density.
function renderSize(mount: HTMLElement) {
  const width = Math.max(1, mount.clientWidth), height = Math.max(1, mount.clientHeight);
  const density = Math.max(1, Math.min(window.devicePixelRatio || 1, 3, Math.sqrt(3_000_000 / (width * height))));
  return { width: Math.round(width * density), height: Math.round(height * density) };
}

// Starting the renderer, once for both pages: the game on its mount, sized by the stage itself rather than the
// window (mobile browser chrome and rotation need not trigger the window's resize), and the two ways it can
// fail: the context lost under it, or a scene that never reaches ready.
export function bootScene(scene: Phaser.Scene, mount: HTMLElement, { onContextLost, onNeverReady, isReady }:
  { onContextLost: () => void; onNeverReady: () => void; isReady: () => boolean }) {
  const game = new Phaser.Game({ type: Phaser.AUTO, parent: mount, backgroundColor: '#17272b',
    scale: { mode: Phaser.Scale.NONE, ...renderSize(mount) },
    render: { antialias: true, roundPixels: false }, fps: { target: 60, forceSetTimeOut: false },
    audio: { noAudio: true }, scene, banner: false });
  const resize = () => {
    if (!mount.clientWidth || !mount.clientHeight) return;
    const { width, height } = renderSize(mount);
    if (game.scale.width !== width || game.scale.height !== height) game.scale.resize(width, height);
  };
  const observer = new ResizeObserver(resize);
  // Moving to another display or changing browser zoom can change density at the same CSS size.
  let densityQuery: MediaQueryList;
  function observeDensity() {
    densityQuery = matchMedia(`(resolution: ${window.devicePixelRatio}dppx)`);
    densityQuery.addEventListener('change', observeDensity, { once: true });
    resize();
  }
  game.events.once(Phaser.Core.Events.DESTROY, () => {
    observer.disconnect();
    densityQuery?.removeEventListener('change', observeDensity);
  });
  game.events.once(Phaser.Core.Events.READY, () => {
    observer.observe(mount);
    observeDensity();
    game.canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); onContextLost(); });
  });
  setTimeout(() => { if (!isReady()) onNeverReady(); }, 15000);
  return game;
}
