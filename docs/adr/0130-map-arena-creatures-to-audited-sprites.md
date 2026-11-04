# 0130. Map arena creatures to audited sprites

Date: 2026-11-04
Status: Accepted for the opt-in arena

## Context

ART-2 audited four selected creature references. The arena still assigned its old three-frame sheet by
roster position and stretched each frame into a square. That cannot show an unmutated ancestor or reliably
identify a mutation. Family precedence in multiclass builds remains an ART-5 design question.

## Decision

Keep this entirely in presentation. No domain, catalogue IDs, legality or combat changes.

- `table/arena-art.js` owns the four-entry sprite manifest: source dimensions, principal silhouette bounds,
  ground anchors and local filenames. Runtime files copy the ART-2 WebPs byte-for-byte; do not regenerate,
  crop or threshold the selected art. Original PNGs are not required to run the arena.
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

## Consequences

- Good: the arena shows the approved art, and a creature's appearance follows its own acquired root in
  every snapshot, historical frames included. Names, seat order, hidden drafts and known spells cannot
  select an appearance.
- Bad: the four runtime images add 1,212,726 bytes at source size, kept to avoid another lossy encode;
  size optimization and retouching are separate measured work. A mixed-family build shows the neutral
  base, which is a placeholder and not a claim about its mutations, until ART-5.
- Neutral: the historical three-frame sheet leaves the tree and the image; its brief in `arena/ART.md` and
  git history keep the provenance. The same scene serves the laboratory and the live view, and a figure
  fitted by the tap width stands 39% (Predator on a desktop) to 81% (Warped on a phone) of its slot height.

## Alternatives considered

- Keep assigning the sheet by roster position: it cannot show an unmutated ancestor and names a class the
  creature may not have.
- Pick a dominant family for mixed builds now: that is the ART-5 precedence question, and a wrong guess
  would read as a claim about the creature's build.
- Re-encode or crop the references to fit the placement slots: a second lossy pass and a geometry the
  audit did not review; uniform scaling with reviewed anchors keeps the ART-2 bytes.
- Tint the enemy team to tell the sides apart: it recolors approved art; labels, formation and rings
  already carry team identity.

## Follow-up

- `TableFiles` routes the four WebPs and `arena-art.js`; `TableFilesTests` holds every served image and module.
- Node tests cover family selection, mixed, unknown and legacy snapshots, historical frames, texture
  fallbacks, source-byte identity against `docs/art/source-intake.json` and mirrored geometry. Browser tests
  cover mutation refreshes, partial and total artwork failure, inspection, six creatures and viewport rotation.
- ART-4 spell effects and ART-5 anatomy overlays remain separate; no Stage 2/3 art cue is implied here.
