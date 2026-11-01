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
| Tier `level` | Tier (Tier 2) | **Stage** (Stage 2) | **Stade** (Stade 2) |
| Evolution | Evolution | **Evolution** | **Évolution** |
| Speed (`Speed` choice) | Speed | **Tempo** | **Tempo** |
| `Speed.Quick` | Quick | **Instinct** | **Instinct** |
| `Speed.Standard` | Standard | **Precision** | **Précision** |
| A Creature's known Spells (the hand) | Spellbook | **Spellbook** | **Grimoire** |

Use the words naturally in a sentence rather than as literal substitutions:

| Before | After |
| --- | --- |
| Choose a package | Choose a mutation |
| Available packages | Available mutations |
| Owned packages | Acquired mutations |
| Buy a package | Acquire a mutation |
| Package acquired | Mutation acquired |
| Tier 2 | Stage 2 |
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

The table translates once, in [`table/terms.js`](../../table/terms.js): the tempo labels, the phase labels and
the stage label all come from there, so no page spells them out a second time. The content studio is an
authoring tool and keeps the content's own words (Tier, level) where they name the file being edited.

French is documented here and not yet shipped: the pages have no localization layer, and this change does
not introduce one.

## Unchanged names

Approved and already in place, not revisited here: the mutation and spell names of
[package-renaming-plan.md](package-renaming-plan.md) -- Brute, Predator, Warped, their evolution paths and
their spells -- and the starting spells **Focus** and **Strike**.

## Still undecided

These keep their current words until a separate naming discussion settles them:

Energy gain, Ongoing effects, Turn order resolution, Tie order, Intent selection, Activation, Cleanup,
Finalization, Battlefield, Round, Match, Round recap, Critical hit, Passive, Capstone.

Historical records -- ADRs, journal entries, frozen experiment records, `spells.md` and the two
`tier-evolution-*` documents -- keep the words they were written in.
