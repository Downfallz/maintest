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
 * The package a reference names, or null.
 *
 * Through the alias map, because `GameSchemaBuilder.Canonical` resolves a package's id and its prerequisites
 * the same way it resolves a spell. Cutting a new version of an opener writes `tier:brute -> tier:brute:v2`,
 * and a prerequisite that says `tier:brute` has to find v2 here or the sheet would draw it as unknown while
 * the builder reads it fine.
 */
export function tierNamed(reference, tiers, resolve = value => value) {
  const target = resolve(reference);
  return (tiers || []).find(tier => tier.id === target) || null;
}

/**
 * What the builder will refuse about this package, in the author's words.
 *
 * `tiers` is every package as the catalogue lists them, because three of the four rules are about how this one
 * sits against the others. A prerequisite naming a package that does not exist is deliberately not a warning:
 * an author mid-edit has one, and the builder says so with the file named.
 */
export function tierWarnings(draft, tiers, resolve = value => value) {
  const warnings = [];
  const level = Number(draft?.level);
  const prerequisites = (draft?.prerequisites || []).filter(Boolean);

  if (!(draft?.spells || []).filter(Boolean).length) {
    warnings.push('A package has to teach at least one spell: a pick that buys nothing is refused.');
  }

  // Through the alias map on both sides: a package that requires the alias pointing at itself requires
  // itself, and reading the two references literally would miss it.
  if (prerequisites.some(reference => resolve(reference) === resolve(draft?.id))) {
    warnings.push('A package cannot require itself.');
  }

  // Only the prerequisites that resolve: one being typed is not yet a level.
  const levels = prerequisites
    .map(reference => Number(tierNamed(reference, tiers, resolve)?.document?.level))
    .filter(Number.isFinite);

  if (Number.isFinite(level) && level > 1) {
    // Checked only once every prerequisite is known, so one mistake reads as one problem -- the same order
    // `GameResources.ValidateClimb` uses, and for the same reason.
    if (levels.length === prerequisites.length && !levels.includes(level - 1)) {
      warnings.push(`A package at level ${level} needs a prerequisite at level ${level - 1}: a family is climbed one level at a time.`);
    }

    if (levels.some(behind => behind >= level)) {
      warnings.push('A prerequisite has to sit above what it opens, so its level has to be lower than this one.');
    }
  }

  return warnings;
}

/**
 * The packages that teach a spell. The link that matters on a spell sheet: a pick buys a package, so a spell
 * no package teaches is one nobody can ever acquire.
 */
export function tiersTeaching(spellId, tiers, resolve = reference => reference) {
  return (tiers || []).filter(tier => (tier.document?.spells || []).some(reference => resolve(reference) === spellId));
}

/** The packages bought behind this one, which is what disabling or renaming it would strand. */
export function tiersBehind(tierId, tiers, resolve = value => value) {
  return (tiers || []).filter(tier => (tier.document?.prerequisites || []).some(reference => resolve(reference) === tierId));
}

/**
 * The creatures that already start with a spell this package teaches. A package may not sell what every
 * creature already has — the pick would buy nothing — and the builder's refusal names no creature, so the
 * sheet names them.
 */
export function startersOverlapping(draft, creatures, resolve = reference => reference) {
  const taught = new Set((draft?.spells || []).map(resolve));
  return (creatures || []).filter(creature =>
    (creature.document?.startingSpellIds || []).map(resolve).some(spell => taught.has(spell)));
}
