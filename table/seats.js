// The hotseat rule: which of the seats a page holds is the one on screen, and whether the device has to be
// passed before that seat's board is shown.
//
// It is a module of its own because it is the one thing in this page that can be wrong in a way nothing else
// would notice. A page bound to the seat its link named goes on polling a seat the match is not asking, while
// the player it *is* asking taps a screen that never offers them anything -- and the host, correctly, answers
// every one of those polls. The page script has no export seam (ADR 0024), so what is worth a test lives here.

/// <summary>Whether the host says this seat is being asked something.</summary>
export function isAsked(view) {
  return Boolean(view) && view.waitingFor !== null && view.waitingFor !== undefined;
}

// The seat on screen: the one the match is asking, else the one holding the device, else the first held. The
// order matters -- an asked seat wins, because a question is the only thing that needs answering, and of two
// asked seats the one holding the device does: both seats are asked their picks, speeds and intents at once
// (ADR 0092, ADR 0111), and the player with the device answers theirs before passing it. `holding` keeps the
// holder's seat on screen for one poll although only the other is asked (see `holdsOn`).
export function activeSeat(views, holder, holding = false) {
  if (!views || views.length === 0) return null;
  const held = views.find(one => one.seat === holder);
  if (held && (holding || isAsked(held.view))) return held;
  return views.find(one => isAsked(one.view)) ?? held ?? views[0];
}

// Whether to keep the holder's seat on screen for this poll although only the other seat is asked. That is
// what the moment between two of the holder's own questions looks like when both seats are asked at once: the
// host is writing the last answer down and has not asked the next, while the other seat is already waiting.
// Without this a poll landing there would put the pass screen up in the middle of one player's speeds. One
// poll only -- the holder was asked on the poll before -- so a player who has answered everything hands the
// device over a poll later, never not at all.
export function holdsOn(previous, views, holder) {
  const was = previous?.find(one => one.seat === holder);
  const now = views?.find(one => one.seat === holder);
  return Boolean(was && now && isAsked(was.view) && !isAsked(now.view) &&
    views.some(one => one.seat !== holder && isAsked(one.view)));
}

// The fence stands while the seat being asked is not the one that said it is holding the device. On one phone
// it is the whole boundary between the two players, so it is up by default and comes down only by a tap.
export function needsPass(current, holder) {
  return Boolean(current) && isAsked(current.view) && current.seat !== holder;
}
