// The names the installed Phaser declares, read from its type declarations, so the craft book
// (docs/art/battle-craft.md) can be held to the engine the battle really ships and not to Phaser 3's examples.
//
//   node scripts/battle/phaser-symbols.mjs Phaser.Cameras.Scene2D.Camera#shake Phaser.Geom.Point
//
// prints each name with `yes` or `no`, and exits 1 when one is missing. A name is written as Phaser's own
// documentation writes it: `Phaser.Namespace.Class`, `Phaser.Namespace.Class#member`, or a property of a
// configuration type (`Phaser.Types.Tweens.TweenBuilderConfig.ease`). A member is also found on what its class
// extends.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

/** Where `npm --prefix battle ci` puts the declarations. */
export const TYPES_PATH = join(root, 'battle/node_modules/phaser/types/phaser.d.ts');

const SCOPE = /^(?:declare\s+)?(?:abstract\s+)?(namespace|class|interface|enum)\s+([A-Za-z_$][\w$]*)/;
const TYPE = /^(?:declare\s+)?type\s+([A-Za-z_$][\w$]*)/;
const VALUE = /^(?:declare\s+)?(?:function|var|let|const)\s+([A-Za-z_$][\w$]*)/;
const MEMBER = /^(?:(?:static|readonly|protected|private|public|abstract)\s+)*([A-Za-z_$][\w$]*)\??\s*(?:[:(<=,]|$)/;
const EXTENDS = /\sextends\s+([^{]+?)(?:\simplements\s|\{|$)/;
const INDENT = 4;

/**
 * Index a generated `phaser.d.ts`. The file is machine-written, four spaces a scope and one declaration a line,
 * so the scopes are read from the indentation. Returns the names declared and what each class or interface
 * extends.
 */
export function indexDeclarations(source) {
  const names = new Set();
  const parents = new Map();
  const scopes = [];
  let commented = false;

  for (const raw of source.split('\n')) {
    const line = raw.trim();
    if (commented) {
      commented = !line.includes('*/');
      continue;
    }
    if (line.startsWith('/*')) {
      commented = !line.includes('*/');
      continue;
    }
    if (line === '' || line.startsWith('//')) continue;

    const indent = raw.length - raw.trimStart().length;
    while (scopes.length > 0 && scopes.at(-1).indent >= indent) scopes.pop();
    const owner = scopes.at(-1);
    const qualify = name => (owner ? `${owner.name}.${name}` : name);

    const scope = SCOPE.exec(line);
    if (scope) {
      const name = qualify(scope[2]);
      names.add(name);
      scopes.push({ indent, name, members: scope[1] !== 'namespace' });
      const extended = EXTENDS.exec(line);
      if (extended) parents.set(name, extended[1].split(',').map(parent => parent.trim().replace(/<.*$/, '')));
      continue;
    }
    const type = TYPE.exec(line);
    if (type) {
      const name = qualify(type[1]);
      names.add(name);
      // An object type lists its properties on the lines below; any other type ends on this line.
      if (line.endsWith('{')) scopes.push({ indent, name, members: true });
      continue;
    }
    const value = VALUE.exec(line);
    if (value) {
      names.add(qualify(value[1]));
      continue;
    }
    // A member sits one step inside its class, interface, enum or object type; anything deeper is the inside
    // of a member's own type.
    if (owner?.members && indent === owner.indent + INDENT) {
      const member = MEMBER.exec(line);
      if (member && member[1] !== 'constructor') names.add(qualify(member[1]));
    }
  }
  return { names, parents };
}

/** Whether the index declares a name, on the class itself or on one it extends. `#` and `.` both name a member. */
export function declares(index, symbol) {
  const name = symbol.replaceAll('#', '.');
  if (index.names.has(name)) return true;
  const cut = name.lastIndexOf('.');
  if (cut < 0) return false;
  const member = name.slice(cut + 1);
  const seen = new Set();
  const queue = [name.slice(0, cut)];
  while (queue.length > 0) {
    const owner = queue.shift();
    if (seen.has(owner)) continue;
    seen.add(owner);
    if (index.names.has(`${owner}.${member}`)) return true;
    queue.push(...(index.parents.get(owner) ?? []));
  }
  return false;
}

/** The index of the installed engine, or null when `battle/` has no packages installed. */
export function installedIndex(path = TYPES_PATH) {
  return existsSync(path) ? indexDeclarations(readFileSync(path, 'utf8')) : null;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const index = installedIndex();
  if (!index) {
    console.error('The Phaser types are not installed: npm --prefix battle ci --ignore-scripts');
    process.exit(2);
  }
  const asked = process.argv.slice(2);
  if (asked.length === 0) {
    console.error('usage: node scripts/battle/phaser-symbols.mjs <Phaser.Name[#member]>...');
    process.exit(2);
  }
  let missing = 0;
  for (const symbol of asked) {
    const found = declares(index, symbol);
    if (!found) missing += 1;
    console.log(`${found ? 'yes' : 'no '}  ${symbol}`);
  }
  process.exit(missing > 0 ? 1 : 0);
}
