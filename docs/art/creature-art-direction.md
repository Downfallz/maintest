# Downfall Arena — Creature art direction and spell presentation

Status: **design handoff, not implementation**. Reviewed with the game owner on 2026-11-03/04.
This document records explicit decisions separately from visual proposals and outstanding questions.
No gameplay changes are authorized by it. The live source of mechanics remains `data/Tiers/` and `data/Spells/`.
The canonical spell display-name register is [`docs/domain/package-renaming-plan.md`](../domain/package-renaming-plan.md), including the later art-review approvals; this document owns their visual presentation only.

## Executive decisions

1. Every creature begins as the **same unmutated base Creature** (art shorthand: **Stage 0**, not a Tier level). A mage forces mutations in combat; the three Stage 1 families are divergent paths, not three unrelated species.
2. **Base Creature (Stage 0 shorthand)**: selected *lean hooked-mask quadruped* concept (previously concept-sheet option #4) — lean long-bodied quadruped, elongated hooked bone-mask skull, dorsal ridges, angular legs, slate-gray flesh and restrained amber corruptions.
3. **Brute**: selected *low armored quadruped with enormous forepaws* concept (previously non-humanoid concept #1) — low, massive, organically armored quadruped with immense front paws. No human posture, equipment or manufactured plate.
4. **Predator**: selected *feral elongated six-legged stalker* concept (previously feral concept #2) — fast, elongated, predatory **six-legged** beast; not an avian skeleton. Claws and spine quills.
5. **Warped**: selected *asymmetric split-mask occult quadruped* direction (previously evolution-study panel #4) — quadruped, asymmetric, bent anatomy, split bone-mask skull and occult membranes; not a robed humanoid. A separate earlier *antlered/tendril, spectacular ascendant Warped* study (also called #4 in the brainstorm) inspires **Transcendent**, not Stage 1. These are distinct images; the Stage 1 source is now identified in ART-2 below, while the Transcendent study and original generation IDs remain unverified.
6. Shared anatomy across all: distinctive bone-mask skull, hooked claws, bony dorsal protrusions. Palette: charcoal/slate blue, desaturated gray, bone ivory and limited ochre/burnt-orange accents. **Reserve bright warm gold for the UI's active/selected-target signal**: ambient creature amber must stay dimmer, smaller and visually distinct from the active/target outline. Maintain strong subject-background value contrast in the eclipse arena.
7. A family owns a dominant silhouette, while Stage 2 and Stage 3 mutations add **visual cues**, Stage 3 usually amplifying the Stage 2 motif. Stage 4 Capstones add a restrained passive-themed accent, not a fourth full silhouette. This is **not** an approval to produce a unique full sprite for every package.
8. Free multiclassing means cosmetic layers must compose or prioritize gracefully. ART-5 now implements a bounded precedence/layer POC for review; final anatomical variants remain open.
9. Physical **Brute**, biologically **Predator**, occult/magical **Warped**. Within Warped: elemental **Stormborn**, authentic death magic/necromancy **Necrotic**, and astral/vital/void **Ethereal**. Do not reduce Necrotic/Revenant to infections, insects or biological parasites: that belongs to Predator.
10. **Approved names to change in a later implementation:** `spell:tornado:v1` Whirlwind/Rafale → **Groundbreaker / Fracture**; `spell:extort:v1` Claim/Revendiquer → **Seize / Prise de force**; `spell:soul_devourer:v1` Soul Feast/Morsure d'âme → **Devour / Dévorer**. Keep IDs and filenames stable; record these as **approved but not yet deployed** in the canonical naming plan, then synchronize authored English display names, balance metadata and UI in a separate PR. French names are documented proposals until a translation storage layer is chosen. No spell mechanics change.

## Provenance and asset status

The four owner-supplied originals are now audited and identified in [ART-2 source intake](asset-intake.md), with [machine-readable hashes and alpha measurements](source-intake.json). The current source register supersedes the earlier unverified, number-only references: base Creature = `1000011763.png`; Brute = `1000011765.png`; Predator = `1000011762.png`; Warped = `1000011764.png`. WebP previews are committed; the owner will add the original PNGs separately under `docs/art/sources/`. These filenames were matched by visible anatomy, correcting an inconsistent chat handoff. Never replace a reviewed source with an approximate regenerated image without visual approval.

The old winged predator and robed wraith sheet is retained as historical prototype provenance. [ART-3](runtime-sprites.md) now uses the four audited WebP references in both arena scenes with explicit mapping, anchors and fallbacks. The audited originals have real transparency; all have maximum alpha 254 and low-alpha traces outside the main silhouette. The separate Transcendent inspiration has not been imported.

## The visual language

| Form | Anatomy and behavior | Signature |
| --- | --- | --- |
| Base Creature (art shorthand Stage 0) | Lean four-legged common ancestor, not specialized | Hooked pale skull, ridge spines, claws, subtle amber shoulder growth |
| Brute | Heavy, low quadruped, enormous front paws, organic shell | Impact, posture, resonance and mass |
| Predator | Sinewy feral six-legged stalker; a dorsal ridge divides into an additional paired set of limbs as Predator mutations develop, retaining ancestral bone-mask and claws | Speed, hooks, quill launch, biological feeding and toxins |
| Warped | Uncanny asymmetrical quadruped, fractured skull, occult membranes | Impossible energy, magic, death, astral phenomena |

**Silhouette evolution:** the base Creature has four legs. Predator's additional limb pair grows from the dorsal/shoulder ridge during mutation, not from a separate species. In cross-family builds, retain the chosen dominant family's leg count and represent other-family traits as non-limb cues under the ART-5 prototype compositing priority; never silently attach or remove legs mid-shot.

Art and Phaser QA: validate silhouettes at actual rendered sprite sizes on desktop and both phone orientations, over the **actual eclipse background**. Make them separable by lightness, edge light, shape and ground shadow, not solely by team tint or colored glow. No essential target/hitbox affordance depends on image detail; reduced motion and missing-art fallback remain functional. All effect sequences represent **existing** domain resolutions, not new movement, ranges, summoned combat entities or damage logic.

## Brute spell-by-spell presentation

These are approved **fictional staging directions**, not final VFX storyboards. All unchanged names below retain the existing EN/FR vocabulary.

| Mutation | Spell EN / FR | Proposed execution or cue | Status |
| --- | --- | --- | --- |
| Brute | Pummel / Marteler | Hammer with a huge front paw | Accepted |
| Brute | Brace / Encaisser | Protective roar resonates in one ally, hardening bone plates; **ally target**, not self-only | Accepted |
| Frenzied | Fury / Furie | Explosive uncontrolled physical blow | Accepted |
| Frenzied | Wild Swing / Coup sauvage | Violent swipe that tears the caster's own tissue | Accepted |
| Ravager | **Groundbreaker / Fracture** | Both forelegs smash the ground; shockwave reaches up to 3 enemies | **New name approved** |
| Ravager | Deranged Charge / Charge démente | Reckless rush, flanks exposed after attack | Accepted |
| Ironhide | Carapace / Carapace | Natural plates interlock and thicken on self | Accepted |
| Ironhide | Body Slam / Écrasement | Shoulder/body impact; plates tighten as defense rises | Accepted |
| Colossus | Vital Surge / Afflux vital | Directed organic regenerative wave from luminous growths to **one ally** | Accepted |
| Colossus | Crushing Stomp / Piétinement | Immense forefoot crushes and stuns the victim | Accepted |
| Oppressor | Crash / Fracas | Skull/shoulder collision stunning one target | Accepted |
| Oppressor | **Seize / Prise de force** | Dominating bite pulls energy from target into caster | **New EN+FR approved** |
| Tyrant | Dominate / Dominer | Two immense front-paw strikes hitting two targets | Accepted |
| Tyrant | Alpha Roar / Rugissement alpha | Dominating roar bolsters one ally's attack and defense | Accepted |

### Brute visual cues by branch

| Branch | Stage 2 cue | Stage 3 intensification |
| --- | --- | --- |
| Frenzied → Ravager | Bulging muscles, dim ember fissures (not UI gold) | Larger asymmetric forelimbs, torn tissue, enlarged claws and irregular dorsal spines |
| Ironhide → Colossus | Thickened organic plates | Layered massive carapace, wider horns, pillar-like limbs |
| Oppressor → Tyrant | More pronounced frontal horns | Natural bone crown and low-intensity resonance organs around skull/thorax (distinct from UI gold) |

No visual cue changes hitbox size or tactical reach by itself.

## Predator spell-by-spell presentation

| Mutation | Spell EN / FR | Proposed execution or cue | Status |
| --- | --- | --- | --- |
| Predator | Venom Claw / Griffe venimeuse | Venomous claw scratch causing bleed | Accepted |
| Predator | Quill / Épine | Detachable dorsal bone quills projected toward up to two foes | Accepted |
| Deathmarked | Pursuit / Traque | Fast hunting lunge, heightened energy | Accepted |
| Deathmarked | Ambush / Embuscade | Explosive ambush that leaves caster temporarily slowed | Accepted |
| Deathstalker | Blood Hunt / Chasse sanglante | Chain attack on two victims draining their energy | Accepted |
| Deathstalker | Eviscerate / Éviscérer | Deep hooked-claw laceration | Accepted |
| Parasite | Bite / Morsure | Feeding bite heals caster | Accepted |
| Parasite | Latch / Agripper | Six-legged grip, parasitic blood feed and regeneration; no persistent unit | Accepted |
| Soulreaver | Blood Price / Sacrifice | Brutal strike damaging its own body | Accepted |
| Soulreaver | **Devour / Dévorer** | Biological energy-draining bite that restores tissue | **New EN+FR approved** |
| Blighted | Toxic Mend / Soin toxique | Glandular secretion heals several allies but temporarily weakens their defense | Name and staging accepted |
| Blighted | Overdrive / Survoltage | Stimulating pheromones energize and empower allies | Name and staging accepted |
| Blightweaver | Paralyzing Barb / Dard paralysant | Toxic bone dart stuns and bleeds | Accepted |
| Blightweaver | Contagion / Contagion | Corrosive spore cloud permanently erodes defense | Accepted |

### Predator visual cues by branch

| Branch | Stage 2 cue | Stage 3 intensification |
| --- | --- | --- |
| Deathmarked → Deathstalker | Longer stalking legs and claws, taut musculature | Oversized talons and extreme ambush posture |
| Parasite → Soulreaver | Feeding jaw, fangs, gripping/absorption organs | Stronger mouth and visible biological siphon structures |
| Blighted → Blightweaver | **Explicitly approved visible** poison glands, organic sacs and channels along back | Pronounced translucent toxic reservoirs, quill apparatus and spore vents |

The three routes must still read as one ancestral species; don't turn Soulreaver into a spectral mage.

## Warped spell-by-spell presentation

All existing spell names remain **unchanged** at this stage. The earlier suggestion to rename Bonewall to Ossify is **withdrawn**: genuine death magic makes Bonewall fully appropriate. Warped's visual language is intentionally **magical**; the Stage 1 body need not biologically justify every spell.

| Mutation | Spell EN / FR | Proposed execution or cue | Status |
| --- | --- | --- | --- |
| Warped | Shock / Décharge | Electrical arc emitted by fractured skull/occult fissure | Proposed, no rename |
| Warped | Revitalize / Revitaliser | Directed pulse of vital energy to one ally | Proposed, no rename |
| Stormborn | Emberstorm / Brasier | Ember fragments/storm across up to three enemies | Proposed |
| Stormborn | Ice Grip / Emprise glaciale | Cold grasp lowers one victim's initiative | Proposed |
| Cataclysm | Incinerate / Incinérer | Concentrated magical incineration on one target | Proposed |
| Cataclysm | Deep Freeze / Gel profond | Freeze pulse stunning one target | Proposed |
| Necrotic | Infestation / Infestation | **Summoned hungry spirits of the dead** torment up to three enemies; necromantic ritual wounds caster | Corrected necromancy direction |
| Necrotic | Bonewall / Mur d'os | Bones of the dead rise to ward one ally; blood ritual harms caster | **Name retained; proposed staging** |
| Revenant | Wraithguard / Garde spectrale | Death spirits stand guard over allies, fed by caster's blood | **Name retained; proposed staging** |
| Revenant | Death Wail / Hurlement funèbre | Voices of the dead scream through enemies and injure caster | Proposed |
| Ethereal | Vital Echo / Écho vital | Spectral healing resonance with continuing regeneration | Proposed |
| Ethereal | Whisper / Murmure | Impossible whispers grant regeneration to multiple allies | Proposed |
| Transcendent | Void Pulse / Onde obscure | Distortion of void/space wounds multiple foes | Proposed |
| Transcendent | Renewal Burst / Renouveau | Concentrated astral vitality and energy renewal to one ally | Proposed |

Important mechanical representation: spectral guardians, bones and hungry spirits are **VFX only**. Do not introduce summoned playable creatures, positions, persistent projectiles, range constraints, or extra interactions without a separate game-design decision.

### Warped branch visual cues — direction, not yet locked sprite specification

| Branch | Stage 2 cue | Stage 3 intensification |
| --- | --- | --- |
| Stormborn → Cataclysm | Elemental heat/cold contrast, steam and pale-blue crystallization; **exact always-on treatment to validate** | More intense elemental manifestations and shards |
| Necrotic → Revenant | **Death magic, not biological infection**: blue-green ghostlight, runes, grave mist, floating bones or souls | Partly spectral body, stronger necromantic aura, levitating bones and dead spirits |
| Ethereal → Transcendent | Translucent limbs, doubled silhouette, subtle astral membranes | Organic halo, impossible appendages and spatial distortion, inspired by the separate spectacular antler/tendril Warped study |

**Stage 4 Capstone direction (proposed, not locked):** Titan/Brute receives heavy, compact bone reinforcement at impact points (Sunder); Apex/Predator gets restrained predatory precision accents around jaws/claws (direct-hit bonus); Archmage/Warped gets a sparse astral energy circuit/aura on the owning Creature (Shared energy passive). Archmage's allies may display a brief energy-transfer VFX, **not** all inherit an Archmage body mutation. Stage 4 remains legible on mixed builds without changing collision/target bounds. The ART-5 prototype implements these cues and precedence for visual validation.

The **necromancy framing is user-corrected and accepted**. Fine colors, amount of floating ornamentation and the Stormborn/Ethereal overlay limits are not individually approved yet.

## Multiclassing, compositing, and pipeline — open design constraints

- A creature can acquire packages across families. Its final appearance must communicate a dominant silhouette and optional acquired motifs; ART-5 now makes its POC choice explicit: highest acquired stage, unique investment, then a stable family tie order; see [mutation cues](mutation-cues.md). This is not a final art-direction lock.
- A naïve layer of all anatomical upgrades can destroy readability, overrun target bounds, or create contradictory skull/limb geometry. Explore a deterministic priority/compatibility policy and a limited set of slots (body, dorsal, facial, aura) in a future design/technical PR.
- Prefer VFX overlays for dynamic glows/magic and reserve baked texture variants for silhouette-critical physical changes; validate this experimentally rather than assuming Phaser sprite tint suffices.
- Source art should use confirmed transparent alpha, consistent feet/ground anchors, controlled transparent padding and orientation. Quality check on the actual eclipse background, at all target viewport sizes, including six creatures and mirrored enemies.
- Effects must be triggered from authoritative combat action frames; animations may be skipped and reduced-motion paths must show the same result. Do not infer damage, targets, buffs or durations from a creature image.
- Maintain independent palette constraints for friendly/enemy identifiers and accessibility; no state communicated by color alone.
- Preserve provenance, source briefs and selected concept references. Old number-only references (#1/#2/#4) are ambiguous across separate image sheets; use the form-to-source mapping in [`source-intake.json`](source-intake.json) as the canonical identity register; the descriptions above explain the visual concepts. Source filenames are verified; original generation IDs remain unknown. The retired Bonewall→Ossify brainstorm was never approved as a renaming; it is included only to prevent reintroducing that rejected suggestion. ART-2 now includes four WebP references and the verified source register; the owner will add the original PNGs separately; generation IDs remain unknown and runtime geometry and QA are recorded in [ART-3](runtime-sprites.md).

## Proposed follow-up slices (not implementation authorization)

1. **ART-2 — Reference asset intake (PNG upload pending)**: [WebP references, corrected identification, transparency/dimension audit, visual review and provenance](asset-intake.md). Runtime integration is recorded separately in ART-3.
2. **ART-3 — Sprite manifest + arena contrast (implemented)**: [asset mapping/fallback, placements, sprite anchoring and viewport QA](runtime-sprites.md).
3. **ART-4 — Spell visual vocabulary (implemented)**: [confirmed physical, biological, elemental, necromantic and astral VFX](spell-vfx.md); no domain changes.
4. **ART-5 — Mutation cues / multiclass POC (implemented)**: [explicit precedence, three-slot budget, stage 2/3 cues and owner-only capstone accents](mutation-cues.md), with cross-family, replay and lifecycle tests.
5. **ART-6 — Polish (implemented)**: [shared timing, 1×/1.5×/2× live playback, bounded motif choreography, reduced motion and opt-in synthesized audio](arena-polish.md), with lifecycle tests and screenshots.

### Out of scope for this documentation PR

No code, assets, domain rules, numeric balance, spell IDs, migrations, live catalogue values, player-facing labels or deployment changes. The only files intended to change in ART-1 are design documentation.
