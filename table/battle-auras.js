// What a creature's lasting conditions add to its figure on the field, beside the badges under it. The badges say
// every condition; the figure shows at most two, each in its own place so they never cover each other: a stun over
// the head, and one condition on the body, the one that matters most to its life. Energy, initiative and damage
// changes stay on the badges alone: a figure wearing everything it carries would read as noise.
const BODY = ['health-loss', 'health-gain', 'defense-loss', 'defense-gain'];

/** @param {readonly string[]} keys the icon keys the badges show (condition-icons.ts) */
export function conditionAuras(keys = []) {
  return { above: keys.includes('stun') ? 'stun' : null, body: BODY.find(key => keys.includes(key)) ?? null };
}

// `id:above+body` per creature that shows anything (`#battle[data-auras]` for tests and tools).
export function aurasShown(entries) {
  return entries.filter(([, aura]) => aura.above || aura.body)
    .map(([id, aura]) => `${id}:${[aura.above, aura.body].filter(Boolean).join('+')}`).join(' ');
}
