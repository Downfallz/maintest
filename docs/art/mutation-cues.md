# ART-5 — Mutation cues and multiclass prototype

Status: implemented visual POC, built on ART-4 (#441). The precedence policy and fine overlay treatment
are reviewable prototype choices, not new gameplay rules or final commissioned anatomy.

## Selection policy

`creatureAppearance` in `table/arena-art.js` reads only the displayed creature's acquired tier IDs.
The register covers the 24 current tiers; tests compare it with authored content. Unknown IDs are ignored,
duplicates count once, and a recognized root is required before a family's later tiers can affect art.
Legacy frames without acquired tiers retain the base Creature. No team passive, name, spell or purchase
order infers a mutation. The engine stores acquired tiers as a set, so array order cannot mean chronology.

Rank families by highest acquired stage, then number of unique recognized acquired tiers, then the fixed
POC tie order **Brute → Predator → Warped**. A capstone counts as stage 4. This chooses one existing body
and its leg count. A newly authoritative purchase can change the winner; a redraw or reordered poll cannot.
This supersedes ART-3's neutral mixed-family fallback and does not imply a gameplay advantage for a family.

| Slot | Selection | Limit |
| --- | --- | --- |
| Body | Highest stage 2/3 cue in the winning family | One |
| Accent | Highest stage 2/3 cue from another family; otherwise a different branch in the winner | One small dorsal cue |
| Capstone | A capstone owned by this creature, preferring the winning family | One |

Within a slot, stage wins, then stable tier ID alphabetical order. A stage 3 cue replaces its own stage 2
cue. Unselected branches remain part of the real build; the Mutation Tree owns its full account. Root-only
mixed builds choose a body but fabricate no branch cue. Three graphics per creature is the hard budget.

## Vocabulary

| Branch | Stage 2 | Stage 3 |
| --- | --- | --- |
| Frenzied → Ravager | Muted ember fissures | Larger, more numerous fractures |
| Ironhide → Colossus | Organic plate ridges | Denser layered carapace ridges |
| Oppressor → Tyrant | Frontal horns | Five-point bone crown |
| Deathmarked → Deathstalker | Talon accent | Enlarged talon set |
| Parasite → Soulreaver | Feeding fangs | Stronger jaw/fang accent |
| Blighted → Blightweaver | Three translucent glands and channels | Five larger reservoirs |
| Stormborn → Cataclysm | Alternating cold/heat shards | Larger shard cluster |
| Necrotic → Revenant | Two ghost faces and spectral trails | Three larger spirits |
| Ethereal → Transcendent | Doubled astral membrane | Larger membrane with crossing halo |

Titan adds compact bone reinforcement, Apex a precision/fang accent, and Archmage a sparse energy circuit.
The Archmage mark is local to its owner even though its gameplay passive is shared. Necromancy uses spirits,
never poison sacs. Gold targeting rings remain brighter and separate from these muted static accents.

These vectors establish readable cues, not new full silhouettes, limbs or skeletal rigs. Actual muscle,
limb and carapace reshaping still needs reviewed texture variants; this POC does not claim to deliver those.
No reference image is regenerated, recolored or edited, and no new image is downloaded.

## Renderer and lifecycle

`arena/src/mutation-cues.ts` owns at most three graphics per figure, drawn inside the existing silhouette
bounds. They follow the image's position, scale, angle, alpha and facing, including lunge/recoil/idle motion.
Stage 3 changes both normalized size and geometry. These marks do not animate independently and remain
visible under reduced motion. Defeat dims them with their body. Target rectangles, anchoring and game
values are unchanged. Appearance text is available to screen readers and in the inspection dialog.

A changed mutation signature rebuilds the figure and destroys its old marks. Historical before/after
frames carry their own selection; switching seats or clearing the roster removes all marks. A missing
requested sprite uses ART-3's existing fallback and suppresses geometry-specific marks, while the text
continues to explain the requested appearance. The actor's own graphics follow a resize or Skip reset.
Spell VFX have their separate short-lived owner and do not erase these persistent cosmetics.

## Acceptance

- Pure tests: register completeness, stages, precedence ties, order/duplicate invariance, mixed builds,
  three-slot budget, upgrade replacement, shared-passive ownership, replay, unknown/malformed data.
- Production Phaser tests: all nine branches at both stages, mirrored enemies, exact graphics budget,
  movement, resize, reduced motion, defeat, old frames, missing texture and empty-roster cleanup.
- Live tests: authoritative polling changes a mixed build, inspection explains appearance, and six advanced
  builds fit desktop, narrow phones and landscape over the eclipse arena.
- Screenshots compare stages 2/3 side by side and six mixed/capstone builds in the actual live shell.

Fine colors and slot anchors remain POC choices to review. The source art's smallest details naturally
lose clarity on 320px phones; full click targets and textual inspection carry the interaction independently.
