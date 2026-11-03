import type { Placement } from './types';

// Positions describe presentation space, never game movement or range.
export function placements(width: number, height: number): Placement[] {
  const portrait = width < 620 && height > width * .65;
  const floor = Math.max(60, height - 44);
  if (portrait) {
    const size = Math.min(width * .30, floor * .40);
    const row = (y: number) => [.17, .5, .83].map(x => ({ x: width * x, y, size, hitWidth: Math.max(44, width * .29) }));
    return [...row(floor * .95), ...row(floor * .45)];
  }
  const size = Math.min(width * .205, floor * .58);
  const coords = [[.36, .90, 1], [.12, .78, .84], [.23, .47, .76], [.64, .90, 1], [.88, .78, .84], [.77, .47, .76]];
  return coords.map(([x, y, scale]) => ({ x: width * x, y: floor * y, size: size * scale, hitWidth: Math.max(44, size * scale * .80) }));
}
