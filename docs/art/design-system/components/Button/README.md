# Button

The filled gold action (`.primary`) and the base slate button.

- One `.primary` per screen: the thing the player came to do. It is gold with grain, label in `accent-ink`, two corners cut by `cut-action` (10px). Hover fills it with `accent-strong`.
- Everything else is the base button: `raised` fill, `line` border, `radius-soft`, at least 44px tall. Hover turns the border `gold`; disabled drops to 50% opacity.
- Focus: base buttons show a 3px `gold` ring offset 4px. The cut `.primary` keeps its ring inside, 2px `accent-ink` at offset -5px, because the clip would hide an outer ring.
- `.primary` works on a `<button>` or an `<a>`. Never ivory text on gold. The consumer provides the label: a verb that says what happens.
