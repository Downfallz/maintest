# 0137. Name the game Downfall Instinct

Status: Accepted.

## Context

The tabletop game, browser clients and development tools share a single product identity. Players
also choose between a classic table and an animated view of the same match.

## Decision

Use **Downfall Instinct** in English and French. Spell it as two words, without a subtitle separator.
Use **Battle view** for the animated client and **battlefield** for the area occupied by the teams.
Use the same title on the installed app, lobby, classic table, battle view, leaderboard, studio,
viewer, rulebook and project documentation. The [brand guide](../art/branding.md) owns the spelling,
wordmark and palette.

## Compatibility

The canonical routes are `/battle` and `/battle-lab`. The former routes redirect on the same host,
preserving the full query so saved seat links and invitations still work. A pending sign-in return
uses the canonical route. The client reads the previous view preference and writes the new value
when the player selects a view. Existing help and upkeep storage keys keep their original values.
The notification worker recognizes a match in either an old or a new tab during rollout.

Compiled .NET project identifiers, the frozen prototype, historical screenshots and accepted ADRs keep their
existing identity: ADRs 0123–0136 say "arena" and `/arena` because that was the name when they were decided,
and this ADR supersedes the name, not those decisions. Current client modules, build scripts and host routes use the new vocabulary.
