// The optional art name comes from the spell visual in the served catalogue. A picture that fails to load keeps
// its place, hidden, so the card's text does not move after it is drawn.
export function spellPortrait(name?: string | null) {
  if (!name || !/^[a-z0-9-]+$/.test(name)) return null;
  const image = document.createElement('img');
  image.className = 'spell-portrait';
  image.alt = '';
  image.loading = 'lazy';
  image.decoding = 'async';
  image.addEventListener('error', () => image.classList.add('missing'), { once: true });
  image.src = `battle-art/spells/${name}.webp`;
  return image;
}
