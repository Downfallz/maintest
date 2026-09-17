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
// order matters -- the asked seat wins, because a question is the only thing that needs answering.
export function activeSeat(views, holder) {
  if (!views || views.length === 0) return null;
  return views.find(one => isAsked(one.view)) ?? views.find(one => one.seat === holder) ?? views[0];
}

// The fence stands while the seat being asked is not the one that said it is holding the device. On one phone
// it is the whole boundary between the two players, so it is up by default and comes down only by a tap.
export function needsPass(current, holder) {
  return Boolean(current) && isAsked(current.view) && current.seat !== holder;
}
