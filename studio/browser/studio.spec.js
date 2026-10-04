import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { aliasOfSpell, surveyPackages } from '../balance.js';
const root = resolve('../..');
const json = path => JSON.parse(readFileSync(join(root, 'data', path), 'utf8'));
function documents(folder, kind) {
  return readdirSync(join(root, 'data', folder), { recursive: true }).filter(name => name.endsWith('.json')).map(name => {
    const path = `${folder}/${name}`;
    const document = json(path);
    return { kind, path, id: document.id, name: document.name, enabled: document.enabled !== false, document };
  });
}
const catalogue = {
  directory: 'data', creatures: documents('Creatures', 'Creature'), spells: documents('Spells', 'Spell'),
  talentTrees: documents('TalentTrees', 'TalentTree'), tiers: documents('Tiers', 'Tier'),
  aliases: json('aliases.json'), balance: json('balance/knobs.json'), contentHash: 'browser-fixture', problems: [],
};

// What the balance overview lists: every enabled package a knob can name, and every enabled spell an alias points at.
const knobbedPackages = surveyPackages(catalogue.balance, catalogue.tiers, catalogue.aliases).rows.length;
const knobbedSpells = catalogue.spells.filter(spell => spell.enabled && aliasOfSpell(spell.id, catalogue.aliases)).length;

const weights = JSON.parse(readFileSync(join(root, 'learning', 'weights', 'greedy.json'), 'utf8'));

async function fit(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}
async function shot(page, info, name) {
  await page.screenshot({ path: info.outputPath(`${name}.png`), fullPage: false, animations: 'disabled' });
}

test.beforeEach(async ({ page }) => {
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: catalogue } }));
  await page.route('**/api/weights', route => route.fulfill({ json: { ok: true, result: { values: weights, order: Object.keys(weights) } } }));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Find your next move.' })).toBeVisible();
});

test('real packages drive the mobile guide and its linked spell details', async ({ page }, info) => {
  await expect(page.locator('.hero-stats')).toContainText('21packages');
  await expect(page.locator('.hero-stats')).toContainText('3tiers');
  await expect(page.locator('.family-pick')).toHaveCount(3);
  if (page.viewportSize().width < 900) {
    const bar = await page.locator('#toolbar').boundingBox();
    expect(bar.y + bar.height).toBe(page.viewportSize().height);
  }
  await expect(page.locator('.tier-section')).toHaveCount(3);
  await fit(page); await shot(page, info, 'explore');
  await page.locator('.family-pick').filter({ hasText: 'Occultist' }).click();
  await expect(page.locator('.package-name').first()).toHaveText('Occultist');
  await page.locator('.package-tile').first().click();
  await expect(page.getByRole('heading', { name: 'Included spells' })).toBeVisible();
  await fit(page); await shot(page, info, 'package');
  await page.locator('.spell-tile').first().click();
  await expect(page.getByRole('heading', { name: 'What it does' })).toBeVisible();
  const name = await page.locator('.reader-heading h2').textContent();
  await fit(page); await shot(page, info, 'spell');
  await page.reload();
  await expect(page.locator('.reader-heading h2')).toHaveText(name);
  await page.getByRole('button', { name: '← Back', exact: true }).click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Occultist');
  await page.goBack();
  await expect(page.getByRole('heading', { name: 'Find your next move.' })).toBeVisible();
});

test('search stays usable while filtering and remembers its query after reading', async ({ page }, info) => {
  await page.locator('#spells-view').click();
  const search = page.getByRole('searchbox', { name: 'Search spells', exact: true });
  await search.fill('pummel');
  await expect(page.locator('.spell-tile')).toHaveCount(1);
  await expect(search).toBeFocused();
  await page.locator('.spell-tile').click();
  await page.getByRole('button', { name: '← Back', exact: true }).click();
  await expect(search).toHaveValue('pummel');
  await search.fill('not a real spell');
  await expect(page.getByText('No spells match.', { exact: false })).toBeVisible();
  await search.fill('');
  await page.getByRole('button', { name: 'Defensive', exact: true }).click();
  await expect(page.locator('.spell-tile').first()).toContainText('Defensive');
  await fit(page); await shot(page, info, 'spells');
});

test('browsing spells shows the tier and package each one is learned from, and filters by tier', async ({ page }, info) => {
  await page.locator('#spells-view').click();
  const search = page.getByRole('searchbox', { name: 'Search spells', exact: true });
  await search.fill('pummel');
  await expect(page.locator('.spell-tile .spell-origin')).toHaveText(/Tier \d · \w+|Starting kit/);
  await search.fill('');
  const tiers = page.getByRole('group', { name: 'Tier' }).or(page.locator('[aria-label="Tier"]'));
  await tiers.getByRole('button', { name: 'Tier 2', exact: true }).click();
  const origins = await page.locator('.spell-tile .spell-origin').allTextContents();
  expect(origins.length).toBeGreaterThan(0);
  for (const text of origins) expect(text).toContain('Tier 2 · ');
  const controls = await page.locator('.library-controls').boundingBox();
  if (page.viewportSize().width < 600) expect(controls.height).toBeLessThan(200);
  await page.locator('.spell-tile').first().scrollIntoViewIfNeeded();
  await fit(page); await shot(page, info, 'spells-by-tier');
  await page.getByRole('button', { name: 'Energy & effects', exact: true }).click();
  await expect(page.locator('.strategy-spell .spell-origin').first()).toBeVisible();
  await fit(page);
});

test('a spell card shows its critical bonus, collapsed in both libraries, and a spell without one says nothing', async ({ page }) => {
  await page.locator('#spells-view').click();
  const search = page.getByRole('searchbox', { name: 'Search spells', exact: true });
  await search.fill('ice spear');
  await expect(page.locator('.spell-tile .spell-meta')).toContainText('+50% crit');
  await search.fill('bone ward');
  await expect(page.locator('.spell-tile .spell-meta')).not.toContainText('crit');
  await search.fill('ice spear');
  await page.getByRole('button', { name: 'Energy & effects', exact: true }).click();
  await expect(page.locator('.strategy-spell').filter({ hasText: 'Ice Spear' }).locator('.strategy-spell-facts')).toContainText('+50% crit');
});

test('a spell shows its value a round next to its tier, and the reader says what the estimate leaves out', async ({ page }, info) => {
  await page.locator('#spells-view').click();
  await page.getByRole('searchbox', { name: 'Search spells', exact: true }).fill('protective slam');
  await expect(page.locator('.spell-tile .spell-value')).toHaveText(/^Value 12\.7 a round · Tier 2 attacks \d+\.\d–12\.7$/);
  await page.locator('.spell-tile').click();
  const reading = page.locator('.value-reading');
  await expect(reading.locator('summary')).toHaveText('Value 12.7 a round · 19.0 a cast');
  await reading.locator('summary').click();
  await expect(reading).toContainText('This one is the highest of that range.');
  await expect(reading).toContainText('Played matches decide balance');
  await fit(page); await shot(page, info, 'spell-value');
});

test('editing is explicit and cancelling a navigation preserves the draft', async ({ page }, info) => {
  await page.locator('.package-tile').first().click();
  await expect(page.locator('#detail input')).toHaveCount(0);
  await page.getByRole('button', { name: 'Edit content' }).click();
  const name = page.getByLabel('Name', { exact: true });
  await name.fill('My draft package');
  page.once('dialog', dialog => dialog.dismiss());
  await page.locator('#spells-view').click();
  await expect(name).toHaveValue('My draft package');
  await fit(page); await shot(page, info, 'editor');
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Back to reading', exact: true }).click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Brute');
});

test('catalogue sheets and tools fit the viewport and leave the current reader intact', async ({ page }, info) => {
  await page.locator('.package-tile').first().click();
  await page.locator('#browse').click();
  await expect(page.locator('#search')).toBeFocused();
  await page.locator('[data-tab=spells]').click();
  await expect(page.locator('#new')).toContainText('New spell');
  await fit(page); await shot(page, info, 'catalogue');
  await page.locator('#nav-close').click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Brute');
  await page.getByRole('button', { name: 'Edit content' }).click();
  await expect(page.getByLabel('Level', { exact: true })).toBeVisible();
  await page.locator('#tools-panel').click();
  await expect(page.locator('#tools')).toBeVisible();
  await expect(page.locator('#scrim')).toBeVisible();
  await fit(page); await shot(page, info, 'tools');
  await page.keyboard.press('Escape');
  await expect(page.locator('#tools')).toBeHidden();
  await expect(page.locator('#tools-panel')).toBeFocused();
});

test('balance shows package initiative and spell knobs before editing, with a path to the package entry', async ({ page }, info) => {
  // The shipped knobs move no package (journal, 2026-10-01); the studio still reads and edits one that does.
  const knobbed = structuredClone(catalogue);
  knobbed.balance.packages['tier:prowler'].knobs = [{ path: '/initiativeBonus', min: 1, max: 5, step: 1 }];
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: knobbed } }));
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Find your next move.' })).toBeVisible();
  await page.locator('#tools-panel').click();
  await page.locator('#balance-panel').click();
  const sheet = page.locator('#balance');
  await expect(sheet).toContainText('enabled packages have an entry');
  await expect(sheet.locator('.balance-item')).toHaveCount(knobbedPackages + knobbedSpells);
  await sheet.getByRole('combobox', { name: 'Filter balance knobs' }).selectOption('packages');
  await expect(sheet.locator('.balance-item')).toHaveCount(knobbedPackages);
  await expect(sheet.locator('.balance-value').first()).toContainText('Initiative +');
  await sheet.getByRole('searchbox', { name: 'Find a balance knob' }).fill('Prowler');
  const prowler = sheet.locator('.balance-item');
  await expect(prowler).toHaveCount(1);
  await prowler.locator('summary').click();
  await expect(prowler).toContainText('/initiativeBonus');
  await fit(page); await shot(page, info, 'balance-package');
  await prowler.getByRole('button', { name: 'Open content' }).click();
  await expect(page.locator('#detail .balance-value')).toContainText('Initiative +');
  await page.getByRole('button', { name: 'Edit content' }).click();
  await expect(page.locator('#balance-strip')).toContainText('What this package is for');
  await expect(page.locator('#balance-strip .knob .pointer')).toHaveText('/initiativeBonus');
  await page.getByLabel('Initiative bonus', { exact: true }).fill('99');
  await expect(page.locator('#balance-strip .knob .value')).toHaveText('99');
  await expect(page.locator('#balance-strip')).toContainText('outside');
});

test('reading a spell that is off shows its missing balance entry as a note, not a check', async ({ page }) => {
  const resting = structuredClone(catalogue);
  const spell = resting.spells.find(item => item.id === 'spell:basic_attack:v1');
  spell.enabled = false; spell.document.enabled = false;
  delete resting.balance.spells[aliasOfSpell(spell.id, resting.aliases)];
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: resting } }));
  // A hash alone does not reload the page, so the catalogue the override serves needs an explicit load.
  await page.goto(`/#entry=${encodeURIComponent(spell.path)}`);
  await page.reload();
  const reader = page.locator('#detail');
  await expect(reader).toContainText('Disabled · this content is not used in matches.');
  const card = reader.locator('.balance-item');
  await expect(card).toHaveCount(1);
  await expect(card).not.toHaveClass(/tone-bad/);
  await expect(card.locator('.balance-item-alert')).toHaveCount(0);
  await expect(card).toContainText('This content is off, so it is out of the build and nothing tunes it.');
});

test('reading a version no alias reaches shows its missing balance entry as a note, not a check', async ({ page }) => {
  const superseded = structuredClone(catalogue);
  const current = superseded.spells.find(item => item.id === 'spell:basic_attack:v1');
  const older = structuredClone(current);
  older.id = 'spell:basic_attack:v0'; older.document.id = older.id; older.path = 'Spells/base/basic_attack.v0.json';
  superseded.spells.push(older);
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: superseded } }));
  await page.goto(`/#entry=${encodeURIComponent(older.path)}`);
  await page.reload();
  const card = page.locator('#detail .balance-item');
  await expect(card).toHaveCount(1);
  await expect(card).not.toHaveClass(/tone-bad/);
  await expect(card).toContainText('No alias points at spell:basic_attack:v0');
});

test('a spell two aliases point at is two rows of the overview, one per entry', async ({ page }) => {
  const doubled = structuredClone(catalogue);
  doubled.aliases['spell:jab'] = 'spell:basic_attack:v1';
  doubled.balance.spells['spell:jab'] = { name: 'Basic Attack', class: 'Brute', intent: 'The same jab, read by another name.', keep: [], knobs: [] };
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: doubled } }));
  await page.reload();
  await page.locator('#tools-panel').click();
  await page.locator('#balance-panel').click();
  const sheet = page.locator('#balance');
  await sheet.getByRole('searchbox', { name: 'Find a balance knob' }).fill('spell:jab');
  await expect(sheet.locator('.balance-item')).toHaveCount(1);
  await expect(sheet.locator('.balance-item')).toContainText('read by another name');
  await sheet.getByRole('searchbox', { name: 'Find a balance knob' }).fill('Basic Attack');
  await expect(sheet.locator('.balance-item')).toHaveCount(2);
});

test('GitHub Pages subpath reads deployed data without a token', async ({ page }) => {
  await page.route('https://downfallz.github.io/maintest/**', async route => {
    const path = new URL(route.request().url()).pathname.replace('/maintest/', '');
    if (path === 'data/catalogue.json') { await route.fulfill({ json: catalogue }); return; }
    if (path === 'data/weights.json') { await route.fulfill({ json: { values: weights, order: Object.keys(weights) } }); return; }
    const relative = path || 'index.html';
    const folder = relative === 'viewer.css' ? 'viewer' : 'studio';
    await route.fulfill({ path: join(root, folder, relative) });
  });
  await page.goto('https://downfallz.github.io/maintest/');
  await expect(page.locator('.family-pick')).toHaveCount(3);
  await expect(page.locator('.hero-stats')).toContainText('21packages');
  await page.getByRole('button', { name: 'Compare energy & effects →' }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(45);
  await expect(page.locator('.strategy-spell .spell-value').first()).toContainText('a round');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Energy & effects', exact: true })).toBeVisible();
  await page.locator('#explore-view').click();
  await page.locator('.package-tile').first().click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Brute');
});

test('energy overview exposes gaps without a wide table and retains filters after reading', async ({ page }, info) => {
  await page.locator('#spells-view').click();
  await page.getByRole('button', { name: 'Energy & effects', exact: true }).click();
  await expect(page).toHaveURL(/#strategy$/);
  await expect(page.locator('.strategy-spell')).toHaveCount(45);
  await page.getByRole('button', { name: '1 energy', exact: true }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(10);
  await expect(page.getByRole('button', { name: 'Control 0', exact: true })).toBeVisible();
  await fit(page); await shot(page, info, 'energy-overview');
  await page.getByRole('button', { name: 'Control 0', exact: true }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(0);
  await expect(page.getByText('No spells in this combination.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'All effects 10', exact: true }).click();
  const pummel = page.locator('.strategy-spell').filter({ hasText: 'Pummel' });
  await pummel.locator('summary').click();
  await pummel.getByRole('button', { name: 'Full spell →' }).click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Pummel');
  await page.getByRole('button', { name: '← Back', exact: true }).click();
  await expect(page.getByRole('button', { name: '1 energy', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.strategy-spell')).toHaveCount(10);
  await page.locator('.strategy-spell').last().scrollIntoViewIfNeeded();
  const sticky = await page.locator('.strategy-sticky').boundingBox();
  expect(sticky.y).toBeGreaterThanOrEqual(0);
  expect(sticky.y + sticky.height).toBeLessThan(page.viewportSize().height);
  await fit(page);
});

test('package comparison combines distinct spells and separates caster effects', async ({ page }, info) => {
  await page.getByRole('button', { name: 'Compare energy & effects →' }).click();
  await page.getByText('Compare packages', { exact: true }).click();
  await page.getByLabel('Spell scope', { exact: true }).selectOption('packages');
  await expect(page.locator('.strategy-spell')).toHaveCount(0);
  await page.getByLabel('Add a package', { exact: true }).selectOption('tier:brute:v1');
  await expect(page.locator('.strategy-spell')).toHaveCount(2);
  await page.getByLabel('Add a package', { exact: true }).selectOption('tier:occultist:v1');
  await expect(page.locator('.strategy-spell')).toHaveCount(4);
  await page.getByLabel('Include starting kit').check();
  await expect(page.locator('.strategy-spell')).toHaveCount(7);
  await fit(page); await shot(page, info, 'package-comparison');
  await page.getByRole('button', { name: 'Remove Brute', exact: true }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(5);
  await page.getByLabel('Spell scope', { exact: true }).selectOption('all');
  await page.getByText('Compare packages', { exact: true }).click();
  await page.getByRole('button', { name: '0 energy', exact: true }).click();
  await page.getByRole('button', { name: 'On caster', exact: true }).click();
  await page.locator('.strategy-effect').filter({ hasText: 'Energy' }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(1);
  await expect(page.locator('.strategy-spell')).toContainText('Momentum');
  await expect(page.locator('.strategy-caster')).toContainText('Energy + 2');
  await page.getByRole('button', { name: 'On targets', exact: true }).click();
  await expect(page.locator('.strategy-spell')).toHaveCount(1);
  await expect(page.locator('.strategy-spell')).toContainText('Wait');
  await fit(page);
});


test('saving a package preserves its document type after browsing another catalogue tab', async ({ page }) => {
  let submitted;
  await page.route('**/api/documents', async route => {
    submitted = route.request().postDataJSON();
    const updated = structuredClone(catalogue);
    const item = updated.tiers.find(item => item.path === submitted.path);
    item.document = submitted.document; item.name = submitted.document.name;
    await route.fulfill({ json: { ok: true, result: { saved: submitted.path, catalogue: updated } } });
  });
  await page.locator('.package-tile').first().click();
  await page.getByRole('button', { name: 'Edit content' }).click();
  await page.getByLabel('Name', { exact: true }).fill('Renamed package');
  await page.locator('#browse').click();
  await page.locator('[data-tab=spells]').click();
  await page.locator('#nav-close').click();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.locator('#banner')).toContainText('Saved');
  expect(submitted.kind).toBe('Tier');
  expect(submitted.document.name).toBe('Renamed package');
  await page.getByRole('button', { name: 'Back to reading', exact: true }).click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Renamed package');
});
