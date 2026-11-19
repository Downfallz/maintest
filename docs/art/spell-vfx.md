# ART-4 — Confirmed spell visual vocabulary

Implemented on top of the merged ART-3 (#439). ART-4 now integrates `main`, including the AU1
history console (#440); opening that console settles VFX through the existing playback cancellation path.
See [ADR 0131](../adr/0131-give-confirmed-arena-spells-a-visual-vocabulary.md) for the presentation boundary.

## Coverage

All 42 family spells and the two base actions have an entry in `data/presentation/spell-art.json`, which the
builder checks and publishes beside the schema and the host serves on each card as `visual`; the page names
no spell. These are shared vector motifs, not new textures or full skeletal animations.

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

The 15 physical spells and Focus are staged (BC6 of [the battle's craft plan](battle-craft-plan.md)); BC7 and
BC8 stage the other families. Each staging is written first as a storyboard
([physical](storyboards/physical.md), [the base actions](storyboards/base-actions.md)), and how much it may ask
for goes by the Stage of the mutation that teaches the spell: [the ladder](storyboards/README.md#the-ladder).
A base action is the quietest thing in the battle, a Stage III spell the loudest. The strokes of the `impact`
motif run along the line of the blow and the `slash` strokes cut 60° across it, over a dark edge, staged or not.

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
