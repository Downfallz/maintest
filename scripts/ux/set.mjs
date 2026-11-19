// The set a journey is walked on (docs/art/ux-craft-plan.md, UX2): the showcase built into a temporary folder
// and served from it, and a real table host with developer accounts, a recording of its own and a fixed seed.
// Each walk gets a new set, so a walk starts from accounts and games nobody made.

import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { createServer as createSocket } from 'node:net';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, rm, stat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hostLinks } from '../../studio/browser/host-log.js';
import { dotnetBinary } from '../../studio/browser/dotnet.js';

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
    let said = '';
    child.stdout.on('data', data => { said += data; }); child.stderr.on('data', data => { said += data; });
    child.once('error', fail);
    child.once('exit', code => (code === 0 ? done(said) : fail(new Error(`${command} ${args.join(' ')} exited ${code}:\n${said}`))));
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
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
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

/**
 * A table host as a developer starts one: accounts by a typed name, a table of seed 7 with a bot in its second
 * seat, the scenarios of the repository, a recording under `folder`. Returns its origin and the first seat's
 * invitation.
 */
export async function startHost(folder) {
  const port = await freePort();
  const host = spawn(dotnetBinary(), [HOST, 'table', '--declaration', 'at-activation', '--p2', 'greedy', '--seed', '7',
    '--accounts', 'dev', '--record', join(folder, 'sessions'), '--port', String(port)], { cwd: root });
  let log = '';
  host.stdout.on('data', data => { log += data; }); host.stderr.on('data', data => { log += data; });
  const end = Date.now() + 30000;
  while (!hostLinks(log)) {
    if (host.exitCode !== null || Date.now() > end) { host.kill('SIGINT'); throw new Error(`The table host did not start:\n${log}`); }
    await new Promise(done => setTimeout(done, 100));
  }
  const close = async () => {
    if (host.exitCode !== null) return;
    const closed = new Promise(done => host.once('exit', done));
    host.kill('SIGINT');
    await closed;
  };
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
