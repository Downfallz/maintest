# 2026-09-23. A match's length is read on the exploring run, and the content as it stands scores the same

- **What changed.** The objective's ten-to-fifteen-round band moves from the greedy mirror to the exploring run
  (ADR 0071, amending ADR 0068). The owner accepted that two greedy agents play a short, narrow game, and the
  mirror sat at 10.05 on the benchmark seeds and 9.97 on 200 unseen ones: on the edge of the band, where the
  first tuning pass with the package knobs spent the whole pass. The exploring run reads 10.03 and 10.83.
- **The measurement.** `score-content` on content `4ab506fa`, under the new objective: **8.35** on the benchmark
  seeds and **10.76** on the 200 from 995317, the same as under the old one, since both runs sit inside the band
  there. The objective is still a different one, the seventh the `score` note lists, so a score taken under it
  compares only with scores taken under it.
