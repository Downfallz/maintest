// Which checkout the static server serves, and on which port (#711). Each checkout has a port of its own,
// derived from its path, so a reviewer's copy and the working tree never share a server. The server answers
// `/__root` with the checkout it serves. The config reuses a running server only when that answer is this checkout.
import { createHash } from 'node:crypto';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const checkout = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function browserPort(root = checkout, env = process.env) {
  if (env.STUDIO_BROWSER_PORT !== undefined) {
    const port = Number(env.STUDIO_BROWSER_PORT);
    if (!Number.isInteger(port) || port < 1024 || port > 65535) {
      throw new Error(`STUDIO_BROWSER_PORT must be a port from 1024 to 65535, not "${env.STUDIO_BROWSER_PORT}"`);
    }
    return port;
  }
  return 5200 + (createHash('sha256').update(root).digest().readUInt32BE(0) % 800);
}

// Returns true when the server on the port serves this checkout, and false when nothing listens there. Throws
// when anything else holds the port, a web server or not, so the specs never run against another tree's pages.
export async function reusable(port, root = checkout) {
  let served;
  try {
    const response = await fetch(`http://127.0.0.1:${port}/__root`, { signal: AbortSignal.timeout(2000) });
    served = response.ok ? (await response.text()).trim() : '';
  } catch (error) {
    if (error.cause?.code === 'ECONNREFUSED') return false;
    served = '';
  }
  if (served === root) return true;
  const holder = served ? `a server for ${served}` : 'something that does not say which checkout it serves';
  throw new Error(`Port ${port} is held by ${holder}, not ${root}. Stop it, or set STUDIO_BROWSER_PORT to another port.`);
}
