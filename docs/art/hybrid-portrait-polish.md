# Hybrid portrait retouching and AAA polish

Use the last approved portrait as the edit target. Keep the primary Stage 3 reference beside it for identity and the secondary family's reference for the specific trait it lends. The [Warped intake](warped-hybrid-aaa-intake.md) and [Predator intake](predator-hybrid-aaa-intake.md) identify the variants by what they show. [ADR 0152](../adr/0152-choose-a-stage-3-hybrid-by-influence.md) explains which portrait the game selects.

## Worked example · Revenant + Brute

The Revenant contributes the hooked skull, green eye and heart, open ribs, ivory grave reliefs and black funeral cloth. The Brute's 15% influence is concentrated in the two forelegs. Their muscle, shoulder plates, broad feet and claws should read even at battle size. The finished creature has two lean hind legs and two massive forelegs. Both near and far forelegs carry weight.

The first edit fixed the count but kept the brown backdrop of the supplied image and made the front pair look too light. Use the approved series references when a detail in the edit target conflicts with the brief. The prompts below capture the edits in reusable form. Left and right describe this right-facing portrait.

### 1. Remove an extra limb

> Edit the supplied portrait in place. From left to right, keep feet one, two, four and five. Remove the third foot and trace its whole limb back to where it joins the belly just below the green heart. Reconstruct the belly contour and open the space beneath the ribs. Keep both hind legs on the left and both heavy forelegs under the head on the right. Keep the pose, proportions, hooked skull, heart, armour, cloth and framing. Show exactly four complete paws.

Count the feet and trace each one to its shoulder or hip. Check the space beneath the heart at thumbnail size. A leftover claw or floating shoulder plate can still read as a fifth limb.

### 2. Restore the family trait

> Use the four-legged revision as the edit target. Thicken both front legs through the shoulders, elbows and wrists, roughly 1.7 times their width in this revision. Widen both front paws and give them broad ivory claws. Keep visible muscle between the heavy shoulder plates. Retain the two lean hind legs, the open space beneath the rib cage and the Revenant's silhouette. Use the series' solid black background throughout, including between the legs.

The width adjustment belongs to this revision. For another hybrid, name the feature and the reference that establishes its proportions. Compare both limbs at the same display size before accepting the edit.

### 3. AAA finishing prompt

The approved Revenant + Brute came from the second pass. Use this finishing prompt when an otherwise approved portrait still needs a material or edge pass.

> Refine the supplied portrait at its original aspect ratio and full resolution. Keep the approved outline, anatomy, pose, palette and framing. Resolve the boundaries between weathered ivory, dark muscle and torn cloth. Preserve the scaled engraving and local green light inside the ribs. Keep the claws and dorsal tips inside the frame, with enough separation to read at battle size. Put pure black (#000000) behind the figure and in the open spaces. Keep the lighting on the creature, with no floor, haze or halo.

For another form, substitute its approved materials, light source and identifying features. Compare the result with the preceding pass on black at full size and at battle size. Use the preceding pass if the finish changes the anatomy or buries the secondary family's trait.

## Ship the approved master

The opaque black background is part of master review. The runtime sprite carries alpha so the battlefield shows through its silhouette. For this portrait, run the existing intake tools from the repository root (Python with Pillow and Node.js).

```sh
python3 scripts/matte-opaque-art.py /path/to/approved-master.png table/battle-art/evolutions/hybrids/warped-revenant-brute.webp
```

Copy the printed canvas and silhouette bounds into the matching row of `table/battle-warped-hybrid-art.js`. Keep the existing artwork ID and path when replacing a portrait. Inspect the WebP on a contrasting ground, particularly the cloth tips, rib shadows and the opening left by the removed leg. The [matte notes](warped-hybrid-aaa-intake.md#alpha-and-silhouettes) explain how dark pockets are treated.

For portraits used in [page pictures](page-pictures.json), regenerate the responsive WebPs and their manifest. Commit the changed derivatives along with the battle sprite. The showcase uses these smaller pictures in its specimen gallery.

```sh
node scripts/ux/pictures.mjs
node scripts/ux/pictures.mjs --check
node scripts/ux/pictures.mjs --budget
node --test table/battle-warped-hybrid-art.test.js table/battle-evolution-art.test.js table/battle-evolution.test.js showcase/showcase.test.js
```
