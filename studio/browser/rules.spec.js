import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

// The rulebook's page (/rules, docs/tabletop/rulebook-plan.md R3), read by a guest against the real built book:
// the contents first on a phone and beside the text on a wide screen, a deep link that lands on its section, the
// filter, What changed, and the Rules entry of the menu. In French (R6) for a browser in French, and in the language
// the reader switches to, kept on the device. Offline is played on the real host (table.rules.js), where the service
// worker is at the root.

const book = JSON.parse(readFileSync(new URL('../../table/rules.json', import.meta.url), 'utf8'));
const french = JSON.parse(readFileSync(new URL('../../table/rules.fr.json', import.meta.url), 'utf8'));
const frenchTempo = french.parts.flatMap(part => part.sections).find(section => section.id === 'tempo');
const sections = book.parts.flatMap(part => part.sections);
const tempo = sections.find(section => section.id === 'tempo');
const sunder = sections.find(section => section.id === 'sunder');

async function guest(page) {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/me') return route.fulfill({ json: { accounts: 'google', account: null, googleClientId: 'test-client', operator: false, notice: null } });
    return route.fulfill({ status: 404, json: { message: 'Not here' } });
  });
  return errors;
}

async function open(page, hash = '') {
  await page.goto(`/table/rules.html${hash}`);
  await expect(page.locator('body')).toHaveAttribute('data-rules', 'ready');
}

async function noOverflow(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

const wide = page => page.viewportSize().width >= 900;

test('the contents list every part of the book, and a part opens to its sections', async ({ page }) => {
  const errors = await guest(page);

  await open(page);

  const contents = page.getByRole('navigation', { name: 'Contents' });
  await expect(contents.locator('summary')).toHaveText(book.parts.map(part => `${part.number} ${part.title}`));
  await expect(page.locator('#rules-status')).toHaveText(`${book.parts.length} parts, ${sections.length} sections`);
  const round = book.parts.find(part => part.sections.includes(tempo));
  await contents.locator('summary', { hasText: round.title }).click();
  await contents.getByRole('link', { name: `${tempo.number} ${tempo.title}` }).click();
  await expect(page).toHaveURL(/#tempo$/);
  await expect(page.locator('#tempo h3')).toBeInViewport();
  await noOverflow(page);
  expect(errors).toEqual([]);
});

test('on a phone the contents are the first screen, and every section leads back to them', async ({ page }) => {
  test.skip(page.viewportSize().width >= 900, 'a wide screen keeps the contents beside the text');
  await guest(page);

  await open(page);

  await expect(page.getByRole('navigation', { name: 'Contents' })).toBeInViewport();
  const back = page.locator('#tempo .rules-to-contents');
  await back.scrollIntoViewIfNeeded();
  await expect(back).toBeVisible();
  expect((await back.boundingBox()).height).toBeGreaterThanOrEqual(44);
  await back.click();
  await expect(page.getByRole('navigation', { name: 'Contents' })).toBeInViewport();
});

test('on a wide screen the contents stay beside the text while it scrolls', async ({ page }) => {
  test.skip(page.viewportSize().width < 900, 'a phone reads the contents first');
  await guest(page);

  await open(page, '#the-end-of-a-match');

  await expect(page.getByRole('navigation', { name: 'Contents' })).toBeInViewport();
  await expect(page.locator('#tempo .rules-to-contents')).toBeHidden();
  const [contents, text] = await Promise.all([page.locator('#rules-contents').boundingBox(), page.locator('#rules-book').boundingBox()]);
  expect(contents.x + contents.width).toBeLessThanOrEqual(text.x);
});

test('a deep link opens its section at the top of the screen, its part open in the contents', async ({ page }) => {
  await guest(page);

  await open(page, '#sunder');

  const heading = page.locator('#sunder h3');
  await expect(heading).toContainText(`${sunder.number} ${sunder.title}`);
  await expect.poll(() => heading.evaluate(node => Math.round(node.getBoundingClientRect().top))).toBeLessThan(80);
  await expect.poll(() => heading.evaluate(node => Math.round(node.getBoundingClientRect().top))).toBeGreaterThanOrEqual(0);
  const link = page.getByRole('navigation', { name: 'Contents' }).getByRole('link', { name: `${sunder.number} ${sunder.title}` });
  await expect(link).toHaveAttribute('aria-current', 'location');
  if (!wide(page)) await noOverflow(page);
});

test('the browser\'s back returns from a section to where the reader was', async ({ page }) => {
  await guest(page);
  await open(page, '#winning');

  const contents = page.getByRole('navigation', { name: 'Contents' });
  await contents.locator('summary', { hasText: book.parts.find(part => part.sections.includes(sunder)).title }).click();
  await contents.getByRole('link', { name: `${sunder.number} ${sunder.title}` }).click();
  await expect(page).toHaveURL(/#sunder$/);

  await page.goBack();

  await expect(page).toHaveURL(/#winning$/);
  await expect(page.locator('#winning h3')).toBeInViewport();
});

test('a concept card\'s id lands on the section it summarises, and the card is drawn there', async ({ page }) => {
  await guest(page);
  const card = book.cards.find(entry => entry.id === 'intent');
  const section = sections.find(entry => entry.id === card.section);

  await open(page, '#intent');

  await expect(page).toHaveURL(new RegExp(`#${section.id}$`));
  const drawn = page.locator(`#${section.id} .rules-card`);
  await expect(drawn).toBeVisible();
  await expect(drawn.locator('.concept-card-title')).toHaveText(section.card.title);
  await expect(drawn.locator('.concept-example')).toHaveText(section.card.example);
});

test('the filter keeps the sections that hold the words, and a deep link clears it', async ({ page }) => {
  await guest(page);
  await open(page);

  await page.getByLabel('Find a rule').fill('sunder');

  await expect(page.locator('#sunder')).toBeVisible();
  await expect(page.locator('#winning')).toBeHidden();
  await expect(page.locator('#rules-status')).toContainText(new RegExp(`of ${sections.length} holds? those words`));
  await page.getByLabel('Find a rule').fill('zzzz nothing');
  await expect(page.locator('#rules-status')).toHaveText('No rule holds those words. Try another word.');
  await page.evaluate(() => { location.hash = 'winning'; });
  await expect(page.getByLabel('Find a rule')).toHaveValue('');
  await expect(page.locator('#winning')).toBeVisible();
});

test('What changed, reached from the contents while a filter reads the book, shows the whole book again', async ({ page }) => {
  await guest(page);
  await open(page);
  await page.getByLabel('Find a rule').fill('sunder');
  await expect(page.locator('#rules-changes-list')).toBeHidden();

  await page.getByRole('navigation', { name: 'Contents' }).getByRole('link', { name: 'What changed' }).click();

  await expect(page.getByLabel('Find a rule')).toHaveValue('');
  await expect(page.locator('#rules-changes-list')).toBeVisible();
  await expect(page.locator('#winning')).toBeVisible();
});

test('every section shows its own link, a target a finger can reach, and its heading reads as its title alone', async ({ page }) => {
  await guest(page);
  await open(page, '#tempo');

  const link = page.getByRole('link', { name: `Link to ${tempo.number} ${tempo.title}` });

  await expect(link).toBeVisible();
  const box = await link.boundingBox();
  expect(Math.min(box.width, box.height)).toBeGreaterThanOrEqual(44);
  await expect(link).toHaveAttribute('href', '#tempo');
  await expect(page.getByRole('heading', { level: 3, name: `${tempo.number} ${tempo.title}`, exact: true })).toBeVisible();
});

test('What changed lists the book\'s changes, and the foot says where its numbers come from', async ({ page }) => {
  await guest(page);

  await open(page, '#what-changed');

  await expect(page.locator('#rules-changes-list li')).toHaveCount(book.changes.length);
  await expect(page.locator('#rules-changes-list li').first()).toContainText([...book.changes].sort((a, b) => b.date.localeCompare(a.date))[0].text);
  await expect(page.locator('#rules-drawn')).toContainText('drawn from the rule set and the content this table plays');
});

test('the main menu carries Rules, marked as the page a reader is on', async ({ page }) => {
  await guest(page);
  await open(page);

  const toggle = page.getByRole('button', { name: 'Open navigation', exact: true });
  if (await toggle.isVisible()) await toggle.click();

  const link = page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Rules', exact: true });
  await expect(link).toHaveAttribute('href', '/rules');
  await expect(link).toHaveAttribute('aria-current', 'page');
});

test('the switch reads the book in French at the same section, and the choice stays on the device', async ({ page }) => {
  const errors = await guest(page);
  await open(page, '#tempo');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');

  await page.getByRole('button', { name: 'Français' }).click();

  await expect(page.locator('body')).toHaveAttribute('data-language', 'fr');
  await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
  await expect(page.getByRole('button', { name: 'Français' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('navigation', { name: 'Sommaire' }).locator('summary')).toHaveText(french.parts.map(part => `${part.number} ${part.title}`));
  await expect(page.locator('#tempo h3')).toContainText(`${frenchTempo.number} ${frenchTempo.title}`);
  await expect(page.locator('#tempo h3')).toBeInViewport();
  await expect(page).toHaveURL(/#tempo$/);
  if (!wide(page)) await noOverflow(page);

  await page.reload();
  await expect(page.locator('body')).toHaveAttribute('data-language', 'fr');
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page.locator('body')).toHaveAttribute('data-language', 'en');
  await expect(page.locator('#tempo h3')).toContainText(`${tempo.number} ${tempo.title}`);
  expect(errors).toEqual([]);
});

test('a French book that cannot be read leaves the English one, in English, and the choice is not kept', async ({ page }) => {
  await guest(page);
  await page.route('**/rules.fr.json', route => route.fulfill({ status: 503, body: 'down' }));
  await open(page, '#tempo');

  await page.getByRole('button', { name: 'Français' }).click();

  await expect(page.locator('#rules-problem')).toHaveText('Could not read the rulebook. Check your connection and reload the page.');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('button', { name: 'English' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#tempo h3')).toContainText(`${tempo.number} ${tempo.title}`);
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page.locator('#rules-problem')).toBeHidden();
  expect(await page.evaluate(() => localStorage.getItem('downfall.language'))).toBe('en');
});

test.describe('a browser in French', () => {
  test.use({ locale: 'fr-CA' });

  test('opens the book in French, its contents, its filter and its changes', async ({ page }) => {
    const errors = await guest(page);

    await open(page, '#what-changed');

    await expect(page).toHaveTitle('Downfall Instinct — les règles');
    const sections = french.parts.flatMap(part => part.sections);
    await expect(page.locator('#rules-status')).toHaveText(`${french.parts.length} chapitres, ${sections.length} sections`);
    await expect(page.locator('#rules-changes-list li')).toHaveCount(french.changes.length);
    await expect(page.locator('#rules-drawn')).toContainText('vient des règles et du contenu de cette table');
    await page.getByLabel('Trouver une règle').fill('zzzz rien');
    await expect(page.locator('#rules-status')).toHaveText('Aucune règle ne contient ces mots. Essayez un autre mot.');
    await noOverflow(page);
    expect(errors).toEqual([]);
  });
});
