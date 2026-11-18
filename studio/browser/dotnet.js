// The dotnet the real-engine journeys start the host with (#707): the one under DOTNET_ROOT, if it holds one;
// else the first one on PATH, spawned as found (the launcher finds its own install folder, and a snap's link
// leads to `snap`, not dotnet); else the usual install folder. A cloud session has /usr/bin/dotnet and no
// DOTNET_ROOT.
import { accessSync, constants, statSync } from 'node:fs';
import { delimiter, join, resolve } from 'node:path';

const name = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';

// A program the journeys can run, not the SDK's own folder of the same name.
export function runnable(path) {
  try {
    if (!statSync(path).isFile()) return false;
    accessSync(path, constants.X_OK);
    return true;
  } catch {
    return false;
  }
}

export function dotnetBinary(env = process.env, canRun = runnable) {
  if (env.DOTNET_ROOT && canRun(resolve(env.DOTNET_ROOT, name))) return resolve(env.DOTNET_ROOT, name);
  for (const folder of (env.PATH ?? '').split(delimiter).filter(Boolean)) {
    const candidate = join(folder, name);
    if (canRun(candidate)) return candidate;
  }
  return resolve('/usr/share/dotnet', name);
}
