// A tutorial in the battle view (ADR 0187, docs/tabletop/onboarding-plan.md, O3b, O4): what the band and the
// briefing say of it, the lesson step the question asked shows, the concept cards that step opens, and the questions the host answers
// for the person, which the page does not draw. Pure: the host says the lesson under `practice.tutorial` of the
// seat's view, and this reads it. Nothing is decided here: a step shows by the question the host asks.

import { conceptCards } from './concepts.js';

/**
 * The controls a step may point at (`pointsAt.control`), each with the element it names in the battle view. The
 * host refuses a lesson that names another (`LessonControls.Names`).
 */
export const CONTROLS = Object.freeze({
  spells: 'combat-open', cast: 'combat-confirm', command: 'planning-open', order: 'order-open', history: 'history-open',
});

const list = value => Array.isArray(value) ? value : [];

/** The lesson of the seat's view, or none: a game, a plain practice, or a block that is not a lesson. */
export function tutorialOf(view) {
  const lesson = view?.practice?.tutorial;
  return lesson && Number.isInteger(lesson.order) && Array.isArray(lesson.steps) && lesson.steps.length ? lesson : null;
}

/**
 * What the band says in a tutorial, in the header where the opponent, History, Help and the notifications stand in a
 * game: its place in the series, its title, how far the person is, and what its restart is called (once the tutorial
 * is done, it is played again from its cut).
 */
export function tutorialBand(lesson) {
  const steps = list(lesson?.steps).length, shown = Math.min(steps, Math.max(1, lesson?.shown ?? 0));
  return {
    eyebrow: `TUTORIAL ${lesson?.order ?? '—'} OF ${Math.max(lesson?.of ?? 0, lesson?.order ?? 0) || '—'}`,
    title: lesson?.title || 'Tutorial',
    detail: lesson?.done ? 'Tutorial complete' : `Step ${shown} of ${steps}`,
    restart: lesson?.done ? 'Play again' : 'Restart the round',
  };
}

/**
 * What the briefing adds in a tutorial (O4): it says what this is and how to get out of it, **Start** in place of
 * Begin, and **Skip the tutorial** until the tutorial is done.
 */
export function tutorialBrief(lesson) {
  return {
    begin: 'Start',
    skip: lesson?.done ? null : 'Skip the tutorial',
    note: 'A short lesson on a real board: nothing played here counts. You can skip it now or leave at any time. You can play it again from Learn to play.',
  };
}

/** Whether leaving now is skipping: a tutorial the host has not said is done. It never opens by itself again. */
export function leavingSkips(view) {
  const lesson = tutorialOf(view);
  return Boolean(lesson && !lesson.done);
}

/**
 * Where the page lets go of the view's tutorial when the person leaves it, skipped or done: the host keeps no
 * practice of it, and the next time it opens it starts from its cut. None outside a tutorial: a plain practice
 * stays to be resumed.
 */
export function leavePath(view) {
  const id = tutorialOf(view) ? view.practice.scenario : null;
  return typeof id === 'string' && id ? `/api/scenarios/${encodeURIComponent(id)}/practice` : null;
}

/** Where the page says the account skipped (`skip`) or finished (`done`) the view's tutorial; none outside a tutorial. */
export function progressPath(view, what) {
  const id = tutorialOf(view) ? view.practice.scenario : null;
  return typeof id === 'string' && id && (what === 'skip' || what === 'done') ? `/api/tutorials/${encodeURIComponent(id)}/${what}` : null;
}

/** Whether the question the view asks is one the host answers for the person, from the recorded answers. */
export function assistedQuestion(view) {
  const lesson = tutorialOf(view);
  return Boolean(lesson && view.waitingFor && list(lesson.assisted).includes(view.waitingFor));
}

/**
 * Whether the view asks a question in a tutorial the host says is done: the lesson is over, the host answers nothing
 * more, and the person is not to play on. Only its end card, Play again and Leave remain.
 */
export function pastTheLesson(view) {
  return Boolean(tutorialOf(view)?.done && view.waitingFor);
}

/**
 * What the dock says once the tutorial is done, in place of waiting for a decision that will not come; none before,
 * and none outside a tutorial.
 */
export function lessonOverText(view) {
  return tutorialOf(view)?.done ? 'Tutorial complete. Play it again, or leave.' : null;
}

/**
 * The view as the page draws it: an assisted question is not the person's, and once the tutorial is done no question
 * is, so the view asks and offers nothing.
 */
export function withoutAssisted(view) {
  return assistedQuestion(view) || pastTheLesson(view) ? { ...view, waitingFor: null, waitingAsked: null, waitingCreature: null, options: {} } : view;
}

const pointer = step => {
  const creature = step.pointsAt?.creature, control = step.pointsAt?.control;
  return {
    creature: Number.isInteger(creature) ? String(creature) : null,
    control: typeof control === 'string' && Object.hasOwn(CONTROLS, control) ? CONTROLS[control] : null,
  };
};

/** The lesson step the question asked shows, with its place among the steps; none for any other question. */
export function lessonStep(view) {
  const lesson = tutorialOf(view);
  if (!lesson || view.over || !view.waitingFor || !Number.isInteger(view.waitingAsked) || assistedQuestion(view)) return null;
  const index = lesson.steps.findIndex(step => step?.at?.kind === view.waitingFor && step.at.round === view.board?.roundNumber
    && (step.at.creature == null || step.at.creature === view.waitingCreature));
  return index < 0 ? null : { index, step: lesson.steps[index] };
}

/**
 * The coach card: the step of the question asked, its lines, the concept cards it opens and what it points at;
 * the tutorial's end once the host says it is done; nothing otherwise, and nothing while `held` (results being
 * read, a recorded board: the question is not on the field yet).
 * @param {{ held?: boolean }} [options]
 */
export function coachCard(view, { held = false } = {}) {
  const lesson = tutorialOf(view);
  if (!lesson || held) return null;
  const at = lessonStep(view);
  if (at) {
    return { key: `step:${at.index}`, done: false, eyebrow: `STEP ${at.index + 1} OF ${lesson.steps.length}`,
      lines: list(at.step.says).filter(line => typeof line === 'string').slice(0, 2), cards: conceptCards(at.step.concepts), ...pointer(at.step) };
  }
  if (!lesson.done) return null;
  return { key: 'done', done: true, eyebrow: 'TUTORIAL COMPLETE',
    lines: [`You finished ${lesson.title || 'the tutorial'}.`, 'Play it again, or leave: nothing played here counts.'],
    cards: [], creature: null, control: null };
}
