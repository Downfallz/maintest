# 0077 — A mobile field guide before the editor

- Status: Accepted
- Date: 2026-09-24

## Context

The studio is mainly consulted on a phone. Its opening screen showed authoring class trees, although
package prerequisites now govern progression (ADR 0057). Current packages and new tiers were present in
the hosted catalogue but hard to discover. Every selection immediately opened an editing form.

## Decision

Make consultation the default: Explore derives families and tier groups from authored package prerequisites;
Spells searches names, effect kinds and teaching packages. Package and spell cards link their prerequisites,
contents and successors. Preserve all prerequisites when a package has multiple parents. Disabled or broken
content stays accessible through Catalogue rather than being advertised as playable.

Use four persistent destinations, with secondary authoring and engine tools in a separate sheet. Opening
content shows a reader; editing is an explicit action. URL fragments identify documents, browser history
supports the reading journey, and navigating away from a dirty draft requires confirmation.

Keep native ES modules, CSS and the existing backend contract. Add pure catalogue helpers and a DOM reader
module, served by both the local route table and the Pages assembly with deployment cache stamps. No runtime
framework, package manager or asset build is needed. The engine remains authoritative for validation.

Add an isolated, pinned Playwright development dependency for browser verification at 320px, 390px and desktop
widths. Tests use actual static assets and fixtures from authored content, including the Pages subpath.
Screenshots and failure traces are CI artifacts. Existing install-free Node tests and .NET page contracts remain.

## Consequences

New packages appear through the same exported catalogue without separate UI metadata. A family groups all
packages reachable from one root; hybrid packages can appear in more than one family. This is a browsing aid,
not an eligibility evaluator. Cards always show complete prerequisites. Orphans remain visible as other paths.

Authoring takes an extra explicit tap. Existing local saves, hosted commits and validation behaviour are
unchanged. Browser dependencies are confined to tests; CI additionally checks navigation and responsive layout.
