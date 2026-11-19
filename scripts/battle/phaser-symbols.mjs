// The names the installed Phaser declares, read from its type declarations, so the craft book
// (docs/art/battle-craft.md) can be held to the engine the battle really ships and not to Phaser 3's examples.
//
//   node scripts/battle/phaser-symbols.mjs Phaser.Cameras.Scene2D.Camera#shake Phaser.Geom.Point
//
// prints each name with `yes` or `no`, and exits 1 when one is missing. A name is written as Phaser's own
// documentation writes it: `Phaser.Namespace.Class`, `Phaser.Namespace.Class#member`, or a property of a
// configuration type (`Phaser.Types.Tweens.TweenBuilderConfig.ease`). A member is also found on what its class
// extends. `#` names a member of a class, an interface, an enum or an object type, never of a namespace.
// PHASER_TYPES_PATH reads another declaration file than the installed one.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

/** Where `npm --prefix battle ci` puts the declarations. */
export const TYPES_PATH = process.env.PHASER_TYPES_PATH || join(root, 'battle/node_modules/phaser/types/phaser.d.ts');

const SCOPE = /^(?:declare\s+)?(?:abstract\s+)?(namespace|class|interface|enum)\s+([A-Za-z_$][\w$]*)/;
const TYPE = /^(?:declare\s+)?type\s+([A-Za-z_$][\w$]*)/;
const VALUE = /^(?:declare\s+)?(?:function|var|let|const)\s+([A-Za-z_$][\w$]*)/;
const MEMBER = /^(?:(?:static|readonly|protected|private|public|abstract)\s+)*([A-Za-z_$][\w$]*)\??\s*(?:[:(<=,]|$)/;
const INDENT = 4;

/** What a class or an interface extends, read without a backtracking pattern: `A<T>, B implements C {` is A and B. */
function extendedBy(line) {
  const at = line.search(/\sextends\s/);
  if (at < 0) return null;
  const rest = line.slice(at).trimStart().slice('extends'.length);
  const cut = [rest.indexOf(' implements '), rest.indexOf('{')].filter(index => index >= 0);
  const list = cut.length > 0 ? rest.slice(0, Math.min(...cut)) : rest;
  return list.split(',').map(parent => parent.split('<')[0].trim()).filter(Boolean);
}

/** Whether a line is inside a block comment, or opens or closes one; `state.commented` carries it to the next. */
function inComment(line, state) {
  if (state.commented) {
    state.commented = !line.includes('*/');
    return true;
  }
  if (line.startsWith('/*')) {
    state.commented = !line.includes('*/');
    return true;
  }
  return false;
}

/** Read one declaration line into the index; `owner` is the scope it sits in, `scopes` the stack it may push on. */
function readDeclaration(line, indent, owner, index, scopes) {
  const qualify = name => (owner ? `${owner.name}.${name}` : name);
  const scope = SCOPE.exec(line);
  if (scope) {
    const name = qualify(scope[2]);
    const members = scope[1] !== 'namespace';
    index.names.add(name);
    scopes.push({ indent, name, members });
    if (members) index.holders.add(name);
    const parents = extendedBy(line);
    if (parents) index.parents.set(name, parents);
    return;
  }
  const type = TYPE.exec(line);
  if (type) {
    const name = qualify(type[1]);
    index.names.add(name);
    // An object type lists its properties on the lines below; any other type ends on this line.
    if (line.endsWith('{')) {
      scopes.push({ indent, name, members: true });
      index.holders.add(name);
    }
    return;
  }
  const value = VALUE.exec(line);
  if (value) {
    index.names.add(qualify(value[1]));
    return;
  }
  // A member sits one step inside its class, interface, enum or object type; anything deeper is the inside
  // of a member's own type.
  if (owner?.members && indent === owner.indent + INDENT) {
    const member = MEMBER.exec(line);
    if (member && member[1] !== 'constructor') index.names.add(qualify(member[1]));
  }
}

/**
 * Index a generated `phaser.d.ts`. The file is machine-written, four spaces a scope and one declaration a line,
 * so the scopes are read from the indentation. Returns the names declared, which of them hold members (a
 * class, an interface, an enum, an object type) and what each class or interface extends.
 */
export function indexDeclarations(source) {
  const index = { names: new Set(), holders: new Set(), parents: new Map() };
  const scopes = [];
  const state = { commented: false };

  for (const raw of source.split('\n')) {
    const line = raw.trim();
    if (inComment(line, state) || line === '' || line.startsWith('//')) continue;
    const indent = raw.length - raw.trimStart().length;
    while (scopes.length > 0 && scopes.at(-1).indent >= indent) scopes.pop();
    readDeclaration(line, indent, scopes.at(-1), index, scopes);
  }
  return index;
}

/**
 * Whether the index declares a name, on the class itself or on one it extends. `.` names anything declared;
 * `#` names a member, so what stands before it must hold members: `Phaser.Math#Clamp` is not a name.
 */
export function declares(index, symbol) {
  const marks = symbol.split('#');
  if (marks.length > 2 || (marks.length === 2 && !index.holders.has(marks[0]))) return false;
  const name = marks.join('.');
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
