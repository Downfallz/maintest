// What the studio needs from whatever holds the content (ADR 0023). The page never talks to a transport: it
// asks a backend for the catalogue, hands it a change, and asks it to build, play or audit. The backend below
// is the local studio host (ADR 0015); the hosted one will be GitHub itself, and what it answers with has to
// have these shapes.
//
// A **change** is one unit of work -- the documents it writes, the paths it removes, and the alias map it
// leaves behind -- rather than one request per file. It is shaped that way because an alias pointing at a
// document that is not there yet, or at one that is gone, does not build: the two belong together. The local
// host spends a request on each part, in the order that keeps the content buildable in between; a hosted
// backend is expected to make the whole change one commit.
//
// Every operation answers with the result the page adopts, and refuses by throwing an Error whose `problems`
// are the lines to show under the message.

/** The one thing the page knows about failure: a message, and the problems that explain it. */
function refusal(message, problems) {
  const error = new Error(message);
  error.problems = problems ?? [];
  return error;
}

/** One request to the local host. A GET when there is no body, a POST when there is. */
async function call(path, body) {
  const response = await fetch(path, body === undefined
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
export function localBackend() {
  return {
    read: () => call('/api/catalogue'),

    // The parts of a change go in the order that leaves the content buildable between requests: documents
    // first, so an alias never points at a file that is not written yet, then removals, then the alias map,
    // which is what prunes the aliases a removal orphaned.
    async change({ kind, write = [], remove = [], aliases }) {
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

      return result;
    },

    build: () => call('/api/build', {}),
    weights: () => call('/api/weights'),
    runs: () => call('/api/runs'),
    play: request => call('/api/runs', request),
    audit: () => call('/api/audit'),
  };
}
