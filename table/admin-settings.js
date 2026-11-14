// Headquarters' Settings tab (docs/tabletop/admin-plan.md, A4; ADR 0177): whether a new player may make an account,
// and the one line every page shows under its header. The host keeps both, stamped with who saved them; this page
// draws what it is sent and says what it saved.

import { ago } from './admin-players.js';

/** What registration open or closed means, in a line under the choice. */
export function registrationLine(open) {
  return open
    ? 'Open: a first sign-in with Google chooses a player name and has an account.'
    : 'Closed: a first sign-in is told new accounts are closed for now. Existing players sign in as before.';
}

/** How much of the notice is used, and whether it is over the host's limit. */
export function noticeCount(text, longest) {
  const used = String(text ?? '').trim().replace(/\s+/g, ' ').length;
  return { line: `${used} / ${longest}`, over: used > longest };
}

/** Who saved the settings last, and when; or that the defaults hold. */
export function savedLine(settings, now = Date.now()) {
  if (!settings?.updatedAt) return 'Never saved: the defaults hold, registration open and no notice.';
  return `Last saved ${ago(settings.updatedAt, now)}${settings.updatedBy ? ` by ${settings.updatedBy}` : ''}.`;
}

/** What the tab says after a save. A refusal keeps the host's own sentence. */
export function settingsSaid(answer) {
  if (!answer?.ok) return { refused: true, line: answer?.body?.message ?? `The host refused it (${answer?.status}).` };
  const settings = answer.body?.settings ?? {};
  const registration = settings.registrationOpen ? 'Registration is open' : 'Registration is closed';
  const notice = settings.notice ? 'the notice shows under every page\'s header' : 'no notice shows';
  return { refused: false, line: `Saved. ${registration}, and ${notice}.` };
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

/**
 * Draws the Settings tab into the page. `send(method, path, body)` is the admin transport's; `shown(available)` says
 * whether this host has settings at all, which a host signing nobody in does not; `warn(bool)` whether registration
 * is closed, which the tab marks.
 */
export function mountSettings({ send, element, shown, warn = () => {} }) {
  let longest = 200;

  function draw(settings) {
    element(settings.registrationOpen ? 'settings-open' : 'settings-closed').checked = true;
    element('settings-notice').value = settings.notice ?? '';
    element('settings-saved').textContent = savedLine(settings);
    drawChoice();
    drawCount();
    warn(settings.registrationOpen === false);
  }

  function drawChoice() {
    element('settings-registration-line').textContent = registrationLine(element('settings-open').checked);
  }

  function drawCount() {
    const count = noticeCount(element('settings-notice').value, longest);
    element('settings-notice-count').textContent = count.line;
    element('settings-notice-count').classList.toggle('refused', count.over);
    element('settings-save').disabled = count.over;
  }

  function say(line, refused) {
    const told = element('settings-said');
    told.textContent = line;
    told.classList.toggle('refused', refused);
  }

  async function redraw() {
    const answer = await send('GET', '/api/admin/settings');
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    if (!answer.ok) {
      say(answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    longest = answer.body.longestNotice ?? longest;
    element('settings-notice').maxLength = longest;
    draw(answer.body.settings ?? {});
  }

  element('settings-open').addEventListener('change', drawChoice);
  element('settings-closed').addEventListener('change', drawChoice);
  element('settings-notice').addEventListener('input', drawCount);
  element('settings-form').addEventListener('submit', async event => {
    event.preventDefault();
    element('settings-save').disabled = true;
    const answer = await send('PUT', '/api/admin/settings', {
      registrationOpen: element('settings-open').checked,
      notice: element('settings-notice').value,
    });
    if (answer.ok) draw(answer.body.settings ?? {});
    else drawCount();
    const outcome = settingsSaid(answer);
    say(outcome.line, outcome.refused);
  });

  return { redraw };
}
