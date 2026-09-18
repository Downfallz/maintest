// The playtest's own record, from the player's side (ADR 0054, stage 5). Three kinds reach the host from here:
// two that are one tap, because a note that takes three taps is a note nobody writes, and a comment typed on
// the end screen. The other two kinds -- a decision and a refusal -- are the host's own account of what
// happened and the host refuses to accept them from a page, so nothing here can write one.

// The one-tap notes, in the order they are offered. A lookup is first because it is the one a player reaches
// for mid-round, with a rulebook open in the other hand.
export const TAPPED = [
  { kind: 'Lookup', label: 'I had to look this up' },
  { kind: 'Misplay', label: 'That was a misplay' },
];

// What a tap posts. No text: the round and the sub-phase the host stamps on it are the subject, which is what
// makes it one tap rather than a question.
export function tappedNote(kind) {
  return TAPPED.some(button => button.kind === kind) ? { kind, text: '' } : null;
}

// What the comment box posts, or nothing. Blank is nothing to record, which is what the host answers too: the
// page checks it so an empty box is an inert button rather than a refusal the player has to read.
export function commentNote(text) {
  const written = (text ?? '').trim();
  return written.length > 0 ? { kind: 'Comment', text: written } : null;
}

// What a blank comment box says back. The host answers the same thing, and the page saying it first means an
// empty box is an inert button rather than a refusal to read -- but it has to say *something*, because the
// line above it is the last note that did land, and leaving that one up is telling the player they just saved
// a comment they did not write.
export const NOTHING_TO_RECORD = 'There is nothing in the box to save.';

// The line a player reads after a note lands. It names the kind, because two taps in a row on a phone are
// indistinguishable otherwise and a player who cannot tell whether the first one registered taps again.
export function noted(kind) {
  switch (kind) {
    case 'Lookup': return 'Noted: you looked a rule up.';
    case 'Misplay': return 'Noted: a misplay.';
    case 'Comment': return 'Comment saved with the session.';
    default: return 'Noted.';
  }
}

// Whether this table keeps what a player writes. A host told --no-record answers every note with a refusal,
// so the buttons are not offered at all: a player who taps one and is refused has still lost the thing they
// noticed, and one who taps one and is not refused believes it was kept.
export function notesAreKept(view) {
  return view?.recording === true;
}

// Whether the comment box belongs on screen. It is the end screen's, not the round's: asking for prose while
// somebody is deciding is asking them to stop playing, and the thing they want to say is usually about the
// match as a whole.
export function commentIsOpen(view) {
  return view?.over === true;
}
