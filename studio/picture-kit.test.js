// The picture kit's spec (studio/browser/picture-kit.spec.js) lets through only the scripts named in its KIT and
// aborts every other: a module the kit's scripts import that KIT leaves out fails the spec's tests, while
// `node --test` stays green (#1125, #1136). This holds KIT to what the page really imports.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { reachedModules, staticImports } from '../scripts/module-imports.mjs';

const spec = readFileSync(new URL('./browser/picture-kit.spec.js', import.meta.url), 'utf8');
const table = fileURLToPath(new URL('../table/', import.meta.url));

// The spec opens /table/rules.html, so a page import('./x.js') is a module of table/.
const kit = () => JSON.parse(spec.match(/^const KIT = (\[[^\]]*\]);$/m)[1].replaceAll("'", '"'));
const pageImports = () => [...new Set([...spec.matchAll(/import\('\.\/([a-z-]+\.js)'\)/g)].map(found => found[1]))];

test('the picture kit\'s spec lets through every module its page imports reach', () => {
  const roots = pageImports();
  assert.ok(roots.includes('picture.js'), 'the spec imports the picture kit in the page');
  const allowed = new Set(kit());
  const missing = [...reachedModules(table, roots)].filter(path => !allowed.has(path));
  assert.deepEqual(missing, [], 'KIT names every module the page reaches, or the spec aborts it');
});

test('the import reader follows import, export and bare imports, and leaves dynamic ones and packages alone', () => {
  assert.deepEqual(staticImports([
    "import { a } from './a.js';", "import * as b from './b.js';", "export { c } from '../c.js';",
    "import './d.js';", "import x from 'node:fs';", "const e = await import('./e.js');",
    "export * from \"./f.js\";",
  ].join('\n')), ['./a.js', './b.js', '../c.js', './d.js', './f.js']);
});

test('the modules reached are walked transitively, each once, the roots included', () => {
  const base = mkdtempSync(join(tmpdir(), 'imports-'));
  mkdirSync(join(base, 'kit'));
  writeFileSync(join(base, 'root.js'), "import './kit/one.js';\n");
  writeFileSync(join(base, 'kit', 'one.js'), "import { two } from '../two.js';\nexport { two };\n");
  writeFileSync(join(base, 'two.js'), "import './root.js';\n");
  assert.deepEqual([...reachedModules(base, ['root.js'])].sort(), ['/kit/one.js', '/root.js', '/two.js']);
});
