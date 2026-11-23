// The modules a page script reaches by its static imports, transitively, for the tests that check a hand-written
// list of a page's scripts against what the scripts really import (table/sw.test.js, studio/picture-kit.test.js,
// #1136). It reads `import ... from`, `export ... from` and a bare `import '...'` of a path from the module's
// folder ('./a.js') or from the base ('/a.js'). A dynamic import() loads a module no static walk sees: the caller
// names the ones it makes as roots, and a reached module that makes one is refused rather than walked past.
import { readFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';

const STATIC = /(?:^|[\s;}])(?:import|export)\s+(?:[^'";]*?\s+from\s+)?['"]((?:\.{1,2})?\/[^'"]+)['"]/g;
const DYNAMIC = /\bimport\s*\(/;

/** The paths a module's source imports statically, in the order they appear. */
export function staticImports(source) {
  return [...source.matchAll(STATIC)].map(found => found[1]);
}

/**
 * Every module reached from `roots` (files under `base`), each named as a path from `base` with a leading slash
 * ('/picture.js'), the roots included. Throws when a reached module imports another dynamically.
 */
export function reachedModules(base, roots) {
  const reached = new Set(), waiting = roots.map(root => join(base, root));
  while (waiting.length > 0) {
    const file = waiting.pop();
    const name = `/${relative(base, file).split(sep).join('/')}`;
    if (reached.has(name)) continue;
    reached.add(name);
    const source = readFileSync(file, 'utf8');
    if (DYNAMIC.test(source)) throw new Error(`${name} imports a module dynamically: name what it loads as a root`);
    for (const path of staticImports(source)) waiting.push(path.startsWith('/') ? join(base, path) : join(dirname(file), path));
  }
  return reached;
}
