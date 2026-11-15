// The morning digest (docs/engineering/ai-workflow-plan.md, W6): one phone screen, in French, of what merged,
// what is red, what waits for the owner and what is left open. The /digest skill gathers the facts with the
// GitHub and session tools into a JSON file; this script adds what merged from git and writes the text, so the
// digest reads the same every morning and studio/digest.test.js can hold its shape.
//
//   git fetch origin main && node scripts/digest/digest.mjs <facts.json>
//
// The facts (every field optional; a list left out reads as empty, a run left out as unknown):
//   now          ISO time of the run; since, the start of the window, defaults to 24 hours before it.
//   merged       [{number, title}]: read from origin/main's first parents when left out.
//   runs         {main, deploy, nightly}: each {status: green | red | running, url}.
//   health       [{number, title, label}]: the open red-main and red-nightly issues.
//   pullRequests [{number, title, checks: green | red | running, openThreads, draft}]: the open ones.
//   sessions     [{title, needs}]: sessions waiting on the owner.
//   issues       [{number, title, label, createdAt}]: open follow-up, flaky and ops issues.

import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const DAY_MS = 24 * 60 * 60 * 1000;
const MAX_ITEMS = 5;
const MAX_TITLE = 60;
const PR_NUMBER = /\(#(\d+)\)$/;
const LOOSE_ENDS = ['follow-up', 'flaky', 'ops'];
const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
const RUN_WORDS = { green: 'vert', red: 'rouge', running: 'en cours' };

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
  const log = execFileSync('git', ['log', 'origin/main', '--first-parent', `--since=${since.toISOString()}`, '--format=%cI%x09%s'], {
    encoding: 'utf8',
  });
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
  if ((pullRequest.openThreads ?? 0) > 0) return `${pullRequest.openThreads} fil(s) ouvert(s)`;
  if (pullRequest.checks === 'running') return 'checks en cours';
  if (pullRequest.checks === 'green') return 'prête, à fusionner';
  return null;
}

// Something is worth a push when it moved or when it waits for the owner; a quiet day says so in one line.
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
  if (!noteworthy(facts)) lines.push('Rien de neuf : rien fusionné, tout est vert, rien ne t’attend.');

  lines.push('', `Fusionné (${merged.length})`);
  lines.push(...(merged.length ? capped(merged, (pr) => `  #${pr.number} ${shorten(pr.title)}`) : ['  rien']));

  const healthOf = (label) => health.find((issue) => issue.label === label);
  lines.push(
    '',
    [runLine('main', runs.main, healthOf('red-main')), runLine('déploiement', runs.deploy), runLine('nuit', runs.nightly, healthOf('red-nightly'))].join(
      ' · ',
    ),
  );
  for (const run of [runs.main, runs.deploy, runs.nightly]) {
    if (run?.status === 'red' && run.url) lines.push(`  ${run.url}`);
  }

  if (waiting.length) {
    lines.push('', `PR en attente (${waiting.length})`);
    lines.push(...capped(waiting, (pr) => `  #${pr.number} ${pr.waits} : ${shorten(pr.title)}`));
  }

  if (sessions.length) {
    lines.push('', `Sessions qui attendent une réponse (${sessions.length})`);
    lines.push(...capped(sessions, (session) => `  ${shorten(session.title)}${session.needs ? ` : ${shorten(session.needs)}` : ''}`));
  }

  if (looseEnds.length) {
    const counts = LOOSE_ENDS.map((label) => [label, looseEnds.filter((issue) => issue.label === label).length])
      .filter(([, count]) => count > 0)
      .map(([label, count]) => `${count} ${label}`)
      .join(', ');
    lines.push('', `Suivis ouverts : ${counts}. Les plus anciens :`);
    lines.push(...looseEnds.slice(0, 3).map((issue) => `  #${issue.number} ${shorten(issue.title)}`));
  }

  return lines.join('\n');
}

export function withDefaults(facts, readMerged = mergedSince) {
  const now = new Date(facts.now ?? Date.now());
  const since = new Date(facts.since ?? now.getTime() - DAY_MS);
  return { ...facts, now: now.toISOString(), since: since.toISOString(), merged: facts.merged ?? readMerged(since) };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const path = process.argv[2];
  if (!path) {
    console.error('usage: node scripts/digest/digest.mjs <facts.json>');
    process.exit(2);
  }
  console.log(render(withDefaults(JSON.parse(readFileSync(path, 'utf8')))));
}
