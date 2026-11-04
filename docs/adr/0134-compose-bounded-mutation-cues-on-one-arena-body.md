# 0134. Compose bounded mutation cues on one arena body

Date: 2026-11-04
Status: Accepted for the opt-in ART-5 prototype

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

Keep this pure presentation register in the existing art adapter. Bound accents inside sprite geometry,
mirror and move them with the image, and leave hit targets and game values alone. Destroy them with their
figure and suppress geometry-specific overlays when its approved image is unavailable. Replay uses only
its own snapshot, and reduced motion retains the static marks. Accessible appearance text accompanies art.

## Consequences

Mixed builds remain deterministic across clients and replay but can change silhouette when a new family
wins the ranking. This is an explicit POC policy for review, not a permanent art-direction lock or a balance
rule. The source sprites are unchanged; full muscular/limb reshaping needs later reviewed texture variants.
The renderer owns at most 18 persistent graphics for six creatures and no extra assets or animation timers.
Tests cover catalogue alignment, policy boundaries and actual Phaser lifecycle/transform behavior.
