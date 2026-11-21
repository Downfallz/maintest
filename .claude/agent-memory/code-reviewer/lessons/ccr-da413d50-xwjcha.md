# code-reviewer lessons (branch ccr-da413d50-xwjcha)
- Tenth time: the committed HEAD moved mid-review (41c3a4e -> 3320497, the copy review answered in docs and two alts); `git rev-parse HEAD` again before the report, diff the two, rerun the tests on a second archive (scene paintings S4).
- A scene-in-its-section test that searches from the section's id to the end of the page still catches a swap of two heads (the earlier one is missed), proved by mutating familyScenes; probe with a swap before calling such a test weak (S4, showcase/showcase.test.js:437).
