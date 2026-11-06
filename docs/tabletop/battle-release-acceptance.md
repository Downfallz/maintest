# Battle release candidate — A8 acceptance record

Candidate first prepared on `c2a1112b` (merged A7 #432) on 2026-11-04, and reconciled on 2026-11-05 with
main `53af584a`, after AU1–AU14 (#440–#464) and ART-2–ART-6 (#436–#450). The battle view remains a preview.
The lobby defaults to Table; players may explicitly select Battle for this browser. The Table link remains
available in the battle view, including renderer recovery.

The reconciliation drops A8's own synthesized sound: ART-6's opt-in playback sound
([ADR 0134](../adr/0134-polish-confirmed-playback-with-opt-in-sound.md)) covers it, and a second engine
would put two sound switches in one settings dialog. The rest of A8 stands, as
[ADR 0136](../adr/0136-gate-the-arena-release-behind-a-reversible-preference.md) records.

## Repeatable gates

- `npm --prefix battle run build`: strict TypeScript, isolated bundle graph, asset budgets.
- `node --test studio/*.test.js table/*.test.js`: all pure presentation/transport tests.
- The battle view suites listed in AGENTS.md, `battle-release.spec.js` among them: interactions, lifecycle,
  missing art, reduced motion, keyboard/focus, rollout and recovery.
- `cd studio/browser && node node_modules/playwright/cli.js test --config battle.release.config.js`:
  complete normal 3v3 games, short round-cap games, recorded replay and two independent seats in desktop
  Chromium, Android touch emulation and iOS WebKit, with sound enabled. Install `chromium webkit` first.
- CI artifact `battle-release`: screenshots, failure traces and `release-metrics.json` per profile.

The frame sample measures 89 requestAnimationFrame intervals on the loaded board. It is a diagnostic,
not a pass/fail FPS claim or a sustained GPU benchmark. Software rendering and CI load affect it.
`720×500` tests the CSS layout available at 200% zoom on a 1440×1000 desktop; it does not emulate every
browser's text-only zoom or assistive technology.

## Asset budgets and coverage

| Resource | Measured bytes on main `53af584a` | Enforced limit |
| --- | ---: | ---: |
| Art a visit loads (background and four creature bodies) | 1,528,370 | 2,097,152 |
| Live JavaScript | 1,556,221 | 1,638,400 |
| Live JavaScript, gzip estimate | 430,283 | 524,288 |

The retired three-form sheet (`creatures.webp`, 487,084 bytes) is still served but no longer loaded, so the
budget does not count it. The live bundle uses 95% of its raw budget: the next large battle feature is
expected to cross it, and raising it is a decision to record here with the measurement that motivated it.
The download budget does not bound GPU memory. No remote fonts or audio downloads are needed.

| Surface | Current coverage | Release limitation |
| --- | --- | --- |
| Battle backdrop | One local WebP, 315,644 bytes | Provisional environment |
| Six creature slots | Four audited bodies chosen by acquired mutations (ADR 0130), bounded mutation cues (ADR 0133) | Final art review still open |
| Missing art | Vector figure with DOM name, stats and legal hit target | Graphical detail is lost, legal input remains |
| Confirmed spells | Visual vocabulary (ADR 0131), text, opt-in playback sound (ADR 0134) | No authored audio files |

## Comparison with the table

Both views use the same host questions, options, catalogue and command endpoints. The battle view completes
mutations, tempos, ties, actions, upkeep/aftermath, history, inspection, results, notes, notices and
replay; [the AU14 record](battle-ux-acceptance.md) lists the parity evidence feature by feature. Choosing
Battle does not transfer a seat, accept an invitation, acknowledge a question or submit a decision; those
actions keep their existing explicit boundaries. Invitations and account recovery stay at the table.

## Local automated evidence after reconciliation (2026-11-05, on main `53af584a`)

The release profile passed all 14 desktop Chromium and Android-profile cases locally in 9 minutes: both
complete-match declarations with sound enabled, recorded sessions, simultaneous seats, the lobby rollout,
renderer recovery, the keyboard/short-viewport check and the cold-scene sample. WebKit runs in CI; its
native libraries are unavailable in the local container. The cold scene fetched `live.js` and exactly the
five WebPs the scene loads. Sampled frame intervals on an idle loaded board, headless Chromium 153, one
worker: 1440×1000 desktop 66.7 ms median / 83.4 ms 95th percentile; 412×839 Android profile at DPR 2.625,
33.2 / 33.4 ms. As below, these are diagnostics of a software-rendered container, not device performance.

## Local automated evidence at first preparation (2026-11-04, on A7)

On 2026-11-04, the six battle browser suites passed 225 cases with six pre-existing duplicate-viewport
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

Do not promote the battle view to the default until these physical-device checks and final art review are recorded.

## Captures from the real host

[Desktop presentation settings](images/battle-release/desktop-settings.jpg) ·
[Android-profile settings](images/battle-release/phone-settings.jpg)
