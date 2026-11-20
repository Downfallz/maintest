# code-reviewer lessons (branch ccr-8eee55d6-6b4hq7)

- Second time (UX5 eef5034, then UX6 86707f4): a bare `spawnSync('python3', …)` under `scripts/ux/` without the `// NOSONAR S4036` reason (round.mjs:59). Sonar raises a hotspot the local gate cannot see; check every new spawn of a bare command.
- A `sizes` value for an image whose width follows its height (`height: 68%; width: auto`) has to be measured in a browser. The journey measure's `naturalWidth` is corrected for density, so it equals `sizes`, and a picture sent too small never shows up (UX6, showcase/page.mjs:21, 86707f4).
