// What the browser suite starts before a spec runs: the static server, on a port of its own for each checkout
// (#711), and the dotnet the real-engine journeys start the host with (#707). Run in Node like the pages' own tests.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createServer as createTcpServer } from 'node:net';
import { tmpdir } from 'node:os';
import { delimiter, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { dotnetBinary } from './browser/dotnet.js';
import { browserPort, checkout, reusable } from './browser/origin.js';

const here = dirname(fileURLToPath(import.meta.url));
const name = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
const runs = (...paths) => (path) => paths.includes(path);

test('the host starts with the dotnet under DOTNET_ROOT when it is set', () => {
  const binary = resolve('/opt/sdk', name);
  assert.equal(dotnetBinary({ DOTNET_ROOT: '/opt/sdk', PATH: '/usr/bin' }, runs(binary, join('/usr/bin', name))), binary);
});

test('without DOTNET_ROOT the host starts with the first dotnet on PATH, as found', () => {
  const found = join('/usr/bin', name);
  assert.equal(dotnetBinary({ PATH: ['/nowhere', '/usr/bin', '/usr/local/bin'].join(delimiter) }, runs(found, join('/usr/local/bin', name))), found);
});

test('a DOTNET_ROOT that holds no dotnet falls back to the one on PATH', () => {
  const found = join('/usr/bin', name);
  assert.equal(dotnetBinary({ DOTNET_ROOT: '/gone', PATH: '/usr/bin' }, runs(found)), found);
});

test('with no dotnet found the host tries the usual install folder', () => {
  assert.equal(dotnetBinary({ PATH: '/usr/bin' }, () => false), resolve('/usr/share/dotnet', name));
});

test('a folder named dotnet on PATH is passed over for the program further on', (t) => {
  const scratch = mkdtempSync(join(tmpdir(), 'dotnet-path-'));
  t.after(() => rmSync(scratch, { recursive: true, force: true }));
  mkdirSync(join(scratch, 'lib', name), { recursive: true });
  mkdirSync(join(scratch, 'bin'));
  writeFileSync(join(scratch, 'bin', name), '#!/bin/sh\n');
  chmodSync(join(scratch, 'bin', name), 0o755);
  assert.equal(dotnetBinary({ PATH: [join(scratch, 'lib'), join(scratch, 'bin')].join(delimiter) }), join(scratch, 'bin', name));
});

test('two checkouts serve the pages on two ports, and one checkout always uses the same port', () => {
  const first = browserPort('/a/checkout', {});
  assert.equal(browserPort('/a/checkout', {}), first);
  assert.notEqual(browserPort('/b/checkout', {}), first);
  assert.ok(first >= 5200 && first < 6000, String(first));
});

test('STUDIO_BROWSER_PORT sets the port, and a value that is not a valid port is refused', () => {
  assert.equal(browserPort(checkout, { STUDIO_BROWSER_PORT: '6123' }), 6123);
  assert.throws(() => browserPort(checkout, { STUDIO_BROWSER_PORT: 'abc' }), /STUDIO_BROWSER_PORT/);
  assert.throws(() => browserPort(checkout, { STUDIO_BROWSER_PORT: '80' }), /STUDIO_BROWSER_PORT/);
});

async function listening(server) {
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  return server.address().port;
}

async function freePort() {
  const probe = createTcpServer();
  const port = await listening(probe);
  await new Promise((done) => probe.close(done));
  return port;
}

test('the suite\'s own server, started for this checkout, is reused', async (t) => {
  const port = await freePort();
  const server = spawn(process.execPath, ['server.js'], { cwd: join(here, 'browser'), env: { ...process.env, STUDIO_BROWSER_PORT: String(port) } });
  t.after(() => server.kill());
  let answer = false;
  for (let attempt = 0; attempt < 50 && !answer; attempt += 1) {
    answer = await reusable(port).catch(() => false);
    if (!answer) await new Promise((done) => setTimeout(done, 100));
  }
  assert.equal(answer, true);
});

test('nothing is reused on a free port, so the config starts its own server', async () => {
  assert.equal(await reusable(await freePort(), checkout), false);
});

test('a server serving another checkout is refused, and says which', async (t) => {
  const server = createServer((request, response) => response.end(request.url === '/__root' ? '/tmp/other' : ''));
  const port = await listening(server);
  t.after(() => server.close());
  await assert.rejects(reusable(port, checkout), /\/tmp\/other.*STUDIO_BROWSER_PORT/s);
});

test('a server that does not say what it serves is refused', async (t) => {
  const server = createServer((request, response) => response.writeHead(404).end());
  const port = await listening(server);
  t.after(() => server.close());
  await assert.rejects(reusable(port, checkout), /STUDIO_BROWSER_PORT/);
});

test('a port held by something that does not speak HTTP is refused, not taken for free', async (t) => {
  const server = createTcpServer((socket) => socket.end('not http\r\n\r\n'));
  const port = await listening(server);
  t.after(() => server.close());
  await assert.rejects(reusable(port, checkout), /STUDIO_BROWSER_PORT/);
});
