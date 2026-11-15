# 2026-09-16. Three seeds of the same configuration disagree by 56 points, so most of this week is a sample

`ci-91` took the third dataset draw, seed 3, chosen in advance and not for its score. With `ci-88` (seed 1)
and `ci-90` (seed 2) that makes **three runs of one configuration** — lambda 0.9, the ADR 0048 baseline,
`search-4` as teacher and baseline, 1000 matches — differing only in which matches were recorded. The
evaluation is the fixed benchmark seeds every time.

| at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) |
| --- | --- | --- | --- |
| value against `search-4` | **0.6625** | **0.0975** | 0.30375 |
| value against `Greedy` | 0.0325 | 0.15375 | 0.03375 |
| value against `Random` | 0.6925 | 0.6575 | 0.79 |
| clone against `Greedy` | 0.725 | 0.79875 | **0.82125** |
| clone against `search-4` | 0.5325 | **0.5775** | 0.52 |
| clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **−0.42375** |

- **The spread from the seed alone is 56 points**, and every effect this project has measured is smaller
  than that. Lambda, the baseline, the teacher — each was one seed against one seed. **`ci-88` beating
  `search-4`, which I reported last night as the first agent ever to do it, is inside this spread and is not
  a result.** ADR 0049 is the proposal that follows from it.

- **This also takes back a headline I gave the user.** ADR 0048's win-rate claim — 0.10125 to 0.29625
  against `Greedy` from fixing the baseline — is a seed-1 sample. The *fit* improvement is real and
  held-out: `baselineR2` 0.0705 to 0.1053, rounds 15-30 from −0.2179 to +0.0685, measured on data, not on
  matches. The **19-point win-rate gain is inside the seed spread** and I should not have called it a
  tripling. ADR 0048 is Accepted and immutable, so the correction lives in ADR 0049 and here.

- **The clone rows are the sharper lesson, because they contradict each other.** `ci-91`'s clone beats
  `Greedy` **0.82125** — the best any policy has managed, against the committed champion's 0.725 — and loses
  to that same champion head to head at **0.42375**, interval from 0.387. Being better against a third party
  does not make you better than the player you are replacing. The champion bar refused it, its second
  correct refusal in one night and its fourth overall.

- **And `ci-90`'s clone looks thinner now.** It cleared every bar, including the champion at 0.53625 with an
  interval from 0.51473. But `ci-91` shows the same quantity swinging to 0.42375 on a neighbouring draw, so
  a margin of 0.036 over one seed is not much to commit a model on. Nothing was committed, which is the
  right outcome for a reason that was not visible an hour ago.

- **What survives all three seeds.** The clone beats `Greedy` more at seeds 2 and 3 (0.79875, 0.82125) than
  at seed 1 (0.725), so seed 1 looks like the weak draw rather than seeds 2 and 3 being lucky — that one is
  consistent across two independent draws and is the only claim here with more than one seed behind it. And
  the value policy loses to `Greedy` on every seed, 0.0325, 0.15375, 0.03375, which is the clearest thing
  the loop has said all week: **at lambda 0.9 it is simply not a good player**, whatever it does to the
  teacher on any given draw.

- **What happens next is a change to the loop, not a new claim from it.** ADR 0049 proposes that a
  configuration be run on at least three seeds and reported as a spread, with the gate requiring every seed.
  The loop fires on every push to the pull request whatever `next.json` says, so that slot goes to **seed 4
  of the same configuration** — a fourth sample of the spread this entry is about, which is the one thing a
  single-seed turn can still usefully contribute. It is not a new experiment and no new knob moves.
