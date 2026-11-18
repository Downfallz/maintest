# 0195. Open the rulebook in the language its address asks for

Date: 2026-11-18
Status: Accepted (settled by the owner on #732). Amends [0193](0193-write-the-rulebook-in-french-beside-the-english.md) (the language `/rules` opens in)
Rulebook: none

## Context

ADR 0193 opens `/rules` in the language stored on the device, else the browser's. The public French book
(`/fr/regles/`) links the table's `/rules`, but the showcase is another origin: it shares no storage with the
table. A French reader on a device that never opened the game, with a browser that prefers English, followed
the link and got the English book (#732).

## Decision

`/rules` opens in the language its address asks for (`?lang=fr` or `?lang=en`), else the one stored on the
device, else the browser's first English or French, else English. The public book links `/rules?lang=` with its
own language. A switch on the page, once its book is read, drops `lang` from the address, so a reload reads the
language chosen. The service worker keeps `/rules` by its path, so the address with `lang` reads the kept copy
offline (ADR 0191).

## Consequences

- Good: a link names its language, whatever the device stored.
- Neutral: the asked language is not stored; only a switch on the page is.

## Alternatives considered

- Store the asked language on arrival: a link would then change what every later visit opens.

## Follow-up

`table/rules.js` (`languageOf`, `askedLanguage`), `showcase/rules.mjs`, `table/README.md`,
`docs/reference/commands.md`.
