// A tutorial in the battle view (ADR 0187, docs/tabletop/onboarding-plan.md, O3b, O4): what the band and the
// briefing say of it, the lesson step the question asked shows, the concept cards that step opens, and the questions the host answers
// for the person, which the page does not draw. Pure: the host says the lesson under `practice.tutorial` of the
// seat's view, and this reads it. Nothing is decided here: a step shows by the question the host asks.

import { NO_CONCEPTS } from './concepts.js';

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
 * What the briefing adds in a tutorial (O4): where it starts, that it does not count, **Start** in place of Begin,
 * and **Skip the tutorial** until the tutorial is done. Under the brief's budget (docs/art/journeys/lessons.md, UX8):
 * Leave stands in the band on every step, so the card does not say it.
 */
export function tutorialBrief(lesson, round) {
  return {
    begin: 'Start',
    skip: lesson?.done ? null : 'Skip the tutorial',
    start: Number.isInteger(round) ? `Round ${round}, against a bot.` : 'Against a bot.',
    note: 'Nothing played here counts. Play it again any time from Learn to play.',
  };
}

const SAME_SITE = /^\/(?![/\\])/;

/**
 * The lesson after this one, as the end card offers it (UX8): its title and link as the seat's view names them
 * (`practice.tutorial.next`, #980). A link that would leave the site goes to Learn to play, which lists the lesson.
 * None after the last, or when the view names none.
 */
export function nextLesson(lesson) {
  const next = lesson?.next, order = lesson?.order;
  if (!next || typeof next !== 'object') return null;
  const place = Number.isInteger(order) ? `tutorial ${order + 1} of ${Math.max(lesson.of ?? 0, order + 1)}` : 'the next tutorial';
  const link = typeof next.link === 'string' && SAME_SITE.test(next.link) ? next.link : '/#learn-to-play';
  return { label: `Next: ${typeof next.title === 'string' && next.title ? next.title : place}`, link };
}

/**
 * What a lesson's end adds for a guest (ADR 0207, UX15): where its progress is kept, what an account keeps and opens,
 * and the way to sign in, to the guest's home at its keep panel. Filled only where no lesson follows: the series is the
 * point. None for a member, or before the host says the lesson is done.
 */
export function guestEnd(lesson, { guest = false } = {}) {
  if (!guest || !lesson?.done) return null;
  return {
    line: 'Your progress is kept in this browser only. An account keeps your tutorials and settings, and opens battles against bots and players, friends and the ladder.',
    label: 'Sign in to keep them', link: '/#keep-tutorials', filled: !nextLesson(lesson),
  };
}

/**
 * The hint a step shows once the person turned away from its question (UX8): the words on the control that asks
 * it, read from the page. The page names a button; it decides no move.
 */
export function coachHint(label) {
  // A control's words without the glyph some carry after them (Mutation Tree ↗), which a screen reader would read.
  const words = typeof label === 'string' ? label.replace(/[↗→↻]/g, '').trim() : '';
  return words ? `Next: ${words}.` : null;
}

/**
 * Whether the person has finished the whole series with this lesson (UX8): the last of the series, and every other
 * one done, as the seat's view says it (`practice.tutorial.othersDone`, #980). One skipped, or not read, it is only
 * the last tutorial.
 */
export function finishedSeries(lesson) {
  const order = lesson?.order, of = lesson?.of;
  return Number.isInteger(order) && Number.isInteger(of) && order >= of && lesson.othersDone === true;
}

const standing = creatures => {
  const all = list(creatures);
  return { alive: all.filter(one => one?.isAlive !== false).length, of: all.length };
};

/**
 * Whether leaving now is skipping: a tutorial the host has not said is done, whose match still plays on. It never
 * opens by itself again. A match over that the host has not called done (a table that could not go on) is neither:
 * nothing is said of the tutorial, so it opens again (#912).
 */
export function leavingSkips(view) {
  const lesson = tutorialOf(view);
  return Boolean(lesson && !lesson.done && !view?.over);
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
 * Whether the round after the lesson is held: once the tutorial is done, its upkeep and its turn order do not open
 * by themselves, for a window would stand over the end card and hold its Play again and Leave (#771).
 */
export function holdsTheNextRound(view) {
  return Boolean(tutorialOf(view)?.done);
}

/**
 * What the dock says once the tutorial is done, in place of waiting for a decision that will not come; none before,
 * and none outside a tutorial.
 */
export function lessonOverText(view) {
  return tutorialOf(view)?.done ? 'Tutorial complete.' : null;
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
 * read, a recorded board: the question is not on the field yet). The cards are those of the rulebook the page
 * read (`concepts`, concepts.js); before it is read the step shows without them.
 * @param {{ held?: boolean, concepts?: import('./concepts.js').Concepts }} [options]
 */
export function coachCard(view, { held = false, concepts = NO_CONCEPTS } = {}) {
  const lesson = tutorialOf(view);
  if (!lesson || held) return null;
  const at = lessonStep(view);
  if (at) {
    return { key: `step:${at.index}`, done: false, eyebrow: `STEP ${at.index + 1} OF ${lesson.steps.length}`,
      lines: list(at.step.says).filter(line => typeof line === 'string').slice(0, 2), cards: concepts.cards(at.step.concepts),
      // The cards the step names, read or not: while the book is read, the question's sheet waits for them.
      names: list(at.step.concepts).filter(id => typeof id === 'string').length, ...pointer(at.step) };
  }
  if (!lesson.done) return null;
  // How the board stands, as the host's board says: who is still standing on each side.
  const ours = standing(view.board?.allies), theirs = standing(view.board?.enemies);
  const board = ours.of && theirs.of ? [`Your team: ${ours.alive} of ${ours.of} standing. Opponent: ${theirs.alive} of ${theirs.of}.`] : [];
  return { key: 'done', done: true, eyebrow: `${tutorialBand(lesson).eyebrow} · COMPLETE`,
    lines: [`You finished ${lesson.title || 'the tutorial'}.`, ...board],
    cards: [], names: 0, creature: null, control: null };
}
