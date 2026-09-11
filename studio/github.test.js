// The hosted studio's writing half, driven with a stubbed transport (ADR 0024). None of this touches a network
// or a token: what is worth asserting is the requests it would have made, because that is what a wrong commit
// looks like before it is a wrong commit.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { githubBackend, repositoryFromLocation, storeToken, storedToken, treeEntries } from './github.js';

const REPOSITORY = { owner: 'downfallz', repo: 'maintest' };

/** Answers GitHub's shapes in the order the write path asks for them, and records every request. */
function github({ branchExists = true, openPulls = [] } = {}) {
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

    if (url.endsWith('/maintest') && method === 'GET') return { status: 200, payload: { default_branch: 'main' } };
    if (/\/git\/refs$/.test(url) && method === 'POST') return { status: 201, payload: { object: { sha: 'newbranch' } } };
    if (/\/git\/commits\/\w+$/.test(url)) return { status: 200, payload: { tree: { sha: 'basetree' } } };
    if (/\/git\/trees$/.test(url)) return { status: 201, payload: { sha: 'newtree' } };
    if (/\/git\/commits$/.test(url)) return { status: 201, payload: { sha: 'c0ffee1' } };
    if (/\/git\/refs\/heads\//.test(url) && method === 'PATCH') return { status: 200, payload: {} };
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

test('building and playing still refuse, and say where the engine is', async () => {
  const page = backend(github());

  await assert.rejects(() => page.build(), /CI builds the content/);
  await assert.rejects(() => page.play({}), /Dispatch a workflow/);
});

test('reading stays on the published files, so it needs no token and no rate limit', async () => {
  assert.deepEqual(await backend(github()).read(), { from: 'catalogue.json' });
  assert.deepEqual(await backend(github()).audit(), { from: 'audit.json' });
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
