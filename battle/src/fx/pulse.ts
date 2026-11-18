import type { Primitive } from './kit';

// Pulse (the craft book, "Glow", the filter): a halo around the figure's own silhouette that swells and goes.
// It costs a framebuffer and a pass each frame while it lasts, so it is for a family's one hero effect, and the
// filter is removed when the beat ends.
const FULL = 8;

export const pulse: Primitive = (kit, play) => {
  const { beat, figure, color, rate } = play;
  if (!figure) return;
  figure.enableFilters();
  const filters = figure.filters?.internal;
  if (!filters) return;
  const halo = filters.addGlow(color, 0, 0, 1, false, 6, 10);
  // A filter is given no room around its object unless asked: without it the halo is cut at the picture's edge.
  halo.setPaddingOverride(null);
  const remove = kit.restore(() => { if (figure.active) filters.remove(halo, true); });
  kit.tween({ targets: halo, outerStrength: FULL * beat.strength!, duration: beat.lasts! / rate / 2, ease: 'Quad.Out', yoyo: true, onComplete: remove });
};
