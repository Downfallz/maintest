import type { Primitive } from './kit';

// Hit flash (the craft book, "Hit flash"): the figure filled in one flat colour for a frame or two. A tint is a
// vertex attribute: it draws nothing more. A staging holds one at most (`SpellArt`), for the flash limits.
export const flash: Primitive = (kit, play) => {
  const { beat, figure, color, rate } = play;
  if (!figure) return;
  figure.setTint(color).setTintMode(kit.engine.TintModes.FILL);
  // A skip during the flash must not leave a figure filled: the tint is cleared at the end or on clear, once.
  const clear = kit.restore(() => { if (figure.active) figure.clearTint(); });
  kit.after(beat.lasts! / rate, clear);
};
