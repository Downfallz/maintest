# FeaturedSpace

The home's first screen (`#featured` in `table/lobby.html`, drawn by `table/lobby-featured.js`): one game, one lesson
or a new battle, by what the player came to do, with the home's one filled action. Its layout is the lobby's own
(`table/lobby.css`); its eyebrow and title are dressed by `identity.css` as `.featured`. The card draws a stand-in
gradient where the painting goes.

- **The rule**: every state holds its room, with the actions at its foot. The text is anchored to the bottom, so a
  line that appears (a turn's status) moves the title up and never the buttons.
- `.featured`: the space, a grid on `soot`, at least 77% of the screen's height on a desktop and the whole screen on
  a phone, the header lying over its top.
- `.featured-art`: the painting behind it, a ground cut to the space's right 74% (all of it on a phone), with a
  scrim of `soot` from the left and from the foot. A lesson adds its creature, a new battle the drifting mist.
- `.featured-status`: what follows the eyebrow, in `bone`; in `gold` and semibold when it is the player's turn.
- `.featured-actions`: two buttons side by side, the filled `primary` and a quiet `secondary`, each 48px tall.
- `.featured-shortcuts`: the links to Your games and Open seats on a band of `soot` at the very foot, 48px tall.
- A host notice lies under the header, over the painting's top, on a phone.
