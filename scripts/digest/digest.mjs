// The morning digest (docs/engineering/ai-workflow-plan.md, W6): one phone screen, in French, of what merged,
// what is red, what waits for the owner and what is left open. It reads git and GitHub's REST API itself (the
// session's proxy signs the requests; the fresh session the scheduled task starts has no GitHub tools). So the
// digest reads the same every morning, and studio/digest.test.js can check its shape. It only reads.
//
//   git fetch origin main:refs/remotes/origin/main && node scripts/digest/digest.mjs [--stdin < facts.json]
//
// The optional facts (on stdin, with --stdin) add what only the session tools know, and overrides what the script would read
// (every field optional; a list left out reads as empty, a run left out as unknown):
//   now          ISO time of the run. since: start of the window, 24 hours before now by default.
//   merged       [{number, title}]: read from origin/main's first parents.
//   runs         {main, deploy, nightly}: each {status: green | red | running | never, url}, read from Actions.
//   health       [{number, title, label}]: the open red-main and red-nightly issues.
//   pullRequests [{number, title, checks: green | red | running, openThreads, draft}]: the open ones; the REST
//                API does not say whether a thread is resolved, so openThreads is left out unless given.
//   sessions     [{title, needs}]: sessions waiting on the owner, from the session tools.
//   issues       [{number, title, label, createdAt}]: open follow-up, flaky and ops issues.
//   unread       [what could not be read]: printed last.

import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const DAY_MS = 24 * 60 * 60 * 1000;
const MAX_ITEMS = 5;
const MAX_TITLE = 60;
const PR_NUMBER = /\(#(\d+)\)$/;
const LOOSE_ENDS = ['follow-up', 'flaky', 'ops'];
const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
const RUN_WORDS = { green: 'vert', red: 'rouge', running: 'en cours', never: 'jamais lancé' };
const PAGE = 100;
const HEALTH_LABELS = ['red-main', 'red-nightly'];
const GITHUB_REMOTE = /github\.com[:/]([A-Za-z0-9_.-]+)\/([A-Za-z0-9_.-]+?)(?:\.git)?\/?$/;
const RUNNING = new Set(['queued', 'pending', 'in_progress', 'requested', 'waiting']);
const RED = new Set(['failure', 'timed_out', 'cancelled', 'action_required', 'startup_failure']);

// A title is someone else's text: one line, and short enough for a phone.
export function shorten(title) {
  const line = String(title ?? '').replace(/\s+/g, ' ').trim();
  return line.length > MAX_TITLE ? `${line.slice(0, MAX_TITLE - 1)}…` : line;
}

// The day in the owner's words and time zone, which the scheduled task is set in.
export function dayLabel(now, timeZone = 'America/Toronto') {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: 'numeric', day: 'numeric' })
    .formatToParts(new Date(now))
    .reduce((all, part) => ({ ...all, [part.type]: part.value }), {});
  return `${Number(parts.day)} ${MONTHS[Number(parts.month) - 1]}`;
}

// `git log --format=%cI%x09%s` lines into the pull requests they merged; a commit with no (#n) is left out.
export function mergedFromLog(log) {
  return log
    .split('\n')
    .map((line) => line.split('\t'))
    .filter(([, subject]) => subject && PR_NUMBER.test(subject))
    .map(([, subject]) => ({ number: Number(PR_NUMBER.exec(subject)[1]), title: subject.replace(PR_NUMBER, '').trim() }));
}

function mergedSince(since) {
  // The window is passed as a date git parses, never as text a caller typed.
  // NOSONAR below: javascript:S4036, git found on PATH. The PATH is the session container's own, the command name
  // and its arguments are fixed, and nothing the digest reads reaches them.
  const args = ['log', 'origin/main', '--first-parent', `--since=${since.toISOString()}`, '--format=%cI%x09%s'];
  const log = execFileSync('git', args, { encoding: 'utf8' }); // NOSONAR
  return mergedFromLog(log);
}

function capped(items, format) {
  const shown = items.slice(0, MAX_ITEMS).map(format);
  return items.length > MAX_ITEMS ? [...shown, `  … et ${items.length - MAX_ITEMS} de plus`] : shown;
}

function runLine(name, run, health) {
  const word = RUN_WORDS[run?.status] ?? 'inconnu';
  const issue = health ? ` (#${health.number})` : '';
  return `${name} : ${word}${issue}`;
}

// What a pull request waits for, or nothing when it waits for its author rather than the owner.
export function waitingFor(pullRequest) {
  if (pullRequest.draft) return null;
  if (pullRequest.checks === 'red') return 'rouge';
  const threads = pullRequest.openThreads ?? 0;
  if (threads > 0) return `${threads} ${threads > 1 ? 'fils ouverts' : 'fil ouvert'}`;
  if (pullRequest.checks === 'running') return 'checks en cours';
  if (pullRequest.checks === 'green') return pullRequest.openThreads === undefined ? 'verte' : 'prête, à fusionner';
  return null;
}

// A quiet day says so in one line, and only when everything was read and every run is green: a run nobody could
// read is not green.
export function quiet(facts) {
  const runs = facts.runs ?? {};
  const fine = (run, ...also) => [ 'green', ...also ].includes(run?.status);
  return (
    !noteworthy(facts) &&
    (facts.unread ?? []).length === 0 &&
    fine(runs.main) &&
    fine(runs.deploy) &&
    fine(runs.nightly, 'never')
  );
}

// Something is worth a push when it moved or when it waits for the owner.
export function noteworthy(facts) {
  const runs = facts.runs ?? {};
  return (
    (facts.merged ?? []).length > 0 ||
    Object.values(runs).some((run) => run?.status === 'red') ||
    (facts.health ?? []).length > 0 ||
    (facts.pullRequests ?? []).some((pullRequest) => waitingFor(pullRequest) !== null) ||
    (facts.sessions ?? []).length > 0
  );
}

export function render(facts) {
  const now = new Date(facts.now ?? Date.now());
  const merged = facts.merged ?? [];
  const runs = facts.runs ?? {};
  const health = facts.health ?? [];
  const waiting = (facts.pullRequests ?? [])
    .map((pullRequest) => ({ ...pullRequest, waits: waitingFor(pullRequest) }))
    .filter((pullRequest) => pullRequest.waits !== null);
  const sessions = facts.sessions ?? [];
  const looseEnds = (facts.issues ?? [])
    .filter((issue) => LOOSE_ENDS.includes(issue.label))
    .sort((a, b) => String(a.createdAt ?? '').localeCompare(String(b.createdAt ?? '')));

  const lines = [`Bilan du ${dayLabel(now)}`];
  if (quiet(facts)) lines.push('Rien de neuf : rien fusionné, tout est vert, rien ne t’attend.');

  lines.push(
    '',
    `Fusionné (${merged.length})`,
    ...(merged.length ? capped(merged, (pr) => `  #${pr.number} ${shorten(pr.title)}`) : ['  rien']),
  );

  const healthOf = (label) => health.find((issue) => issue.label === label);
  const runLines = [runLine('main', runs.main, healthOf('red-main')), runLine('déploiement', runs.deploy), runLine('nightly', runs.nightly, healthOf('red-nightly'))];
  const redUrls = [runs.main, runs.deploy, runs.nightly].filter((run) => run?.status === 'red' && run.url).map((run) => `  ${run.url}`);
  lines.push('', runLines.join(' · '), ...redUrls);

  if (waiting.length) {
    lines.push('', `PR en attente (${waiting.length})`, ...capped(waiting, (pr) => `  #${pr.number} ${pr.waits} : ${shorten(pr.title)}`));
  }

  if (sessions.length) {
    const sessionLine = (session) => {
      const needs = session.needs ? ' : ' + shorten(session.needs) : '';
      return `  ${shorten(session.title)}${needs}`;
    };
    lines.push('', `Sessions qui attendent une réponse (${sessions.length})`, ...capped(sessions, sessionLine));
  }

  if (looseEnds.length) {
    const counts = LOOSE_ENDS.map((label) => [label, looseEnds.filter((issue) => issue.label === label).length])
      .filter(([, count]) => count > 0)
      .map(([label, count]) => `${count} ${label}`)
      .join(', ');
    lines.push(
      '',
      `Suivis ouverts : ${counts}. Les plus anciens :`,
      ...looseEnds.slice(0, 3).map((issue) => `  #${issue.number} ${shorten(issue.title)}`),
    );
  }

  if ((facts.unread ?? []).length) lines.push('', `Non lu : ${facts.unread.join(', ')}`);

  return lines.join('\n');
}

// A workflow run as the digest's words.
export function runStatus(run) {
  if (RUNNING.has(run.status)) return 'running';
  return run.conclusion === 'success' ? 'green' : 'red';
}

// A deployment's newest status (the Deployments API) as the digest's words.
export function deploymentStatus(status) {
  if (!status || ['queued', 'pending', 'in_progress'].includes(status.state)) return 'running';
  return ['failure', 'error'].includes(status.state) ? 'red' : 'green';
}

export function checksStatus(checkRuns) {
  if (checkRuns.length === 0) return undefined;
  if (checkRuns.some((check) => RED.has(check.conclusion))) return 'red';
  if (checkRuns.some((check) => check.status !== 'completed')) return 'running';
  return 'green';
}

export function originRepository(url) {
  const match = GITHUB_REMOTE.exec(url.trim());
  if (!match) throw new Error(`origin is not a GitHub repository: ${url}`);
  return `${match[1]}/${match[2]}`;
}

// Through curl, which follows the session's HTTPS proxy and so is signed by it; Node's fetch would go
// around the proxy, unsigned, and meet the anonymous rate limit of a shared address.
async function githubGet(path) {
  // NOSONAR: javascript:S4036, as for git above; the path is built from the origin's owner/name and fixed routes.
  const args = ['-sS', '--fail', '--max-time', '20', '-H', 'accept: application/vnd.github+json', `https://api.github.com${path}`];
  const body = execFileSync('curl', args, { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 }); // NOSONAR
  return JSON.parse(body);
}

// What GitHub says, field by field: a field that fails is named in unread and left out, so it reads as unknown.
export async function readGitHub(repository, get = githubGet) {
  const facts = { runs: {}, unread: [] };
  const attempt = async (name, read) => {
    try {
      await read();
    } catch {
      facts.unread.push(name);
    }
  };
  const base = `/repos/${repository}`;

  await attempt('main', async () => {
    const { workflow_runs: runs } = await get(`${base}/actions/workflows/ci.yml/runs?branch=main&event=push&per_page=1`);
    if (runs.length) facts.runs.main = { status: runStatus(runs[0]), url: runs[0].html_url };
  });
  // The rollout job deploys to the azure environment, from CI or by hand (deploy.yml: a rollback, a first
  // deployment), so the newest azure deployment is the one running, however many pushes skipped deploying.
  await attempt('déploiement', async () => {
    const [deployment] = await get(`${base}/deployments?environment=azure&per_page=1`);
    if (!deployment) return;
    const [status] = await get(`${base}/deployments/${deployment.id}/statuses?per_page=1`);
    facts.runs.deploy = { status: deploymentStatus(status), url: status?.log_url || status?.target_url || undefined };
  });
  await attempt('nightly', async () => {
    // main only: a dispatch on a feature branch is that branch's experiment, not the nightly's health.
    const { workflow_runs: runs } = await get(`${base}/actions/workflows/nightly.yml/runs?branch=main&per_page=1`);
    facts.runs.nightly = runs.length ? { status: runStatus(runs[0]), url: runs[0].html_url } : { status: 'never' };
  });
  // One label at a time, so pull requests and other issues never crowd a label out of the page.
  const health = [];
  const looseEnds = [];
  const labels = [...HEALTH_LABELS.map((each) => [each, health]), ...LOOSE_ENDS.map((each) => [each, looseEnds])];
  await Promise.all(
    labels.map(([label, into]) =>
      attempt(label, async () => {
        const issues = (await get(`${base}/issues?state=open&labels=${label}&per_page=${PAGE}`)).filter((issue) => !issue.pull_request);
        if (issues.length === PAGE) facts.unread.push(`${label} au-delà de ${PAGE}`);
        into.push(...issues.map((issue) => ({ number: issue.number, title: issue.title, label, createdAt: issue.created_at })));
      }),
    ),
  );
  const byNumber = (a, b) => a.number - b.number;
  health.sort(byNumber);
  looseEnds.sort(byNumber);
  facts.health = health;
  facts.issues = looseEnds;
  await attempt('PR', async () => {
    const pulls = await get(`${base}/pulls?state=open&per_page=${PAGE}`);
    if (pulls.length === PAGE) facts.unread.push(`PR au-delà de ${PAGE}`);
    // Both: a pull_request run checks out the merge commit, and the Actions API lists it by head_sha where the
    // head commit's check runs do not carry it (as scripts/workflow-metrics.py reads them).
    const statusOf = async (pull) => {
      const [{ check_runs: checks }, { workflow_runs: runs }] = await Promise.all([
        get(`${base}/commits/${pull.head.sha}/check-runs?per_page=${PAGE}`),
        get(`${base}/actions/runs?head_sha=${pull.head.sha}&per_page=${PAGE}`),
      ]);
      return { number: pull.number, title: pull.title, draft: pull.draft, checks: checksStatus([...checks, ...runs]) };
    };
    facts.pullRequests = await Promise.all(pulls.map(statusOf));
  });
  return facts;
}

// The given facts win over what was read, field by field; what it leaves out is read.
export async function gather(given, { readMerged = mergedSince, readHub = readGitHub, repository } = {}) {
  const now = new Date(given.now ?? Date.now());
  const since = new Date(given.since ?? now.getTime() - DAY_MS);
  const hub = await readHub(repository);
  const unread = [...(hub.unread ?? []), ...(given.unread ?? [])];
  let merged = given.merged;
  if (merged === undefined) {
    try {
      merged = readMerged(since);
    } catch {
      unread.unshift('fusions');
    }
  }
  return { ...hub, ...given, runs: { ...hub.runs, ...given.runs }, unread, now: now.toISOString(), since: since.toISOString(), merged };
}

// The facts come on stdin, and only with --stdin, so the script opens no path a caller names and never waits on a
// terminal that sends nothing.
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const given = process.argv.includes('--stdin') ? JSON.parse(readFileSync(0, 'utf8') || '{}') : {};
  const remote = execFileSync('git', ['remote', 'get-url', 'origin'], { encoding: 'utf8' }); // NOSONAR: S4036, as above
  const repository = originRepository(remote);
  console.log(render(await gather(given, { repository })));
}
