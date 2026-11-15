import { coachCard, tutorialBand, tutorialOf } from '../../table/battle-tutorial.js';
import { conceptCard } from '../../table/concepts.js';

type Coach = NonNullable<ReturnType<typeof coachCard>>;
type Concept = NonNullable<ReturnType<typeof conceptCard>>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
const paragraphs = (lines: readonly string[]) => lines.map(line => { const p = document.createElement('p'); p.textContent = line; return p; });

/**
 * The coach of a tutorial (ADR 0187): the band's words, the card of the lesson step the question asked shows, and
 * the concept cards that step opens, once, before the question's own sheet. Nothing is kept: a card opens when its
 * step first shows on this page, and again from the coach card.
 */
export function tutorialControls(onChange: () => void) {
  const dialog = el<HTMLDialogElement>('concept-dialog'), coach = el('coach');
  let shown: Coach | null = null, key = '', queue: Concept[] = [], pointed: HTMLElement[] = [], dismissed = false;
  const opened = new Set<string>();

  function draw() {
    const card = queue[0];
    if (!card) return;
    text('concept-title', card.title); text('concept-example', card.example); text('concept-rulebook', card.rulebook.label);
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
  // The end card is read once: Keep playing puts it away, and the band keeps saying the tutorial is complete.
  el('coach-continue').addEventListener('click', () => { dismissed = true; onChange(); });

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
    if (lesson) {
      // Kept until the next practice is opened: a view that is not there yet leaves the band as it reads.
      document.getElementById('battle')!.dataset.tutorial = String(lesson.order);
      const band = tutorialBand(lesson);
      text('practice-eyebrow', band.eyebrow); text('practice-title', band.title); text('practice-detail', band.detail);
      text('practice-brief-eyebrow', band.eyebrow);
      el('practice-band').setAttribute('aria-label', 'Tutorial');
    }
    shown = lesson ? coachCard(view, { held }) : null;
    if (shown?.done && dismissed) shown = null;
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
    point(shown);
    // Read only once it has opened: a step whose cards could not open yet still owes them.
    if (shown && owed() && canAuto && open(shown.cards)) opened.add(shown.key);
  }
  const owed = () => Boolean(shown?.cards.length && !opened.has(shown.key));
  /** Whether a concept card is open or still to open for the step shown: the question's own sheet waits for it. */
  const pending = () => dialog.open || owed();
  /** A new practice teaches its steps again. */
  function reset() { document.getElementById('battle')!.dataset.tutorial = ''; opened.clear(); key = ''; dismissed = false; if (dialog.open) dialog.close(); }
  return { render, pending, reset };
}
