import { coachCard, leavePath, leavingSkips, progressPath, tutorialBand, tutorialBrief, tutorialOf } from '../../table/battle-tutorial.js';
import { NO_CONCEPTS, RULES_BOOK, loadConcepts } from '../../table/concepts.js';

type Coach = NonNullable<ReturnType<typeof coachCard>>;
type Concept = Coach['cards'][number];
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
const paragraphs = (lines: readonly string[]) => lines.map(line => { const p = document.createElement('p'); p.textContent = line; return p; });

/**
 * The coach of a tutorial (ADR 0187): the band's words, the card of the lesson step the question asked shows, and
 * the concept cards that step opens, once, before the question's own sheet. Nothing is kept: a card opens when its
 * step first shows on this page, and again from the coach card. The cards are read from the rulebook the table
 * serves (rulebook-plan.md R4), and each links to its section in a new tab, so the battle stays on its question.
 */
export function tutorialControls(onChange: () => void, request: (path: string, options?: RequestInit) => Promise<Response>) {
  const dialog = el<HTMLDialogElement>('concept-dialog'), coach = el('coach');
  let shown: Coach | null = null, key = '', queue: Concept[] = [], pointed: HTMLElement[] = [];
  let latest: unknown = null, doneSaid = false, concepts = NO_CONCEPTS, asked = false;
  const opened = new Set<string>();
  /**
   * Reads the rulebook's cards once a tutorial shows, and not in a game; until they are there a step shows without
   * them. Read, the coach card is drawn again with them, and a step whose cards have not opened yet opens them. Not
   * read (offline, the host not reached), the next practice asks again.
   */
  async function readConcepts() {
    if (asked) return;
    asked = true;
    concepts = await loadConcepts(async () => {
      const response = await request(new URL(RULES_BOOK, document.baseURI).href);
      return response.ok ? response.json() : null;
    });
    if (concepts.all.length) { key = ''; onChange(); }
  }
  // Where the card stands on the field, to come back to from a sheet.
  const home = { parent: coach.parentElement!, before: coach.nextSibling };
  // In a tutorial the band stands in the header, in place of the opponent, History, Help and the notifications,
  // not over the field (battle.css); a plain practice keeps it on the field, where it comes back to.
  const band = el('practice-band'), bandHome = { parent: band.parentElement!, before: band.nextSibling };
  const header = document.querySelector<HTMLElement>('#battle .hud-top')!;
  function toHeader() {
    if (band.parentElement !== header) header.insertBefore(band, header.querySelector('.tools'));
  }
  function toField() {
    if (band.parentElement !== bandHome.parent) bandHome.parent.insertBefore(band, bandHome.before);
  }
  // A sheet docks under the field unless the screen gives it a side (battle.css, the spell choice's own query).
  const beside = window.matchMedia('(min-width: 760px), (orientation: landscape) and (max-height: 520px)');
  beside.addEventListener('change', () => onChange());

  /**
   * On a phone the field over a docked sheet is too short to carry the card (#650): the card stands in the
   * sheet, under the spell choice's heading or at the top of the command sheet, and comes back to the field
   * when the sheet closes.
   */
  function place() {
    const combat = el<HTMLDialogElement>('combat-dialog'), planning = el<HTMLDialogElement>('planning-dialog');
    // Only a card that shows: a game, a plain practice and a tutorial between two steps leave the sheets as they are.
    const docked = Boolean(shown) && document.getElementById('battle')!.dataset.docked === 'true' && !beside.matches;
    const sheet = docked ? [combat, planning].find(one => one.open) ?? null : null;
    if (sheet === combat && coach.previousElementSibling?.id !== 'combat-heading') el('combat-heading').after(coach);
    else if (sheet === planning && coach.parentElement !== planning) planning.prepend(coach);
    else if (!sheet && coach.parentElement !== home.parent) home.parent.insertBefore(coach, home.before);
    coach.classList.toggle('in-sheet', Boolean(sheet));
  }

  /** Says to the host that the account skipped or finished this tutorial; the host keeps it, or not: the page goes on. */
  async function say(what: 'skip' | 'done') {
    const path = progressPath(latest, what);
    if (!path) return;
    try { await request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}', keepalive: true }); }
    // The host was not reached: a done is said again at the next change; a skip not kept opens the tutorial once more.
    catch { if (what === 'done') doneSaid = false; }
  }
  /** Lets go of the tutorial's practice at the host: the next time it opens, it starts from its cut. */
  async function letGo() {
    const path = leavePath(latest);
    if (!path) return;
    // Not let go of, it ends by itself after an idle hour, and a done one is not resumed: the page goes on either way.
    try { await request(path, { method: 'DELETE', headers: { 'Content-Type': 'application/json' }, body: '{}', keepalive: true }); } catch { /* The host was not reached. */ }
  }
  // Leaving waits for the host's answer this long and no longer: a slow network must not hold the person in a
  // tutorial they asked to leave, and the requests, sent with keepalive, outlive the page.
  const SKIP_WAIT_MS = 1500;
  async function leaveTo(address: string) {
    // A tutorial not done is skipped (O4) before its practice is let go of, which the skip does not need.
    const skipping = leavingSkips(latest);
    const told = async () => {
      if (skipping) await say('skip');
      await letGo();
    };
    await Promise.race([told(), new Promise(resolve => setTimeout(resolve, SKIP_WAIT_MS))]);
    location.assign(address);
  }
  /**
   * Leaving a tutorial, done or not, lets go of its practice, and one not done is skipped (O4): the host is told
   * before the page goes where the link leads.
   */
  function leave(event: MouseEvent) {
    // A click that opens the link elsewhere leaves this page, and the tutorial, where they are.
    if (!leavePath(latest) || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    void leaveTo((event.currentTarget as HTMLAnchorElement).href);
  }
  for (const id of ['practice-leave', 'coach-leave']) el(id).addEventListener('click', leave);
  el('practice-brief-skip').addEventListener('click', () => {
    el<HTMLButtonElement>('practice-brief-skip').disabled = true;
    void leaveTo('/');
  });

  function draw() {
    const card = queue[0];
    if (!card) return;
    text('concept-title', card.title); text('concept-example', card.example); text('concept-rulebook', card.rules.label);
    el<HTMLAnchorElement>('concept-rulebook').href = card.rules.href;
    el('concept-lines').replaceChildren(...paragraphs(card.lines));
    text('concept-next', queue.length > 1 ? `Next card · ${queue[1].title}` : 'Back to the battle');
  }
  function open(cards: Concept[]) {
    if (!cards.length || dialog.open) return false;
    queue = [...cards]; draw(); dialog.showModal(); onChange();
    return true;
  }
  el('concept-next').addEventListener('click', () => { queue.shift(); if (queue.length) draw(); else dialog.close(); });
  dialog.addEventListener('close', () => { queue = []; onChange(); });
  // A tutorial done is not played on: its end card stays, with Play again, which starts it over from its cut, and Leave.
  el('coach-again').addEventListener('click', () => el('practice-restart').click());

  function point(card: Coach | null) {
    const next = [card?.creature ? document.querySelector<HTMLElement>(`#actor-layer [data-creature="${card.creature}"]`) : null,
      card?.control ? document.getElementById(card.control) : null].filter((one): one is HTMLElement => Boolean(one));
    for (const one of pointed) if (!next.includes(one)) delete one.dataset.coach;
    for (const one of next) one.dataset.coach = 'true';
    pointed = next;
  }

  /** Draws the band and the coach card for this view; `canAuto` says the field is clear for a card to open by itself. */
  function render(view: unknown, { held, canAuto }: { held: boolean; canAuto: boolean }) {
    const lesson = tutorialOf(view);
    if (lesson) { latest = view; void readConcepts(); }
    if (lesson) {
      const brief = tutorialBrief(lesson);
      text('practice-brief-begin', brief.begin); text('practice-brief-note', brief.note);
      el('practice-brief-skip').hidden = !brief.skip;
      // The host says the tutorial is done: the account keeps it, said once for this practice.
      if (lesson.done && !doneSaid) { doneSaid = true; void say('done'); }
      // Kept until the next practice is opened: a view that is not there yet leaves the band as it reads.
      document.getElementById('battle')!.dataset.tutorial = String(lesson.order);
      const band = tutorialBand(lesson);
      text('practice-eyebrow', band.eyebrow); text('practice-title', band.title); text('practice-detail', band.detail);
      text('practice-brief-eyebrow', band.eyebrow); text('practice-restart', band.restart);
      el('practice-band').setAttribute('aria-label', 'Tutorial');
      toHeader();
    }
    shown = lesson ? coachCard(view, { held, concepts }) : null;
    coach.hidden = !shown;
    coach.dataset.done = String(shown?.done === true);
    const nextKey = shown?.key ?? '';
    if (shown && nextKey !== key) {
      text('coach-step', shown.eyebrow);
      el('coach-lines').replaceChildren(...paragraphs(shown.lines));
      el('coach-cards').replaceChildren(...shown.cards.map((card: Concept) => {
        const button = document.createElement('button'); button.type = 'button'; button.dataset.concept = card.id;
        const mark = document.createElement('span'); mark.setAttribute('aria-hidden', 'true'); mark.textContent = ' ⓘ';
        button.append(card.title, mark);
        button.addEventListener('click', () => open([card]));
        return button;
      }));
      el('coach-cards').hidden = shown.cards.length === 0;
      el('coach-end').hidden = !shown.done;
    }
    key = nextKey;
    place();
    point(shown);
    // Read only once it has opened: a step whose cards could not open yet still owes them.
    if (shown && owed() && canAuto && open(shown.cards)) opened.add(shown.key);
  }
  const owed = () => Boolean(shown?.cards.length && !opened.has(shown.key));
  /** Whether a concept card is open or still to open for the step shown: the question's own sheet waits for it. */
  const pending = () => dialog.open || owed();
  /** A new practice teaches its steps again. */
  function reset() {
    document.getElementById('battle')!.dataset.tutorial = ''; opened.clear(); key = ''; latest = null; doneSaid = false;
    if (!concepts.all.length) asked = false;
    toField(); text('practice-restart', 'Restart the round');
    text('practice-brief-begin', 'Begin'); el('practice-brief-skip').hidden = true; el<HTMLButtonElement>('practice-brief-skip').disabled = false;
    if (dialog.open) dialog.close();
  }
  return { render, pending, reset };
}
