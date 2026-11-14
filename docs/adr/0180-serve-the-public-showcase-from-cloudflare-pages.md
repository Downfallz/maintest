# 0180. Serve the public showcase from Cloudflare Pages

Date: 2026-11-13

Status: Proposed. Completes the apex decision left open in [0175](0175-serve-the-table-at-its-own-domain-behind-cloudflare.md).

## Context

New visitors want to see what a creature can become and how a duel plays. A player following an invitation
wants their seat. The domains give each journey its own starting point.

## Decision

Cloudflare Pages serves the static public site at the apex. The repository holds both publications so a change
to a mutation's name or artwork can be built from the same catalogue. The site carries local copies of only
the assets it uses. English and French are separate HTML documents, with illustrated content and Play links
available before JavaScript runs.

Azure Container Apps continues to serve the game at play.downfallinstinct.com. Play enters the existing lobby.
The lobby offers sign-in to a guest and the dashboard to a player; invitations keep their original destination.
The two sites link using ordinary navigation. Accounts, APIs and installed game services belong to the game origin.

## Alternatives and rollout

Azure Static Web Apps can serve the same output. Cloudflare Pages uses the DNS provider already responsible
for the apex and its Git integration builds the showcase independently of the container image.
See the [implementation and rollout plan](../../showcase/README.md).
