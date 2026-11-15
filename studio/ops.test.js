// The production report (ADR 0185): scripts/ops/read.sh gathers the readings ops-report.yml prints, with a fake
// `az` and `curl` here, and scripts/ops/report.mjs turns them into one phone screen, in French, that names what to
// look at first and prints nothing a public log should not hold.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { GIVEN_UP, concerns, cost, logLines, mask, metricLatest, metricMax, metricTotal, render, revisions, tending } from '../scripts/ops/report.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const NOW = '2026-11-16T10:23:00Z';

const metric = (...values) => ({ value: [{ timeseries: [{ data: values.map((total) => ({ total, average: total, maximum: total })) }] }] });
const logs = (...rows) => ({ tables: [{ columns: [{ name: 'app' }, { name: 'line' }, { name: 'n' }], rows }] });

const healthy = () => ({
  azure: { signedIn: true },
  lobby: { url: 'https://play.example.test', status: 200, seconds: 1.8 },
  'revisions-table': {
    value: [
      { name: 'downfall-table--old', properties: { active: false, replicas: 0, healthState: 'Healthy', createdTime: '2026-11-14T10:00:00Z' } },
      {
        name: 'downfall-table--new',
        properties: {
          active: true,
          replicas: 1,
          healthState: 'Healthy',
          runningState: 'Running',
          createdTime: '2026-11-15T20:00:00Z',
          template: { containers: [{ image: 'ghcr.io/downfallz/downfall-table:7b37d70' }] },
        },
      },
    ],
  },
  'revisions-worker': { value: [{ name: 'downfall-worker--new', properties: { active: true, replicas: 0, healthState: 'Healthy' } }] },
  'restarts-table': metric(0, 0),
  'restarts-worker': metric(0),
  '5xx': metric(0),
  queue: { value: [{ timeseries: [{ data: [{ average: 3 }, { average: 0 }, {}] }] }] },
  executions: {
    value: [
      { properties: { status: 'Succeeded', startTime: '2026-11-16T10:17:00Z' } },
      { properties: { status: 'Succeeded', startTime: '2026-11-16T09:17:00Z' } },
      { properties: { status: 'Succeeded', startTime: '2026-11-14T09:17:00Z' } },
    ],
  },
  logs: logs(),
  cost: { properties: { columns: [{ name: 'Cost' }, { name: 'Currency' }], rows: [[0.42, 'CAD']] } },
  budget: { value: [{ properties: { amount: 5, currentSpend: { amount: 0.42, unit: 'CAD' } } }] },
});

test('a healthy table says so on its first line and fits a phone', () => {
  const text = render(healthy(), NOW);
  const lines = text.split('\n');
  assert.match(lines[0], /^Production, 16 nov\. à 5 h 23/);
  assert.equal(lines[1], 'Rien à signaler.');
  assert.match(text, /Table : https:\/\/play\.example\.test répond 200 en 1,8 s/);
  assert.match(text, /downfall-table--new \(active, 1 réplica, saine\)/);
  assert.match(text, /Image : ghcr\.io\/downfallz\/downfall-table:7b37d70/);
  assert.match(text, /Maintenance 24 h : 2 passes, 0 en échec/);
  assert.match(text, /Coût du mois : 0,42 CAD sur 5 CAD \(8\u00a0%\)/);
  assert.ok(lines.length <= 20, `${lines.length} lines`);
  assert.ok(lines.every((line) => line.length <= 200));
});

test('only the active revisions are listed', () => {
  assert.deepEqual(
    revisions(healthy()['revisions-table']).map((revision) => revision.name),
    ['downfall-table--new'],
  );
});

test('a table that does not answer is the first thing to look at', () => {
  const readings = { ...healthy(), lobby: { url: 'https://play.example.test', status: 0, seconds: 90 } };
  assert.deepEqual(concerns(readings, NOW), ['la table ne répond pas']);
  assert.match(render(readings, NOW).split('\n')[1], /^À regarder : la table ne répond pas\.$/);
});

test('a failed tending pass, a turn given up on, 5xx answers and restarts are each named', () => {
  const readings = {
    ...healthy(),
    executions: { value: [{ properties: { status: 'Failed', startTime: '2026-11-16T10:17:00Z' } }] },
    '5xx': metric(2, 1),
    'restarts-worker': metric(3),
    logs: logs(
      ['downfall-worker', '  {"table":"t1"} given up on after 5 deliveries, onto agent-turns-poison: boom', 2],
      ['downfall-worker', '  Turns: through the queue agent-turns, given up on after 5 deliveries onto agent-turns-poison, kept for good on agent-turns-dlq.', 4],
      ['downfall-worker', '  Dead letter on agent-turns-dlq: {"table":"t1"} from agent-turns, given up on after 5 deliveries: boom', 2],
    ),
  };
  assert.deepEqual(concerns(readings, NOW), [
    '1 passe de maintenance en échec',
    '2 tours abandonnés',
    '3 erreurs 5xx',
    '3 redémarrages',
  ]);
});

test('the line that counts a turn given up on is the worker\'s alone, in the report and in the alert', () => {
  const read = (path) => readFileSync(join(root, path), 'utf8');
  assert.ok(read('src/DownfallArena.Application/Matches/Journaling/AgentTurnWorker.cs').includes(`deliveries, onto {poison.Name}`));
  assert.ok(!read('src/DownfallArena.Cli/Table/TableHost.cs').includes(GIVEN_UP), 'the hosts\' banner');
  assert.ok(!read('src/DownfallArena.Application/Work/DeadLetterWorker.cs').includes(GIVEN_UP), 'the dead-letter worker');
  assert.ok(read('infra/main.bicep').includes(`Log_s has "${GIVEN_UP}"`), 'the alert');
  assert.ok(read('scripts/ops/read.sh').includes(`Log_s has '${GIVEN_UP}'`), 'the error lines');
});

test('the cost is a concern from 80 % of the budget', () => {
  const readings = { ...healthy(), cost: { properties: { columns: [{ name: 'Cost' }, { name: 'Currency' }], rows: [[4.1, 'CAD']] } } };
  assert.deepEqual(concerns(readings, NOW), ['82\u00a0% du budget']);
});

test('the tending runs are counted over the 24 hours before the report', () => {
  assert.deepEqual(tending(healthy().executions, NOW), { runs: 2, failed: 0, running: 0, last: { status: 'Succeeded', startTime: '2026-11-16T10:17:00Z' } });
});

test('a metric is the sum of its points, and a queue its latest point', () => {
  assert.equal(metricTotal(metric(1, 2, 3)), 6);
  assert.equal(metricLatest(healthy().queue), 0);
  assert.equal(metricTotal({ value: [] }), 0);
  assert.equal(metricLatest({ value: [{ timeseries: [{ data: [{ average: 0.5 }] }] }] }), 1, 'a whole count of messages');
});

test('restarts are the highest count a replica reached, not the sum of the hours that report it', () => {
  assert.equal(metricMax(metric(1, 1, 3, 3)), 3);
  assert.equal(metricMax({ value: [] }), 0);
});

test('the month to date is read whatever the cost column is called', () => {
  assert.deepEqual(cost({ properties: { columns: [{ name: 'PreTaxCost' }, { name: 'Currency' }], rows: [[1.5, 'USD']] } }), { amount: 1.5, currency: 'USD' });
  assert.equal(cost({ properties: { columns: [], rows: [] } }), undefined);
});

test('an error line loses emails, ids, tokens and query strings before it reaches a public log', () => {
  const line = mask(
    'Seat for ana@example.com in 3f2504e0-4f89-11d3-9a0c-0305e82c3301, acct_8hd72kd9s0 token eyJhbGciOiJIUzI1NiJ9abcdefghijk at https://x.test/a?sig=secret',
  );
  assert.doesNotMatch(line, /ana@example\.com|3f2504e0|8hd72kd9s0|eyJhbGci|sig=secret/);
  assert.match(line, /<courriel>/);
  assert.match(line, /<id>/);
  assert.match(line, /acct_…/);
  assert.match(line, /https:\/\/x\.test\/a\?…/);
  assert.ok(mask('x'.repeat(400)).length <= 160);
});

test('error lines that differ by an id alone are counted as one', () => {
  const rows = logLines(
    logs(
      ['downfall-table', 'The table 3f2504e0-4f89-11d3-9a0c-0305e82c3301 could not be read', 2],
      ['downfall-table', 'The table 9b2504e0-4f89-11d3-9a0c-0305e82c3302 could not be read', 1],
      ['downfall-worker', 'Something else failed', 1],
    ),
  );
  assert.deepEqual(rows, [
    { app: 'downfall-table', line: 'The table <id> could not be read', count: 3 },
    { app: 'downfall-worker', line: 'Something else failed', count: 1 },
  ]);
});

test('a reading that failed, or that was not made, is said on its last line, and the rest is printed', () => {
  const readings = { ...healthy(), cost: { error: 'AuthorizationFailed' } };
  delete readings.budget;
  const text = render(readings, NOW);
  assert.match(text, /Table : https/);
  assert.match(text.split('\n').at(-1), /^Non lu : coût \(AuthorizationFailed\), budget$/);
});

test('without the ops identity the report reads the table alone and says how to give it one', () => {
  const text = render({ azure: { signedIn: false }, lobby: { url: 'https://play.example.test', status: 200, seconds: 0.4 } }, NOW);
  assert.match(text, /Rien à signaler/);
  assert.match(text, /Azure non lu : pas d'identité ops \(infra\/runbooks\/ops-identity\.md\)/);
  assert.doesNotMatch(text, /Non lu/);
});

// A fake `az` and `curl` on the path: each answers from what the call names, and `az` fails the job's executions.
function fakeTools() {
  const dir = mkdtempSync(join(tmpdir(), 'ops-read-'));
  const bin = join(dir, 'bin');
  mkdirSync(bin);
  const tool = (name, body) => {
    writeFileSync(join(bin, name), `#!/usr/bin/env bash\n${body}\n`);
    chmodSync(join(bin, name), 0o755);
  };
  tool(
    'az',
    `echo "$*" >> "${join(dir, 'az.log')}"
case "$*" in
  "account show"*) echo sub-1 ;;
  *executions*) echo "ERROR: (AuthorizationFailed) no" >&2; exit 1 ;;
  *Microsoft.App/containerApps/downfall-table\\?*) echo '{"properties":{"configuration":{"ingress":{"fqdn":"downfall-table.example.test"}}}}' ;;
  *storageAccounts\\?*) echo '{"value":[{"id":"/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/st"}]}' ;;
  *workspaces\\?*) echo '{"value":[{"properties":{"customerId":"ws-1"}}]}' ;;
  *) echo '{"value":[]}' ;;
esac`,
  );
  tool('curl', `echo "$*" >> "${join(dir, 'curl.log')}"; echo "200 0.5"`);
  const run = (env) =>
    execFileSync('bash', [join(root, 'scripts/ops/read.sh'), join(dir, 'out')], {
      encoding: 'utf8',
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}`, RESOURCE_GROUP: 'rg', NAME_PREFIX: 'downfall', ...env },
    });
  const read = (name) => JSON.parse(readFileSync(join(dir, 'out', `${name}.json`), 'utf8'));
  const log = (name) => readFileSync(join(dir, `${name}.log`), 'utf8');
  return { run, read, log, files: () => readdirSync(join(dir, 'out')).sort(), done: () => rmSync(dir, { recursive: true, force: true }) };
}

test('the readings go one file each, and one that fails is written as its error while the rest are read', () => {
  const tools = fakeTools();
  try {
    tools.run({ AZURE: '1' });
    assert.deepEqual(tools.read('azure'), { signedIn: true });
    assert.match(tools.read('executions').error, /AuthorizationFailed/);
    assert.deepEqual(tools.read('revisions-table'), { value: [] });
    assert.deepEqual(tools.read('lobby'), { url: 'https://downfall-table.example.test', status: 200, seconds: 0.5 });
    for (const name of ['5xx', 'budget', 'cost', 'logs', 'queue', 'restarts-table', 'restarts-worker', 'revisions-worker']) {
      assert.ok(tools.files().includes(`${name}.json`), name);
    }
    assert.match(tools.log('az'), /api\.loganalytics\.io\/v1\/workspaces\/ws-1\/query/);
    assert.match(tools.log('az'), /storageAccounts\/st\/queueServices\/default\/providers\/Microsoft\.Insights\/metrics/);
  } finally {
    tools.done();
  }
});

test('the table is asked at its own domain when it has one', () => {
  const tools = fakeTools();
  try {
    tools.run({ AZURE: '1', TABLE_DOMAIN: 'play.example.test' });
    assert.match(tools.log('curl'), /https:\/\/play\.example\.test\/api\/lobby/);
  } finally {
    tools.done();
  }
});

test('without Azure only the table is read, and az is never called', () => {
  const tools = fakeTools();
  try {
    tools.run({ AZURE: '0', TABLE_DOMAIN: 'play.example.test' });
    assert.deepEqual(tools.files(), ['azure.json', 'lobby.json']);
    assert.deepEqual(tools.read('azure'), { signedIn: false });
    assert.throws(() => tools.log('az'));
  } finally {
    tools.done();
  }
});
