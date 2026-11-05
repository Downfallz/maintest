import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createServer } from 'node:net';
import { expect } from '@playwright/test';
import { hostLinks } from './host-log.js';

const root = resolve('../..');

// A real table host on a free port, for the journeys that read the engine's own projection: the Cli as the
// build left it, a bot in the second seat, nothing recorded. The caller stops it with `stopHost`.
export async function startHost(declaration, args = [], { twoPeople = false } = {}) {
  const probe = createServer(); await new Promise(done => probe.listen(0, '127.0.0.1', done));
  const port = probe.address().port; await new Promise(done => probe.close(done));
  const host = spawn(resolve(process.env.DOTNET_ROOT ?? '/usr/share/dotnet', 'dotnet'),
    [resolve(root, 'artifacts/bin/DownfallArena.Cli/debug/DownfallArena.Cli.dll'), 'table', '--declaration', declaration,
      ...(twoPeople || args.includes('--p2') ? [] : ['--p2', 'greedy']), ...(args.includes('--record') ? [] : ['--no-record']), '--seed', '7', '--port', String(port), ...args], { cwd: root });
  let log = ''; host.stdout.on('data', data => { log += data; }); host.stderr.on('data', data => { log += data; });
  try {
    await expect.poll(() => Boolean(hostLinks(log)), { timeout: 20000, message: 'Table host prints complete seat, session and admin links' }).toBe(true);
    return { host, ...hostLinks(log) };
  } catch (error) { host.kill('SIGINT'); throw error; }
}

export async function stopHost(host) {
  if (host?.exitCode !== null) return;
  const closed = new Promise(done => host.once('exit', done));
  host.kill('SIGINT');
  await closed;
}
