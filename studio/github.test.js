// The hosted studio's writing half, driven with a stubbed transport (ADR 0024). None of this touches a network
// or a token: what is worth asserting is the requests it would have made, because that is what a wrong commit
// looks like before it is a wrong commit.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { githubBackend, repositoryFromLocation, storeToken, storedToken, treeEntries } from './github.js';

const REPOSITORY = { owner: 'downfallz', repo: 'maintest' };

/** Answers GitHub's shapes in the order the write path asks for them, and records every request. */
function github({
  branchExists = true,
  openPulls = [],
  onBranch = {},
  existingRuns = [{ id: 10, html_url: 'https://github.com/run/10' }],
  appears = { id: 11, html_url: 'https://github.com/run/11' },
  appearsAfter = 0,
} = {}) {
  // A dispatched run does not exist the instant the dispatch is accepted, which is the whole reason the backend
  // looks more than once. The stub says so: nothing new until `appearsAfter` looks have gone by.
  let dispatched = false;
  let looks = 0;
  const calls = [];
  const answer = (url, method) => {
    // Only the studio branch can be missing; the default branch always answers, or nothing could be based on it.
    if (/\/git\/ref\/heads\/studio\/content$/.test(url) && method === 'GET') {
      return branchExists
        ? { status: 200, payload: { object: { sha: 'branchhead' } } }
        : { status: 404, payload: { message: 'Not Found' } };
    }

    if (/\/git\/ref\/heads\/main$/.test(url) && method === 'GET') {
      return { status: 200, payload: { object: { sha: 'mainhead' } } };
    }

    if (/\/contents\//.test(url)) {
      const path = /\/contents\/([^?]+)/.exec(url)[1];
      return path in onBranch
        ? { status: 200, payload: { content: Buffer.from(JSON.stringify(onBranch[path]), 'utf8').toString('base64') } }
        : { status: 404, payload: { message: 'Not Found' } };
    }

    if (url.endsWith('/maintest') && method === 'GET') return { status: 200, payload: { default_branch: 'main' } };
    if (/\/git\/refs$/.test(url) && method === 'POST') return { status: 201, payload: { object: { sha: 'newbranch' } } };
    if (/\/git\/commits\/\w+$/.test(url)) return { status: 200, payload: { tree: { sha: 'basetree' } } };
    if (/\/git\/trees$/.test(url)) return { status: 201, payload: { sha: 'newtree' } };
    if (/\/git\/commits$/.test(url)) return { status: 201, payload: { sha: 'c0ffee1' } };
    if (/\/git\/refs\/heads\//.test(url) && method === 'PATCH') return { status: 200, payload: {} };
    if (/\/actions\/workflows\/[^/]+\/dispatches$/.test(url)) {
      dispatched = true;
      looks = 0;
      return { status: 204, payload: null };
    }

    if (/\/actions\/workflows\/[^/]+\/runs\?/.test(url)) {
      if (dispatched) looks += 1;
      const visible = dispatched && appears && looks > appearsAfter ? [appears, ...existingRuns] : existingRuns;
      return { status: 200, payload: { workflow_runs: visible.toSorted((left, right) => right.id - left.id).slice(0, 1) } };
    }

    if (/\/pulls\?/.test(url)) return { status: 200, payload: openPulls };
    if (/\/pulls$/.test(url) && method === 'POST') return { status: 201, payload: { number: 51, html_url: 'https://github.com/downfallz/maintest/pull/51' } };
    return { status: 500, payload: { message: `nothing stubbed for ${method} ${url}` } };
  };

  return {
    calls,
    transport: async (url, options = {}) => {
      const method = options.method ?? 'GET';
      calls.push({ url, method, headers: options.headers, body: options.body ? JSON.parse(options.body) : undefined });
      const { status, payload } = answer(url, method);
      return { ok: status < 400, status, statusText: 'stub', json: async () => payload };
    },
  };
}

const backend = (stub, options = {}) => githubBackend({
  transport: stub.transport,
  token: 'ghp_stub',
  repository: REPOSITORY,
  published: async name => ({ from: name }),
  ...options,
});

test('a save is one tree, one commit and one move of the branch', async () => {
  const stub = github();

  const result = await backend(stub).change({
    kind: 'spells',
    write: [{ path: 'Spells/sorcerer/rejuvenate.v1.json', document: { id: 'spell:rejuvenate:v1' } }],
  });

  const posts = stub.calls.filter(call => call.method === 'POST').map(call => call.url.replace(/.*maintest/, ''));
  assert.deepEqual(posts, ['/git/trees', '/git/commits', '/pulls']);
  assert.equal(stub.calls.filter(call => call.method === 'PATCH').length, 1, 'the branch moves exactly once');
  assert.equal(result.commit, 'c0ffee1');
  assert.deepEqual(result.pullRequest, { number: 51, url: 'https://github.com/downfallz/maintest/pull/51' });
});

test('the commit is built on the branch head, not on whatever main is', async () => {
  const stub = github();

  await backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/a.json', document: {} }] });

  const commit = stub.calls.find(call => /\/git\/commits$/.test(call.url));
  assert.deepEqual(commit.body.parents, ['branchhead']);
  assert.equal(commit.body.tree, 'newtree');
  const tree = stub.calls.find(call => /\/git\/trees$/.test(call.url));
  assert.equal(tree.body.base_tree, 'basetree', 'a tree without its base would delete every other file');
});

test('the branch is created from the default branch when it is not there yet', async () => {
  const stub = github({ branchExists: false });

  await backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/a.json', document: {} }] });

  const created = stub.calls.find(call => /\/git\/refs$/.test(call.url) && call.method === 'POST');
  assert.deepEqual(created.body, { ref: 'refs/heads/studio/content', sha: 'mainhead' });

  // And the first commit on the new branch is based on it, not on a head that was never there.
  const commit = stub.calls.find(call => /\/git\/commits$/.test(call.url));
  assert.deepEqual(commit.body.parents, ['mainhead']);
});

test('a pull request already open is reused rather than opened again', async () => {
  const stub = github({ openPulls: [{ number: 12, html_url: 'https://github.com/downfallz/maintest/pull/12' }] });

  const result = await backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/a.json', document: {} }] });

  assert.equal(stub.calls.filter(call => /\/pulls$/.test(call.url) && call.method === 'POST').length, 0);
  assert.equal(result.pullRequest.number, 12);
});

test('a document and the alias that names it land in the same commit', async () => {
  const entries = treeEntries({
    write: [{ path: 'Spells/a.v2.json', document: { id: 'spell:a:v2' } }],
    aliases: { 'spell:b': 'spell:b:v1', 'spell:a': 'spell:a:v2' },
  });

  // An alias pointing at a document that is not in the same tree does not build, which is why this is one commit.
  assert.deepEqual(entries.map(entry => entry.path), ['data/Spells/a.v2.json', 'data/aliases.json']);
  assert.equal(entries[0].content, '{\n  "id": "spell:a:v2"\n}\n');
  // Sorted, so re-saving an unchanged map is not a diff.
  assert.equal(entries[1].content, '{\n  "spell:a": "spell:a:v2",\n  "spell:b": "spell:b:v1"\n}\n');
});

test('the aliases are sorted the way the local host sorts them, not by the reader collation', () => {
  const entries = treeEntries({ aliases: { 'spell:apple': 'a', 'spell:Zeal': 'z', 'spell:Apple': 'A', 'spell:banana': 'b' } });

  // ContentStore writes a SortedDictionary with StringComparer.Ordinal. `localeCompare` would put apple before
  // Apple and banana before Zeal, so every hosted save would reorder the whole file and fight the local host.
  assert.deepEqual(
    Object.keys(JSON.parse(entries[0].content)),
    ['spell:Apple', 'spell:Zeal', 'spell:apple', 'spell:banana']);
});

test('a removal is the path with no blob behind it', async () => {
  const entries = treeEntries({ remove: ['Spells/gone.json'] });

  assert.deepEqual(entries, [{ path: 'data/Spells/gone.json', mode: '100644', type: 'blob', sha: null }]);
});

test('every file carries the trailing newline the repository writes', async () => {
  for (const entry of treeEntries({ write: [{ path: 'Spells/a.json', document: {} }], aliases: {} })) {
    assert.ok(entry.content.endsWith('\n'), `${entry.path} would show as "no newline at end of file"`);
  }
});

test('the token travels in the header and the request goes to api.github.com only', async () => {
  const stub = github();

  await backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/a.json', document: {} }] });

  for (const call of stub.calls) {
    assert.ok(call.url.startsWith('https://api.github.com/'), `${call.url} is not api.github.com`);
    assert.equal(call.headers.authorization, 'Bearer ghp_stub');
    assert.equal(call.headers['x-github-api-version'], '2022-11-28');
  }
});

test("GitHub's own refusal is what the page shows, with its errors as the lines under it", async () => {
  const stub = github();
  const refusing = {
    transport: async () => ({
      ok: false,
      status: 422,
      statusText: 'Unprocessable Entity',
      json: async () => ({ message: 'Reference update failed', errors: [{ message: 'is at a1b2c3d but expected 0ddba11' }] }),
    }),
  };

  await assert.rejects(
    () => backend(refusing).change({ kind: 'spells', write: [{ path: 'Spells/a.json', document: {} }] }),
    error => {
      assert.equal(error.message, 'Reference update failed');
      assert.deepEqual(error.problems, ['is at a1b2c3d but expected 0ddba11']);
      return true;
    });
  assert.equal(stub.calls.length, 0);
});

test('launching a workflow runs it on the studio branch, not on the default one', async () => {
  const stub = github();

  const run = await backend(stub).dispatch('tune.yml', { wait: async () => {} });

  const sent = stub.calls.find(call => /\/dispatches$/.test(call.url));
  // The point of running it here at all: the content on the branch is the content this page is showing.
  assert.deepEqual(sent.body, { ref: 'studio/content' });
  assert.match(sent.url, /\/actions\/workflows\/tune\.yml\/dispatches$/);
  assert.equal(run.branch, 'studio/content');
});

test('the run it answers with is the one it started, not the one that was already there', async () => {
  // The dispatch endpoint answers 204 with no body, so the run has to be found -- and ids increase, which is
  // what tells a new run from the last one without trusting two clocks.
  const stub = github();

  const run = await backend(stub).dispatch('evaluate.yml', { wait: async () => {} });

  assert.deepEqual(run, { id: 11, url: 'https://github.com/run/11', branch: 'studio/content' });
});

test('a run that takes a moment to appear is waited for rather than called missing', async () => {
  const stub = github({ appearsAfter: 3 });
  let waited = 0;

  const run = await backend(stub).dispatch('search.yml', { wait: async () => { waited += 1; } });

  assert.equal(run.id, 11);
  assert.ok(waited >= 3, 'it asked again instead of answering on the first look');
});

test('a dispatch whose run never appears says nothing rather than claiming a failure', async () => {
  const stub = github({ appears: null });

  const run = await backend(stub).dispatch('iterate.yml', { attempts: 2, wait: async () => {} });

  // It was dispatched; only finding it timed out, and those are not the same thing to tell someone -- a caller
  // cannot tell a null from a refusal, so this answers with the dispatch it made and says the run is pending.
  assert.deepEqual(run, { id: null, url: null, branch: 'studio/content', pending: true });
  assert.equal(stub.calls.filter(call => /\/dispatches$/.test(call.url)).length, 1);
});

test('there is nothing to run against until something has been saved', async () => {
  const stub = github({ branchExists: false });

  await assert.rejects(
    () => backend(stub).dispatch('tune.yml', { wait: async () => {} }),
    error => {
      assert.match(error.message, /no studio\/content branch yet/);
      assert.match(error.problems[0], /Save a change from this page first/);
      return true;
    });

  // Running the default branch instead would tune content that is not the content on screen.
  assert.equal(stub.calls.filter(call => /\/dispatches$/.test(call.url)).length, 0);
});

test('building and playing still refuse, and say where the engine is', async () => {
  const page = backend(github());

  await assert.rejects(() => page.build(), /CI builds the content/);
  await assert.rejects(() => page.play({}), /Launch one of the workflows/);
});

test('reading stays on the published files while the branch has nothing of its own', async () => {
  assert.deepEqual(await backend(github()).read(), { from: 'catalogue.json' });
  assert.deepEqual(await backend(github()).audit(), { from: 'audit.json' });
});

test('the aliases come from the branch once it has them, not from what main published', async () => {
  // The published catalogue is built from main, so a page reading only that would rebuild the whole alias map
  // from a snapshot that predates its own commits -- and silently undo a version cut still waiting in the PR.
  const stub = github({ onBranch: { 'data/aliases.json': { 'spell:a': 'spell:a:v2' } } });

  const catalogue = await backend(stub, { published: async () => ({ spells: [], aliases: { 'spell:a': 'spell:a:v1' } }) }).read();

  assert.deepEqual(catalogue.aliases, { 'spell:a': 'spell:a:v2' });
  assert.deepEqual(catalogue.spells, [], 'the rest of the catalogue is still what the site published');
});

test('a file on the branch is decoded as UTF-8, not as one byte per character', async () => {
  // GitHub answers base64 of UTF-8 bytes. Decoding it as if each byte were a character turns an accent into
  // mojibake, and the aliases would come back subtly wrong rather than failing.
  const stub = github({ onBranch: { 'data/aliases.json': { 'spell:épée': 'spell:épée:v1', 'spell:大剣': 'spell:大剣:v2' } } });

  const catalogue = await backend(stub, { published: async () => ({ aliases: {} }) }).read();

  assert.deepEqual(catalogue.aliases, { 'spell:épée': 'spell:épée:v1', 'spell:大剣': 'spell:大剣:v2' });
});

test('a create-only write refuses a path the branch already holds, rather than overwriting it', async () => {
  const stub = github({ onBranch: { 'data/Spells/a.v2.json': { id: 'spell:a:v2' } } });

  // The Trees API replaces whatever a path holds; the local host refuses, and so must this one.
  await assert.rejects(
    () => backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/a.v2.json', document: {}, create: true }] }),
    /'Spells\/a\.v2\.json' already exists on studio\/content/);

  assert.equal(stub.calls.filter(call => call.method === 'POST').length, 0, 'nothing is committed when a name clashes');
});

test('a create-only write goes through when the path is free', async () => {
  const stub = github();

  const result = await backend(stub).change({ kind: 'spells', write: [{ path: 'Spells/new.v1.json', document: {}, create: true }] });

  assert.equal(result.commit, 'c0ffee1');
});

test('a page not served from a project site says so instead of guessing a repository', () => {
  assert.throws(() => githubBackend({ published: async () => ({}) }), /cannot tell which repository/);
});

test('the repository is read from the project site the page is served from', () => {
  assert.deepEqual(repositoryFromLocation({ hostname: 'downfallz.github.io', pathname: '/maintest/' }), REPOSITORY);
  assert.deepEqual(repositoryFromLocation({ hostname: 'downfallz.github.io', pathname: '/maintest/index.html' }), REPOSITORY);
  // A user site has no repository segment, and neither does anything else.
  assert.equal(repositoryFromLocation({ hostname: 'downfallz.github.io', pathname: '/' }), null);
  assert.equal(repositoryFromLocation({ hostname: 'example.com', pathname: '/maintest/' }), null);
  assert.equal(repositoryFromLocation({ hostname: '127.0.0.1', pathname: '/' }), null);
});

test('a browser that refuses to hold the token says so rather than pretending it kept it', () => {
  const blocked = { getItem: () => { throw new Error('site data blocked'); }, setItem: () => { throw new Error('site data blocked'); }, removeItem: () => {} };

  assert.equal(storedToken(blocked), null);
  assert.equal(storeToken('ghp_x', blocked), false);

  const held = new Map();
  const working = { getItem: key => held.get(key) ?? null, setItem: (key, value) => held.set(key, value), removeItem: key => held.delete(key) };
  assert.equal(storeToken('ghp_x', working), true);
  assert.equal(storedToken(working), 'ghp_x');
  assert.equal(storeToken(null, working), true);
  assert.equal(storedToken(working), null);
});

test('the knobs are one blob of the commit, beside the documents they describe', () => {
  const entries = treeEntries({
    write: [{ path: 'Spells/a.v1.json', document: { id: 'spell:a:v1' } }],
    balance: { version: 'knobs:v1', spells: { 'spell:a': { intent: 'New.' } } },
  });

  assert.deepEqual(entries.map(entry => entry.path), ['data/Spells/a.v1.json', 'data/balance/knobs.json']);
});

test('the knobs file is written in the order it carries, not sorted like the alias map', () => {
  const [entry] = treeEntries({ balance: { version: 'knobs:v1', spells: { 'spell:zeal': {}, 'spell:apple': {} } } });

  // Sorting would reorder every entry to change one, which is a diff nobody reads.
  assert.ok(entry.content.indexOf('spell:zeal') < entry.content.indexOf('spell:apple'));
});

test('the knobs blob ends with a newline, the way every authored file in this repository does', () => {
  const [entry] = treeEntries({ balance: { version: 'knobs:v1' } });

  assert.ok(entry.content.endsWith('\n'));
});

test('a change that only moves the knobs names them rather than counting removals it did not make', async () => {
  const stub = github();

  const result = await backend(stub).change({ kind: 'spells', balance: { version: 'knobs:v1', spells: {} } });

  assert.equal(result.saved, 'the balance knobs');
});

test('a change that only moves the knobs says so in its commit subject', async () => {
  const stub = github();

  await backend(stub).change({ kind: 'spells', balance: { version: 'knobs:v1', spells: {} } });

  const commit = stub.calls.find(call => /\/git\/commits$/.test(call.url));
  assert.equal(commit.body.message, 'Studio: save the balance knobs');
});

test('one written document is still reported by its path, which is what the author was looking at', async () => {
  const stub = github();

  const result = await backend(stub).change({
    kind: 'spells',
    write: [{ path: 'Spells/a.v1.json', document: {} }],
    balance: { version: 'knobs:v1' },
  });

  assert.equal(result.saved, 'Spells/a.v1.json');
});

test('the knobs come from the branch once it has them, not from what main published', async () => {
  // The knobs file is written whole, so a page reading main's copy and then saving one entry would discard
  // every balance edit already committed to the branch -- the same failure the alias overlay exists to prevent.
  const stub = github({ onBranch: { 'data/balance/knobs.json': { version: 'knobs:v1', spells: { 'spell:a': { intent: 'On the branch.' } } } } });

  const catalogue = await backend(stub, {
    published: async () => ({ spells: [], balance: { version: 'knobs:v1', spells: { 'spell:a': { intent: 'On main.' } } } }),
  }).read();

  assert.equal(catalogue.balance.spells['spell:a'].intent, 'On the branch.');
  assert.deepEqual(catalogue.spells, [], 'the rest of the catalogue is still what the site published');
});

test('a branch that has not touched the knobs keeps the ones main published', async () => {
  const stub = github({ onBranch: { 'data/aliases.json': { 'spell:a': 'spell:a:v2' } } });

  const catalogue = await backend(stub, {
    published: async () => ({ balance: { version: 'knobs:v1', spells: { 'spell:a': { intent: 'On main.' } } } }),
  }).read();

  assert.equal(catalogue.balance.spells['spell:a'].intent, 'On main.');
  assert.deepEqual(catalogue.aliases, { 'spell:a': 'spell:a:v2' }, 'and the alias overlay still applies');
});
