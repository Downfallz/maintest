// The local gate's memory (scripts/gate/state.sh) and the Stop hook that reads it (.claude/hooks/stop.sh), run
// on a throwaway repository like the pages' own tests (ADR 0024): what the hook says, and when it stays quiet.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

function repository() {
  const dir = mkdtempSync(join(tmpdir(), 'gate-hook-'));
  const git = (...args) => execFileSync('git', args, { cwd: dir, encoding: 'utf8' }).trim();
  git('init', '-q', '-b', 'main');
  git('config', 'user.email', 'gate@example.invalid');
  git('config', 'user.name', 'gate');
  for (const file of ['scripts/gate/state.sh', '.claude/hooks/stop.sh']) {
    mkdirSync(join(dir, dirname(file)), { recursive: true });
    copyFileSync(join(root, file), join(dir, file));
  }
  writeFileSync(join(dir, 'app.js'), 'one\n');
  git('add', '-A');
  git('commit', '-q', '-m', 'start');
  git('update-ref', 'refs/remotes/origin/main', 'HEAD');
  git('switch', '-q', '-c', 'stage');
  const run = (file, args = [], input = '') =>
    execFileSync('bash', [join(dir, file), ...args], { cwd: dir, input, encoding: 'utf8', env: { ...process.env, CLAUDE_PROJECT_DIR: dir } });
  const stop = (input = '{"stop_hook_active": false}') => run('.claude/hooks/stop.sh', [], input).trim();
  const state = (...args) => run('scripts/gate/state.sh', args).trim();
  return { dir, git, stop, state, edit: (file, text) => writeFileSync(join(dir, file), text), done: () => rmSync(dir, { recursive: true, force: true }) };
}

test('the stop hook is quiet when nothing changed since the branch left main', () => {
  const repo = repository();
  try {
    assert.equal(repo.state('changed'), '');
    assert.equal(repo.stop(), '');
  } finally {
    repo.done();
  }
});

test('the stop hook stops the turn once, with valid JSON naming the paths, when code changed after the gate', () => {
  const repo = repository();
  try {
    repo.edit('app.js', 'two\n');
    repo.edit('new "quoted".js', 'x\n');

    const answer = JSON.parse(repo.stop());
    assert.equal(answer.decision, 'block');
    assert.match(answer.reason, /^2 path\(s\) changed since the last green gate \(app\.js, new "quoted"\.js\)/);
    assert.equal(repo.stop(), '', 'the same tree is not warned twice');

    repo.edit('app.js', 'three\n');
    assert.equal(JSON.parse(repo.stop()).decision, 'block', 'a new tree is warned again');
  } finally {
    repo.done();
  }
});

test('the stop hook lets go of a turn it already continued', () => {
  const repo = repository();
  try {
    repo.edit('app.js', 'two\n');
    assert.equal(repo.stop('{"stop_hook_active": true}'), '');
  } finally {
    repo.done();
  }
});

test('a recorded gate quiets the hook until a file changes, markdown included', () => {
  const repo = repository();
  try {
    repo.edit('app.js', 'two\n');
    repo.state('record');
    assert.equal(repo.state('changed'), '');
    assert.equal(repo.stop(), '');

    repo.edit('AGENTS.md', 'budget\n');
    assert.equal(repo.state('changed'), 'AGENTS.md');
  } finally {
    repo.done();
  }
});

test('a gate recorded on another branch does not answer for this one', () => {
  const repo = repository();
  try {
    repo.edit('app.js', 'two\n');
    repo.state('record');
    repo.git('stash', '-q');
    repo.git('switch', '-q', '-c', 'next', 'origin/main');

    assert.equal(repo.state('changed'), '');
  } finally {
    repo.done();
  }
});

test('the stop hook waits for a gate that is still running', () => {
  const repo = repository();
  const gate = spawn('sleep', ['30']);
  try {
    repo.edit('app.js', 'two\n');
    mkdirSync(join(repo.dir, '.git', 'claude-gate'), { recursive: true });
    writeFileSync(join(repo.dir, '.git', 'claude-gate', 'running'), `${gate.pid} tree\n`);

    assert.equal(repo.stop(), '');
    gate.kill();
  } finally {
    gate.kill();
    repo.done();
  }
});

test('the fingerprint of a working tree needs no index', () => {
  const repo = repository();
  try {
    const before = repo.state('tree');
    rmSync(join(repo.dir, '.git', 'index'));
    assert.equal(repo.state('tree'), before);
  } finally {
    repo.done();
  }
});
