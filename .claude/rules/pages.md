---
paths:
  - "table/**"
  - "studio/**"
  - "viewer/**"
  - "showcase/**"
---

# Static page rules

- No dependency, no build step, no CDN, no remote font (ADR 0024). The tests run on Node's own runner:
  `node --test studio/*.test.js table/*.test.js showcase/*.test.js`. Only `studio/browser` uses pinned Playwright.
- `identity.css` is loaded last by every table page (ADR 0158); colours and type come from docs/art/branding.md.
- Design for 320 px first, then phone, tablet, desktop. Touch targets of 44 px.
- A player reads the words of docs/domain/player-terminology.md (Mutation, Stage, Tempo, Instinct/Precision,
  Upkeep → Command → Combat → Aftermath), not the domain's own.
- A new file joins the host's file table (`src/DownfallArena.Cli/Table/TableFiles.cs`, or the studio's
  `StudioFiles.cs`) and its test, or it is a 404 once deployed.
- The page shows what the host decided. A rule, a legal option or a card text computed in a page is a bug.
- Read what changed from the host's streams or the relay, never on a timer (ADR 0164).
