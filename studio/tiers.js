// What the page knows about a package, with no DOM in it (ADR 0024).
//
// A package is the unit of evolution: a pick buys one, and every spell in it arrives at once with one
// initiative bonus (ADR 0056). It is authored rather than derived from the talent tree (ADR 0057), so these
// are the readings an author needs while typing, and the cross-file links the sheets draw.
//
// None of this is a validation. `ContentStore` checks a document against the same DTOs the data builder uses,
// and the builder checks what spans files; a browser can run neither, so everything here only saves a round
// trip and CI stays the authority (ADR 0015). Each warning names a rule `GameResources` actually enforces, so
// a reading that disagrees with the builder is a bug here, not a second opinion.

/**
 * A list field as a list. `ContentStore.ReadAll` keeps the raw document of a file that does not parse into its
 * DTO, so the studio can list it with what is wrong, which means every field here may be any JSON at all: a
 * `spells` that is a number reaches this module as a number. Iterating it would throw, and these run from the
 * spell sheets as well as the package ones -- so one malformed package file would stop unrelated sheets from
 * opening at all, instead of the catalogue showing its problem.
 */
function asArray(value) {
  return Array.isArray(value) ? value : [];
}

/**
 * The package a reference names, or null.
 *
 * Through the alias map, because `GameSchemaBuilder.Canonical` resolves a package's id and its prerequisites
 * the same way it resolves a spell. Cutting a new version of an opener writes `tier:brute -> tier:brute:v2`,
 * and a prerequisite that says `tier:brute` has to find v2 here or the sheet would draw it as unknown while
 * the builder reads it fine.
 */
export function tierNamed(reference, tiers, resolve = value => value) {
  const target = resolve(reference);
  return asArray(tiers).find(tier => tier.id === target) || null;
}

/**
 * What a package's passive does, a line per property, in the words and order the table's card prints them
 * (`EffectLine.Of(Passive)`, ADR 0100). Empty for a package that gives nothing through it, and for a `passive`
 * that is not an object: a malformed file reaches this module as whatever JSON it holds.
 */
export function passiveLines(passive) {
  if (!passive || typeof passive !== 'object' || Array.isArray(passive)) return [];
  const lines = [];
  if (passive.stunImmunity === true) lines.push('Immune to stun');
  if (Number(passive.upkeepEnergy) > 0) lines.push(`Energy +${Number(passive.upkeepEnergy)} at every upkeep`);
  if (Number(passive.damageBonus) > 0) lines.push(`Damage +${Number(passive.damageBonus)} on every hit`);
  return lines;
}

/**
 * Every package a draft names on its way up: the all-of list, then the any-of one (ADR 0100). The order
 * `ValidateClimb` reads them in, and the one a sheet lists them in.
 */
export function requiredTiers(draft) {
  return [...asArray(draft?.prerequisites), ...asArray(draft?.anyOf)].filter(Boolean);
}

/**
 * What the builder will refuse about this package, in the author's words.
 *
 * `tiers` is every package as the catalogue lists them, because most of the rules are about how this one sits
 * against the others. A prerequisite naming a package that does not exist is deliberately not a warning: an
 * author mid-edit has one, and the builder says so with the file named.
 */
export function tierWarnings(draft, tiers, resolve = value => value) {
  const warnings = [];
  const level = Number(draft?.level);
  const prerequisites = asArray(draft?.prerequisites).filter(Boolean);
  const anyOf = asArray(draft?.anyOf).filter(Boolean);
  const required = [...prerequisites, ...anyOf];

  // A capstone teaches nothing and is bought for its passive (ADR 0100): only a package that gives neither is
  // a pick that buys nothing. `Tier` refuses it in its constructor.
  if (!asArray(draft?.spells).filter(Boolean).length && !passiveLines(draft?.passive).length) {
    warnings.push('A package has to teach at least one spell or give a passive: a pick that buys nothing is refused.');
  }

  // `Passive.Of` refuses a negative amount: a package that took energy or damage away would be a curse.
  for (const key of ['upkeepEnergy', 'damageBonus']) {
    if (Number(draft?.passive?.[key]) < 0) {
      warnings.push(`A passive's ${key} cannot be negative: a package that took it away would be a curse, not a purchase.`);
    }
  }

  // Through the alias map on both sides: a package that requires the alias pointing at itself requires
  // itself, and reading the two references literally would miss it.
  if (prerequisites.some(reference => resolve(reference) === resolve(draft?.id))) {
    warnings.push('A package cannot require itself.');
  }

  if (anyOf.some(reference => resolve(reference) === resolve(draft?.id))) {
    warnings.push('A package cannot be opened by itself.');
  }

  // Only the prerequisites that resolve: one being typed is not yet a level.
  const levelOf = reference => Number(tierNamed(reference, tiers, resolve)?.document?.level);
  const levels = required.map(levelOf).filter(Number.isFinite);

  if (Number.isFinite(level)) {
    // Checked only once every prerequisite is known, so one mistake reads as one problem -- the same order
    // `GameResources.ValidateClimb` uses, and for the same reason. Only this half is about climbing, so only
    // this half asks for a level above one. Both lists count: a capstone's only steps are its any-of ones.
    if (level > 1 && levels.length === required.length && !levels.includes(level - 1)) {
      warnings.push(`A package at level ${level} needs a prerequisite at level ${level - 1}: a family is climbed one level at a time.`);
    }

    // Every level, including the first. `ValidateClimb` does not gate this one either, and a level-1 package
    // with a prerequisite is the case that makes the difference: nothing sits above level 1, so any
    // prerequisite it names is one the builder refuses.
    if (levels.some(behind => behind >= level)) {
      warnings.push('A prerequisite has to sit above what it opens, so its level has to be lower than this one.');
    }

    // Any one of an any-of list may be the step a creature climbed through, so `ValidateClimb` holds every one
    // of them to a level below -- not just one, the way the all-of list is held.
    for (const reference of anyOf) {
      const behind = levelOf(reference);
      if (Number.isFinite(behind) && behind !== level - 1) {
        warnings.push(`${reference} at level ${behind} can open this level-${level} package: every package of an any-of list is a step a creature climbs through, so each sits at level ${level - 1}.`);
      }
    }
  }

  return warnings;
}

/**
 * The packages that teach a spell. The link that matters on a spell sheet: a pick buys a package, so a spell
 * no package teaches is one nobody can ever acquire.
 */
export function tiersTeaching(spellId, tiers, resolve = reference => reference) {
  return asArray(tiers).filter(tier => asArray(tier.document?.spells).some(reference => resolve(reference) === spellId));
}

/**
 * The packages bought behind this one, which is what disabling or renaming it would strand. An any-of entry
 * counts: removing one only narrows what opens a capstone, but the reference is still to this package.
 */
export function tiersBehind(tierId, tiers, resolve = value => value) {
  return asArray(tiers).filter(tier => requiredTiers(tier.document).some(reference => resolve(reference) === tierId));
}

/**
 * The creatures that already start with a spell this package teaches. A package may not sell what every
 * creature already has — the pick would buy nothing — and the builder's refusal names no creature, so the
 * sheet names them.
 */
export function startersOverlapping(draft, creatures, resolve = reference => reference) {
  const taught = new Set(asArray(draft?.spells).map(resolve));
  return asArray(creatures).filter(creature =>
    asArray(creature.document?.startingSpellIds).map(resolve).some(spell => taught.has(spell)));
}
