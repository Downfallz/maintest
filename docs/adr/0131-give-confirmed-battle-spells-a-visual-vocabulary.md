# 0131. Give confirmed battle spells a visual vocabulary

Date: 2026-11-04
Status: Accepted for the opt-in battle

## Context

The confirmed-action queue (ADR 0127) treats every damage outcome as a physical lunge and every other
outcome as a colored pulse. This makes Shock collide with its victim and a necromantic blood cost strike
its own caster. ART-3 now supplies the approved sprites; ART-4 supplies spell presentation without
changing combat, the wire protocol, catalogue names or mutation precedence.

## Decision

Author each spell's presentation family, motif and delivery beside the content, in
`data/presentation/spell-art.json`, keyed by the versioned spell ID. The data builder checks it against the
authored spells and the three vocabularies and publishes it next to the schema, outside the content hash, the
way the package tags are (docs/domain/package-identity-tags.md); the table host serves it on each card as
`visual`. The page names no spell and no effect: the stage-3 sweep that keeps content out of `table/` holds
(docs/tabletop/app-roadmap.md), and a content pass that adds a spell adds its picture in the same place. The
frame's original `action.spell` chooses the card; localized labels and creature forms never select effects.
All 44 authored spells are drawn, Focus and Strike included. A card nobody drew, an older host that serves
none and an unknown ID use a neutral mark. Fizzles use only a broken cast mark at the caster.

The existing applied-outcome adapter remains authoritative for recipients and text. Deduplicate effect
recipients, exclude the caster from contact/lunge selection, and never derive destinations from declared
or dropped targets. An applied zero-damage outcome still records contact; the number displayed remains
zero. Preserve multiple recipients, even on a single-target-looking motif. Blood costs get a wound at the
caster, separately from any ward or recovery it receives in the same resolution. Preserve the structured
outcome kind, effect and caster flag through the recap adapter: a lasting effect the catalogue calls harmful,
landing on the caster from its own cast, is a blood cost too, even though its generic outcome tone is
`condition`. Which effects are harmful is the catalogue's to say; the page neither names one nor parses the
display text to recognize it.

Draw bounded, short-lived Phaser vectors for physical impacts/plates, biological claws/quills/spores,
elemental arcs/fire/ice, necromantic spirits/bones, and astral vitality/void. Only melee delivery moves the
caster toward a confirmed recipient. Projectiles reach their confirmed recipients at the existing impact
boundary. The frame, never a tween, changes health, energy, conditions or death.

Own every effect in a small renderer and destroy it on completion or cancellation. Preserve the existing
queue durations, Skip, resize, visibility and connection cancellation, and reduced-motion outcome frames
without particles or displacement. No effect introduces a combat entity, extra asset, timer that sends a
command, persistent aura, sound or dependency. Bright warm gold remains the active/target UI signal.

## Consequences

- Good: a confirmed cast reads as what it was. Shock arcs from its caster instead of colliding, a quill
  travels to the creatures it actually hit, a blood cost is a wound at the caster and not a strike by it,
  and a buff on the caster is drawn beside that wound rather than hidden by it. Names such as
  `toxic_waves`, `soul_devourer` and `summon_minions` keep their IDs while following the approved fiction.
- Bad: the vocabulary is shared, not bespoke: several spells draw one motif, and there is no per-target
  chain, paw pose or transfer choreography. Final tuning is ART-6.
- Neutral: the renderer owns every vector it draws and the queue's durations, Skip, resize, visibility and
  reduced-motion paths are unchanged. Buffs stay accessible in DOM labels and inspection; a motif's colour
  is never the only outcome information. Stage 2/3 overlays and multiclass precedence remain ART-5; the
  lab's authored Strike and the manual before/after replay are as they were.

## Alternatives considered

- Keep the lunge for every damage outcome and colour the pulse by tone: it is what sent Shock into its
  victim and the blood cost into its own caster.
- Select the effect from the catalogue label: a rename or a translation would silently change the picture;
  the wire's versioned spell ID is the only stable identity.
- Keep the ID-to-motif table in a page module: it is content the page knows by name, which the stage-3 sweep
  exists to prevent, and the next spell would need a page change; beside the content, the builder checks it.
- Derive recipients from the declared targets: a dropped target would be hit on screen; applied outcomes
  are the only confirmed recipients.
- Recognize a blood cost from the outcome text: the recap text is for people and changes; the structured
  outcome kind, effect and caster flag are kept through the adapter instead.
- Ship sprite sheets or particle textures per family: another asset pipeline before ART-6 decides on
  choreography; bounded local vectors need no download and no image budget.

## Follow-up

- `SpellArt` (Infrastructure) publishes and reads the art the way `PackageIdentities` does the tags; the data
  builder and the studio's build publish it, `AddGameResources` registers it, and the table composer puts it on
  the cards. `data/README.md` names the file.
- Pure tests cover the builder's checks, the card's visual on the catalogue, completeness against `data/Spells`,
  identity through renamed labels, self-cost,
  dropped and missing recipients, unknown IDs and fizzles. Browser tests boot the production scene in a
  test-only harness to inspect the drawn objects, movement, cleanup and reduced motion, beside the existing
  live queue and lifecycle journeys.
- ART-5 (mutation overlays) and ART-6 (choreography, audio, polish) build on this vocabulary.
