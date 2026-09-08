# Legacy prototypes

Everything under this folder predates the clean slate (see `docs/adr/0002-clean-slate-restart.md`).
It contains two or three overlapping generations of the engine, all work in progress, none of them buildable
as a whole. It is kept in the tree for one reason: it is the best record we have of the game design ideas
(phases, intents, speed resolution, conditions, talents, evolution) and of what did not work.

Rules:

- Read-only. Nothing here is built, tested, or maintained.
- Do not reference these projects from `src/` or `tests/`.
- When a concept from here is carried into the new domain, document it in `docs/domain/` and, if it is a
  decision, in an ADR. Then the legacy version stops mattering.

The most complete generation is `DownfallArena/DA.Game.Domain2` (a `Match` aggregate with rounds, planning
and combat phases, condition resolution, and talent unlocks). Start there if you are looking for prior art.

When nothing here is needed anymore, delete the folder. The full history stays in git (last legacy commit on
`main` before the restart: `185fe30`).
