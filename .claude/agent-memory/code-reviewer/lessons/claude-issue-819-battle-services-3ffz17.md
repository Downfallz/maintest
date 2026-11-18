# code-reviewer lessons (branch claude/issue-819-battle-services-3ffz17)

- Hiding `#phaser-mount canvas` does not hide a layout bug from `fitsViewport`: Phaser runs at `Scale.NONE` with zoom 1, so it never writes an inline px size (only `_resetZoom` does), the canvas is sized by CSS 100% of an absolute mount, and `#match-dialog` is `showModal` (top layer). Check the scale mode before claiming the canvas could overflow (#819, 6149c91).
- Old and new copies of a canvas spec run side by side at load share the CPU: the old one's canvases slowed the new one's `goto` to 30.8 s. Run the new copy alone at the same load too before calling a failure its own (#819, 6149c91).
- A diagnostic thrown from a helper's catch after a 20 s `toPass` rarely reaches the report when the test timeout is 30 s and mount can take most of it: Playwright reports "Test timeout" and the click's call log instead (#819, 6149c91).
