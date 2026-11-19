# The pages' craft: what the search found

Status: research notes, 2026-11-19. The evidence behind [the plan](ux-craft-plan.md). Stage UX1 turns these notes
into the pages' craft book and reads every link again against its entry.

Two searches of the web ran in the session that wrote the plan: one on the patterns of games (the first visit,
teaching, the home of an asynchronous game, the frame of a match on a phone), one on accessibility and on what
the web platform offers a static page. Each link below was opened by the search that cites it. The session that
wrote the plan did not open them a second time. A number given here is the source's, not ours.

What could not be opened is listed at the end and is cited nowhere.

## 1. The first visit and the sign-in

| Pattern | What the source says | Source |
| --- | --- | --- |
| Play before signing up | Lead a visitor through the product's value first; registration comes after. Twitter's flow that started with choices raised completed sign-ups by 29%. Not a game. | [Wroblewski, 2010](https://www.lukew.com/ff/entry.asp?1128) |
| No wall before the value shows | A wall that asks for an account before any content has a high cost for an uncertain gain, worst on a phone. Usability tests, no number. | [NN/g, login walls](https://www.nngroup.com/articles/login-walls/) |
| A guest, then the same account | Start on an anonymous identity and link the credential to it, so progress carries. Warn a guest that the account lives on one device. | [Firebase, 2023](https://firebase.blog/posts/2023/07/best-practices-for-anonymous-authentication) |
| Ask when the account earns something | Say what signing in gives before the form; label the button with what happens. | [web.dev, sign-in forms](https://web.dev/articles/sign-in-form-best-practices) |
| Google's button | The personalised button names a returning person and needs 200 px of width. One Tap, once closed, stays away two hours, then a day, a week, four weeks. | [Google Identity, button](https://developers.google.com/identity/gsi/web/guides/personalized-button), [features](https://developers.google.com/identity/gsi/web/guides/features) |
| The first screen belongs to the game | Start fast, in the game's own style, with no choice the player cannot yet understand. | [Apple, WWDC17 session 811](https://developer.apple.com/videos/play/wwdc2017/811/) |

## 2. Teaching

| Pattern | What the source says | Source |
| --- | --- | --- |
| Do, do not read | The player performs the action to learn it. A pointing hand teaches tapping the hand. | [Fan, GDC 2012](https://www.gamedeveloper.com/design/gdc-2012-10-tutorial-tips-from-i-plants-vs-zombies-i-creator-george-fan), WWDC17 811 |
| One concept at a time | List what must be learned, rank it, give each step one concept. Marvel Snap orders its card unlocks to teach one mechanic at a time. | [Acagamic, 2023](https://acagamic.com/blog/2023/game-onboarding-plan/), [Hagman, GDC 2023](https://mobilegamer.biz/second-dinner-reveals-the-secrets-of-marvel-snaps-onboarding-and-card-design/) |
| Eight words | At most eight words on screen at once. Brode says the same, and that Snap averaged 11. | Fan, [Brode, GDC 2023](https://mobilegamer.biz/second-dinners-ben-brode-reveals-marvel-snaps-recipe-for-success-literally/) |
| A hint when it is needed | Help shown in context raised play time by 16% and progress by 40% in the complex game of three, over 45,000 players. No effect in the two simple ones. | [Andersen et al., CHI 2012](https://grail.cs.washington.edu/projects/game-abtesting/chi2012/) |
| A tutorial pays where trying does not teach | The same study: tutorials raised play time by up to 29% in the complex game and did nothing in the simple ones. Restricting what the player may touch showed no gain. | Andersen et al. |
| Skip, and play again | Offer a skip; what can be skipped can be played again. | WWDC17 811 |
| A first win | Hearthstone sends a new player to practice matches first. "Little victories add a little joy, even if you lose." | [Hearthstone wiki](https://hearthstone.wiki.gg/wiki/New_player_experience), Brode |
| The why, and one decision first | Start with one unit and one decision; each opens more. Explain why a mechanic matters. | [Notes on GMTK](https://notes.hamatti.org/gaming/video-games/can-we-improve-tutorials-for-complex-games), Acagamic |
| Watch a newcomer | One tester of Total War Saga: Troy spent 40 minutes on the first turn, looking for how to end it. | [Troy, UX deep dive](https://gamedeveloper.com/production/deep-dive-evolving-the-ux-ui-of-i-a-total-war-saga-troy-i-using-player-feedback) |

## 3. The home of an asynchronous game

| Pattern | What the source says | Source |
| --- | --- | --- |
| One dominant action | Menus grow busy; the action that starts play stays the most dominant thing on the screen. | WWDC17 811 |
| "Your move" first | chess.com's 2022 home puts a badge on play when an opponent has moved, and marks friends online. | [chess.com, 2022](https://www.chess.com/news/view/chesscom-redesigns-homepage) |
| An empty state says, teaches and links | "Totally empty states cause confusion about how and whether the system is working." Link to the step that fills it. | [NN/g, empty states](https://www.nngroup.com/articles/empty-state-interface-design/) |
| Design the wait | A game of turns played apart is mostly waiting. Simultaneous orders suit it; give the one who waits something of their own. | [Johnson, 2009](https://www.gamedeveloper.com/game-platforms/analysis-asynchronicity-in-game-design) |
| A ladder of notices | Board Game Arena lets a player choose: a notice each turn, one notice until they come back, or a daily report. | [Board Game Arena](https://en.doc.boardgamearena.com/Turn_based_FAQ) |
| Ask for notices in context | Never ask on load: a refusal cannot be asked again. Ask behind the page's own question, when the gain is plain. | [web.dev, push permission](https://web.dev/articles/push-notifications-permissions-ux) |
| A menu that is a place | Hearthstone's team: "our game is UI". No measured result was found for ambient motion in a menu. | [Sakamoto, GDC 2015](https://gamedeveloper.com/design/video-designing-an-immersive-user-interface-for-i-hearthstone-i-) |

## 4. The frame of a match on a phone

| Pattern | What the source says | Source |
| --- | --- | --- |
| Target size | 24 CSS px is the floor of WCAG 2.2 AA, 44 the aim, about 1 cm of glass. Spacing does not rescue a small target. | [WCAG 2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html), [NN/g](https://www.nngroup.com/articles/touch-target-size/) |
| Where a thumb goes | People touch the centre most, fastest and most accurately. The author of the "thumb zone" maps asks readers to ignore the drawings. | [Hoober, 2017](https://www.uxmatters.com/mt/archives/2017/03/design-for-fingers-touch-and-people-part-1.php) |
| Clarity over cool | "We would sacrifice cool ideas for the sake of clarity every time." Show what each unit will do before it acts. | [Ma, Into the Breach](https://gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time-) |
| Show the effect | A small looping picture of the move is "a thousand times more effective" than its sentence. | Ma |
| Details on a tap | A tooltip does not exist on a touch screen. What matters is on the page; a long press is never the only way. | [NN/g, tooltips](https://www.nngroup.com/articles/tooltip-guidelines/) |
| Undo, then one confirmation | "Never use a warning when you mean undo." Confirm only what cannot be undone, and name the consequence on the button. | [Raskin, 2007](https://alistapart.com/article/neveruseawarning/), [NN/g](https://www.nngroup.com/articles/confirmation-dialog/) |
| The turn's end always in view | Troy hid End Turn behind its notices and stranded newcomers. | Troy |
| State on the screen, not in memory | "The more information is always available to players, the less there is to learn and remember." | [Hodent, 2019](https://celiahodent.com/understanding-the-success-of-fortnite-ux/) |
| Answer within 100 ms | 0.1 s feels instant, 1 s keeps the thought, 10 s keeps the attention. | [Nielsen, 1993](https://www.nngroup.com/articles/response-times-3-important-limits/), [RAIL](https://web.dev/articles/rail) |
| Juice the frequent actions | "Maximum output for minimum input." Put it on what a player does every turn, not on rare events. | [Jonasson and Purho](https://gamedeveloper.com/design/video-is-your-game-juicy-enough-), [Woods](https://garden.bradwoods.io/notes/design/juice) |

## 5. Motion

| Pattern | What the source says | Source |
| --- | --- | --- |
| Durations | About 100 ms for a small answer, 200 to 300 ms for a window, 500 ms starts to drag. An exit is shorter than an entry. Ease out to enter, ease in to leave. | [NN/g, 2020](https://www.nngroup.com/articles/animation-duration/), [Head, 2016](https://valhead.com/2016/05/05/how-fast-should-your-ui-animations-be/) |
| Tokens | Material 3 names its durations (short 50 to 200 ms, medium 250 to 400) and three curves: standard, one to enter, one to leave. | [Material Components](https://github.com/material-components/material-components-android/blob/master/docs/theming/Motion.md) |
| Transform and opacity only | Animating `top` and `left` dropped half the frames in web.dev's test; `transform` dropped about 1%. | [web.dev, animations](https://web.dev/articles/animations-guide) |
| Less motion is not no motion | The triggers are scale, spin, parallax and motion at the edge of sight. Remove those; a fade or a change of colour stays. | [WebKit](https://webkit.org/blog/7551/responsive-design-for-motion/), [Head](https://alistapart.com/article/designing-safer-web-animation-for-motion-sensitivity/), [web.dev](https://web.dev/articles/prefers-reduced-motion) |
| Parallax, sparingly | Most people are indifferent to it, it slows a page and a hand hides it on a phone. | [NN/g, parallax](https://www.nngroup.com/articles/parallax-usability/) |
| A purpose for each | "Microinteractions shouldn't be fluffy and should always serve a purpose." | [NN/g](https://www.nngroup.com/articles/microinteractions/) |

## 6. Pictures and speed

| Pattern | What the source says | Source |
| --- | --- | --- |
| The largest picture is the LCP | Put it in the HTML as an `<img>`, never lazy, never a CSS background, with `fetchpriority="high"`. | [web.dev, LCP](https://web.dev/articles/optimize-lcp) |
| The thresholds | At the 75th percentile: LCP good to 2.5 s, poor past 4.0 s. INP good to 200 ms. CLS good to 0.1, poor past 0.25. | [web.dev, vitals](https://web.dev/articles/defining-core-web-vitals-thresholds) |
| The right size | AVIF, then WebP. Width and height on every picture. Little gain past twice the screen's density. | [web.dev, images](https://web.dev/learn/performance/image-performance) |
| A skeleton, a spinner or a bar | Nothing under a second. A skeleton for a page under ten. A bar past ten: people shown one waited about three times longer. | [NN/g, skeletons](https://www.nngroup.com/articles/skeleton-screens/), [progress](https://www.nngroup.com/articles/progress-indicators/) |
| A creature at rest on a menu | Animated WebP plays everywhere and keeps its alpha. A video is smaller and has none. A strip of frames moved by `steps()` is the one form that can be paused. The last line is the search's judgment. | [MDN, image types](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types) |

## 7. Accessibility: the standard

WCAG 2.2 is a W3C Recommendation. The criteria that bear on this game, each at level A or AA unless said:

| Criterion | What it asks here |
| --- | --- |
| 1.4.1 Use of colour | A team, a selection, a reachable target and a disabled control carry a shape, a mark or a word beside the colour. |
| 1.4.3, 1.4.11 Contrast | Text 4.5:1, large text 3:1, and 3:1 for the edge of a control, a focus ring and a meaningful graphic such as a health bar. |
| 1.4.10 Reflow | No scrolling in two directions at 320 px. The board is exempt; the frame around it and the windows are not. |
| 1.4.12 Text spacing | Nothing clips at a line height of 1.5 and a letter spacing of 0.12 em. |
| 2.2.1 Timing | A limit can be turned off or extended. A limit that binds both players of a live game is exempt. |
| 2.2.2 Pause, stop, hide | What moves by itself for more than five seconds beside other content can be paused, stopped or hidden. |
| 2.3.1 Three flashes | Nothing flashes more than three times in a second. |
| 2.3.3 Animation from interactions (AAA) | Motion an action starts can be turned off. |
| 2.4.11 Focus not obscured | A focused control is never wholly under a fixed bar or a toast. |
| 2.5.7 Dragging | Every drag has a way with single taps. |
| 2.5.8 Target size | 24 by 24 CSS px, or spaced so that circles of 24 do not meet. |
| 3.2.6 Consistent help | Help and the rules sit in the same place on every page. |
| 4.1.3 Status messages | A turn that changes, a result and an error reach a screen reader without moving the focus. |

Sources: [what is new in 2.2](https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/),
[the Recommendation](https://www.w3.org/TR/WCAG22/).

APCA, the contrast method once proposed for WCAG 3, is in no standard. The working draft of September 2026 says the
method is not chosen. Hold to the WCAG 2 ratios
([Roselli, 2026](https://adrianroselli.com/2026/04/wcag3-contrast-as-of-april-2026.html)).

## 8. Accessibility: what games add

| Guidance | What applies to a game of turns | Source |
| --- | --- | --- |
| Game Accessibility Guidelines | Basic: no fact by colour or by sound alone, large spaced touch controls, a readable default size, prompts at the player's pace, settings kept. Intermediate: reminders of the objective during play, background movement that can be turned off, a contrast option, a screen reader on a phone. Advanced: a text size setting, portrait and landscape, no sudden movement. | [The full list](https://gameaccessibilityguidelines.com/full-list/) |
| Xbox guideline 101, text | On a phone, 18 px at 100 DPI as the least, scaling with density. Text grows to 200%. The platform's zoom is not an answer. | [XAG 101](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101) |
| Xbox guideline 103, a second channel | Every cue the eye needs has a second sense. A greyed control carries a second mark. Haptics accompany another cue. | [XAG 103](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/103) |
| Xbox guideline 117, distractions | No camera shake without a switch. A way to stop what moves behind text. | [XAG 117](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117) |
| Xbox guideline 118, photosensitivity | A flash is a change of 10% in luminance; more than three a second over a fifth of the screen fails. | [XAG 118](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/118) |
| AbleGamers' patterns | Second Channel, Clear Text, Undo Redo, Slow It Down, Training Ground, Total Recall. | [Accessible Player Experiences](https://accessible.games/accessible-player-experiences/) |

## 9. A canvas and a screen reader

- Figma keeps a tree of what its canvas shows and renders it as real elements over the canvas, so a screen reader, a
  magnifier and voice control all reach it
  ([Figma](https://www.figma.com/blog/building-accessibility-into-a-canvas-based-product/)).
- lichess, read in its source: the board is a table with one button a square, keys that say the last move, the
  clocks and the legal moves, and one live region for what happens
  ([render.ts](https://raw.githubusercontent.com/lichess-org/lila/master/ui/lib/src/nvui/render.ts),
  [notify.ts](https://raw.githubusercontent.com/lichess-org/lila/master/ui/lib/src/nvui/notify.ts)).
- `role=log` suits a turn's events, `role=status` whose turn it is, `role=alert` an error. The region is in the
  first HTML, empty. One resolved action is one sentence
  ([MDN, live regions](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions)).
- A native `<dialog>` holds the focus and closes on Escape. Whether the focus returns to what opened it is to be
  tested ([MDN, dialog](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog)).

## 10. Testing

| Tool | What it catches and misses | Source |
| --- | --- | --- |
| axe in Playwright | Names, roles, labels, the contrast of text in the DOM. Nothing inside a canvas. The target-size rule needs the `wcag22aa` tag. Scan each state, a window open included. | [Playwright](https://playwright.dev/docs/accessibility-testing) |
| Lighthouse | A weighted subset of axe. It adds nothing to axe in Playwright. | [Lighthouse scoring](https://developer.chrome.com/docs/lighthouse/accessibility/scoring) |
| What automation finds | Deque: 57% of the issues by count in 13,000 pages, over 16 of 50 criteria. The UK's GDS, 2017: the best of ten tools found 37% of 143 planted barriers. Both on pages, not on a canvas. | [Deque](https://www.deque.com/automated-accessibility-coverage-report/), [GDS](https://accessibility.blog.gov.uk/2017/02/24/what-we-found-when-we-tested-tools-on-the-worlds-least-accessible-webpage/) |
| What Playwright can pretend | Less motion, forced colours, more contrast, a colour scheme. Not less transparency. | [emulateMedia](https://playwright.dev/docs/api/class-page#page-emulate-media) |

What stays for a person: a session with VoiceOver and with TalkBack. Live regions behave differently in each.

## 11. What the platform offers a static page

The first version that supports each, as caniuse gave it in November 2026.

| Feature | Chromium | Safari, iOS | Firefox | The trap |
| --- | --- | --- | --- | --- |
| View transitions inside a page | 111 | 18.0 | 144 | One at a time; each named part is unique or the transition is skipped. |
| View transitions between two pages (`@view-transition`) | 126 | 18.2 | 144, in part | Same origin only. Chrome skips it past four seconds. |
| `@starting-style` and `allow-discrete`: a window that enters and leaves | 117 | 17.4 | 129 | For transitions, not keyframes. The backdrop needs its own block. |
| Popover | 114 | 17.0 | 125 | Whole on iOS from 18.3. |
| `linear()`: a spring as an easing | 113 | 17.2 | 112 | Declare a `cubic-bezier` first. |
| `::details-content` | 131 | 18.4 | 143 | |
| Scroll-driven animation | 115 | 26 | 159 | New in Firefox: decoration only, behind `@supports`. |
| Anchor positioning | 125 | 27 | 147 | Too recent on iOS: keep a place without it. |
| Speculation rules (a page loaded ahead) | 109 | none | none | The page's script runs before it is seen. |
| `navigator.vibrate` | yes | none | none on Android | A second channel on Android only. |
| `setAppBadge` | desktop 81 | iOS 16.4, installed | none | Not Chrome on Android. |
| AVIF, `fetchpriority`, `loading="lazy"` | 85, 101, 77 | 16.4, 17.2, 16.4 | 93, 132, 121 | |

Sources: [caniuse](https://caniuse.com/view-transitions),
[Chrome, between pages](https://developer.chrome.com/docs/web-platform/view-transitions/cross-document),
[MDN, `@starting-style`](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@starting-style),
[Chrome, `linear()`](https://developer.chrome.com/docs/css-ui/css-linear-easing-function),
[MDN, vibrate](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/vibrate).

## What is contested or thin

- No published test compares a guest with a sign-in wall in a game. The two numbers found are not from games, and
  one of them, "about 20% leave at a forced sign-up", names no source.
- The CHI 2012 study is on three Flash games, not on a tactics game on a phone.
- A bot that poses as a person costs trust. Label it. This game does.
- Screen shake comes from action games. In a game of turns it is a cost to some players, behind a switch.
- Whether a skeleton helps rests on one study, and a shimmer can distract.
- Nothing measured was found on ambient motion in a menu.
- No opened source says "replace movement by a fade" in those words: the Chrome and WebKit pages say less motion is
  not no motion, and to serve a simpler animation.

## What could not be opened

The Material Design 2 and 3 motion pages and Apple's page on motion, which render by script. lichess's guide to its
blind mode and its lessons (403). The talks themselves: write-ups were read, not the videos. Celia Hodent's book.
Nothing was found on the home screens of Words With Friends, Polytopia or Marvel Snap. The costs of
`backdrop-filter` and of a large shadow have no primary source here. Lottie and Rive were not read: each is a
runtime the pages' rule against a dependency excludes.
