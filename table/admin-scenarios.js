// Headquarters' match scenarios (docs/tabletop/scenarios-plan.md, S5, ADR 0179): a recorded session cut at the top of a
// round from its row in the Sessions tab, and the Scenarios tab that lists them with the link a signed-in player opens
// as practice, and lists one under Practice for every signed-in player (S6). The host cuts, keeps and lists; this page
// asks and draws.

export const LONGEST_TITLE = 80;
export const LONGEST_GOAL = 300;

/** Each scenario as the tab draws it, from `GET /api/admin/scenarios`. */
export function scenarioRows(answer) {
  return (answer?.scenarios ?? []).map(one => ({
    id: one.id,
    title: one.title || `Round ${one.round}`,
    goal: one.goal ?? '',
    facts: `session ${one.session ?? 'unknown'} · round ${one.round} · played as ${one.seat}`,
    session: one.session ? `/session/${encodeURIComponent(one.session)}` : null,
    link: one.link,
    listed: one.listed === true,
  }));
}

/** The listing toggle of a row: what it says, and what it sends to the host when pressed. */
export function listingToggle(row) {
  return row.listed
    ? { label: 'take off Practice', state: 'Listed under Practice', body: { listed: false } }
    : { label: 'list under Practice', state: '', body: { listed: true } };
}

/** The rounds offered as the chooser's options, and what to say when there are none or the journal stopped early. */
export function roundChoices(answer) {
  const rounds = (answer?.rounds ?? []).filter(Number.isInteger);
  const lines = [];
  if (!rounds.length) lines.push('This session offers no round to start from.');
  if (answer?.stoppedBy) lines.push(`The journal stopped rebuilding after round ${rounds.at(-1) ?? '—'}: ${answer.stoppedBy}`);
  return { rounds, note: lines.join(' ') };
}

/** What the sheet sends to cut a scenario, or the problem that keeps it from being sent. */
export function cutAsked({ session, round, seat = 'player1', title = '', goal = '' }) {
  const chosen = Number(round);
  if (!session) return { problem: 'Choose a session.' };
  if (!Number.isInteger(chosen) || chosen < 1) return { problem: 'Choose a round to start from.' };
  if (seat !== 'player1' && seat !== 'player2') return { problem: 'Choose the seat the player plays.' };
  if (title.trim().length > LONGEST_TITLE) return { problem: `The title is ${LONGEST_TITLE} characters at most.` };
  if (goal.trim().length > LONGEST_GOAL) return { problem: `The goal is ${LONGEST_GOAL} characters at most.` };
  const body = { session, round: chosen, seat };
  if (title.trim()) body.title = title.trim();
  if (goal.trim()) body.goal = goal.trim();
  return { body };
}

/** The link a player opens, as an absolute address to copy. */
export function shareLink(link, origin) {
  return link ? new URL(link, origin).href : '';
}

// ---- The tab and the sheet. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

async function copied(text) {
  try { await navigator.clipboard.writeText(text); return true; } catch { return false; }
}

/**
 * Draws the Scenarios tab and the sheet that cuts one from a session row. `send(method, path, body)` is the admin
 * transport's; `shown(available)` says whether this host keeps scenarios, which a host recording nothing does not.
 */
export function mountScenarios({ send, element, shown }) {
  let session = null;
  const origin = () => globalThis.location?.origin ?? 'http://localhost';

  function said(id, line, refused = false) {
    const told = element(id);
    told.textContent = line;
    told.classList.toggle('refused', refused);
  }

  function linkTools(link) {
    const tools = made('div', 'session-links');
    const open = made('a', '', 'open');
    open.href = link;
    open.target = '_blank';
    open.rel = 'noopener';
    const copy = made('button', 'link-button', 'copy link');
    copy.type = 'button';
    copy.addEventListener('click', async () => { copy.textContent = await copied(shareLink(link, origin())) ? 'copied' : 'copy failed'; });
    tools.append(open, copy);
    return tools;
  }

  async function redraw() {
    const answer = await send('GET', '/api/admin/scenarios');
    if (answer.status === 404) {
      shown(false);
      return;
    }
    shown(true);
    if (!answer.ok) {
      said('scenarios-said', answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    const rows = scenarioRows(answer.body);
    element('scenarios').replaceChildren(...rows.map(row => {
      const item = made('li', 'session-row scenario-row');
      item.dataset.scenario = row.id;
      const body = made('div');
      const toggle = listingToggle(row);
      const heading = made('div', 'session-id', row.title);
      if (toggle.state) heading.append(' ', made('span', 'status-tag listed', toggle.state));
      body.append(heading, made('div', 'muted', row.facts));
      if (row.goal) body.append(made('div', 'activity-detail', row.goal));
      const tools = linkTools(row.link);
      if (row.session) {
        const source = made('a', '', 'session');
        source.href = row.session;
        tools.append(source);
      }
      const listing = made('button', 'link-button', toggle.label);
      listing.type = 'button';
      listing.dataset.listing = row.listed ? 'listed' : 'unlisted';
      listing.addEventListener('click', async () => {
        listing.disabled = true;
        const answer = await send('PUT', `/api/admin/scenarios/${encodeURIComponent(row.id)}/listing`, toggle.body);
        said('scenarios-said', answer.ok
          ? (toggle.body.listed ? `“${row.title}” is listed under Practice for signed-in players.` : `“${row.title}” is off Practice; its link still opens it.`)
          : answer.body?.message ?? `The host answered ${answer.status}.`, !answer.ok);
        await redraw();
      });
      tools.append(listing);
      const remove = made('button', 'link-button danger-link', 'remove…');
      remove.type = 'button';
      remove.addEventListener('click', async () => {
        if (!globalThis.confirm?.(`Remove the scenario “${row.title}”? Its link stops working; practices already open keep going until they end.`)) return;
        const removed = await send('DELETE', `/api/admin/scenarios/${encodeURIComponent(row.id)}`);
        said('scenarios-said', removed.ok ? `Removed “${row.title}”.` : removed.body?.message ?? `The host answered ${removed.status}.`, !removed.ok);
        await redraw();
      });
      tools.append(remove);
      body.append(tools);
      item.append(body);
      return item;
    }));
    element('scenarios-empty').hidden = rows.length > 0;
    element('tab-scenarios-count').textContent = rows.length ? String(rows.length) : '';
  }

  function check() {
    const asked = cutAsked({ session, round: element('scenario-round').value, seat: element('scenario-seat').value, title: element('scenario-title').value, goal: element('scenario-goal').value });
    element('scenario-submit').disabled = Boolean(asked.problem);
    return asked;
  }

  /** Opens the sheet on a recorded session: the rounds it offers are read from the host before anything is chosen. */
  async function open(row) {
    session = row.id;
    element('scenario-sheet-name').textContent = row.id;
    element('scenario-players').textContent = row.players ?? '';
    element('scenario-round').replaceChildren();
    element('scenario-title').value = '';
    element('scenario-goal').value = '';
    element('scenario-made').hidden = true;
    said('scenario-said', 'Reading the rounds this session can start from…');
    element('scenario-submit').disabled = true;
    const sheet = element('scenario-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
    const answer = await send('GET', `/api/admin/scenarios/rounds?session=${encodeURIComponent(row.id)}`);
    if (session !== row.id) return;
    if (!answer.ok) {
      said('scenario-said', answer.body?.message ?? `The host answered ${answer.status}.`, true);
      return;
    }
    const { rounds, note } = roundChoices(answer.body);
    element('scenario-round').replaceChildren(...rounds.map(round => Object.assign(made('option', '', `Round ${round}`), { value: String(round) })));
    said('scenario-said', note, rounds.length === 0);
    check();
  }

  for (const id of ['scenario-round', 'scenario-seat']) element(id).addEventListener('change', check);
  for (const id of ['scenario-title', 'scenario-goal']) element(id).addEventListener('input', check);
  element('scenario-cancel').addEventListener('click', () => element('scenario-sheet').close?.());
  element('scenario-submit').addEventListener('click', async () => {
    const asked = check();
    if (asked.problem) return;
    element('scenario-submit').disabled = true;
    const answer = await send('POST', '/api/admin/scenarios', asked.body);
    if (!answer.ok) {
      said('scenario-said', answer.body?.message ?? `The host answered ${answer.status}.`, true);
      element('scenario-submit').disabled = false;
      return;
    }
    said('scenario-said', `Cut from round ${asked.body.round}. A signed-in player opens it as practice from this link:`);
    element('scenario-made').replaceChildren(made('code', '', shareLink(answer.body.link, origin())), linkTools(answer.body.link));
    element('scenario-made').hidden = false;
    await redraw();
  });

  return { redraw, open };
}
