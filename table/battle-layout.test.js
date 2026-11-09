import test from 'node:test';
import assert from 'node:assert/strict';
import { isPortrait, placements, strikeApproach, dieSpot } from './battle-layout.js';

const sizes = { phone: [390, 760], phoneLandscape: [844, 300], narrow: [320, 480], desktop: [1440, 850], short: [720, 420] };

test('a phone held upright is portrait; a phone turned, a tablet and a desktop are not', () => {
  assert.equal(isPortrait(...sizes.phone), true);
  assert.equal(isPortrait(...sizes.narrow), true);
  assert.equal(isPortrait(...sizes.phoneLandscape), false);
  assert.equal(isPortrait(...sizes.desktop), false);
  assert.equal(isPortrait(619, 400), false, 'a wide-but-narrow stage is a landscape one');
});

test('every stage places six figures inside it with a hit target a finger can land on', () => {
  for (const [name, [width, height]] of Object.entries(sizes)) {
    const placed = placements(width, height);
    assert.equal(placed.length, 6, name);
    for (const one of placed) {
      assert.ok(one.x - one.size / 2 >= 0 && one.x + one.size / 2 <= width, `${name}: figure inside the stage horizontally`);
      assert.ok(one.y - one.size >= 0 && one.y <= height, `${name}: figure inside the stage vertically`);
      assert.ok(one.hitWidth >= 44, `${name}: hit target at least 44px wide`);
      assert.ok(one.size > 0, `${name}: a visible figure`);
    }
  }
});

test('portrait stacks the allies on a lower row and the enemies on an upper one', () => {
  const placed = placements(...sizes.phone);
  const allies = placed.slice(0, 3);
  const enemies = placed.slice(3);
  assert.equal(new Set(allies.map(one => one.y)).size, 1);
  assert.equal(new Set(enemies.map(one => one.y)).size, 1);
  assert.ok(allies[0].y > enemies[0].y);
  assert.deepEqual(allies.map(one => one.x), enemies.map(one => one.x));
});

test('landscape faces the allies on the left against the enemies on the right', () => {
  const placed = placements(...sizes.desktop);
  const allies = placed.slice(0, 3);
  const enemies = placed.slice(3);
  assert.ok(Math.max(...allies.map(one => one.x)) < 720);
  assert.ok(Math.min(...enemies.map(one => one.x)) > 720);
  assert.deepEqual(allies.map(one => one.size), enemies.map(one => one.size), 'the formations mirror each other');
});

test('the same stage always gives the same placements, and the smallest stage still has room', () => {
  assert.deepEqual(placements(1024, 600), placements(1024, 600));
  const tiny = placements(1, 1);
  assert.equal(tiny.length, 6);
  assert.ok(tiny.every(one => one.hitWidth === 44));
});

test('a smaller team keeps its side of the field in both orientations', () => {
  for (const count of [1, 2]) {
    for (const [width, height] of [sizes.phone, sizes.desktop]) {
      const placed = placements(width, height, count, count);
      assert.equal(placed.length, count * 2, `${count}v${count} at ${width}x${height}`);
      assert.ok(placed.every(one => one.hitWidth >= 44 && one.x > 0 && one.x < width));
      if (isPortrait(width, height)) assert.ok(placed[0].y > placed[count].y, 'allies on the lower row');
      else assert.ok(placed[0].x < width / 2 && placed[count].x > width / 2, 'allies left, enemies right');
    }
  }
});

test('attacks approach horizontally on desktop and vertically on a phone without crossing the target', () => {
  const actor = { x: 50, y: 400, size: 96 };
  assert.deepEqual(strikeApproach(actor, { ...actor, y: 180 }), { x: 50, y: 232.8, dx: 0, dy: -1 });
  assert.deepEqual(strikeApproach({ ...actor, y: 180 }, actor), { x: 50, y: 347.2, dx: 0, dy: 1 });
  const left = strikeApproach({ x: 800, y: 400, size: 200 }, { x: 400, y: 400, size: 200 });
  assert.equal(left.x, 510); assert.equal(left.dx, -1); assert.equal(left.dy, 0);
});
test('an overlapping or very close target never sends the attacker backwards past its origin', () => {
  const one = { x: 10, y: 20, size: 200 };
  assert.deepEqual(strikeApproach(one, one), { x: 10, y: 20, dx: 0, dy: 0 });
  const close = strikeApproach(one, { ...one, x: 15 });
  assert.ok(close.x > one.x && close.x < 15);
});

test('portrait rows never overlap their hit targets, even on the short stage a mutation sheet leaves', () => {
  // The hit target spans the figure and the caption under it, as placeHitTargets lays it out.
  const span = one => { const top = Math.max(0, one.y - one.size); return [top, top + Math.max(44, one.size + (one.caption ?? 42))]; };
  for (const [width, height] of [sizes.phone, sizes.narrow, [320, 255], [390, 260], [320, 208], [360, 300]]) {
    const placed = placements(width, height, 3, 3, true);
    const [allyTop] = span(placed[0]), [, enemyBottom] = span(placed[3]);
    assert.ok(enemyBottom <= allyTop + 1e-6, `${width}x${height}: enemy targets end at ${enemyBottom}, allies start at ${allyTop}`);
    assert.ok(placed.every(one => one.size > 0 && one.hitWidth >= 44), `${width}x${height}: visible figures, reachable targets`);
  }
});

test('every figure stands above its caption by as much as the stage has room for, its caption where it always hung', () => {
  for (const [name, [width, height]] of Object.entries(sizes)) {
    for (const one of placements(width, height)) {
      assert.ok(one.lift >= 0 && one.lift <= 34, `${name}: a bounded lift`);
      assert.equal(one.caption, 42 + one.lift, `${name}: the caption keeps its place under the original feet`);
      assert.ok(one.y - one.size >= -1e-6, `${name}: a lifted figure stays inside the stage`);
    }
  }
  // A phone held upright has the room: every figure rises clear of its name.
  assert.ok(placements(...sizes.phone).every(one => one.lift > 20));
});

test('a die and the words under it stay inside the field, whichever edge its figure stands at', () => {
  const field = 400, home = { x: 340, y: 300, size: 120 };
  const spot = dieSpot(home, 110, field);
  assert.ok(spot.x + 110 / 2 <= field, 'the words end inside the right edge');
  assert.ok(spot.x + spot.size / 2 <= field);
  const left = dieSpot({ x: 10, y: 300, size: 120 }, 110, field);
  assert.ok(left.x - 110 / 2 >= 0, 'and start inside the left one');
  // A figure in the open keeps its die beside its head.
  const middle = dieSpot({ x: 200, y: 300, size: 120 }, 40, field);
  assert.equal(middle.x, 200 + 120 * .42);
  assert.ok(dieSpot({ x: 200, y: 20, size: 120 }, 0, field).y > 0, 'never off the top');
});
