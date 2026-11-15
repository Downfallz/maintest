// A tutorial in the battle view (ADR 0187, docs/tabletop/onboarding-plan.md, O3b): what the band says of it, the
// lesson step the question asked shows, the concept cards that step opens, and the questions the host answers
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

/** What the band over the field says in a tutorial: its place in the series, its title, and how far the person is. */
export function tutorialBand(lesson) {
  const steps = list(lesson?.steps).length, shown = Math.min(steps, Math.max(1, lesson?.shown ?? 0));
  return {
    eyebrow: `TUTORIAL ${lesson?.order ?? '—'} OF ${Math.max(lesson?.of ?? 0, lesson?.order ?? 0) || '—'}`,
    title: lesson?.title || 'Tutorial',
    detail: lesson?.done ? 'Tutorial complete' : `Step ${shown} of ${steps}`,
  };
}

/** Whether the question the view asks is one the host answers for the person, from the recorded answers. */
export function assistedQuestion(view) {
  const lesson = tutorialOf(view);
  return Boolean(lesson && view.waitingFor && list(lesson.assisted).includes(view.waitingFor));
}

/** The view as the page draws it: an assisted question is not the person's, so the view asks nothing. */
export function withoutAssisted(view) {
  return assistedQuestion(view) ? { ...view, waitingFor: null, waitingAsked: null, waitingCreature: null } : view;
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
    lines: [`You finished ${lesson.title || 'the tutorial'}.`, 'Play on, restart the round, or leave: nothing played here counts.'],
    cards: [], creature: null, control: null };
}
