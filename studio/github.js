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
  const repo = (location?.pathname ?? '').split('/').filter(Boolean)[0];
  return owner && repo ? { owner, repo } : null;
}

/**
 * The token, or null. A browser with site data blocked throws on `localStorage` rather than answering, which is
 * "no token" and not a crash: the page then reads, and says so when the user tries to write.
 */
export function storedToken(store = globalThis.localStorage) {
  try {
    return store?.getItem(TOKEN_KEY) || null;
  } catch {
    return null;
  }
}

/** Keeps or forgets the token. Answers whether it stuck, so the page can say when a browser refuses to hold it. */
export function storeToken(token, store = globalThis.localStorage) {
  try {
    if (token) {
      store?.setItem(TOKEN_KEY, token);
    } else {
      store?.removeItem(TOKEN_KEY);
    }

    return storedToken(store) === (token || null);
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

/** The aliases file is written sorted, so re-saving an unchanged map is not a diff. */
function aliasesFile(aliases) {
  return asFile(Object.fromEntries(Object.keys(aliases).sort().map(key => [key, aliases[key]])));
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

  /** The studio branch's head, created from the repository's default branch the first time it is needed. */
  async function head() {
    const existing = await api(`/git/ref/heads/${branch}`).catch(error => {
      if (/not found/i.test(error.message)) return null;
      throw error;
    });

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

  return {
    kind: 'hosted',

    read: () => published('catalogue.json'),
    audit: () => published('audit.json'),
    weights: () => published('weights.json'),
    runs: async () => [],

    async change(request) {
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

    // Building and playing need the engine, which a browser does not have. The workflows do (ADR 0023).
    build: async () => { throw refusal('Building needs the engine. CI builds the content on every push to this branch; watch the pull request.'); },
    play: async () => { throw refusal('Playing a match needs the engine. Dispatch a workflow under Actions instead.'); },
  };
}
