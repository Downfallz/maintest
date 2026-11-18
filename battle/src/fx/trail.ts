import type { Primitive } from './kit';

// Trail (the craft book, "Trail"): particles left behind a head that travels from the caster to a target. They
// stay where they were left, so they draw the path. The head carries the meaning; the trail is dimmer and smaller.
export const trail: Primitive = (kit, play) => {
  const { beat, at, from, rate } = play;
  const cap = kit.room(beat.count!);
  if (!cap || !from) return;
  const travel = beat.lasts! / rate, life = Math.min(220 / rate, travel / 2), size = Math.max(.2, at.radius / 110);
  const head = { x: from.x, y: from.y };
  const emitter = kit.emitter({ ...play, at: { ...at, x: 0, y: 0 } }, cap, {
    lifespan: life, frequency: life / cap, quantity: 1, speed: 0,
    scale: { start: size, end: 0 }, alpha: { start: .7, end: 0 },
  });
  emitter.startFollow(head);
  // The head travels for the whole beat and eases as the quill does (`SpellEffects.windup`), so a trail that
  // starts with the quill stays behind it. What it left fades for one life more, or until the window changes.
  kit.tween({ targets: head, x: at.x, y: at.y, duration: travel, ease: 'Cubic.In', onComplete: () => emitter.stop() });
  kit.after(travel + life, () => kit.release(emitter));
};
