// What the studio needs from whatever holds the content (ADR 0023). The page never talks to a transport: it
// asks a backend for the catalogue, hands it a change, and asks it to build, play or audit. The backend below
// is the local studio host (ADR 0015); the hosted one will be GitHub itself, and what it answers with has to
// have these shapes.
//
// A **change** is one unit of work -- the documents it writes, the paths it removes, the alias map and the
// balance knobs it leaves behind -- rather than one request per file. It is shaped that way because an alias
// pointing at a document that is not there yet, or at one that is gone, does not build, and because a spell
// with no knobs entry fails `check-knobs` while a knobs entry for a spell nobody points at fails it too
// (ADR 0025): the four belong together. The local host spends a request on each part, in the order that keeps
// the content buildable in between; a hosted backend is expected to make the whole change one commit.
//
// Every operation answers with the result the page adopts, and refuses by throwing an Error whose `problems`
// are the lines to show under the message.
//
// There are three: the local host, the published page, and the published page with a token, which writes through
// GitHub and lives in `github.js` because it is the only one with a transport of its own to explain.
//
// Each backend takes the transport it talks through, defaulting to the page's `fetch` (ADR 0024). That is what
// makes them testable in Node: a test hands one a stub and asserts on the requests it would have made, which
// is where a write to GitHub can go wrong without failing loudly.

import { githubBackend, repositoryFromLocation, storedToken } from './github.js';

/** The one thing the page knows about failure: a message, and the problems that explain it. */
function refusal(message, problems) {
  const error = new Error(message);
  error.problems = problems ?? [];
  return error;
}

/** One request to the local host. A GET when there is no body, a POST when there is. */
async function request(transport, path, body) {
  const response = await transport(path, body === undefined
    ? { headers: { accept: 'application/json' } }
    : { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
  const payload = await response.json().catch(() => ({ ok: false, message: `${response.status} ${response.statusText}` }));
  if (!payload.ok) {
    throw refusal(payload.message || 'The studio refused that.', payload.problems);
  }

  return payload.result;
}

/**
 * The studio host of ADR 0015: the authored files on this machine, and the engine in the same process.
 * Reachable only while `dotnet run -- studio` is running, which is what a hosted backend is for.
 */
export function localBackend(transport = globalThis.fetch) {
  const call = (path, body) => request(transport, path, body);
  return {
    // Which of the two this is, for the one place the page draws something different (the launch sheet):
    // an engine to play with here, workflows to dispatch on the hosted page.
    kind: 'local',
    read: () => call('/api/catalogue'),

    // The parts of a change go in the order that leaves the content buildable between requests: documents
    // first, so an alias never points at a file that is not written yet, then removals, then the alias map,
    // which is what prunes the aliases a removal orphaned, and last the knobs, which name spells by the alias
    // the map has just settled.
    async change({ kind, write = [], remove = [], aliases, balance }) {
      let result = {};
      for (const document of write) {
        result = {
          ...result,
          ...await call('/api/documents', {
            kind,
            path: document.path,
            document: document.document,
            create: document.create === true,
          }),
        };
      }

      for (const path of remove) {
        result = { ...result, ...await call('/api/documents/delete', { kind, path }) };
      }

      if (aliases) {
        result = { ...result, ...await call('/api/aliases', { aliases }) };
      }

      if (balance) {
        result = { ...result, ...await call('/api/balance', { balance }) };
      }

      return result;
    },

    build: () => call('/api/build', {}),
    weights: () => call('/api/weights'),
    runs: () => call('/api/runs'),
    play: request => call('/api/runs', request),
    audit: () => call('/api/audit'),
  };
}

/**
 * The deployment that published this page, appended to every file the page fetches beside itself. The publish
 * step rewrites it (`.github/workflows/pages.yml`); it stays empty everywhere else, because only the published
 * site serves these as static files a browser can hold on to. Without it a deployment that only moved `data/`
 * changes no URL at all, and a returning browser answers `read()` from the catalogue it cached last week.
 */
const DEPLOYMENT = '';

/** One published file next to the page. Relative, so it does not care what path the site is served under. */
async function published(transport, name) {
  const response = await transport(`data/${name}${DEPLOYMENT}`, { headers: { accept: 'application/json' } });
  if (!response.ok) {
    throw refusal(`Could not read data/${name} (${response.status}). The site publishes it on every push to main;`
      + ' a fresh deployment may still be running.');
  }

  return response.json();
}

/** What a hosted studio without a token cannot do: say so where the user tried, not where the page loaded. */
function readOnly(what) {
  return refusal(`${what} needs either the engine or a token, and this page has neither (ADR 0023). Paste a`
    + ' fine-grained token for this repository to save from here, run'
    + ' `dotnet run --project src/DownfallArena.Cli -- studio` for the version with an engine, or use the'
    + ' workflows under Actions.');
}

/**
 * The published page (ADR 0023): the content as `studio --export` wrote it, served as static files next to the
 * page. It reads what the engine knew when the site was last built and cannot change anything yet -- writing
 * is the next step of the ADR, and every operation that needs the engine refuses rather than pretending.
 */
export function hostedBackend(transport = globalThis.fetch) {
  return {
    kind: 'hosted',
    read: () => published(transport, 'catalogue.json'),
    audit: () => published(transport, 'audit.json'),
    weights: () => published(transport, 'weights.json'),

    // Not a refusal: a published page has played no runs of its own, and an empty list is what that is.
    runs: async () => [],

    change: async () => { throw readOnly('Saving'); },
    build: async () => { throw readOnly('Building'); },
    play: async () => { throw readOnly('Playing a match'); },
  };
}

/**
 * The backend for where this page is being served from, and what it has to write with. The local host binds the
 * loopback address and nothing else (ADR 0015), so "not loopback" is exactly "not the local studio"; away from
 * it, a stored token is what separates a page that can save from one that can only read (ADR 0023).
 */
export function backendForThisPage(transport = globalThis.fetch) {
  if (['127.0.0.1', 'localhost', '[::1]', '::1'].includes(globalThis.location?.hostname)) {
    return localBackend(transport);
  }

  const token = storedToken();
  const repository = repositoryFromLocation();
  return token && repository
    ? githubBackend({ transport, token, repository, published: name => published(transport, name) })
    : hostedBackend(transport);
}
