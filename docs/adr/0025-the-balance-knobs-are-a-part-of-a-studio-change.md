# 0025. The balance knobs are a part of a studio change, like the alias map

Date: 2026-09-11

Status: Accepted

## Context

`data/balance/knobs.json` says what a tuning pass may move about each spell, between which bounds, and — the
part no number can say — what the spell is for ([ADR 0021](0021-tune-the-catalogue-with-a-declared-search-space.md)).
The studio reads it today and writes none of it: the views show a spell's intent, its invariants and every knob
against the value the content carries, and the catalogue carries the file through whole so a page anywhere can
read it.

Writing it is the next step, and the shape of that write is not obvious, because the knobs file is not one more
authored document. It is **one file describing every spell**, keyed by unversioned alias, that `check-knobs`
validates against the content as a pair. Three things follow from that pairing, and each one is a way for a
studio session to leave the repository failing:

- `validate` requires an entry, with an intent, for **every enabled spell**. Creating a spell in the studio and
  saving it alone leaves the build green and `check-knobs` red.
- An entry naming a spell no alias resolves to is equally a failure. Deleting a spell without taking its entry
  leaves exactly that.
- `startingKitOffersAChoice` names three aliases by hand. Deleting one of those spells does not fail the
  file — it makes the constraint check nothing, silently, which is worse.

None of these is hypothetical. They are the three things an author does most in the studio: add a spell, cut a
spell, cut a version.

The repository already has a file in precisely this position. `aliases.json` is one file, written whole, that
must change *in the same commit* as the documents it points at — an alias pointing at a document that is not
written yet, or at one that is gone, does not build. `backend.js` already says so, and a change is already
shaped around it:

```js
change({ kind, write: [...], remove: [...], aliases })
```

## Decision

The balance knobs become **a fourth part of a studio change**, alongside the documents written, the paths
removed, and the alias map:

```js
change({ kind, write: [...], remove: [...], aliases, balance })
```

`balance` is the whole knobs document, written whole, exactly as `aliases` is the whole alias map. A change
carrying it writes `data/balance/knobs.json` as part of the same unit of work: one commit on a hosted backend,
and on the local host one more request in the order that keeps the content readable in between.

What this buys is that the three failures above stop being things an author has to remember:

- creating a spell writes its file, points its alias, **and** seeds its entry, in one change;
- deleting a spell removes its file, prunes its alias, **and** prunes its entry;
- cutting a `:v2` repoints the alias and the entry follows it, because the entry is keyed by the unversioned
  alias and that is the key that moved.

The page validates what a browser can: a pointer that addresses a number in the spell as it stands, bounds the
right way round, a step that moves something, no duplicate pointer, an intent that is not empty. It does not
validate what it cannot — dominance, indistinguishable spells, tiers, the objective's score — and does not
pretend to. `check-knobs` in CI stays the authority, the same division [ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md)
already set for content: a hosted save answers with the commit and the pull request to watch, not with a
verdict.

## Consequences

A studio session can no longer leave `check-knobs` failing through the three doors above. That is the point.

The knobs file is written whole on every change that touches it, so two authors editing different spells in
two hosted sessions will conflict on that file where they would not have conflicted on their spells. This is
the same exposure `aliases.json` already carries and is accepted for the same reason: the file is small, the
conflict is a text conflict in a pull request, and the alternative — writing only the entry that changed —
cannot be expressed through the Git Trees API without reading, patching and racing.

A part a change does not touch is omitted entirely rather than sent unchanged, and that holds in both
directions. Editing a creature or a tree sends no `balance`, so it does not touch `data/balance/` and does not
show up in its history; widening a band on its own sends no `write`, so it does not rewrite the spell's file
with the bytes already in it. The second half is what makes a balance pass reviewable — a commit that says
`Studio: save the balance knobs` and carries one file.

The local host gains one route for the knobs and `ContentStore` one writer. That writer validates the document
is an object and nothing further: the shape belongs to `check-knobs` and to `balance.js`, and a DTO in the
engine would be a third definition of it, free to drift from both — the same reasoning that made the read a
passthrough.

Nothing here touches the content hash. The data builder reads `Creatures`, `Spells`, `TalentTrees` and
`aliases.json` only, which is what lets the knobs be retuned without invalidating a benchmark digest, and a
test holds that open.

## Alternatives considered

**A route of its own, called on its own.** Simplest to write, and wrong for the same reason a separate alias
route would be: the page would make two calls, either could fail, and a spell created without its entry — or an
entry seeded for a spell whose write failed — is exactly the state this ADR exists to prevent. On a hosted
backend it would also be two commits and two pull requests for one edit.

**Generating the entry from the spell.** An intent cannot be generated. That is the whole argument of ADR 0021:
the loop measures everything a balance pass needs except what a spell is *for*, and a search that only chases
the metrics will happily make every spell the same spell. A seeded entry can carry the name and the class,
because those are in the document; the intent has to be typed by a person, and the studio asks for it.

**Letting the studio write nothing and leaving the file to a text editor.** What we have today. It keeps the
file out of the studio's blast radius, and it is why `check-knobs` can fail after a session that looked
successful. The knobs are authoring metadata, and the studio is where authoring happens.
