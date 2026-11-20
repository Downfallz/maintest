// The optional art name comes from the spell visual in the served catalogue.
export function spellPortrait(name?: string | null) {
  if (!name || !/^[a-z0-9-]+$/.test(name)) return null;
  const image = document.createElement('img');
  image.className = 'spell-portrait';
  image.alt = '';
  image.loading = 'lazy';
  image.decoding = 'async';
  image.addEventListener('error', () => image.remove(), { once: true });
  image.src = `battle-art/spells/${name}.webp`;
  return image;
}
