// The set a journey is walked on (docs/art/ux-craft-plan.md, UX2): the showcase built into a temporary folder
// and served from it, and a real table host with developer accounts, a recording of its own and a fixed seed.
// Each walk gets a new set, so a walk starts from accounts and games nobody made.

import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { createServer as createSocket } from 'node:net';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hostLinks } from '../../studio/browser/host-log.js';
import { dotnetBinary } from '../../studio/browser/dotnet.js';

/** The walk's two phones. */
export const VIEWPORTS = [{ width: 320, height: 568 }, { width: 390, height: 844 }];
/** A desktop, for a strip that names it: what plays only on a wide screen (the coach's fade on the field, #1140). */
export const DESKTOP = { width: 1280, height: 800 };

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const HOST = join(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll');
const SCHEMA = join(root, 'data/dst/game.schema.json');
const BUNDLE = join(root, 'table/battle-build');

/** What a walk needs built first, each with the command that builds it; empty when the set can be raised. */
export function missing() {
  return [
    [HOST, 'dotnet build'],
    [SCHEMA, 'dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst'],
    [BUNDLE, 'npm --prefix battle ci --ignore-scripts && npm --prefix battle run build'],
  ].filter(([path]) => !existsSync(path)).map(([path, command]) => `${path.slice(root.length + 1)} is missing: run \`${command}\`.`);
}

function run(command, args, options) {
  return new Promise((done, fail) => {
    const child = spawn(command, args, options);
    // What it printed on standard output is its answer (the push keys); both streams say why it failed.
    let said = '', printed = '';
    child.stdout.on('data', data => { said += data; printed += data; }); child.stderr.on('data', data => { said += data; });
    child.once('error', fail);
    child.once('exit', code => (code === 0 ? done(printed) : fail(new Error(`${command} ${args.join(' ')} exited ${code}:\n${said}`))));
  });
}

/** Build the showcase into `folder`, as its publication does. */
export async function buildShowcase(folder) {
  await run(process.execPath, [join(root, 'showcase/build.mjs'), folder], { cwd: root });
  return folder;
}

const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.webp': 'image/webp', '.svg': 'image/svg+xml', '.woff2': 'font/woff2', '.json': 'application/json' };

/** Serve a built showcase on a free local port, a folder answering with its `index.html`. */
export async function serveShowcase(folder) {
  const server = createServer(async (request, response) => {
    let pathname;
    // A malformed escape is the caller's error: said with a 400, never a rejection nobody catches (#1093).
    try { pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname); } catch { response.writeHead(400).end(); return; }
    let file = resolve(folder, `.${pathname}`);
    if (file !== folder && !file.startsWith(`${folder}/`)) { response.writeHead(404).end(); return; }
    try {
      if ((await stat(file)).isDirectory()) file = join(file, 'index.html');
      response.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' }).end(await readFile(file));
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  return { origin: `http://127.0.0.1:${server.address().port}`, close: () => new Promise(done => server.close(done)) };
}

async function freePort() {
  const probe = createSocket();
  await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const { port } = probe.address();
  await new Promise(done => probe.close(done));
  return port;
}

/** A process is gone once it has an exit code or was ended by a signal (a crash, out of memory). */
export const exited = child => child.exitCode !== null || child.signalCode !== null;

const STOPPED_LATE = Symbol('the process outlived its grace');

/**
 * Stop a process: an interrupt, then a kill once `grace` ms have passed. Returns at once when it is already gone, so
 * a host a signal killed does not hold the walk until its spec's timeout (#1093).
 */
export async function stopProcess(child, { grace = 10000 } = {}) {
  // A program never spawned has no pid, and may never say `exit`: there is nothing to stop.
  if (exited(child) || child.pid === undefined) return;
  const closed = new Promise(done => child.once('exit', done));
  child.kill('SIGINT');
  let timer;
  const late = new Promise(done => { timer = setTimeout(done, grace, STOPPED_LATE); });
  try {
    if (await Promise.race([closed, late]) === STOPPED_LATE) { child.kill('SIGKILL'); await closed; }
  } finally {
    clearTimeout(timer);
  }
}

/**
 * What the walk's host is started with, as a developer starts one: accounts by a typed name, a table of seed 7 with a
 * bot in its second seat, the scenarios of the repository, a recording under `folder`, and the push keys written there,
 * so the pages offer pop-ups as a hosted table does (#1126).
 */
export function hostArguments(folder, port) {
  return [HOST, 'table', '--declaration', 'at-activation', '--p2', 'greedy', '--seed', '7', '--accounts', 'dev',
    '--record', join(folder, 'sessions'), '--push-keys', join(folder, PUSH_KEYS), '--port', String(port)];
}

const PUSH_KEYS = 'push-keys.json';
let printedKeys = null;

/** A pair of Web Push keys, printed by the host's `push-keys` once a run and written into `folder`. */
async function writePushKeys(folder, program) {
  printedKeys ??= run(program, [HOST, 'push-keys'], { cwd: root }).catch(error => { printedKeys = null; throw error; });
  await writeFile(join(folder, PUSH_KEYS), await printedKeys);
}

/**
 * The browser never asked for notifications, read in the page. Headless Chromium answers every notification
 * permission with a refusal, which the pop-ups offer rightly takes as a no: the walk stands in a browser that has not
 * answered yet, as studio/browser/table.push.js does. A page without notifications is left as it is.
 */
export function neverAskedInPage() {
  if (!globalThis.Notification) return;
  Object.defineProperty(globalThis.Notification, 'permission', { configurable: true, get: () => 'default' });
  globalThis.Notification.requestPermission = async () => 'default';
}

/**
 * A table host started with `hostArguments` on a free port. Returns its origin and the first seat's invitation.
 */
export async function startHost(folder, { program = dotnetBinary() } = {}) {
  const port = await freePort();
  // A program that cannot print the keys cannot start the host either: said as the host's own failure.
  try { await writePushKeys(folder, program); } catch (error) { throw new Error(`The table host did not start (its push keys: ${error.message})`); }
  const host = spawn(program, hostArguments(folder, port), { cwd: root });
  let log = '', failed = null;
  host.stdout.on('data', data => { log += data; }); host.stderr.on('data', data => { log += data; });
  // A program that cannot be spawned emits an error, and never an exit: without a listener it ends the process.
  host.once('error', error => { failed = error; });
  // Waited on what the host says, or on its end, for 30 s at most.
  await new Promise(done => {
    const timer = setTimeout(settled, 30000);
    function check() { if (hostLinks(log) || failed || exited(host)) settled(); }
    function settled() {
      clearTimeout(timer);
      for (const stream of [host.stdout, host.stderr]) stream.off('data', check);
      host.off('exit', check); host.off('error', check);
      done();
    }
    for (const stream of [host.stdout, host.stderr]) stream.on('data', check);
    host.on('exit', check); host.on('error', check);
    check();
  });
  if (!hostLinks(log)) {
    await stopProcess(host, { grace: 2000 });
    const why = failed ? ` (${failed.message})` : '';
    throw new Error(`The table host did not start${why}:\n${log}`);
  }
  const close = () => stopProcess(host);
  return { origin: `http://127.0.0.1:${port}`, invitation: hostLinks(log).join, close };
}

/** The whole set of one walk. `showcase` is a folder built once and shared: the walks only read it. */
export async function raiseSet(showcase) {
  const folder = await mkdtemp(join(tmpdir(), 'ux-journey-'));
  let site, host;
  try {
    site = await serveShowcase(showcase);
    host = await startHost(folder);
  } catch (error) {
    await site?.close(); await rm(folder, { recursive: true, force: true });
    throw error;
  }
  return {
    showcase: site.origin, game: host.origin, invitation: host.invitation,
    close: async () => { await host.close(); await site.close(); await rm(folder, { recursive: true, force: true }); },
  };
}
