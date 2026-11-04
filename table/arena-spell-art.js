// Presentation vocabulary only. Stable content IDs survive catalogue translations and renames.
// A profile supplies shape and delivery, never damage, target count, timing of rules or duration of buffs.
const groups = [
  ['physical', 'impact', 'melee', ['heavy_strike', 'pummel', 'enraged_charge', 'psycho_rush', 'shield_bash', 'crushing_stomp', 'protective_slam', 'chain_slash']],
  ['physical', 'slash', 'melee', ['reckless_swing']],
  ['physical', 'wave', 'cast', ['tornado']],
  ['physical', 'plates', 'cast', ['guard', 'full_plate']],
  ['physical', 'roar', 'cast', ['thundering_seal']],
  ['physical', 'vital', 'cast', ['restorative_gush']],
  ['physical', 'bite', 'melee', ['extort']],
  ['biological', 'slash', 'melee', ['poison_slash', 'momentum', 'ambush', 'night_raid', 'mortal_wound', 'hateful_sacrifice']],
  ['biological', 'bite', 'melee', ['parasite_jab', 'latch', 'soul_devourer']],
  ['biological', 'quill', 'projectile', ['throwing_star', 'tranquilizer_dart']],
  ['biological', 'spores', 'cast', ['noxious_cure', 'adrenaline_tonic', 'infectious_blast']],
  ['elemental', 'arc', 'cast', ['lightning_bolt']],
  ['elemental', 'fire', 'cast', ['meteor', 'engulfing_flames']],
  ['elemental', 'ice', 'cast', ['frostbite', 'ice_spear']],
  ['necromantic', 'spirits', 'cast', ['summon_minions', 'crazed_specter', 'revenant_guards']],
  ['necromantic', 'bones', 'cast', ['bone_ward']],
  ['astral', 'vital', 'cast', ['rejuvenate', 'healing_screech', 'soothing_chant', 'restorative_burst']],
  ['astral', 'void', 'cast', ['toxic_waves']],
  ['neutral', 'focus', 'cast', ['wait']],
];

export const SPELL_ART = Object.freeze(Object.fromEntries(groups.flatMap(([family, motif, delivery, names]) =>
  names.map(name => [`spell:${name}:v1`, Object.freeze({ family, motif, delivery })]))));

const fallback = Object.freeze({ family: 'neutral', motif: 'focus', delivery: 'cast' });
const fizzle = Object.freeze({ family: 'neutral', motif: 'fizzle', delivery: 'cast' });
export function spellVisual(action) {
  if (action.status === 'Fizzled') return fizzle;
  const id = action.action?.spell;
  return Object.hasOwn(SPELL_ART, id) ? SPELL_ART[id] : fallback;
}

// ADR 0131: only confirmed caster-side outcomes select the wound motif, never authored costs.
export function casterWound(effect, actorId) {
  return effect.target.id === actorId && (effect.tone === 'harm'
    || (effect.onCaster === true && effect.kind === 'ConditionOutcome' && effect.effect?.kind === 'Bleed'));
}
