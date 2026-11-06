# 0130. Map battle creatures to audited sprites

Date: 2026-11-04
Status: Accepted for the opt-in battle

## Context

ART-2 audited four selected creature references. The battle view still assigned its old three-frame sheet by
roster position and stretched each frame into a square. That cannot show an unmutated ancestor or reliably
identify a mutation. Family precedence in multiclass builds remains an ART-5 design question.

## Decision

Keep this entirely in presentation. No domain, catalogue IDs, legality or combat changes.

- `table/battle-art.js` owns the four-entry sprite manifest: source dimensions, principal silhouette bounds,
  ground anchors and local filenames. Runtime files copy the ART-2 WebPs byte-for-byte; do not regenerate,
  crop or threshold the selected art. Original PNGs are not required to run the battle.
- Map the displayed snapshot's acquired root IDs (`brute`, `prowler`, `occultist`, all `tier:…:v1`) to Brute,
  Predator and Warped. A single known family uses that form through its later stages. No known root, legacy
  missing mutation data, or several known families uses the base Creature. This is a neutral placeholder,
  not a dominant-family or cross-family anatomy policy. ART-5 will replace the mixed-family placeholder.
- Read historical snapshots for playback. Never borrow the latest creature's mutations to decorate an
  earlier turn. Names, seat order, hidden drafts and known spells cannot select an appearance.
- Scale uniformly to the existing HTML target's width and placement height. Align the reviewed ground point
  with its ring, compensate for the enemy mirror, and place impact bursts within the visible silhouette.
  Principal bounds at alpha >= 128 are diagnostic geometry, not an instruction to remove softer pixels.
- Slightly dim the eclipse background and preserve both teams' source colors. Team labels, formations,
  health captions and active/target rings remain independent of texture detail. No additional postprocessing
  or GPU-only effect is required. The same scene serves the laboratory and live view.
- A missing family texture uses the base texture; if that also fails, use the existing labelled/selectable
  vector. Loading failure cannot prevent inspection or a legal action. Reduced motion and cancellation retain
  their existing paths.

## Consequences and verification

The four runtime images add 1,212,726 bytes; the historical sheet remains available for provenance but the
scene no longer downloads it. This first integration retains source-size textures to avoid another lossy
encode. Size optimization and art retouching require separate measured work.

Node tests cover family selection, mixed/unknown/legacy snapshots, historical frames, texture fallbacks,
source-byte identity and mirrored geometry. Host route tests cover all four WebPs. Browser tests exercise
mutation refreshes, partial/total artwork failures, inspection, six creatures and viewport rotation, alongside
the existing targeting, action playback, reduced motion and complete-loop suite.

ART-4 spell effects and ART-5 anatomy overlays remain separate. No Stage 2/3 art cue is implied by this slice.
