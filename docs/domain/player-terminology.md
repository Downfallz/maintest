# Player-facing terminology

The words a player reads at the table, in the rulebook and on the player aid, and the French a translation
will use. This page is the source of truth for **presentation** names. The domain's own words -- the ones in
[glossary.md](glossary.md), in the code and in every serialized contract -- are not renamed by it.

Approved direction: **Dark Fantasy Accessible**. Warlords command monstrous creatures that mutate during an
apocalyptic battle; the words carry that fantasy and stay short, plain and easy to learn.

## Approved names

| Internal (domain, code, data) | Legacy player-facing | Player-facing EN | Player-facing FR |
| --- | --- | --- | --- |
| Tier (`tier:...` ids, `data/Tiers/`) | Package | **Mutation** | **Mutation** |
| Tier prerequisite graph | Talent atlas | **Mutation Tree** | **Arbre des mutations** |
| Tier `level` | Tier (Tier 2) | **Stage** (Stage II) | **Stade** (Stade II) |
| Evolution | Evolution | **Evolution** | **Évolution** |
| Speed (`Speed` choice) | Speed | **Tempo** | **Tempo** |
| `Speed.Quick` | Quick | **Instinct** | **Instinct** |
| `Speed.Standard` | Standard | **Precision** | **Précision** |
| A Creature's known Spells (the hand) | Spellbook | **Spellbook** | **Grimoire** |
| The base creature definition (`creature:main:v1`, named `Main`) | Main | **Creature**, numbered by id (Creature 4) | **Créature** (Créature 4) |
| A game against another player, not a bot (the lobby's button, the Create window's tab) | PvP | **Multiplayer** | **Multijoueur** |

A Stage is written in Roman numerals, I to IV, in both languages: Stage III, Stade IV. The domain and the data
keep the number (`level: 3`); only what a player reads turns it into a numeral (`stageLabel` in `table/terms.js`). The studio is for
authors, who edit the `level` itself, so it keeps the number (Stage 3). What a screen reader speaks, an
announcement or an `aria-label`, also keeps the number (`stageSpoken`: Stage 1), since "Stage I" may be read
as the pronoun I (#769).

Use the words naturally in a sentence rather than as literal substitutions:

| Before | After |
| --- | --- |
| Choose a package | Choose a mutation |
| Available packages | Available mutations |
| Owned packages | Acquired mutations |
| Buy a package | Acquire a mutation |
| Package acquired | Mutation acquired |
| Tier 2 | Stage II |
| Choose speed | Choose tempo |
| Speeds revealed | Tempos revealed |
| Talent atlas | Mutation Tree |
| Your spellbook | Your spellbook |

## Round phases

The four phases of the domain's `RoundPhase` keep their internal names; the player reads these:

| Internal (`RoundPhase`) | Legacy player-facing | Player-facing EN | Player-facing FR |
| --- | --- | --- | --- |
| `StartOfRound` | Start | **Upkeep** | **Entretien** |
| `Planning` | Planning | **Command** | **Commandement** |
| `Combat` | Combat | **Combat** | **Combat** |
| `EndOfRound` | End | **Aftermath** | **Retombées** |

A round reads **Upkeep → Command → Combat → Aftermath**. Each step belongs to the phase the engine's
`RoundFlow` puts it in, and keeps its current name (Evolution, Tempo, Tie order, Spells, Activation, Cleanup):
the table shows a step under its phase, for example *Command · Tempo*, *Combat · Activation* or
*Aftermath · Cleanup*. Only the step the engine calls `Speed` is shown as Tempo.

| Phase | Steps a player sees |
| --- | --- |
| Upkeep | Energy, regeneration and damage over time are applied automatically. |
| Command | Evolution (on an evolution round), Tempo, Tie order. |
| Combat | Spells (Intent selection), then Activation: the timeline is walked slot by slot. |
| Aftermath | Cleanup: conditions count down, then the next round begins. |

## Tempo

Every creature picks a tempo each round. The rules are the engine's, unchanged:

- **Instinct** (internally `Quick`) acts before every Precision creature, and **cannot critically hit**.
- **Precision** (internally `Standard`) acts after every Instinct creature, and **keeps its critical-hit
  chance**.
- **Initiative** orders the creatures within each tempo group; a tie between the sides is settled by a d20
  roll-off, and a tie within one side by its owner's tie order.

Labels, tooltips, the timeline's group headings, action summaries and the critical-chance indicator all say
Instinct and Precision. A critical chance shown on a card is the Precision chance.

## Choosing spells

In the default combat declaration (`AtActivation`, ADR 0112) a creature **chooses its spell when its slot
comes up**, with its targets, and nothing is declared beforehand: the Spells step passes without asking
anyone. Only a table started with hidden intents (`HiddenIntent`) declares a spell per creature in that step,
before Activation. Player guidance keeps that distinction. The tabletop rulebook still teaches hidden
intents and says so in its opening box.

## Coming back to a game

The recap a player reads on coming back to a game (docs/tabletop/async-recap-plan.md):

| What the page shows | Player-facing EN | Player-facing FR |
| --- | --- | --- |
| Recap (`recap-dialog`) | **Where you are** | **Où vous en êtes** |
| Settle the board on the present | **Go to my turn** | **Aller à mon tour** |
| The same, in a match that ended while away | **See the result** | **Voir le résultat** |
| Play the unseen actions back one at a time | **Replay step by step** | **Revoir pas à pas** |
| The journal's mark where the Last visit ended | **You left here** | **Vous en étiez ici** |
| A line the seat was not shown | **New** | **Nouveau** |
| The recap's count of the two sides, never a lead (#757) | **Standing and health** | **Debout et santé** |
| One side has more creatures standing | **You have more creatures standing** (**Mara has**) | **Vous avez plus de créatures debout** (**Mara a**) |
| As many standing, one side has more health | **As many creatures standing; you have more health** | **Autant de créatures debout ; vous avez plus de santé** |
| Both sides match | **As many creatures standing, as much health** | **Autant de créatures debout, autant de santé** |
| The recap's line on a Your turn card, in the inbox and in the notice | **3 casts since your last visit · last move 5 h ago** (**1 cast** for one) | **3 sorts lancés depuis votre dernière visite · dernier coup il y a 5 h** (**1 sort lancé**) |

## Notifications

What a notification service answers is said as "notification service", never "push service", and its refusal as
"this table", never "host" (#1233). Settings, the seat page and the battle view say a refusal in one sentence, the
host's (`PushTest.UnknownServiceMessage`). The other sentences of the notification pages say "this table" and
"notification service" too (#1266). The pages' fallbacks are `push.js`'s (`UNREACHABLE`, `answeredWith`), which
Settings, the seat page and the battle view share; `table/terminology.test.js` refuses "host" and "push service" in
the string literals of `push.js`, `devices.js` and `battle-services.js`. The push routes' refusals are `PushTest`'s
constants. A page names its own control: Settings' switch is "Pop-ups on this device", the seat page's and the battle
view's button "Notify me when it's my turn". A sentence about the browser's feature says "notifications"; one about
what appears on the screen says "pop-ups", as Settings does.

| What the page shows | Player-facing EN | Player-facing FR |
| --- | --- | --- |
| A browser whose notification service the table does not send to | **This browser's notification service is not one this table can send to. Try another browser.** | **Le service de notifications de ce navigateur n'est pas de ceux auxquels cette table peut envoyer. Essayez un autre navigateur.** |
| The table could not be reached (Settings, the seat page's and the battle view's test) | **Could not reach this table. Try again.** | **Impossible de joindre cette table. Réessayez.** |
| The table refused a test without a sentence of its own | **This table answered 503.** (the status) | **Cette table a répondu 503.** |
| The body of the page's own test notice | **Shown by the page itself. A notification from this table should follow.** | **Affichée par la page elle-même. Une notification envoyée par cette table devrait suivre.** |
| A subscription its notification service let go of, refused again (Settings) | **This subscription is gone from its notification service. Turn "Pop-ups on this device" on again.** | **Cet abonnement n'existe plus chez son service de notifications. Réactivez « Fenêtres surgissantes sur cet appareil ».** |
| The same, on a seat | **This subscription is gone from its notification service. Tap "Notify me when it's my turn" again.** | **Cet abonnement n'existe plus chez son service de notifications. Touchez de nouveau « Me prévenir quand c'est mon tour ».** |
| A table given no push keys | **This table sends no notifications.** | **Cette table n'envoie pas de notifications.** |

Settings' ways to sign in name a way whose removal stopped before it finished (#1262):

| What the page shows | Player-facing EN | Player-facing FR |
| --- | --- | --- |
| The way's line | **Its removal did not finish. You no longer sign in with it.** | **Son retrait n'a pas abouti. Vous ne vous en servez plus pour vous connecter.** |
| Its button | **Finish removing** | **Terminer le retrait** |
| What the button asks first | **Finish removing Discord?** *You no longer sign in with Discord. This finishes its removal. You stay signed in here. Pop-ups stop on every device, this one too.* | **Terminer le retrait de Discord ?** *Vous ne vous connectez plus avec Discord. Ceci termine son retrait. Vous restez connecté ici. Les fenêtres surgissantes s'arrêtent sur tous les appareils, celui-ci compris.* |
| The line once done | **Discord removed.** | **Discord retiré.** |

## The ladder

The leaderboard and the rulebook's section on it (ADR 0124, `rules/05-reference.md`):

| What the page shows | Player-facing EN | Player-facing FR |
| --- | --- | --- |
| The leaderboard (`/leaderboard`) | **Ladder** | **Classement** |
| A player's Glicko-2 rating | **Rating** | **Cote** |
| The rating's deviation | **±** | **±** |
| A rating whose deviation is above the threshold | **Provisional** | **Provisoire** |
| A rating whose deviation is at or below it | **Settled** | **Établie** |
| A game that counts on the ladder (ADR 0155, ADR 0171) | **Ranked** | **Classée** (partie classée) |
| A game opened not to count | **Unranked** | **Hors classement** |

## Presentation and identifiers

These names are presentation only. Nothing below is renamed, and no game rule, balance value or simulation
behaviour moves with them:

- Domain types and enums: `Speed.Quick`, `Speed.Standard`, `RoundPhase.StartOfRound`, `Planning`, `Combat`,
  `EndOfRound`, `SubPhase`, `Tier`.
- Content ids and files: `tier:...:vN`, `data/Tiers/`, spell ids, the balance knobs and the content hash.
- Serialized values: commands (`{ "kind": "Speed", "speed": "Quick" }`), `waitingFor` values, DTOs, the HTTP
  API, traces, datasets and recorded sessions.
- CSS classes, element ids and test hooks of the static pages (`tab-mat`, `talent-*`, `speed-*`,
  `package-card`).

The table translates once, in [`table/terms.js`](../../table/terms.js): the tempo labels, the phase labels and the
stage label all come from there, so no page spells them out a second time. `table/terminology.test.js` reads the
tables of this page. It holds `terms.js` to the Player-facing EN column. It also fails when the table's pages, the
sentences of its scripts or the battle view use a legacy word that column replaced: Package, Talent atlas, Tier, PvP,
and Speed, Quick, Standard or Planning as a capitalised label; or a sentence the Before column lists, in any case.
The content studio is an authoring tool and keeps the content's own words (Tier, level) where they name the file
being edited.

French ships in one place: the official rulebook's French edition (`rules/fr/`, ADR 0193), whose build refuses
an English word where this page's FR column has another. The pages have no localization layer; `/rules` alone
reads in French.

## Unchanged names

Approved and already in place, not revisited here: the mutation and spell names of
[package-renaming-plan.md](package-renaming-plan.md) -- Brute, Predator, Warped, their evolution paths and
their spells -- and the starting spells **Focus** and **Strike**.

## Still undecided

These keep their current words until a separate naming discussion settles them:

Energy gain, Ongoing effects, Turn order resolution, Tie order, Intent selection, Activation, Cleanup,
Finalization, Battlefield, Round, Match, Round recap, Critical hit, Passive, Capstone.

Until then, the rulebook's French edition (`rules/fr/`, ADR 0193) writes them as: partie (Match), round,
Résolution de l'ordre des tours, Ordre des égalités, Activation, Nettoyage (Cleanup), Clôture (Finalization),
critique, passif, clé de voûte (Capstone); and, for words of its own, frise (timeline), départage (roll-off) and
brèche (sunder). A French text elsewhere uses the same words.

Historical records -- ADRs, journal entries, frozen experiment records, `spells.md` and the two
`tier-evolution-*` documents -- keep the words they were written in.
