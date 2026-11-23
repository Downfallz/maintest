// The modules a page script reaches by its static imports, transitively, for the tests that hold a hand-written list
// of a page's scripts to what the scripts really import (table/sw.test.js, studio/picture-kit.test.js, #1136).
// It reads `import ... from`, `export ... from` and a bare `import '...'` of a relative path; a dynamic import(),
// which loads only when it runs, is the caller's to name as a root.
import { readFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';

const STATIC = /(?:^|[\s;}])(?:import|export)\s+(?:[^'";]*?\s+from\s+)?['"](\.{1,2}\/[^'"]+)['"]/g;

/** The relative paths a module's source imports statically, in the order they appear. */
export function staticImports(source) {
  return [...source.matchAll(STATIC)].map(found => found[1]);
}

/**
 * Every module reached from `roots` (files under `base`), each named as a path from `base` with a leading slash
 * ('/picture.js'), the roots included.
 */
export function reachedModules(base, roots) {
  const reached = new Set(), waiting = roots.map(root => join(base, root));
  while (waiting.length > 0) {
    const file = waiting.pop();
    const name = `/${relative(base, file).split(sep).join('/')}`;
    if (reached.has(name)) continue;
    reached.add(name);
    for (const path of staticImports(readFileSync(file, 'utf8'))) waiting.push(join(dirname(file), path));
  }
  return reached;
}
