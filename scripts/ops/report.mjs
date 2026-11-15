// The production report (ADR 0185): the readings scripts/ops/read.sh wrote, one JSON file each, turned into one
// phone screen, in French, whose second line says what to look at first. ops-report.yml prints it to its log,
// which a session reads and which is public, so it prints counts and masked lines, never a record.
//
//   node scripts/ops/report.mjs <dir> [--now <ISO time>]

import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const TIME_ZONE = 'America/Toronto';
const DAY_MS = 24 * 60 * 60 * 1000;
const MAX_LINE = 160;
const MAX_ERROR_LINES = 3;
const BUDGET_WARNING = 0.8;
const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
// The worker's line as it puts a turn on the poison queue (AgentTurnWorker). Not "given up on after": every host's
// banner says that as it starts, and the dead-letter worker as it keeps the same turn.
export const GIVEN_UP = 'deliveries, onto';
const RUN_WORDS = { Succeeded: 'réussie', Failed: 'en échec', Running: 'en cours', Processing: 'en cours', Stopped: 'arrêtée' };

// The Azure readings, in the order the last line names those it could not read.
const AZURE_READINGS = {
  'revisions-table': 'révisions de la table',
  'revisions-worker': 'révisions du worker',
  'restarts-table': 'redémarrages de la table',
  'restarts-worker': 'redémarrages du worker',
  '5xx': 'erreurs 5xx',
  executions: 'maintenance',
  queue: 'files',
  logs: 'journal',
  cost: 'coût',
  budget: 'budget',
};

const number = (value, digits = 2) => Number(value).toLocaleString('fr-CA', { maximumFractionDigits: digits });
// French counts zero and one in the singular.
const plural = (count, one, many = `${one}s`) => `${count} ${count <= 1 ? one : many}`;
const usable = (reading) => reading !== undefined && reading !== null && reading.error === undefined;

/** A console line as a public log may hold it: no email, id, token or query string, one line, short. */
export function mask(line) {
  const masked = String(line ?? '')
    .replace(/(https?:\/\/[^\s?]+)\?\S*/g, '$1?…')
    .replace(/[\w.+-]+@[\w-]+(\.[\w-]+)+/g, '<courriel>')
    .replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, '<id>')
    .replace(/\b([a-z]{2,8})_[A-Za-z0-9-]{6,}/g, '$1_…')
    .replace(/[A-Za-z0-9+/=_-]{20,}/g, '…')
    .replace(/\s+/g, ' ')
    .trim();
  return masked.length > MAX_LINE ? `${masked.slice(0, MAX_LINE - 1)}…` : masked;
}

/** The points of a metric's first series, the hours of the window. */
function points(reading) {
  return (reading?.value?.[0]?.timeseries ?? []).flatMap((series) => series.data ?? []);
}

/** A metric summed over its window: restarts, 5xx answers. */
export function metricTotal(reading) {
  return points(reading).reduce((sum, point) => sum + (point.total ?? 0), 0);
}

/** A metric's latest point: the messages a queue holds. */
export function metricLatest(reading) {
  const known = points(reading).filter((point) => point.average !== undefined);
  return known.length === 0 ? 0 : known.at(-1).average;
}

/** The revisions that serve, newest first. */
export function revisions(reading) {
  return (reading?.value ?? [])
    .filter((revision) => revision.properties?.active)
    .map((revision) => ({
      name: revision.name,
      replicas: revision.properties.replicas ?? 0,
      health: revision.properties.healthState,
      created: revision.properties.createdTime,
      image: revision.properties.template?.containers?.[0]?.image,
    }))
    .sort((a, b) => String(b.created ?? '').localeCompare(String(a.created ?? '')));
}

/** The tending job's runs that started in the 24 hours before the report. */
export function tending(reading, now) {
  const since = new Date(now).getTime() - DAY_MS;
  const runs = (reading?.value ?? [])
    .map((execution) => ({ status: execution.properties?.status, startTime: execution.properties?.startTime }))
    .filter((run) => new Date(run.startTime).getTime() >= since)
    .sort((a, b) => b.startTime.localeCompare(a.startTime));
  return {
    runs: runs.length,
    failed: runs.filter((run) => run.status === 'Failed').length,
    running: runs.filter((run) => run.status === 'Running' || run.status === 'Processing').length,
    last: runs[0],
  };
}

/** The console's error lines, masked, and counted again: two lines that differ by an id are one. */
export function logLines(reading) {
  const table = reading?.tables?.[0];
  if (!table) return [];
  const column = (name) => table.columns.findIndex((each) => each.name === name);
  const [app, line, count] = [column('app'), column('line'), column('n')];
  const grouped = new Map();
  for (const row of table.rows) {
    const entry = { app: row[app] || '?', line: mask(row[line]) };
    const key = `${entry.app}\t${entry.line}`;
    const held = grouped.get(key) ?? { ...entry, count: 0 };
    held.count += Number(row[count]) || 0;
    grouped.set(key, held);
  }
  return [...grouped.values()].sort((a, b) => b.count - a.count);
}

/** The turns the worker gave up on: the line it writes as one goes on the poison queue. */
function givenUp(reading) {
  const table = reading?.tables?.[0];
  if (!table) return 0;
  const line = table.columns.findIndex((each) => each.name === 'line');
  const count = table.columns.findIndex((each) => each.name === 'n');
  return table.rows.filter((row) => String(row[line]).includes(GIVEN_UP)).reduce((sum, row) => sum + (Number(row[count]) || 0), 0);
}

/** The month to date from Cost Management, whatever the account calls its cost column. */
export function cost(reading) {
  const columns = (reading?.properties?.columns ?? []).map((column) => column.name);
  const row = reading?.properties?.rows?.[0];
  const amount = columns.findIndex((name) => ['Cost', 'PreTaxCost', 'CostUSD'].includes(name));
  if (!row || amount < 0) return undefined;
  const currency = columns.indexOf('Currency');
  return { amount: Number(row[amount]), currency: currency < 0 ? '' : row[currency] };
}

function budgetAmount(reading) {
  return reading?.value?.[0]?.properties?.amount;
}

/** What to look at first, most urgent first; empty when nothing is wrong. */
export function concerns(readings, now) {
  const found = [];
  const lobby = readings.lobby;
  if (usable(lobby) && lobby.status === 0) found.push('la table ne répond pas');
  else if (usable(lobby) && lobby.status !== 200) found.push(`la table répond ${lobby.status}`);
  if (usable(readings.executions)) {
    const { failed } = tending(readings.executions, now);
    if (failed > 0) found.push(`${plural(failed, 'passe')} de maintenance en échec`);
  }
  if (usable(readings.logs)) {
    const turns = givenUp(readings.logs);
    if (turns > 0) found.push(`${plural(turns, 'tour abandonné', 'tours abandonnés')}`);
  }
  if (usable(readings['5xx'])) {
    const errors = metricTotal(readings['5xx']);
    if (errors > 0) found.push(`${plural(errors, 'erreur 5xx', 'erreurs 5xx')}`);
  }
  const restarts = ['restarts-table', 'restarts-worker'].filter((name) => usable(readings[name])).reduce((sum, name) => sum + metricTotal(readings[name]), 0);
  if (restarts > 0) found.push(plural(restarts, 'redémarrage'));
  const spent = cost(readings.cost);
  const budget = budgetAmount(readings.budget);
  if (spent && budget && spent.amount >= BUDGET_WARNING * budget) found.push(`${Math.round((100 * spent.amount) / budget)}\u00a0% du budget`);
  return found;
}

function clock(time) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: TIME_ZONE, hour: 'numeric', minute: '2-digit', hourCycle: 'h23' })
    .formatToParts(new Date(time))
    .reduce((all, part) => ({ ...all, [part.type]: part.value }), {});
  return `${Number(parts.hour)} h ${parts.minute}`;
}

function day(time) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: TIME_ZONE, month: 'numeric', day: 'numeric' })
    .formatToParts(new Date(time))
    .reduce((all, part) => ({ ...all, [part.type]: part.value }), {});
  return `${Number(parts.day)} ${MONTHS[Number(parts.month) - 1]}`;
}

function health(state) {
  if (state === 'Healthy') return 'saine';
  if (state === 'Unhealthy') return 'en mauvaise santé';
  if (state === 'None') return 'au repos';
  return String(state ?? 'état inconnu').toLowerCase();
}

function revisionText(revision) {
  return `${revision.name} (active, ${plural(revision.replicas, 'réplica')}, ${health(revision.health)})`;
}

/** Why a reading failed, in a few words: the code Azure gives in parentheses, or the start of the message. */
function why(reading) {
  const error = String(reading.error);
  const code = /\(([A-Za-z]+)\)/.exec(error);
  return code ? code[1] : mask(error).slice(0, 60);
}

/** The report, one line each, that fits a phone. */
export function render(readings, now) {
  const lines = [`Production, ${day(now)} à ${clock(now)}`];
  const found = concerns(readings, now);
  lines.push(found.length === 0 ? 'Rien à signaler.' : `À regarder : ${found.join(', ')}.`);

  const lobby = readings.lobby;
  if (usable(lobby)) {
    lines.push(
      lobby.status === 0
        ? `Table : ${lobby.url} ne répond pas (${number(lobby.seconds, 0)} s)`
        : `Table : ${lobby.url} répond ${lobby.status} en ${number(lobby.seconds, 1)} s`,
    );
  }

  const signedIn = readings.azure?.signedIn === true;
  if (!signedIn) {
    lines.push("Azure non lu : pas d'identité ops (infra/runbooks/ops-identity.md)");
  } else {
    const serving = ['revisions-table', 'revisions-worker'].filter((name) => usable(readings[name])).flatMap((name) => revisions(readings[name]));
    if (serving.length > 0) lines.push(`Révisions : ${serving.map(revisionText).join(' · ')}`);
    const image = revisions(readings['revisions-table']).find((revision) => revision.image)?.image;
    if (image) lines.push(`Image : ${image}`);
    if (usable(readings['restarts-table']) || usable(readings['restarts-worker'])) {
      const count = (name) => (usable(readings[name]) ? metricTotal(readings[name]) : '?');
      lines.push(`Redémarrages 24 h : table ${count('restarts-table')} · worker ${count('restarts-worker')}`);
    }
    if (usable(readings['5xx'])) lines.push(`Erreurs 5xx 24 h : ${metricTotal(readings['5xx'])}`);
    if (usable(readings.executions)) {
      const tend = tending(readings.executions, now);
      const last = tend.last ? ` (dernière à ${clock(tend.last.startTime)} : ${RUN_WORDS[tend.last.status] ?? String(tend.last.status).toLowerCase()})` : '';
      lines.push(`Maintenance 24 h : ${plural(tend.runs, 'passe')}, ${tend.failed} en échec${last}`);
    }
    if (usable(readings.queue)) lines.push(`Files : ${plural(metricLatest(readings.queue), 'message')} en attente`);
    if (usable(readings.logs)) {
      lines.push(`Tours abandonnés 24 h : ${givenUp(readings.logs)}`);
      const errors = logLines(readings.logs);
      const total = errors.reduce((sum, entry) => sum + entry.count, 0);
      lines.push(`Erreurs du journal 24 h : ${plural(total, 'ligne')}`);
      for (const entry of errors.slice(0, MAX_ERROR_LINES)) lines.push(`  ${entry.count} × ${entry.app} : ${entry.line}`);
    }
    const spent = cost(readings.cost);
    if (spent) {
      const budget = budgetAmount(readings.budget);
      const money = (amount) => `${number(amount)} ${spent.currency}`.trim();
      lines.push(
        budget
          ? `Coût du mois : ${money(spent.amount)} sur ${money(budget)} (${Math.round((100 * spent.amount) / budget)}\u00a0%)`
          : `Coût du mois : ${money(spent.amount)}`,
      );
    }
    const unread = Object.entries(AZURE_READINGS)
      .filter(([name]) => !usable(readings[name]))
      .map(([name, label]) => (readings[name]?.error === undefined ? label : `${label} (${why(readings[name])})`));
    if (!usable(lobby)) unread.unshift(lobby ? `table (${why(lobby)})` : 'table');
    if (unread.length > 0) lines.push(`Non lu : ${unread.join(', ')}`);
  }
  if (!signedIn && !usable(lobby)) lines.push(`Non lu : ${lobby ? `table (${why(lobby)})` : 'table'}`);
  return lines.join('\n');
}

/** Every reading in the directory, by its file's name. */
export function readAll(dir) {
  const readings = {};
  for (const file of readdirSync(dir).filter((name) => name.endsWith('.json'))) {
    try {
      readings[file.slice(0, -'.json'.length)] = JSON.parse(readFileSync(join(dir, file), 'utf8'));
    } catch (failure) {
      readings[file.slice(0, -'.json'.length)] = { error: `not JSON: ${failure.message}` };
    }
  }
  return readings;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [dir] = process.argv.slice(2);
  const at = process.argv.indexOf('--now');
  if (!dir || !existsSync(dir)) {
    console.error('Usage: node scripts/ops/report.mjs <dir> [--now <ISO time>]');
    process.exit(2);
  }
  console.log(render(readAll(dir), at > 0 ? process.argv[at + 1] : new Date().toISOString()));
}
