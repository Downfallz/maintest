// Illustrative design data only. These are authored frames, not simulated game rules.
// Never import this module from the live table or derive live outcomes from its values.
const creature = (id, name, form, health, energy, totalDefense, conditions = []) => ({
  id, name, form, health, maxHealth: 24, energy, totalDefense, conditions, isAlive: true,
});

export function battleFixture() {
  return {
    roundNumber: 5, phase: 'Combat', subPhase: 'Activation', activationCursor: 0, slot: 'Player1',
    allies: [
      creature('a1', 'Brute', 'brute', 19, 4, 2),
      creature('a2', 'Stalker', 'predator', 13, 3, 0, ['Marked · 1 round']),
      creature('a3', 'Occultist', 'warped', 21, 5, 1),
    ],
    enemies: [
      creature('b1', 'Ironhide', 'brute', 18, 3, 3),
      creature('b2', 'Predator', 'predator', 11, 4, 0),
      creature('b3', 'Hollow', 'warped', 7, 2, 1, ['Marked · 2 rounds']),
    ],
    timeline: [
      { creature: 'a1', owner: 'Player1', speed: 'Quick' },
      { creature: 'b2', owner: 'Player2', speed: 'Quick' },
      { creature: 'a2', owner: 'Player1', speed: 'Quick' },
      { creature: 'b1', owner: 'Player2', speed: 'Standard' },
      { creature: 'a3', owner: 'Player1', speed: 'Standard' },
      { creature: 'b3', owner: 'Player2', speed: 'Standard' },
    ],
  };
}

export const sampleAction = Object.freeze({
  actor: 'a1', label: 'Strike', targets: Object.freeze(['b1', 'b2', 'b3']),
});

// Each target has a fixed, deliberately illustrative outcome. No cost or damage formula lives here.
export function sampleFrame(target) {
  const outcomes = { b1: { health: 14 }, b2: { health: 5 }, b3: { health: 0, isAlive: false } };
  if (!Object.hasOwn(outcomes, target)) return null;
  const before = battleFixture();
  const after = battleFixture();
  after.allies[0].energy = 2;
  Object.assign(after.enemies.find(one => one.id === target), outcomes[target]);
  return { before, after };
}
