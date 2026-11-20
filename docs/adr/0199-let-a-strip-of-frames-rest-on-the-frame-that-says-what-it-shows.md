# 0199. Let a strip of frames rest on the frame that says what it shows

Date: 2026-11-20
Status: Accepted
Rulebook: none

## Context

[ADR 0198](0198-send-a-pages-pictures-at-the-size-they-are-drawn-and-draw-one-icon-set.md) made a strip of frames
its first frame under less motion. The first strip, the showcase's Cast (UX6), starts with the Brute at rest and
ends on the blow's contact: under less motion a visitor saw a creature waiting, while the strip's name says the
blow lands. On the pull request the owner asked for the moment of contact as the still form.

## Decision

A strip may name the frame it rests on under less motion: `still`, from 1 to its count, in `table/picture.js`,
written as `data-still` on the strip. The first frame stays the default. Under the system's wish for less motion
and under the account's, the kit sets that frame by a margin, a pose and not a move. In full motion nothing
changes: before it is in view the strip is its first frame, then it plays once and rests on its last.

## Consequences

- Good: a strip says the same thing whether it plays or not, and its name for a screen reader stays true.
- Good: the default keeps every strip that names no frame as ADR 0198 drew it.
- Bad: a strip's still frame is chosen by hand, in the page that draws it, and a strip filmed again can move it.
- Neutral: the kit declares one rule a frame (`data-still="2"` to `"12"`), as it does for the count.

## Alternatives considered

- Reorder the strip so its first frame is the contact: the strip would then play from the blow back to the leap.
- Rest on the last frame under less motion: the last is often the return to rest, as it is for Pummel.

## Follow-up

`table/identity.css` and `showcase/showcase.css` (the still rules), `table/picture.test.js`,
`docs/art/page-pictures.md`.
