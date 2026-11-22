# ART-4 — Confirmed spell visual vocabulary

Implemented on top of the merged ART-3 (#439). ART-4 now integrates `main`, including the AU1
history console (#440); opening that console settles VFX through the existing playback cancellation path.
See [ADR 0131](../adr/0131-give-confirmed-arena-spells-a-visual-vocabulary.md) for the presentation boundary.

## Portraits

A spell visual may name a `portrait`. The battle page draws it from the picture kit as `spell-<name>`
([page-pictures.md](page-pictures.md), ADR 0198): the spell card at 40 px beside the spell's name, the journal at
24 px, each size from its own files; a compact card, in a docked sheet, keeps its text only. The picture is
decorative (`alt=""`): the name beside it is the text. A package's visual (`data/presentation/mutation-art.json`)
may name one too, from the same set; today only the three capstones do. The mutation preview and the Mutation Tree draw it at 24 px beside the package's name.
The showcase's field guide draws each one at 40 px beside the spell's name, and a capstone's beside its passive.
The statistics page (`/stats`) draws a spell's at 40 px beside the spell cast most, and at 24 px in each row. The owner approved the 47 paintings,
512 px each: the starting actions, every spell of Stages I to III, and the Titan, Apex and Archmage passives. They
are kept in `docs/art/spell-portraits/`, outside the image, as the kit's sources, and the kit derives the sizes sent.
`table/battle-spell-art.test.js` holds the names of both files and the kit's declarations together, both ways.

## Coverage

All 42 family spells and the two base actions have an entry in `data/presentation/spell-art.json`, which the
builder checks and publishes beside the schema and the host serves on each card as `visual`; the page names
no spell. The effects are shared vector motifs, not new textures or full skeletal animations; only the
portraits above are paintings.

| Vocabulary | Shape and delivery | Examples |
| --- | --- | --- |
| Physical | Melee contact, heavy strokes, ground waves, interlocking plates, resonance rings | Pummel, Groundbreaker, Brace, Alpha Roar |
| Biological | Claw marks, biting teeth, traveling bone quills, rounded gland/spore particles | Venom Claw, Devour, Quill, Toxic Mend |
| Elemental | Jagged caster-to-recipient arc, ember tongues, angular ice crystal | Shock, Emberstorm, Deep Freeze |
| Necromantic | Blue-green dead spirits and ivory bone wards; a separate blood-cost wound | Infestation, Wraithguard, Bonewall |
| Astral | Vertical vitality rings and contracting spatial aperture | Revitalize, Renewal Burst, Void Pulse |
| Neutral | Plain mark; broken cast ring on fizzle | Focus, a card nobody drew, fizzled casts |

Names in this table describe the approved direction; ART-4 does not deploy pending renames. In particular,
`spell:tornado:v1`, `spell:extort:v1` and `spell:soul_devourer:v1` still use their current catalogue labels.
No biological motif is assigned to the Necrotic/Revenant branch. No spirit is a summoned game unit.

## Staging

Since [ADR 0196](../adr/0196-stage-a-spell-in-beats-an-effects-kit-plays.md) an entry may also carry a `staging`:
a list of beats the effects kit plays. The spell's own motif is one of those beats. A spell without a staging
is drawn as described here. So is a staged spell until the kit has loaded. `battle/README.md`, "The effects kit", lists the beats.

All 44 entries are staged: the 15 physical spells and Focus in BC6 of [the battle's craft plan](battle-craft-plan.md),
the 14 biological spells in BC7, the 14 elemental, astral and necromantic spells in BC8. Each staging is written
first as a storyboard ([the base actions](storyboards/base-actions.md), [physical](storyboards/physical.md),
[biological](storyboards/biological.md), [elemental](storyboards/elemental.md), [astral](storyboards/astral.md),
[necromantic](storyboards/necromantic.md)), and how much it may ask
for goes by the Stage of the mutation that teaches the spell: [the ladder](storyboards/README.md#the-ladder).
A base action is the quietest thing in the battle, a Stage III spell the loudest. The strokes of the `impact`
motif run along the line of the blow and the `slash` strokes cut 60° across it, over a dark edge, staged or not.

## The battle's colours

Besides each family's own colour (`LIGHT_COLOURS`, `table/battle-ground.js`), the battle names five colours, by
which a beat is tinted (`tint`) and a motif is drawn. They are written once, in `table/battle-motion.js`, and
`table/battle-motion.test.js` holds each to this table, and bone and soot to the palette (`docs/art/branding.css`).

| Name | Value | Where it comes from | What it is for |
| --- | --- | --- | --- |
| Bone | `0xd8c7a7` | `--brand-bone` | The hit flash, the chips of a blow, the bone pieces. |
| Soot | `0x070d10` | `--brand-soot` | The dark edge under the pale strokes of a motif. |
| Blood | `0x9a3b34` | The owner's choice, #919: the paler `0xb66f7e` read pink over the sky. | A wound, a blood cost, the `blood` tint. |
| Ember | `0xe0763f` | The owner's choice, #934: warmer, so fire reads as fire. | The `fire` motif, the `ember` tint. |
| Hide | `0x8c7f68` | The owner's choice, #943: bone in shadow. | The flakes of a lowered defense, which must not read as the chips of a hit. |

None of them is the target marker's gold.

## Timing and integrity

- Use the existing 840 ms windup, 1700 ms impact frame and 500 ms settlement. A quill leaves its caster
  with the throw, at 240 ms, and flies for the 600 ms left of the windup, on a low arc when thrown across the field
  (#909). Local impact vectors fade over 600 ms, inside the impact frame.
- Apply motifs only to confirmed outcome recipients, once per creature except a separate self-cost wound.
  A caster receiving its own Bonewall shows both the blood cost and the ward.
  Preserve the applied outcome kind, effect and caster flag: Bonewall, Wraithguard and Death Wail
  pay with a caster-side lasting effect the catalogue calls harmful, while Infestation uses immediate Damage. Empty outcomes never invent
  recipients from the declaration. Fizzles never launch quills or hit a recipient.
- Scale shapes to the visible creature and viewport; mirrored creatures use the same ground/body anchors.
  Shapes differ as well as colors. Warm gold remains reserved for the target/active marker.
- Only melee moves the caster across the field; a cast loads where it stands and a projectile's caster throws
  from its ground (BC5 of [the battle's craft plan](battle-craft-plan.md)). Health lost by another creature,
  whatever the family, moves the view a little, by how much was lost. A blood cost cannot cause that on its own.
- Skip, rotation, connection interruption, hidden tab and seat changes reuse the existing cancellation
  path. Completed graphics are destroyed immediately; cancelled graphics and their tweens are destroyed.
- Reduced motion keeps the same authoritative result text and after-frame, without vector animation,
  projectiles, camera shake or displacement. Newly created historical figures are anchored even when
  reduced motion jumps directly to impact after a mutation. No additional runtime asset download is introduced.

## Review and limits

The browser suite captures actual production Phaser rendering against the eclipse backdrop for six
creatures at small-phone, phone and desktop sizes, and exercises landscape cancellation. It also checks
projectiles, magical casts without lunging, self-cost, multiple recipients, fizzle, reduced motion and
object cleanup. The existing live integration tests continue to verify outcome labels and historical
frames independently of the renderer.

This slice establishes a readable vocabulary. Several spells intentionally share one motif; it does not
provide unique paw poses, a chained attack for each target, detailed blood/energy-transfer choreography,
persistent wards or biological body mutations. Final artistic tuning remains reviewable in ART-6.
Manual match replay remains static before/after frames; it does not acquire a new auto-play mode here.
