# GameCard

A card for one game in the lobby: grain and soot over a panel gradient, a bone-tinted edge, the top right corner cut.

- The consumer provides an eyebrow (the round), a status tag, a `card-title` heading in `bone`, a `caption` line and one button pinned to the foot.
- `.mine` and `.invitation` tint the edge gold (70% into `line-soft`): the player has something to do there. Only that card may carry the screen's `.primary`.
- `.opponent` and `.game-row` cut by `cut-row` (8px); the rest by `cut` (12px). The diagonal across the cut is drawn so the edge stays continuous.
- The `your-turn` status tag is gold text on `accent-wash` with a gold edge.
