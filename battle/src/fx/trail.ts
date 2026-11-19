import { placed, sized, type Primitive } from './kit';

// Trail (the craft book, "Trail"): particles left behind a head that travels from the caster to a target. They
// stay where they were left, so they draw the path. The head carries the meaning; the trail is dimmer and smaller.
// At the feet of both figures the path runs along the ground: a crack from the caster to its target. A crack
// opens at an even pace and stays open for the whole beat: a head that eased in crossed the field in its last
// frame, and pieces that lived 220 ms were gone before a film could show a line (#918).
export const trail: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level);
  const cap = kit.room(beat.count!);
  if (!cap || !play.from) return;
  const from = placed(play.from, beat.level);
  const crack = beat.level === 'feet', travel = beat.lasts! / rate;
  const life = crack ? travel : Math.min(220 / rate, travel / 2), size = sized(beat, at.radius / 110, .2);
  const head = { x: from.x, y: from.y };
  const emitter = kit.emitter({ ...play, at: { ...at, x: 0, y: 0 } }, cap, {
    lifespan: life, frequency: life / cap, quantity: 1, speed: 0,
    scale: { start: size, end: 0 }, alpha: { start: .7, end: 0 },
  });
  emitter.startFollow(head);
  // The head travels for the whole beat and eases as the quill does (`SpellEffects.windup`), so a trail that
  // starts with the quill stays behind it. What it left fades for one life more, or until the window changes.
  kit.tween({ targets: head, x: at.x, y: at.y, duration: travel, ease: crack ? 'Linear' : 'Cubic.In', onComplete: () => emitter.stop() });
  kit.after(travel + life, () => kit.release(emitter));
};
