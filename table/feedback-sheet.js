// Report or suggest (ADR 0189, docs/tabletop/feedback-plan.md, R2): one entry, a sheet whose first question is
// "something is wrong" or "I have an idea", and the page's own error catcher. The page sends the words, the page's
// path and its last errors; the host adds the table, the seat, the round and the position, which a page cannot be
// trusted with. Nothing here is written to the device: a draft and the errors live as long as the page.

// The host's own values (SendFeedback.BugCategories and IdeaPlaces), in the order the sheet asks them.
export const BUG_CATEGORIES = [
  { value: 'rules', label: 'The rules did something wrong' },
  { value: 'looks', label: 'Something looks wrong' },
  { value: 'stuck', label: 'It is stuck or slow' },
  { value: 'other', label: 'Something else' },
];
export const IDEA_PLACES = [
  { value: 'rules', label: 'The rules' },
  { value: 'battle', label: 'The battle' },
  { value: 'lobby', label: 'The lobby and friends' },
  { value: 'other', label: 'Something else' },
];
export const SHORTEST = 10;
export const LONGEST = 2000;
const KEPT_ERRORS = 10;
const LONGEST_ERROR = 500;
// An address's query and fragment, as the host cuts them (FeedbackApi.WithoutQueries): a seat link carries its key in one.
const QUERIES = /[?#][^\s'")]*/g;

/** A sheet nobody has filled yet: no kind chosen, and the position attached when there is one. */
export function feedbackDraft() {
  return { kind: null, category: null, happened: '', expected: '', attach: true, where: null, problem: '', wish: '' };
}

const length = text => (text ?? '').trim().length;
const fits = (text, required) => (required ? length(text) >= SHORTEST : true) && length(text) <= LONGEST;

/** Whether Send may be pressed: a choice made, and the required words between ten and 2,000 characters. */
export function feedbackReady(draft) {
  if (draft.kind === 'bug') return Boolean(draft.category) && fits(draft.happened, true) && fits(draft.expected, false);
  if (draft.kind === 'idea') return Boolean(draft.where) && fits(draft.problem, true) && fits(draft.wish, false);
  return false;
}

const optional = (name, text) => (length(text) ? { [name]: text.trim() } : {});

/** What `POST /api/feedback` is sent. An idea carries no position and no errors: it is about the game, not a moment. */
export function feedbackBody(draft, { page, errors, inGame }) {
  if (draft.kind === 'idea') {
    return { kind: 'idea', where: draft.where, problem: draft.problem.trim(), ...optional('wish', draft.wish), page };
  }
  return {
    kind: 'bug', category: draft.category, happened: draft.happened.trim(), ...optional('expected', draft.expected),
    attach: Boolean(inGame && draft.attach), page, errors,
  };
}

/** The line under the fields: what the host and the page add, so a player knows a few words are enough. */
export function attachedNote(kind, { inGame, attach = false }) {
  if (kind === 'idea') return inGame ? 'We add the page, and the game and round you send it from.' : 'We add the page you send it from.';
  if (!inGame) return 'We add the page and its last errors. A few words are enough.';
  return `We add the game, the round, ${attach ? 'this position, ' : ''}the page and its last errors. A few words are enough.`;
}

/** The page's last ten errors, newest last, held in memory only. */
export function createPageErrors(limit = KEPT_ERRORS) {
  const kept = [];
  return {
    add(message, where) {
      kept.push({ message: (String(message ?? '').replace(QUERIES, '') || 'Unknown error').slice(0, LONGEST_ERROR), where: where ?? null });
      if (kept.length > limit) kept.splice(0, kept.length - limit);
    },
    list: () => kept.map(one => ({ ...one })),
  };
}

// A script's address without its origin or query: the file is what helps, and a query can carry a seat's key.
function fileOf(address) {
  if (!address) return null;
  try { return new URL(address, 'https://page.invalid').pathname; } catch { return null; }
}

/** Keeps every uncaught error and unhandled rejection the page meets, from now on. */
export function catchPageErrors(target, errors) {
  target.addEventListener('error', event => {
    const file = fileOf(event.filename);
    errors.add(event.message || event.error?.message, file ? `${file}:${event.lineno ?? 0}` : null);
  });
  target.addEventListener('unhandledrejection', event => {
    const reason = event.reason;
    errors.add(reason instanceof Error ? reason.message : typeof reason === 'string' ? reason : String(reason ?? ''), null);
  });
}

const DID_NOT_ANSWER = 'The host did not answer. Your words are kept here: try again.';

/** The sentence a refusal reads as. The host's codes are stable (FeedbackErrors, FeedbackApi); its words are not shown raw. */
export function refusalSentence(status, answer) {
  switch (answer?.error) {
    case 'Feedback.TooSoon': {
      const seconds = Math.max(1, Number(answer.retryAfterSeconds) || 120);
      return `You sent feedback a moment ago. Try again in ${seconds} second${seconds === 1 ? '' : 's'}: your words are kept here.`;
    }
    case 'Feedback.TooMany': return 'You can send five a day. Try again tomorrow.';
    case 'Feedback.HostFull': return 'The game is taking no more feedback today. Try again tomorrow.';
    case 'Feedback.Muted': return 'Your feedback is paused for now.';
    case 'Feedback.SignInRequired': return 'Sign in to send feedback, or send it from a game you are playing.';
    case 'Feedback.SeatRequired': return 'Send your feedback from a game you are playing.';
    case 'Feedback.NotYourSeat': return 'This seat belongs to another player.';
    case 'Feedback.Invalid': return answer.message || 'Check what you wrote and try again.';
    case 'Feedback.Unavailable': return 'Your feedback could not be kept just now. Try again in a few minutes.';
    default: return status === 404 ? 'This table keeps no feedback.' : DID_NOT_ANSWER;
  }
}

/** Whether the host has stopped this player's feedback: the entry is then hidden for the page's life. */
export function mutedBy(status, answer) {
  return status === 403 && answer?.error === 'Feedback.Muted';
}

/** What the sheet says once the host kept it, or added it to what the player sent before. */
export function sentSentence(kind, answer) {
  if (answer?.merged) return kind === 'idea' ? 'Added to the idea you sent within the hour.' : 'Added to the report you sent from this round.';
  return `Thank you. Your ${kind === 'idea' ? 'idea' : 'report'} reached the game’s operator.`;
}

const choices = (name, list) => list.map(one =>
  `<label class="feedback-choice"><input type="radio" name="${name}" value="${one.value}"><span>${one.label}</span></label>`).join('');

const sheet = `<div class="feedback-panels lobby-page home-page">
  <dialog id="feedback-dialog" class="home-dialog feedback-dialog" aria-labelledby="feedback-title">
    <div class="dialog-heading"><div><p class="eyebrow">Report or suggest</p><h2 id="feedback-title">What would you like to tell us?</h2></div><button id="feedback-close" type="button" aria-label="Close report or suggest">×</button></div>
    <form id="feedback-form" class="home-dialog-body" novalidate>
      <div id="feedback-kinds" class="feedback-kinds">
        <button id="feedback-bug" type="button" data-feedback-kind="bug"><strong>Something is wrong</strong><small>A rule that misbehaved, a screen that looks wrong, a game that is stuck</small></button>
        <button id="feedback-idea" type="button" data-feedback-kind="idea"><strong>I have an idea</strong><small>Something you would like the game to do</small></button>
      </div>
      <div id="feedback-bug-fields" hidden>
        <fieldset class="feedback-choices"><legend>What went wrong?</legend>${choices('feedback-category', BUG_CATEGORIES)}</fieldset>
        <label for="feedback-happened">What happened <small>· ${SHORTEST} characters at least</small></label><textarea id="feedback-happened" rows="4" maxlength="${LONGEST}"></textarea>
        <label for="feedback-expected">What you expected <small>· optional</small></label><textarea id="feedback-expected" rows="3" maxlength="${LONGEST}"></textarea>
        <label id="feedback-attach-row" class="feedback-attach" for="feedback-attach"><input id="feedback-attach" type="checkbox" checked><span>Attach this position<small>What your seat sees now, nothing of your opponent’s hidden choices</small></span></label>
      </div>
      <div id="feedback-idea-fields" hidden>
        <fieldset class="feedback-choices"><legend>What is it about?</legend>${choices('feedback-where', IDEA_PLACES)}</fieldset>
        <label for="feedback-problem">What were you trying to do, and what got in the way? <small>· ${SHORTEST} characters at least</small></label><textarea id="feedback-problem" rows="4" maxlength="${LONGEST}"></textarea>
        <label for="feedback-wish">How you would like it <small>· optional</small></label><textarea id="feedback-wish" rows="3" maxlength="${LONGEST}"></textarea>
      </div>
      <p id="feedback-note" class="muted" hidden></p>
      <p id="feedback-refusal" class="problem" role="alert" hidden></p>
      <p id="feedback-sent" class="feedback-sent" role="status" hidden></p>
      <div id="feedback-actions" class="dialog-actions" hidden><button id="feedback-back" type="button" class="secondary">Back</button><button id="feedback-send" type="submit" class="primary" disabled>Send</button></div>
      <div id="feedback-done" class="dialog-actions" hidden><button id="feedback-finish" type="button" class="primary">Close</button></div>
    </form>
  </dialog>
</div>`;

const TITLES = { bug: 'Something is wrong', idea: 'I have an idea', sent: 'Sent' };

/**
 * The sheet, added to the page once, opened by any element marked `data-feedback-open`. `token` names the seat the
 * page plays, if any, and `inGame` whether a position can be attached. A mute hides every entry through the root's
 * `data-feedback-muted`, whatever else shows or hides them.
 * @param {Document} doc
 * @param {{ request?: (path: string, init: RequestInit) => Promise<Response>, token?: () => string | null,
 *   inGame?: () => boolean, errors?: ReturnType<typeof createPageErrors>, page?: () => string,
 *   fallbackFocus?: () => HTMLElement | null, onClose?: () => void }} [options]
 */
export function mountFeedback(doc, { request = (...args) => fetch(...args), token = () => null, inGame = () => false, errors = createPageErrors(),
  page = () => doc.defaultView?.location.pathname ?? '/', fallbackFocus = () => null, onClose = () => {} } = {}) {
  doc.body.insertAdjacentHTML('beforeend', sheet);
  const el = id => doc.getElementById(id);
  const dialog = el('feedback-dialog');
  let draft = feedbackDraft(), step = 'choose', sending = false, opener = null;

  function render() {
    const game = Boolean(inGame());
    for (const kind of ['bug', 'idea']) el(`feedback-${kind}-fields`).hidden = step !== kind;
    el('feedback-kinds').hidden = step !== 'choose';
    el('feedback-title').textContent = TITLES[step] ?? 'What would you like to tell us?';
    el('feedback-attach-row').hidden = !game;
    el('feedback-actions').hidden = step !== 'bug' && step !== 'idea';
    el('feedback-done').hidden = step !== 'sent';
    el('feedback-sent').hidden = step !== 'sent';
    el('feedback-note').hidden = step !== 'bug' && step !== 'idea';
    el('feedback-note').textContent = step === 'bug' || step === 'idea' ? attachedNote(step, { inGame: game, attach: draft.attach }) : '';
    el('feedback-send').disabled = sending || !feedbackReady(draft);
  }
  function refuse(sentence) {
    el('feedback-refusal').textContent = sentence ?? '';
    el('feedback-refusal').hidden = !sentence;
  }
  function fill() {
    for (const [field, id] of [['happened', 'feedback-happened'], ['expected', 'feedback-expected'], ['problem', 'feedback-problem'], ['wish', 'feedback-wish']]) el(id).value = draft[field];
    el('feedback-attach').checked = draft.attach;
    for (const input of dialog.querySelectorAll('input[type="radio"]')) {
      input.checked = input.value === (input.name === 'feedback-category' ? draft.category : draft.where);
    }
  }
  function choose(kind) {
    draft.kind = kind; step = kind; refuse(null); render();
    dialog.querySelector(`#feedback-${kind}-fields input`)?.focus();
  }

  for (const button of dialog.querySelectorAll('[data-feedback-kind]')) button.addEventListener('click', () => choose(button.dataset.feedbackKind));
  // A radio's or a checkbox's change is an input in every browser but an older Safari: both are read, which is harmless.
  const onField = event => {
    const target = event.target;
    if (target.name === 'feedback-category') draft.category = target.value;
    else if (target.name === 'feedback-where') draft.where = target.value;
    else if (target.id === 'feedback-attach') draft.attach = target.checked;
    else if (target.tagName === 'TEXTAREA') draft[target.id.slice('feedback-'.length)] = target.value;
    render();
  };
  dialog.addEventListener('input', onField);
  dialog.addEventListener('change', onField);
  el('feedback-back').addEventListener('click', () => { step = 'choose'; refuse(null); render(); el(`feedback-${draft.kind ?? 'bug'}`).focus(); });
  el('feedback-close').addEventListener('click', () => dialog.close());
  el('feedback-finish').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => {
    if (step === 'sent') { draft = feedbackDraft(); fill(); }
    step = 'choose'; refuse(null);
    const back = opener?.getClientRects().length ? opener : fallbackFocus();
    opener = null;
    back?.focus({ preventScroll: true });
    onClose();
  });
  el('feedback-form').addEventListener('submit', async event => {
    event.preventDefault();
    if (sending || !feedbackReady(draft)) return;
    const kind = draft.kind, seat = token();
    sending = true; refuse(null); render();
    let status = 0, answer = null;
    try {
      const response = await request('/api/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(seat ? { 'X-Seat-Token': seat } : {}) },
        body: JSON.stringify(feedbackBody(draft, { page: page(), errors: errors.list(), inGame: Boolean(inGame()) })),
      });
      status = response.status;
      answer = await response.json().catch(() => null);
    } catch {
      // A request that never reached the host is said as such; the words stay in the sheet.
    } finally {
      sending = false;
    }
    const kept = status === 200 || status === 201;
    if (mutedBy(status, answer)) doc.documentElement.dataset.feedbackMuted = 'true';
    // Closed while it was sending: what was kept leaves the sheet, what was refused stays to be sent again.
    if (!dialog.open) {
      if (kept) { draft = feedbackDraft(); fill(); }
      render();
      return;
    }
    if (kept) {
      step = 'sent';
      el('feedback-sent').textContent = sentSentence(kind, answer);
      render();
      el('feedback-finish').focus();
      return;
    }
    refuse(refusalSentence(status, answer));
    render();
  });
  doc.addEventListener('click', event => {
    const entry = event.target.closest?.('[data-feedback-open]');
    if (!entry || dialog.open) return;
    opener = entry;
    fill(); render();
    dialog.showModal();
    el('feedback-bug').focus();
  });
  render();
  return {
    isOpen: () => dialog.open,
    muted: () => doc.documentElement.dataset.feedbackMuted === 'true',
  };
}
