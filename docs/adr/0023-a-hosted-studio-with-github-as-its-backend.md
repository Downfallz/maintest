# 0023. A hosted studio, with GitHub as its backend

Date: 2026-09-10
Status: Accepted

## Context

[ADR 0015](0015-content-studio.md) bound the studio to the loopback address on the reasoning that it is an
authoring tool for the machine it runs on, not a service. That reasoning holds for what it protects: the API
writes files, deletes them, and takes an agent spec like `heuristic:<path>` that the engine then reads, so
publishing it as it stands would be a write-and-read primitive on the internet. But the maintainer authors
from a phone most of the time, and a tool that needs a laptop left running is a tool that is not there when
the idea is. Three facts make a different shape possible now: the repository is **public**, so reading the
content needs no credential at all; `studio.js` funnels all nine API calls through a single function, so the
backend is one seam and not a rewrite; and the long-running work already runs on the CI runners
(`tune.yml`, `search.yml`, `iterate.yml`, `evaluate.yml`).

## Decision

We will give the studio a second, **hosted** mode: the same page, served as a static site from GitHub Pages,
with GitHub itself as the backend. It reads the authored content straight from the public repository with no
token; it writes through the Git Trees API, so a save is one commit on a branch and a pull request rather than
a file on a disk; and the panels that need the engine either dispatch a workflow and read the run, or read
what CI publishes alongside the page. Authentication is a **fine-grained personal access token**, scoped to
this one repository with contents and actions write, that the maintainer pastes once and the page keeps in
`localStorage`; it is sent to `api.github.com` and nowhere else. The local studio keeps its loopback host, its
direct file writes and its in-process engine, unchanged: the hosted mode is a second implementation of the
same nine calls, not a replacement.

## Consequences

- Good: authoring from anywhere, with nothing running anywhere. No server to operate, no port to expose, no
  tunnel to keep alive, and the dangerous API of ADR 0015 stays on the loopback address where it was put.
- Good: every hosted edit becomes a commit, a branch and a pull request, so a content change gets a diff, a
  review and the full CI gate. The local studio writes straight to the working tree; the hosted one cannot
  skip the checks even by accident.
- Good: it fixes a real defect on the way. "Save as next version" writes the new spell file and repoints the
  alias in **two** API calls today, so a failure between them leaves the content inconsistent. One Trees
  commit makes it atomic.
- Bad: **a standing credential in a browser.** `localStorage` on a shared or compromised device is a
  repository write, and any script that ever runs on the page can read it. What bounds it: the page is served
  from GitHub Pages with no third-party script, the token is fine-grained to one repository rather than a
  classic all-repositories one, it carries an expiry, and it is revocable in one click. What does not remove
  it: any of that. This is the consequence we are accepting, not one we are arguing away.
- Bad: validation leaves the process. `ContentStore` validates a document against the same DTOs the data
  builder uses, so "valid content" has one definition; a page in a browser cannot run that. CI becomes the
  authority instead, at a two-minute round trip rather than instant feedback.
- Bad: two implementations of the same nine calls, which can drift. The seam is one function and the field
  lists are already shared, but nothing enforces that both backends answer alike.
- Bad: a save is a commit, so the hosted studio is slower per edit than the local one and its history is
  noisier. Batching several edits into one commit is a later refinement, not part of this decision.
- Neutral: the local studio is untouched, and stays the fast path when the engine is at hand.
- Neutral: Pages must be enabled on the repository, and CI gains a step that publishes the page with the
  derived files it reads.

## Alternatives considered

- Expose the existing host beyond the loopback address: it needs authentication, HTTPS, and confinement of the
  agent-spec paths the run API reads, all to end up operating a service that must also be running. It reverses
  ADR 0015 for a worse result than not reversing it.
- A tunnel (Tailscale, Cloudflare) in front of the local studio: zero code and genuinely good, but the machine
  must be on, which is the thing being solved. It stays the right answer for a session at a desk.
- OAuth device flow instead of a token: better, and **deferred rather than rejected**. It needs a backend to
  hold the client secret, which trades the browser credential for a service to operate. The page reaches the
  same API either way, so moving to it later replaces how a token is obtained and nothing else.
- A read-only companion: cheap and safe, and it answers a different question. The ask is to author.
- A committed token, or a classic personal access token: neither. A secret in the repository is a secret
  published, and a classic token is every repository the account can reach.

## Follow-up

- `studio/studio.js`: the `call()` seam becomes an interface with a local backend and a GitHub backend.
- `studio/README.md` and `AGENTS.md`: the two modes, and what each one can do.
- `.github/workflows/`: publishing the page, and the derived files it reads (the consolidated content, the
  audit, the weights) alongside it.
- ADR 0015 keeps its decision for the local host; this ADR adds a mode rather than superseding it.
