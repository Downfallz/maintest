# code-reviewer lessons (branch claude/stage-promotion-plan-h3mad9)

- "No encoder's version written" in an ffmpeg/libx264 MP4 is false: `+bitexact` drops Lavf's tag, but x264 still writes its SEI ("x264 - core 164 ... options:") and "Lavc libx264"; run `strings` on the output before trusting a reproducibility claim (promo P2, encodeFilm, 7449f20).
- A workflow whose environment admits only `main` cannot run before merge: ask for it to be called unproven, not "built" (promo P2 promo-film.yml, 7449f20; like #892).
- An account-scope Blob Data Contributor (API, worker, tend) reaches every container added later: read "the processes never see it" against the account-scope roles (promo P2, ADR 0206, 7449f20).
