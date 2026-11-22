// What Dependabot watches (.github/dependabot.yml), read as text: the file is small, and a YAML parser is a
// dependency the pages' tests do without (ADR 0024).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const config = readFileSync(new URL('../.github/dependabot.yml', import.meta.url), 'utf8');
const browser = JSON.parse(readFileSync(new URL('./browser/package.json', import.meta.url), 'utf8'));

/** The block of one update entry: from its `- package-ecosystem` line to the next one. */
function entry(ecosystem, directory) {
  const blocks = config.split(/^(?= {2}- package-ecosystem:)/m).slice(1);
  return blocks.find((block) => block.includes(`package-ecosystem: ${ecosystem}\n`) && block.includes(`directory: ${directory}\n`));
}

test("the browser suite's packages are watched weekly, all but Playwright, which moves the browser a session has", () => {
  const block = entry('npm', '/studio/browser');
  assert.ok(block, 'an npm entry for /studio/browser');
  assert.match(block, /^ {6}interval: weekly$/m);
  // Playwright's version is the browser the cloud sessions have installed: it is moved by hand, with them.
  assert.match(block, /^ {6}- dependency-name: "@playwright\/test"$/m);
  for (const name of Object.keys({ ...browser.dependencies, ...browser.devDependencies })) {
    if (name === '@playwright/test') continue;
    assert.doesNotMatch(block, new RegExp(`dependency-name: "${name}"`), `${name} is watched`);
  }
});

test('the Microsoft.Extensions group stays on the major version the projects target', () => {
  const block = entry('nuget', '/');
  assert.ok(block, 'the NuGet entry');
  // The projects target net10.0: a Microsoft.Extensions 11 belongs with the move to .NET 11 (#765).
  assert.match(block, /^ {6}- dependency-name: "Microsoft\.Extensions\.\*"\n {8}update-types: \["version-update:semver-major"\]$/m);
});
