# code-reviewer lessons (branch fix/1135-1136-toast-clock-offline-imports)

- To prove a timer flake fixed, insert `await page.waitForTimeout(<timer + 1 s>)` before the racing read in the new spec and in an `origin/main` copy: #1135's old test failed on desktop, the new one passed; then remove the CSS rule under test to check that the fixed test still fails (#1135, c442c57).
- Playwright's page clock does not drive CSS animations, and an animation with `fill: both` stays in `getAnimations()` after it ends: `pause()` plus `currentTime` in the same evaluate holds its plateau however late the read is (#1135, battle-windows.spec.js).
- An import walker that follows only `./` and `../` specifiers ignores `/x.js` and every `import()` in the modules it reaches: a list check built on it fails open on both. Ask it to follow them or to fail on them (#1136, scripts/module-imports.mjs).
