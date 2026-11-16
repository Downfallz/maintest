// Voiding a match from the admin panel (ADR 0176): a sheet that asks why -- a category and an explanation the players
// will read -- names the table and what happens to it, and voids nothing without both. A void is final.

export const VOID_CATEGORIES = Object.freeze([
  { value: 'stuck', label: 'The match is stuck' },
  { value: 'engine', label: 'The engine misbehaved' },
  { value: 'abuse', label: 'A player abused the game' },
  { value: 'mistake', label: 'The operator made a mistake' },
  { value: 'other', label: 'Other' },
]);

export const SHORTEST_EXPLANATION = 10;
export const LONGEST_EXPLANATION = 500;

/** What a category is, in words. */
export function voidLabel(category) {
  return VOID_CATEGORIES.find(one => one.value === category)?.label ?? 'Voided';
}

/** The request a void sends, or the reason it is not sent yet. The host checks it again. */
export function voidAsked(category, explanation) {
  const said = String(explanation ?? '').trim();
  if (!VOID_CATEGORIES.some(one => one.value === category)) return { problem: 'Choose why the match is voided.' };
  if (said.length < SHORTEST_EXPLANATION) return { problem: `Explain it to the players in at least ${SHORTEST_EXPLANATION} characters.` };
  if (said.length > LONGEST_EXPLANATION) return { problem: `Keep the explanation to ${LONGEST_EXPLANATION} characters.` };
  return { body: { category, explanation: said } };
}

/** What happens when the match is voided, said before it is. */
export function voidWarning(row) {
  const what = row?.over
    ? 'Its result is withdrawn: it no longer counts on the ladder or in either player\'s statistics, and their finished games show it as voided.'
    : 'It ends now, with no result, and its seats stop working.';
  return `${what} Each player is told your explanation. A void is final.`;
}

/** What the page says after the host answered a void. A refusal keeps the host's own sentence. */
export function voidSaid(answer, id) {
  if (!answer?.ok) return { refused: true, line: answer?.body?.message ?? `The host refused it (${answer?.status}).` };
  return { refused: false, line: answer.body?.withdrawn ? `${id} voided; its result is withdrawn.` : `${id} voided.` };
}

/**
 * The Void sheet. `send(method, path, body)` is the admin transport's; `after(line, refused)` is told what happened,
 * once the sheet is closed, for the page to say it and redraw. Returns `open(row)`, for a table card or a session row:
 * `{ id, players, over }`.
 */
export function mountVoid({ send, element, after }) {
  let shown = null;
  element('void-category').replaceChildren(...[{ value: '', label: 'Choose…' }, ...VOID_CATEGORIES].map(one => Object.assign(document.createElement('option'), { value: one.value, textContent: one.label })));

  function check() {
    const asked = voidAsked(element('void-category').value, element('void-explanation').value);
    element('void-submit').disabled = Boolean(asked.problem);
    element('void-count').textContent = `${element('void-explanation').value.trim().length} / ${LONGEST_EXPLANATION}`;
    return asked;
  }

  function open(row) {
    shown = row;
    element('void-sheet-name').textContent = row.id;
    element('void-players').textContent = row.players ?? '';
    element('void-warning').textContent = voidWarning(row);
    element('void-category').value = '';
    element('void-explanation').value = '';
    element('void-said').textContent = '';
    check();
    const sheet = element('void-sheet');
    if (!sheet.open) sheet.showModal?.() ?? sheet.setAttribute('open', '');
  }

  element('void-category').addEventListener('change', check);
  element('void-explanation').addEventListener('input', check);
  element('void-cancel').addEventListener('click', () => element('void-sheet').close?.());
  element('void-submit').addEventListener('click', async () => {
    const asked = check();
    if (asked.problem || !shown) return;
    element('void-submit').disabled = true;
    const answer = await send('POST', `/api/tables/${encodeURIComponent(shown.id)}/void`, asked.body);
    const outcome = voidSaid(answer, shown.id);
    if (outcome.refused) {
      element('void-said').textContent = outcome.line;
      element('void-submit').disabled = false;
      return;
    }
    element('void-sheet').close?.();
    await after(outcome.line, false);
  });

  return { open };
}
