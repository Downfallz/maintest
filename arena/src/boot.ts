import Phaser from 'phaser';

// Starting the renderer, once for both pages: the game on its mount, sized by the stage itself rather than the
// window (mobile browser chrome and rotation need not trigger the window's resize), and the two ways it can
// fail: the context lost under it, or a scene that never reaches ready.
export function bootScene(scene: Phaser.Scene, mount: HTMLElement, { onContextLost, onNeverReady, isReady }:
  { onContextLost: () => void; onNeverReady: () => void; isReady: () => boolean }) {
  const game = new Phaser.Game({ type: Phaser.AUTO, parent: mount, backgroundColor: '#17272b',
    scale: { mode: Phaser.Scale.RESIZE, width: '100%', height: '100%' },
    render: { antialias: true, roundPixels: false }, fps: { target: 60, forceSetTimeOut: false },
    audio: { noAudio: true }, scene, banner: false });
  const resize = new ResizeObserver(() => {
    const { width, height } = mount.getBoundingClientRect();
    if (width > 0 && height > 0) {
      // RESIZE refreshes from its cached parent bounds. A shell change (replay opening) can resize only
      // the mount, without a window event to refresh that cache. Read it before resizing the canvas.
      game.scale.getParentBounds();
      game.scale.resize(width, height);
    }
  });
  resize.observe(mount);
  game.events.once(Phaser.Core.Events.DESTROY, () => resize.disconnect());
  game.events.once(Phaser.Core.Events.READY, () => {
    game.canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); onContextLost(); });
  });
  setTimeout(() => { if (!isReady()) onNeverReady(); }, 15000);
  return game;
}
