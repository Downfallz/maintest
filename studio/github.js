// The hosted studio's writing half (ADR 0023): GitHub is the backend, so a save is a commit on a branch and a
// pull request rather than a file on a disk. Everything here is one seam -- `githubBackend()` answers the same
// shapes `backend.js` documents -- and every request goes to api.github.com and nowhere else.
//
// A change is one commit on purpose. The alias map and the documents it names have to land together: an alias
// pointing at a file that is not there does not build, and the local host spends a request per part only
// because it can keep the disk buildable in between. The Git Trees API has no such excuse.
//
// The transport is a parameter (ADR 0024), so the tests drive this with a stub and assert on the requests.

const API = 'https://api.github.com';

/** How the dispatch waits for its run to appear. A parameter, so a test can drive it without sleeping. */
const pause = milliseconds => new Promise(resolve => { setTimeout(resolve, milliseconds); });

/** Where the content lives in the repository. A path from the page is relative to the content root, not to it. */
const CONTENT_ROOT = 'data';
const ALIASES_FILE = `${CONTENT_ROOT}/aliases.json`;

/** The branch a hosted studio writes to, and the one pull request it keeps open. */
export const STUDIO_BRANCH = 'studio/content';

/** Where the token lives between visits: one key, one repository, revocable in one click on GitHub. */
export const TOKEN_KEY = 'downfall.studio.github-token';

function refusal(message, problems) {
  const error = new Error(message);
  error.problems = problems ?? [];
  return error;
}

/** The repository the page is served from: Pages gives a project site `<owner>.github.io/<repo>/`. */
export function repositoryFromLocation(location = globalThis.location) {
  const owner = /^([^.]+)\.github\.io$/.exec(location?.hostname ?? '')?.[1];
  const repo = (location?.pathname ?? '').split('/').find(Boolean);
  return owner && repo ? { owner, repo } : null;
}

/**
 * The token, or null. A browser with site data blocked throws on the `localStorage` *property itself*, not on
 * `getItem`, so reaching for it is inside the try: as a default parameter it would throw before the body ran,
 * the catch would never fire, and since the page asks for the token while choosing its backend, the whole
 * published studio would fail to load instead of falling back to reading.
 */
export function storedToken(store) {
  try {
    return (store ?? globalThis.localStorage)?.getItem(TOKEN_KEY) || null;
  } catch {
    return null;
  }
}

/** Keeps or forgets the token. Answers whether it stuck, so the page can say when a browser refuses to hold it. */
export function storeToken(token, store) {
  try {
    const held = store ?? globalThis.localStorage;
    if (token) {
      held?.setItem(TOKEN_KEY, token);
    } else {
      held?.removeItem(TOKEN_KEY);
    }

    return storedToken(held) === (token || null);
  } catch {
    return false;
  }
}

/**
 * Authored JSON as the repository holds it: two-space indent, keys in the order given, and a final newline. The
 * newline is the repository's convention and is what keeps a diff to the lines that changed.
 */
function asFile(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

/**
 * The aliases file is written sorted, so re-saving an unchanged map is not a diff -- and sorted the way the
 * local host sorts it, which is `StringComparer.Ordinal`. Not `localeCompare`: that orders by the reader's
 * collation, so it would put `spell:apple` before `spell:Apple` where ordinal does the opposite, and every
 * hosted save would reorder the whole file and disagree with what `ContentStore` writes.
 */
function aliasesFile(aliases) {
  const ordinal = (left, right) => (left < right ? -1 : Number(left > right));
  return asFile(Object.fromEntries(Object.keys(aliases).sort(ordinal).map(key => [key, aliases[key]])));
}

/**
 * The tree entries of one change. A written document is a blob with its content; a removal is the same path with
 * a null sha, which is how the Trees API spells "not in this tree any more".
 */
export function treeEntries({ write = [], remove = [], aliases }) {
  const entries = write.map(document => ({
    path: `${CONTENT_ROOT}/${document.path}`,
    mode: '100644',
    type: 'blob',
    content: asFile(document.document),
  }));

  for (const path of remove) {
    entries.push({ path: `${CONTENT_ROOT}/${path}`, mode: '100644', type: 'blob', sha: null });
  }

  if (aliases) {
    entries.push({ path: ALIASES_FILE, mode: '100644', type: 'blob', content: aliasesFile(aliases) });
  }

  return entries;
}

/** What one change says it did, as a commit subject. The body is the diff; the subject has to carry the intent. */
function subject({ kind, write = [], remove = [], aliases }) {
  const wrote = write.map(document => document.path);
  if (wrote.length === 1 && remove.length === 0) {
    return `Studio: save ${wrote[0]}${aliases ? ' and repoint its alias' : ''}`;
  }

  const parts = [wrote.length && `save ${wrote.length} file(s)`, remove.length && `remove ${remove.length}`, aliases && 'repoint aliases'];
  return `Studio: ${parts.filter(Boolean).join(', ')} in ${kind}`;
}

/**
 * GitHub as the studio's backend. It reads what the site published next to the page -- no token, no rate limit,
 * and the content as CI last built it -- and writes through the Git Trees API.
 *
 * Validation is not here and cannot be: `ContentStore` checks a document against the same DTOs the data builder
 * uses, and a page in a browser cannot run that. CI is the authority (ADR 0023), which is why a save answers
 * with where to watch it rather than with a rebuilt catalogue.
 */
export function githubBackend({ transport = globalThis.fetch, token, repository, branch = STUDIO_BRANCH, published }) {
  if (!repository) {
    throw refusal('This page is not served from a GitHub Pages project site, so it cannot tell which repository to write to.');
  }

  const { owner, repo } = repository;

  async function api(path, { method = 'GET', body } = {}) {
    const response = await transport(`${API}/repos/${owner}/${repo}${path}`, {
      method,
      headers: {
        accept: 'application/vnd.github+json',
        authorization: `Bearer ${token}`,
        'x-github-api-version': '2022-11-28',
        ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      // GitHub's own message is the useful one; its `errors` are the lines that explain it.
      throw refusal(
        payload?.message || `GitHub answered ${response.status} ${response.statusText}.`,
        (payload?.errors ?? []).map(problem => problem.message || `${problem.resource ?? ''} ${problem.field ?? ''} ${problem.code ?? ''}`.trim()));
    }

    return { status: response.status, payload };
  }

  /** Answers null for 404 rather than throwing, for the things that are legitimately not there yet. */
  const optional = error => {
    if (/not found/i.test(error.message)) return null;
    throw error;
  };

  /**
   * One file as the studio branch has it, or null. GitHub answers base64 with newlines in it, and the content is
   * UTF-8, so it is decoded through bytes rather than through `atob` alone, which would mangle anything not ASCII.
   */
  async function branchFile(path) {
    const found = await api(`/contents/${path}?ref=${encodeURIComponent(branch)}`).catch(optional);
    if (!found) {
      return null;
    }

    // `atob` answers one byte per character, so a code point and a code unit are the same number here; the
    // code-point form is the one that stays right if this ever decodes something that is not a byte string.
    const bytes = Uint8Array.from(atob(found.payload.content.replace(/\s/g, '')), character => character.codePointAt(0));
    return JSON.parse(new TextDecoder().decode(bytes));
  }

  /** Whether the branch already holds these paths, which is what a create-only write must not overwrite. */
  async function taken(paths) {
    const found = await Promise.all(paths.map(async path => (await api(`/contents/${CONTENT_ROOT}/${path}?ref=${encodeURIComponent(branch)}`).catch(optional)) && path));
    return found.filter(Boolean);
  }

  /** The studio branch's head, created from the repository's default branch the first time it is needed. */
  async function head() {
    const existing = await api(`/git/ref/heads/${branch}`).catch(optional);

    if (existing) {
      return existing.payload.object.sha;
    }

    const meta = await api('');
    const base = await api(`/git/ref/heads/${meta.payload.default_branch}`);
    await api('/git/refs', { method: 'POST', body: { ref: `refs/heads/${branch}`, sha: base.payload.object.sha } });
    return base.payload.object.sha;
  }

  /** The one pull request this branch keeps open, opened the first time there is something on the branch. */
  async function pullRequest(defaultBranch) {
    const open = await api(`/pulls?head=${owner}:${encodeURIComponent(branch)}&state=open`);
    if (open.payload?.length) {
      return open.payload[0];
    }

    const created = await api('/pulls', {
      method: 'POST',
      body: {
        title: 'Studio: content edited from the hosted studio',
        head: branch,
        base: defaultBranch,
        body: 'Opened by the hosted content studio (ADR 0023). Every save from the page is a commit on this'
          + ' branch; CI validates the content, because the page cannot.',
      },
    });
    return created.payload;
  }

  /**
   * The newest run of one workflow on this branch, as an id, or 0. Ids increase, so comparing them is how a
   * dispatch finds the run it started without trusting two clocks to agree.
   */
  async function newestRun(workflow) {
    const runs = await api(`/actions/workflows/${workflow}/runs?branch=${encodeURIComponent(branch)}&per_page=1`).catch(optional);
    return runs?.payload?.workflow_runs?.[0] ?? null;
  }

  /**
   * Runs one of the repository's workflows against the studio branch, and answers the run to watch.
   *
   * The dispatch endpoint answers 204 with no body: it does not say which run it started. So the newest run is
   * noted first, and the answer is the first run newer than that one -- which takes a moment to exist, so this
   * waits and asks again rather than reporting nothing for a run that is on its way.
   */
  async function dispatch(workflow, { attempts = 8, wait = pause } = {}) {
    if (!await api(`/git/ref/heads/${branch}`).catch(optional)) {
      throw refusal(`There is no ${branch} branch yet, so there is nothing of yours to run against.`,
        ['Save a change from this page first: that is what creates the branch these workflows run on.']);
    }

    const before = (await newestRun(workflow))?.id ?? 0;
    await api(`/actions/workflows/${workflow}/dispatches`, { method: 'POST', body: { ref: branch } });

    for (let attempt = 0; attempt < attempts; attempt += 1) {
      await wait(attempt === 0 ? 0 : 1500);
      const run = await newestRun(workflow);
      if (run && run.id > before) {
        return { id: run.id, url: run.html_url, branch };
      }
    }

    // It was dispatched; only finding it timed out. That is not a failure, and it must not read as one -- so it
    // answers with the dispatch it did make rather than with nothing, which a caller cannot tell from an error.
    return { id: null, url: null, branch, pending: true };
  }

  return {
    kind: 'hosted',

    /**
     * The catalogue as the site published it, with the aliases as the *branch* has them. The published files come
     * from `main`, so a page that read only those would rebuild the whole alias map from a snapshot that predates
     * its own commits, and silently undo an earlier version cut still waiting in the pull request.
     */
    async read() {
      const catalogue = await published('catalogue.json');
      const aliases = await branchFile(ALIASES_FILE);
      return aliases ? { ...catalogue, aliases } : catalogue;
    },
    audit: () => published('audit.json'),
    weights: () => published('weights.json'),
    runs: async () => [],

    async change(request) {
      // The Trees API overwrites whatever a path holds, so "create, do not replace" is checked rather than
      // expressed -- the local host refuses the same way, and losing a file to a reused name is not a save.
      const creating = (request.write ?? []).filter(document => document.create === true).map(document => document.path);
      const clash = creating.length ? await taken(creating) : [];
      if (clash.length) {
        throw refusal(`'${clash[0]}' already exists on ${branch}. Open it to change it, or pick another name.`, clash.slice(1));
      }

      const parent = await head();
      const base = await api(`/git/commits/${parent}`);
      const tree = await api('/git/trees', {
        method: 'POST',
        body: { base_tree: base.payload.tree.sha, tree: treeEntries(request) },
      });

      const commit = await api('/git/commits', {
        method: 'POST',
        body: { message: subject(request), tree: tree.payload.sha, parents: [parent] },
      });

      await api(`/git/refs/heads/${branch}`, { method: 'PATCH', body: { sha: commit.payload.sha } });

      const meta = await api('');
      const pull = await pullRequest(meta.payload.default_branch);
      return {
        saved: request.write?.[0]?.path ?? `${request.remove?.length ?? 0} removal(s)`,
        commit: commit.payload.sha,
        branch,
        pullRequest: { number: pull.number, url: pull.html_url },
      };
    },

    // Building and playing need the engine, which a browser does not have. The workflows do (ADR 0023), and
    // with a token that can reach Actions the page starts them itself rather than sending you to a form.
    dispatch,

    build: async () => { throw refusal('Building needs the engine. CI builds the content on every push to this branch; watch the pull request.'); },
    play: async () => { throw refusal('Playing a match needs the engine. Launch one of the workflows instead: they run on the studio branch.'); },
  };
}
