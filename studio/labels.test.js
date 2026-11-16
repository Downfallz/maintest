// .github/scripts/ensure-labels.sh, which labels.yml runs: it creates on GitHub the labels .github/labels.json
// declares and the repository does not have, with a fake `gh` here that lists labels and records what is created.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { chmodSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

// Runs the script on `declared` against a repository that has `existing`; returns its output and the calls
// that created a label, one argument list each.
function ensure(declared, existing) {
  const dir = mkdtempSync(join(tmpdir(), 'labels-'));
  try {
    const gh = [
      '#!/usr/bin/env bash',
      'if [[ "$1 $2" == "label list" ]]; then cat "$FAKE_DIR/existing.json"; exit 0; fi',
      'printf \'%s\\n\' "$(jq -cn \'$ARGS.positional\' --args -- "$@")" >> "$FAKE_DIR/calls"',
    ].join('\n');
    writeFileSync(join(dir, 'gh'), gh);
    chmodSync(join(dir, 'gh'), 0o755);
    writeFileSync(join(dir, 'existing.json'), JSON.stringify(existing.map(name => ({ name }))));
    writeFileSync(join(dir, 'labels.json'), JSON.stringify(declared));
    const output = execFileSync('bash', [join(root, '.github/scripts/ensure-labels.sh'), join(dir, 'labels.json')], {
      encoding: 'utf8',
      env: { ...process.env, PATH: `${dir}:${process.env.PATH}`, FAKE_DIR: dir },
    });
    const calls = existsSync(join(dir, 'calls')) ? readFileSync(join(dir, 'calls'), 'utf8').trim().split('\n').map(line => JSON.parse(line)) : [];
    return { output, calls };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

test('a declared label the repository does not have is created with its description', () => {
  const { output, calls } = ensure({ ops: 'A portal, a setting', flaky: 'Passed only on a retry: "again"' }, ['bug']);
  assert.deepEqual(calls, [
    ['label', 'create', 'ops', '--description', 'A portal, a setting'],
    ['label', 'create', 'flaky', '--description', 'Passed only on a retry: "again"'],
  ]);
  assert.match(output, /Labels created: 2\./);
});

test('a label the repository has is left as it is, whatever its case', () => {
  const { output, calls } = ensure({ 'follow-up': 'Work a session left', ops: 'A portal' }, ['Follow-Up', 'ops', 'bug']);
  assert.deepEqual(calls, []);
  assert.match(output, /Labels created: 0\./);
});

test('the labels file gives every label a description GitHub accepts', () => {
  const declared = JSON.parse(readFileSync(join(root, '.github/labels.json'), 'utf8'));
  for (const [name, description] of Object.entries(declared)) {
    assert.ok(description.length > 0 && description.length <= 100, `${name}: 1 to 100 characters`);
    assert.ok(!/[\t\n]/.test(description), `${name}: one line, no tab`);
  }
});
