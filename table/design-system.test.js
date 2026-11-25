// The design system (docs/art/design-system/README.md) is built from the repository by
// scripts/design-system/build.mjs. These tests keep its sources in step with the palette and the shared parts.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ASSETS, buildBundleCss, buildTokens, readBranding, tokenValue, writtenFiles } from '../scripts/design-system/build.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const source = join(root, 'docs', 'art', 'design-system');

test('every colour of the palette becomes a token with a usage note, at its value in branding.css', () => {
  const branding = readBranding();
  const tokens = buildTokens({ branding });
  const byName = new Map(tokens.color.tokens.map((token) => [token.name, token]));
  for (const [name, value] of branding.declarations.filter(([, v]) => /^#[0-9a-f]{6}$/i.test(v))) {
    assert.equal(byName.get(name)?.value.dark, value.toLowerCase(), name);
    assert.ok(byName.get(name).usage, `${name} has a usage note`);
  }
});

test('a role token becomes an alias of the token it names', () => {
  assert.equal(tokenValue('var(--brand-gold)'), '{brand-gold}');
  assert.equal(tokenValue('rgb(11 21 25 / 94%)'), 'rgba(11, 21, 25, 0.94)');
  const tokens = buildTokens();
  const names = new Set(tokens.color.tokens.map((token) => token.name));
  for (const token of tokens.color.tokens) {
    const alias = /^\{(.+)\}$/.exec(token.value.dark);
    if (alias) assert.ok(names.has(alias[1]), `${token.name} aliases ${alias[1]}`);
  }
});

test('a colour added to branding.css without a usage note stops the build', () => {
  const branding = readBranding();
  branding.declarations.push(['brand-new', '#123456']);
  assert.throws(() => buildTokens({ branding }), /--brand-new has no usage note/);
});

test('a page colour the notes name must still be written in its page sheet', () => {
  assert.throws(() => buildTokens({ pageCss: '' }), /parchment .* is no longer in table\/table.css/);
});

test('the radii, the cut and the font come from branding.css', () => {
  const tokens = buildTokens();
  const branding = new Map(readBranding().declarations);
  assert.equal(tokens.radius.tokens.find((t) => t.name === 'radius-sharp').value, branding.get('radius-sharp'));
  assert.equal(tokens.cut.tokens.find((t) => t.name === 'cut').value, branding.get('cut'));
  assert.equal(tokens.type.families.display, branding.get('font-display'));
  const [font] = tokens.type.fonts;
  assert.ok(existsSync(join(root, 'table', font.file)), font.file);
});

test('the motion tokens come from branding.css, each with its usage', () => {
  const tokens = buildTokens();
  const branding = readBranding().declarations.filter(([name]) => name.startsWith('motion-'));
  assert.ok(branding.length >= 9);
  assert.deepEqual(tokens.motion.tokens.map((t) => [t.name, t.value]), branding);
  for (const token of tokens.motion.tokens) assert.ok(token.usage.length > 10, token.name);
});

test('the shared stylesheet carries identity.css whole and the textures it reads', () => {
  const css = buildBundleCss();
  assert.ok(css.includes(readFileSync(join(root, 'table', 'identity.css'), 'utf8').trimEnd()));
  for (const name of ['--tex-grain', '--tex-soot', '--tex-rule']) assert.match(css, new RegExp(`${name}: url\\(`));
});

test('every component has its card and its guidelines, and the cover has only its card', () => {
  const files = writtenFiles();
  assert.ok(files.includes('README.md'));
  const components = [...new Set(files.filter((f) => f.startsWith('components/')).map((f) => f.split('/')[1]))];
  for (const name of components) {
    const preview = readFileSync(join(source, 'components', name, 'preview.html'), 'utf8');
    assert.match(preview.split('\n')[0], /^<!-- @dsCard /, `${name} starts with its marker`);
    assert.match(preview, /<html lang="en">/, `${name} names its language`);
    assert.equal(files.includes(`components/${name}/README.md`), name !== 'Cover', name);
  }
});

test('the cards draw with tokens, not raw colours', () => {
  for (const file of writtenFiles().filter((f) => f.endsWith('.html'))) {
    const html = readFileSync(join(source, file), 'utf8');
    assert.doesNotMatch(html, /#[0-9a-f]{3,8}\b/i, file);
  }
});

test('every picture the design system shows is in the repository', () => {
  for (const path of Object.values(ASSETS).flat()) assert.ok(existsSync(join(root, path)), path);
});

test('the site header card lays out wider than the width where identity.css folds the navigation', () => {
  const identity = readFileSync(join(root, 'table', 'identity.css'), 'utf8');
  const fold = Number(/@media \(max-width: (\d+)px\) \{\n\s*\.home-header \.home-navigation/.exec(identity)?.[1]);
  assert.ok(fold > 0, 'identity.css folds the navigation under some width');
  const marker = readFileSync(join(source, 'components', 'SiteHeader', 'preview.html'), 'utf8').split('\n')[0];
  const width = Number(/\bwidth=(\d+)/.exec(marker)?.[1]);
  assert.ok(width > fold, `the card lays out at ${width}px, the navigation folds at ${fold}px`);
});

test('the frame that gives a tag its game-card context draws no card', () => {
  const frame = readFileSync(join(source, 'frame.css'), 'utf8');
  const rules = frame.split('\n');
  const contents = rules.find((line) => line.includes('.tag-frame') && line.includes('display: contents'));
  assert.ok(contents, 'frame.css hides the tag frame box');
  const selector = contents.slice(0, contents.indexOf('{'));
  const card = rules.findIndex((line) => line.startsWith('.game-card {'));
  // .game-card sets display: flex; the frame's rule wins only by being more specific or coming later.
  assert.ok(selector.includes('.game-card.tag-frame') || rules.indexOf(contents) > card, selector);
});

test('the home\'s featured space has its card, naming its parts and its one rule (#986)', () => {
  const card = join(source, 'components', 'FeaturedSpace');
  const readme = readFileSync(join(card, 'README.md'), 'utf8');
  for (const part of ['.featured', '.featured-art', '.featured-status', '.featured-actions', '.featured-shortcuts']) assert.ok(readme.includes(`\`${part}\``), part);
  assert.match(readFileSync(join(card, 'preview.html'), 'utf8').split('\n')[0], /^<!-- @dsCard group="Surfaces"/);
});

test('the guidelines name what forced colours and more contrast change, as identity.css writes it (#1270)', () => {
  const readme = readFileSync(join(source, 'README.md'), 'utf8');
  const identity = readFileSync(join(root, 'table', 'identity.css'), 'utf8');
  const section = /^## Forced colours and more contrast\n([\s\S]*?)(?=^## )/m.exec(readme)?.[1] ?? '';
  assert.ok(section, 'the README has a section on forced colours and more contrast');
  // Each rule the section names is one identity.css holds, under the media query the section names.
  assert.match(identity, /@media \(forced-colors: active\) \{\s*:focus-visible \{ outline: 2px solid Highlight !important;/);
  assert.match(identity, /@media \(prefers-contrast: more\) \{\s*:root \{ --muted: var\(--ink\); \}/);
  for (const words of ['`forced-colors: active`', '`Highlight`', 'underline', '`prefers-contrast: more`', '`muted`', '7:1', 'box-shadow']) {
    assert.ok(section.includes(words), `the section says ${words}`);
  }
});
