# Arena release candidate — A8 acceptance record

Candidate based on `c2a1112b` (merged A7 #432). Date: 2026-11-04.
The arena remains a preview. The lobby defaults to Table; players may explicitly select Arena for this
browser. The Table link remains available in the arena, including renderer recovery.

## Repeatable gates

- `npm --prefix arena run build`: strict TypeScript, isolated bundle graph, asset budgets.
- `node --test studio/*.test.js table/*.test.js`: all pure presentation/transport tests.
- The five arena suites plus `arena-release.spec.js`: existing interactions, lifecycle, missing art and
  reduced motion, with added keyboard/focus, sound, rollout and recovery checks.
- `cd studio/browser && node node_modules/playwright/cli.js test --config arena.release.config.js`:
  complete normal 3v3 games, short round-cap games, recorded replay and two independent seats in desktop
  Chromium, Android touch emulation and iOS WebKit. Install `chromium webkit` first.
- CI artifact `arena-release`: screenshots, failure traces and `release-metrics.json` per profile.

The frame sample measures 89 requestAnimationFrame intervals on the loaded board. It is a diagnostic,
not a pass/fail FPS claim or a sustained GPU benchmark. Software rendering and CI load affect it.
`720×500` tests the CSS layout available at 200% zoom on a 1440×1000 desktop; it does not emulate every
browser's text-only zoom or assistive technology.

## Asset budgets and coverage

| Resource | Measured bytes at implementation | Enforced limit |
| --- | ---: | ---: |
| Both initial WebP images | 802,728 | 2,097,152 |
| Live JavaScript | 1,451,907 | 1,638,400 |
| Live JavaScript, gzip estimate | 395,277 | 524,288 |

The backdrop is 1672×941 and the sheet 2172×724 pixels: roughly 12 MiB total at four bytes per pixel before GPU copies and other surfaces. The download budget does not bound GPU memory.

Rerun the build for the exact final bundle size. No remote fonts or audio downloads are needed.

| Surface | Current coverage | Release limitation |
| --- | --- | --- |
| Arena backdrop | One local WebP, 315,644 bytes | Provisional environment |
| Six creature slots | Three local forms, reused/mirrored; 487,084-byte sheet | Positional forms, not mutation-specific identity; ART-2 owns replacement |
| Missing art | Vector figure with DOM name, stats and legal hit target | Graphical detail is lost, legal input remains |
| Damage/heal/energy/condition/fizzle | Confirmed frame effects, text, optional synthesized cue | No final authored spell audio/art |

## Comparison with the table

Both views use the same host questions, options, catalogue and command endpoints. The arena completes
mutations, tempos, ties, actions, upkeep/aftermath review, concession and replay. It adds a viewport-bound
scene and explicit sound opt-in. The table retains account/invitation recovery, notification settings and
its richer reference surfaces. Choosing Arena does not transfer a seat, accept an invitation, acknowledge
a question or submit a decision; those actions keep their existing explicit boundaries.

## Local automated evidence

On 2026-11-04, the six arena browser suites passed 225 cases with six pre-existing duplicate-viewport
skips. All 836 JavaScript tests and 11 direct host route tests passed; the .NET solution built with zero
warnings/errors. The standard local .NET test/format launchers hit a named-pipe permission restriction;
CI runs the normal commands. Changed C# whitespace was checked separately.

The release suite additionally passed all 16 desktop Chromium/Android-profile cases locally, including
both complete-match declarations, recorded sessions and simultaneous seats. WebKit runs in CI; its native
libraries were unavailable in the local container. These remain emulated browsers, not physical phones.

Headless Chromium 153 in this container, with two test workers and an idle loaded board:

| CSS viewport | Sample median frame interval | Sample 95th percentile |
| --- | ---: | ---: |
| 320×740 | 16.7 ms | 33.3 ms |
| 390×844 | 16.7 ms | 33.4 ms |
| 1440×1000 | 49.9 ms | 50.1 ms |

The desktop sample shows why these measurements cannot certify device performance. It is a loaded
headless environment; the separate single-worker desktop sample measured 33.3/50.1 ms. Reproduce the
release suite on target hardware and retain its JSON before changing the default. No FPS guarantee is made.

## Physical-device release gate — still pending

| Environment | Required exercise | Evidence needed |
| --- | --- | --- |
| Android Chrome, actual phone | Full match, portrait/landscape, browser bars, hide/return, sound on/off, renderer recovery | Device/OS/browser, screenshots, warm frame-time sample and any thermal/input problems |
| iOS Safari, actual phone | Same full match plus audio gesture/background resume and safe-area controls | Device/OS/browser and same evidence |
| Desktop with keyboard and screen reader | Full choice/target/confirm flow, dialogs, result/replay, 200% zoom | Browser/AT versions, focus and announcement findings |

Use a normal twelve-round 3v3, exercise late mutations and a defeated creature, then read its recording.
Repeat the same workflow in Table and confirm the host outcomes and legal choices agree. Keep all primary
controls visible and tappable; hiding overflow alone is not acceptance. If a candidate fails, select Table
in the lobby and use the retained Table link; no deployment or data migration is needed.

Do not promote the arena to the default until these physical-device checks and final art review are recorded.

## Captures from the real host

[Desktop presentation settings](images/arena-release/desktop-settings.jpg) ·
[Android-profile settings](images/arena-release/phone-settings.jpg)
