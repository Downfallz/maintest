// Signing in with Discord (sign-in methods plan, P3; #1194), and with Microsoft the same way (P6): the lobby's button
// leaves for Discord's authorize page,
// which is routed here to a page that sends the browser back to /signin/discord with a code and a state, as Discord
// does; the return page posts the code to a mocked host and goes back to the lobby. The return page and its modules are
// served from table/ at the root, as the table host serves them.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const table = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'table');
const DISCORD_APP = '1311111111111111111';
const AUTHORIZE = 'https://discord.com/oauth2/authorize**';
const MICROSOFT_APP = '6731de76-14a6-49ae-97bc-6eba6914391e';
const MICROSOFT_AUTHORIZE = 'https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize**';

/**
 * The lobby of a host that signs in with Google and Discord, nobody signed in. `discord` answers the code the return
 * page posts; `back` is what Discord's authorize page does: a query to send back with, or nothing to stay there.
 */
async function lobby(page, { discord = () => ({ json: { needsUsername: true, ticket: 'name-ticket' } }), back = state => `code=the-code&state=${state}` } = {}) {
  const seen = { errors: [], posted: [], authorized: [] };
  page.on('pageerror', error => seen.errors.push(error.message));
  await page.addInitScript(() => {
    window.google = { accounts: { id: { initialize() {}, renderButton(el) {
      const button = document.createElement('button'); button.textContent = 'Continue with Google'; button.style.height = '44px'; el.append(button);
    } } } };
  });
  // Discord's and Microsoft's authorize pages: each sends the browser back to the redirect it was given, with the
  // state the page sent.
  await page.route(url => url.href.startsWith(AUTHORIZE.slice(0, -2)) || url.href.startsWith(MICROSOFT_AUTHORIZE.slice(0, -2)), route => {
    const asked = new URL(route.request().url());
    seen.authorized.push(asked);
    const query = back(asked.searchParams.get('state'));
    const body = query === null
      ? '<title>Discord</title><p>Authorize Downfall Instinct?</p>'
      : `<title>Discord</title><script>location.replace(${JSON.stringify(`${asked.searchParams.get('redirect_uri')}?${query}`)})</script>`;
    return route.fulfill({ contentType: 'text/html', body });
  });
  // The return page, and the files it names from the root, as the table host serves them.
  await page.route(/\/signin\/(discord|microsoft)(\?|$)/, route => route.fulfill({ contentType: 'text/html', body: readFileSync(join(table, 'signin.html'), 'utf8') }));
  await page.route(/^http:\/\/127\.0\.0\.1:\d+\/(fonts\/)?[\w.-]+\.(js|css|svg|woff2)(\?.*)?$/, route => route.fulfill({ path: join(table, new URL(route.request().url()).pathname) }));
  await page.route(/\/lobby(\?|$)/, route => route.fulfill({ status: 302, headers: { location: '/table/lobby.html' } }));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/me') {
      return route.fulfill({ json: { accounts: 'signin', signIn: [{ provider: 'google', clientId: 'test-client' }, { provider: 'discord', clientId: DISCORD_APP }, { provider: 'microsoft', clientId: MICROSOFT_APP }], account: null, operator: false, notice: null } });
    }
    if (path === '/api/account/discord' || path === '/api/account/microsoft') {
      seen.posted.push(path === '/api/account/discord' ? route.request().postDataJSON() : { microsoft: route.request().postDataJSON() });
      const answer = discord();
      return route.fulfill({ status: answer.status ?? 200, headers: answer.headers, json: answer.json });
    }
    if (path === '/api/lobby') return route.fulfill({ json: { recording: true, format: '3v3', formats: ['3v3'], ongoing: 0, open: 0 } });
    if (['/api/lobby/adopt', '/api/lobby/mine', '/api/lobby/resume'].includes(path)) return route.fulfill({ json: { tables: [], invitations: [] } });
    return route.fulfill({ status: 404, json: { message: `Unexpected ${path}` } });
  });
  await page.goto('/table/lobby.html');
  await expect(page.locator('body')).toHaveAttribute('data-home', 'guest');
  return seen;
}

const discordButton = page => page.getByRole('button', { name: 'Continue with Discord' });

test('the Discord button sits under Google\'s, as tall as a finger needs', async ({ page }) => {
  const seen = await lobby(page);

  const google = await page.locator('#google-button').boundingBox();
  const discord = await discordButton(page).boundingBox();

  expect(discord.y).toBeGreaterThanOrEqual(google.y + google.height);
  expect(discord.height).toBeGreaterThanOrEqual(44);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(seen.errors).toEqual([]);
});

test('a new player back from Discord is asked for a name in the lobby', async ({ page }) => {
  const seen = await lobby(page);

  await discordButton(page).click();

  await expect(page.getByRole('dialog', { name: 'Choose your player name' })).toBeVisible();
  expect(new URL(page.url()).pathname).toBe('/table/lobby.html');
  const asked = seen.authorized[0];
  expect(asked.searchParams.get('client_id')).toBe(DISCORD_APP);
  expect(asked.searchParams.get('scope')).toBe('identify');
  expect(asked.searchParams.get('code_challenge_method')).toBe('S256');
  const origin = new URL(page.url()).origin;
  expect(seen.posted).toEqual([{ code: 'the-code', verifier: expect.stringMatching(/^[\w-]{43}$/), redirect: `${origin}/signin/discord` }]);
  expect(seen.errors).toEqual([]);
});

test('a return with another state stays on the return page with the way back, and posts nothing', async ({ page }, info) => {
  const seen = await lobby(page, { back: () => 'code=somebody-elses&state=forged' });

  await discordButton(page).click();

  await expect(page.locator('#signin-state')).toHaveText('This sign-in was not started from this tab, so it was not used. Sign in again from the lobby.');
  await expect(page.getByRole('link', { name: 'Back to the lobby' })).toBeVisible();
  expect(page.url()).toMatch(/\/signin\/discord$/);
  expect(seen.posted).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('signin-discord-mismatch.png'), fullPage: true, animations: 'disabled' });
  expect(seen.errors).toEqual([]);
});

test('a return Discord sent with an error says so on the return page, with the way back', async ({ page }, info) => {
  const seen = await lobby(page, { back: () => 'error=server_error' });

  await discordButton(page).click();

  await expect(page.locator('#signin-state')).toHaveText('Discord could not sign you in. Try again from the lobby.');
  await expect(page.getByRole('link', { name: 'Back to the lobby' })).toBeVisible();
  expect(seen.posted).toEqual([]);
  await page.screenshot({ path: info.outputPath('signin-discord-failed.png'), fullPage: true, animations: 'disabled' });
  expect(seen.errors).toEqual([]);
});

test('a sign-in the host refuses for its network\'s count says the host\'s words on the return page', async ({ page }) => {
  const message = 'Too many sign-ins from your network just now. Try again in a minute.';
  const seen = await lobby(page, { discord: () => ({ status: 429, json: { error: 'Account.TooManySignIns', message } }) });

  await discordButton(page).click();

  await expect(page.locator('#signin-state')).toHaveText(message);
  await expect(page.getByRole('link', { name: 'Back to the lobby' })).toBeVisible();
  expect(seen.posted).toHaveLength(1);
  expect(seen.errors).toEqual([]);
});

test('a sign-in the host was too busy to check for a second is posted once more and signs in (#1204)', async ({ page }) => {
  const message = 'Too many sign-ins from your network just now. Try again in a minute.';
  const answers = [{ status: 429, headers: { 'Retry-After': '1' }, json: { error: 'Account.TooManySignIns', message } }, { json: { needsUsername: true, ticket: 'name-ticket' } }];
  await page.clock.install();
  const seen = await lobby(page, { discord: () => answers.shift() });

  await discordButton(page).click();
  await expect(page.locator('#signin-state')).toHaveText('Checking your sign-in…');
  await expect.poll(() => seen.posted.length).toBe(1);
  // The wait the host asked for, and the up to one second more the page adds.
  await page.clock.fastForward(2000);

  await expect(page.getByRole('dialog', { name: 'Choose your player name' })).toBeVisible();
  expect(seen.posted).toHaveLength(2);
  expect(seen.posted[1]).toEqual(seen.posted[0]);
  expect(seen.errors).toEqual([]);
});

test('back from Discord\'s authorize page leaves the Discord button pressable', async ({ page }) => {
  const seen = await lobby(page, { back: () => null });
  await discordButton(page).click();
  await expect(page).toHaveTitle('Discord');

  await page.goBack();

  await expect(discordButton(page)).toBeEnabled();
  await discordButton(page).click();
  await expect(page).toHaveTitle('Discord');
  expect(seen.authorized).toHaveLength(2);
  expect(seen.errors).toEqual([]);
});

// Playwright's Chromium keeps no page in its back-forward cache, so the Back above reloads the lobby. A page the cache
// gives back fires pageshow with persisted, its button still as the press left it: played here on the page itself.
// Discord answering 204 leaves the lobby where it is, the button pressed.
test('a lobby the back-forward cache gives back gives the Discord button back', async ({ page }) => {
  const seen = await lobby(page);
  await page.route(AUTHORIZE, route => { seen.authorized.push(route.request().url()); return route.fulfill({ status: 204 }); });
  await discordButton(page).click();
  await expect.poll(() => seen.authorized.length).toBe(1);
  await expect(discordButton(page)).toBeDisabled();

  await page.evaluate(() => dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true })));

  await expect(discordButton(page)).toBeEnabled();
  expect(seen.errors).toEqual([]);
});

test('a new player back from Microsoft is asked for a name in the lobby, its button under Discord\'s', async ({ page }) => {
  const seen = await lobby(page);
  const microsoftButton = page.getByRole('button', { name: 'Continue with Microsoft' });
  const discord = await discordButton(page).boundingBox();
  const microsoft = await microsoftButton.boundingBox();
  expect(microsoft.y).toBeGreaterThanOrEqual(discord.y + discord.height);
  expect(microsoft.height).toBeGreaterThanOrEqual(44);

  await microsoftButton.click();

  await expect(page.getByRole('dialog', { name: 'Choose your player name' })).toBeVisible();
  const asked = seen.authorized[0];
  expect(asked.origin + asked.pathname).toBe('https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize');
  expect(asked.searchParams.get('client_id')).toBe(MICROSOFT_APP);
  expect(asked.searchParams.get('scope')).toBe('openid');
  const origin = new URL(page.url()).origin;
  expect(seen.posted).toEqual([{ microsoft: { code: 'the-code', verifier: expect.stringMatching(/^[\w-]{43}$/), redirect: `${origin}/signin/microsoft` } }]);
  expect(seen.errors).toEqual([]);
});
