# 0131. Give confirmed arena spells a visual vocabulary

Date: 2026-11-04
Status: Accepted for the opt-in arena

## Context

The confirmed-action queue (ADR 0127) treats every damage outcome as a physical lunge and every other
outcome as a colored pulse. This makes Shock collide with its victim and a necromantic blood cost strike
its own caster. ART-3 now supplies the approved sprites; ART-4 supplies spell presentation without
changing combat, the wire protocol, catalogue names or mutation precedence.

## Decision

Map exact, versioned spell IDs to a presentation family, motif and delivery in `table/arena-spell-art.js`.
The frame's original `action.spell` is authoritative for identity; localized catalogue labels and creature
forms never select effects. Cover all 44 authored spells, including Focus and Strike. Unknown IDs use a
neutral local cue. Fizzles use only a broken cast mark at the caster.

The existing applied-outcome adapter remains authoritative for recipients and text. Deduplicate effect
recipients, exclude the caster from contact/lunge selection, and never derive destinations from declared
or dropped targets. An applied zero-damage outcome still records contact; the number displayed remains
zero. Preserve multiple recipients, even on a single-target-looking motif. Blood costs get a wound at the
caster, separately from any ward or recovery it receives in the same resolution. Preserve structured
outcome kind, effect and caster metadata through the recap adapter: a caster-side Bleed condition is a
blood cost too, even though its generic outcome tone is `condition`. Never parse the display text to
recognize it.

Keep that condition-name recognition in `arena-spell-art.js`, alongside the spell vocabulary. The page's
content-independence test permits only `Bleed` in that dedicated module; every other effect name and all
generic playback/page modules remain subject to the sweep. This exception selects a visual motif from an
applied outcome and carries no balance value or rule prediction.

Draw bounded, short-lived Phaser vectors for physical impacts/plates, biological claws/quills/spores,
elemental arcs/fire/ice, necromantic spirits/bones, and astral vitality/void. Only melee delivery moves the
caster toward a confirmed recipient. Projectiles reach their confirmed recipients at the existing impact
boundary. The frame, never a tween, changes health, energy, conditions or death.

Own every effect in a small renderer and destroy it on completion or cancellation. Preserve the existing
queue durations, Skip, resize, visibility and connection cancellation, and reduced-motion outcome frames
without particles or displacement. No effect introduces a combat entity, extra asset, timer that sends a
command, persistent aura, sound or dependency. Bright warm gold remains the active/target UI signal.

## Consequences

Names such as `toxic_waves`, `soul_devourer` and `summon_minions` retain their IDs while following the
approved fiction: astral Void Pulse, biological Devour and necromantic Infestation respectively. Buffs
remain accessible in DOM labels and inspection; motif color is never the only outcome information.

The vocabulary is deliberately shared across spells rather than 44 bespoke animated sequences. Stage
2/3 body overlays and multiclass precedence remain ART-5; final choreography and audio remain ART-6.
The isolated lab's original authored Strike demonstration and manual before/after replay remain as they
were. Confirmed-action playback uses the new cue path; manual replay stays static.

Pure tests cover catalogue completeness, identity, self-cost, dropped/missing recipients, unknown IDs and
fizzles. Browser tests run the production Phaser scene (with test-only bootstrapping) to inspect actual
objects, movement, cleanup and reduced motion, alongside the existing live queue and lifecycle journeys.
