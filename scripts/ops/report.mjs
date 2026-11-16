// The production report (ADR 0186): the readings scripts/ops/read.sh wrote, one JSON file each, turned into one
// phone screen, in French, whose second line says what to look at first. ops-report.yml prints it to its log,
// which a session reads and which is public, so it prints counts and masked lines (no name a person chose, no
// address, id, token, suspension reason or void explanation), never a record. A host settings notice is shown to
// every player, so it is printed as it is.
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

// The words before an operator's name in the host's audit lines, and no others: "reset by peer." is not a person.
const OPERATOR = new RegExp(
  `\\b((?:${[
    'requeued', 'dropped', 'removed', 'saved', 'restored', 'deleted', 'suspended', 'everywhere', 'opened', 'again',
    'Practice', 'run', 'voided', 'Session \\S+ closed', 'renamed from \\S+ to \\S+', 'round \\d+', 'keep \\S+ on \\S+',
  ].join('|')}) by|shown to) [^\\s:]+?(?=\\.?$|: | \\()`,
  'g',
);

/**
 * A console line without the names people chose: the host's audit lines name a player by the name they picked
 * (`Account acct_… (name)`, `renamed from a to b`) and the operator by an admin's player name or sign-in
 * (`requeued by name:`, `voided by name (`, `shown to name.`), and a suspension's or a void's reason is free text
 * an operator wrote (the host no longer prints them; an older image did).
 */
function unnamed(line) {
  return line
    .replace(/\b(suspended by \S+?|voided by \S+ \([^)]*\)[^:]*): .*$/, '$1: <motif>')
    .replace(/\b(acct_[A-Za-z0-9]+) \([^)]*\)/g, '$1 (<joueur>)')
    .replace(/\brenamed from \S+ to \S+/g, 'renamed from <joueur> to <joueur>')
    .replace(OPERATOR, '$1 <joueur>');
}

/** A console line as a public log may hold it: no name, email, id, token or query string, one line, short. */
export function mask(line) {
  // A word with an @ in it is an address, or near enough: masked whole, in one pass over the words.
  const words = unnamed(String(line ?? '').replace(/\s+/g, ' ').trim())
    .split(/(\s+)/)
    .map((word) => (word.includes('@') ? '<courriel>' : word))
    .join('');
  const masked = words
    .replace(/(https?:\/\/[^\s?]+)\?\S*/g, '$1?…')
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

/** A metric summed over its window: 5xx answers. */
export function metricTotal(reading) {
  return points(reading).reduce((sum, point) => sum + (point.total ?? 0), 0);
}

/** A metric's highest point: the restarts a replica has had, which the metric counts so far, not per hour. */
export function metricMax(reading) {
  return points(reading).reduce((most, point) => Math.max(most, point.maximum ?? 0), 0);
}

/** A metric's latest point: the messages a queue holds. */
export function metricLatest(reading) {
  const known = points(reading).filter((point) => point.average !== undefined);
  return known.length === 0 ? 0 : Math.round(known.at(-1).average);
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

const percent = (amount, budget) => Math.round((100 * amount) / budget);

function lobbyConcern(lobby) {
  if (!usable(lobby) || lobby.status === 200) return undefined;
  return lobby.status === 0 ? 'la table ne répond pas' : `la table répond ${lobby.status}`;
}

function countConcern(count, one, many) {
  return count > 0 ? plural(count, one, many) : undefined;
}

function restartsOf(readings) {
  return ['restarts-table', 'restarts-worker'].filter((name) => usable(readings[name])).reduce((sum, name) => sum + metricMax(readings[name]), 0);
}

function budgetConcern(readings) {
  const spent = cost(readings.cost);
  const budget = budgetAmount(readings.budget);
  return spent && budget && spent.amount >= BUDGET_WARNING * budget ? `${percent(spent.amount, budget)} % du budget` : undefined;
}

/** What to look at first, most urgent first; empty when nothing is wrong. */
export function concerns(readings, now) {
  const failedPasses = usable(readings.executions) ? tending(readings.executions, now).failed : 0;
  const turns = usable(readings.logs) ? givenUp(readings.logs) : 0;
  const errors = usable(readings['5xx']) ? metricTotal(readings['5xx']) : 0;
  const tendConcern = countConcern(failedPasses, 'passe');
  return [
    lobbyConcern(readings.lobby),
    tendConcern && `${tendConcern} de maintenance en échec`,
    countConcern(turns, 'tour abandonné', 'tours abandonnés'),
    countConcern(errors, 'erreur 5xx', 'erreurs 5xx'),
    countConcern(restartsOf(readings), 'redémarrage'),
    budgetConcern(readings),
  ].filter(Boolean);
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

function lobbyLines(lobby) {
  if (!usable(lobby)) return [];
  return [
    lobby.status === 0
      ? `Table : ${lobby.url} ne répond pas (${number(lobby.seconds, 0)} s)`
      : `Table : ${lobby.url} répond ${lobby.status} en ${number(lobby.seconds, 1)} s`,
  ];
}

function revisionLines(readings) {
  const serving = ['revisions-table', 'revisions-worker'].filter((name) => usable(readings[name])).flatMap((name) => revisions(readings[name]));
  const image = revisions(readings['revisions-table']).find((revision) => revision.image)?.image;
  return [serving.length > 0 && `Révisions : ${serving.map(revisionText).join(' · ')}`, image && `Image : ${image}`];
}

function restartLines(readings) {
  if (!usable(readings['restarts-table']) && !usable(readings['restarts-worker'])) return [];
  const count = (name) => (usable(readings[name]) ? metricMax(readings[name]) : '?');
  return [`Redémarrages (max. par réplica, 24 h) : table ${count('restarts-table')} · worker ${count('restarts-worker')}`];
}

function tendLines(reading, now) {
  if (!usable(reading)) return [];
  const tend = tending(reading, now);
  const word = tend.last && (RUN_WORDS[tend.last.status] ?? String(tend.last.status).toLowerCase());
  const last = tend.last ? ` (dernière à ${clock(tend.last.startTime)} : ${word})` : '';
  return [`Maintenance 24 h : ${plural(tend.runs, 'passe')}, ${tend.failed} en échec${last}`];
}

function journalLines(reading) {
  if (!usable(reading)) return [];
  const errors = logLines(reading);
  const total = errors.reduce((sum, entry) => sum + entry.count, 0);
  return [
    `Tours abandonnés 24 h : ${givenUp(reading)}`,
    `Erreurs du journal 24 h : ${plural(total, 'ligne')}`,
    ...errors.slice(0, MAX_ERROR_LINES).map((entry) => `  ${entry.count} × ${entry.app} : ${entry.line}`),
  ];
}

function costLines(readings) {
  const spent = cost(readings.cost);
  if (!spent) return [];
  const budget = budgetAmount(readings.budget);
  const money = (amount) => `${number(amount)} ${spent.currency}`.trim();
  const share = budget ? ` sur ${money(budget)} (${percent(spent.amount, budget)} %)` : '';
  return [`Coût du mois : ${money(spent.amount)}${share}`];
}

function unreadLabel(label, reading) {
  return reading?.error === undefined ? label : `${label} (${why(reading)})`;
}

function unreadLines(readings, signedIn) {
  const azure = signedIn ? Object.entries(AZURE_READINGS).filter(([name]) => !usable(readings[name])) : [];
  const unread = azure.map(([name, label]) => unreadLabel(label, readings[name]));
  if (!usable(readings.lobby)) unread.unshift(unreadLabel('table', readings.lobby));
  return unread.length > 0 ? [`Non lu : ${unread.join(', ')}`] : [];
}

/** The report, one line each, that fits a phone. */
export function render(readings, now) {
  const found = concerns(readings, now);
  const signedIn = readings.azure?.signedIn === true;
  const azure = signedIn
    ? [
        ...revisionLines(readings),
        ...restartLines(readings),
        usable(readings['5xx']) && `Erreurs 5xx 24 h : ${metricTotal(readings['5xx'])}`,
        ...tendLines(readings.executions, now),
        usable(readings.queue) && `Files : ${plural(metricLatest(readings.queue), 'message')} en attente`,
        ...journalLines(readings.logs),
        ...costLines(readings),
      ]
    : ["Azure non lu : pas d'identité ops (infra/runbooks/ops-identity.md)"];
  return [
    `Production, ${day(now)} à ${clock(now)}`,
    found.length === 0 ? 'Rien à signaler.' : `À regarder : ${found.join(', ')}.`,
    ...lobbyLines(readings.lobby),
    ...azure,
    ...unreadLines(readings, signedIn),
  ]
    .filter(Boolean)
    .join('\n');
}

/** Every reading in the directory, by its file's name. */
export function readAll(dir) {
  const readings = {};
  for (const file of readdirSync(dir).filter((name) => name.endsWith('.json'))) {
    try {
      readings[file.slice(0, -'.json'.length)] = JSON.parse(readFileSync(join(dir, file), 'utf8'));
    } catch (error_) {
      readings[file.slice(0, -'.json'.length)] = { error: `not JSON: ${error_.message}` };
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
