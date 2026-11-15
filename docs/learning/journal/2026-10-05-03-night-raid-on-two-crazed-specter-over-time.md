# 2026-10-05. Night Raid on two, Crazed Specter over time

- **The owner's change, by hand.**
  - **Night Raid** reaches two enemies rather than three, and hits them for 4 rather than 3 while draining 3
    rather than 2. That is more than a round's income, so each target ends the raid a turn behind.
  - **Crazed Specter** deals 4 rather than 9 and leaves each target bleeding 4 a round for 2 rounds. It has no
    critical chance (0.38 before), and its caster still bleeds 4 for a round.
- **Knobs.**
  - Night Raid's damage now ranges 2 to 4 and its drain 2 to 3.
  - Crazed Specter's damage ranges 2 to 6. Its new bleed has a knob from 2 to 5, and the critical-chance knob
    is gone.
  - `check-knobs` passes. It reads Night Raid at 6.53 a round at most, as it read the old raid low: a drain
    is worth little on a board-free reading.
- Content `ad3e4d00`, benchmark digest regenerated. The policy refit of #284 is played on this content, and the
  exploit panel was measured on `e6f72578`, the content just before.
