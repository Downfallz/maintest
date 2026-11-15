# SectionHeading

A lobby section's title: `section-title` in `bone`, with an optional counter, over the rune rule.

- The rule is drawn by `.section-heading::after`: `--tex-rule` (the rune, 10×15px), then a 1px hairline of bone at 55% fading to transparent.
- The counter (`.lobby-count`) is a pill in `text`, `muted`, inside the title.
- Use one per lobby section. Inside panels use the plain `panel-title` h2 without the rule.
