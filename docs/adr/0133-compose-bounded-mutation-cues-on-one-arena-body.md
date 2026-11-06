# 0133. Compose bounded mutation cues on one arena body

Date: 2026-11-04
Status: Accepted for the opt-in arena, as a prototype policy

## Context

ART-3 deliberately falls back to the common ancestor for mixed families. ART-4 supplies confirmed spell
VFX but leaves stage 2/3 mutations invisible. Arbitrarily layering every acquired anatomical trait would
obscure targeting and combine incompatible limb counts. Acquired tiers are a set, not a purchase history.

## Decision

Use the displayed snapshot's acquired tier IDs to select one existing body by highest stage, unique
investment, then the explicit prototype tie order Brute, Predator, Warped. Require a recognized root.
Never use labels, roster position, purchase order or shared teammate passives to infer a mutation.

Select up to three static vector accents: one dominant branch, one secondary branch, one owned capstone.
Higher stages replace lower stages of the same motif; stable tier ID breaks remaining branch ties.
Prefer another family for the secondary branch, falling back to a different branch of the dominant family.
The capstone prefers the dominant family. Document the full policy and vocabulary in
[ART-5](../art/mutation-cues.md); omitted visual traits never remove real acquired mutations.

Author which body each package belongs to and what it draws beside the content, in
`data/presentation/mutation-art.json`, keyed by the package's versioned id: the data builder checks it against
the authored packages, the vocabularies and the root each package descends from, and publishes it next to the
schema, outside the content hash, the way the package tags and the spell art are (ADR 0131); the table host
serves it on each catalogue package as `visual`, beside its `level`. The page names no package: it reads the
family, the stage and the motif off the catalogue, and the stage-3 sweep now refuses a `tier:` id in a page
file as it refuses a spell's. Bound accents inside sprite geometry, mirror and move them with the image, and
leave hit targets and game values alone. Destroy them with their figure and suppress geometry-specific
overlays when its approved image is unavailable. Replay uses only its own snapshot, and reduced motion
retains the static marks. The inspection says the appearance in the catalogue's package names.

## Consequences

- Good: a stage 2 or 3 purchase shows on the creature, a mixed build keeps one deterministic body on
  every client and in replay, and a content pass that adds a package adds its picture beside the content
  and changes no page.
- Bad: the tie order and the three-slot budget are a prototype policy, not an art-direction lock or a
  balance rule; a mixed build can change silhouette when a new family wins the ranking, and full muscle or
  limb reshaping still needs reviewed texture variants.
- Neutral: the renderer owns at most eighteen persistent graphics for six creatures and no extra asset or
  animation timer. Omitted visual traits never remove real acquired mutations; the Mutation Tree stays the
  authority on a build.

## Alternatives considered

- Layer every acquired anatomical trait: it obscures targeting and combines incompatible limb counts.
- Pick the first bought, the latest bought or the family with the most packages: acquired tiers are a set,
  not a purchase history, so only stage and count are stable across clients and replay.
- Keep the package-to-motif register in a page module: it is content the page knows by name, which the
  stage-3 sweep exists to prevent, and the next package would need a page change; beside the content, the
  builder checks it against the packages and their roots.
- Say the appearance in the renderer's motif names: they are not the player's words; the catalogue's
  package names are.

## Follow-up

- `MutationArt` (Infrastructure) publishes and reads the art the way `PackageIdentities` and `SpellArt` do;
  the data builder and the studio's build publish it, `AddGameResources` registers it, and the table composer
  puts it on the packages. `data/README.md` names the file.
- `TablePageContentTests` sweeps `tier:` ids out of page files with the other content ids.
- Pure tests cover the builder's checks, the package's visual on the catalogue, completeness against
  `data/Tiers`, the precedence policy and its boundaries; browser tests run the production scene for the
  layers' lifecycle, transform and fallbacks.
- ART-6 (choreography, audio, polish) and the reviewed texture variants build on this.
