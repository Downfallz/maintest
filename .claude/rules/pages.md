---
paths:
  - "table/**"
  - "studio/**"
  - "viewer/**"
---

# Static page rules

- No dependency, no build step, no CDN, no remote font (ADR 0024). The tests run on Node's own runner:
  `node --test studio/*.test.js table/*.test.js showcase/*.test.js`. Only `studio/browser` uses pinned Playwright.
- `identity.css` is loaded last by every table page (ADR 0158); colours and type come from docs/art/branding.md.
- Design for 320 px first, then phone, tablet, desktop. Touch targets of 44 px.
- A player reads the words of docs/domain/player-terminology.md (Mutation, Stage, Tempo, Instinct/Precision,
  Upkeep → Command → Combat → Aftermath), not the domain's own. A scanner of those words reads the page through
  `scripts/terminology.mjs` and refuses every sentence of its Before column, whatever its case (#675).
- A new file joins the host's file table (`src/DownfallArena.Cli/Table/TableFiles.cs`, or the studio's
  `StudioFiles.cs`) and its test, or it is a 404 once deployed.
- A picture is checked at each breakpoint it is declared for (320, phone, tablet, desktop): the `currentSrc` the
  browser picks from `sizes`, and the crop the box makes. One width passing says nothing of the others (S2, S8,
  S11, #1090, #1103).
- A CSS change is checked in the cascade: a later or more specific rule can win over it, and a fit check measures
  the element itself, not a neighbour's box (#1035, #1087, UX12).
- A change to a visible string greps the old text in every suite: `studio/browser/` (the real-engine
  `battle.*.js` and `table.*.js` too, which only CI plays) and `scripts/ux/` (the journey walk) (#979).
- A `<dialog>` that fades stays displayed while it leaves: a script or a spec reads `open`, never `isVisible` or
  `display`. A change that makes one fade greps `isVisible` on it in `studio/browser/` and `scripts/ux/` (#979).
- The page shows what the host decided. A rule, a legal option or a card text computed in a page is a bug.
- Read what changed from the host's streams or the relay, never on a timer (ADR 0164).
