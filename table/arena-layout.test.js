import test from 'node:test';
import assert from 'node:assert/strict';
import { isPortrait, placements } from './arena-layout.js';

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
