import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
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

async function fit(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}
async function shot(page, info, name) {
  await page.screenshot({ path: info.outputPath(`${name}.png`), fullPage: false, animations: 'disabled' });
}

test.beforeEach(async ({ page }) => {
  await page.route('**/api/catalogue', route => route.fulfill({ json: { ok: true, result: catalogue } }));
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

test('GitHub Pages subpath reads deployed data without a token', async ({ page }) => {
  await page.route('https://downfallz.github.io/maintest/**', async route => {
    const path = new URL(route.request().url()).pathname.replace('/maintest/', '');
    if (path === 'data/catalogue.json') { await route.fulfill({ json: catalogue }); return; }
    const relative = path || 'index.html';
    const folder = relative === 'viewer.css' ? 'viewer' : 'studio';
    await route.fulfill({ path: join(root, folder, relative) });
  });
  await page.goto('https://downfallz.github.io/maintest/');
  await expect(page.locator('.family-pick')).toHaveCount(3);
  await expect(page.locator('.hero-stats')).toContainText('21packages');
  await page.locator('.package-tile').first().click();
  await expect(page.locator('.reader-heading h2')).toHaveText('Brute');
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
