import { placed, type Primitive } from './kit';

// Slow push on a capstone (the craft book): the view moves in on the caster while a great spell gathers, and lets
// go at the impact. Like the shake, it is the view's: the kit hands the scene the push, the scene lays it over the
// camera beside the tremor, lets it go when the blow lands, and its cancel path puts the camera back. It draws
// nothing. Its length is on the action's clock, which the scene's view already runs at the playback rate.
export const push: Primitive = (kit, play) => {
  const { beat } = play, at = placed(play.at, beat.level);
  kit.view?.push?.(beat.strength!, beat.lasts!, { x: at.x, y: at.y });
};
