// The five journeys of docs/art/ux-craft-plan.md, and the match's windows of its UX12, each walked as a new player
// would walk it: the screens it names, in order, and how to get from one to the next. A walk presses what a player
// presses; where it cannot (Google's own button), it says so with a staged screen. `strips` names the
// interactions `--strip` films.

import { GAME_ORIGIN } from '../../table/site-origins.js';
import { END_WORD, answer, asked, choose, latest, chooseRecordedSpell, lessons, playLesson, playUntil, readUntilAsked, roundEndRead, takeSeat } from './play.mjs';
import { DESKTOP, VIEWPORTS, neverAskedInPage } from './set.mjs';

export const WALKER = 'Walker', FRIEND = 'Rook', LEARNER = 'Learner';
const FIRST_LESSON = 'sc-repo-tutorial-your-first-combat';

// The developer form stands in for Google's button: the one step of the doors a walk cannot press.
async function signIn(page) {
  await page.locator('#dev-username').fill(WALKER);
  await page.locator('#dev-sign-in button[type="submit"]').click();
}

// An account that exists before the walk begins, signed in on `page`'s browser: the walk is of what follows.
export async function account(walk, page, username, { skipLessons = true } = {}) {
  const headers = { Origin: walk.set.game };
  const made = await page.request.post(`${walk.set.game}/api/account/dev`, { data: { username }, headers });
  if (!made.ok()) throw new Error(`The host refused the account ${username}: ${made.status()}`);
  if (!skipLessons) return;
  const skipped = await page.request.post(`${walk.set.game}/api/tutorials/${FIRST_LESSON}/skip`, { data: {}, headers });
  if (!skipped.ok()) throw new Error(`The host refused to skip the lessons of ${username}: ${skipped.status()}`);
}

// The home once its sections are in: its lists no longer say they are being found.
async function home(page, origin) {
  await page.goto(`${origin}/`);
  await page.locator('body[data-home="member"]').waitFor();
  await page.waitForFunction(() => !/Finding/.test(document.querySelector('#my-tables')?.textContent ?? 'Finding'));
}

// A table a person opens for another player, and the link its waiting room passes on: the friend's invitation.
async function invitationFrom(walk, name) {
  const inviter = await walk.person();
  await account(walk, inviter, name);
  const headers = { Origin: walk.set.game };
  const made = await inviter.request.post(`${walk.set.game}/api/lobby/tables`, { data: { opponent: 'person' }, headers });
  if (!made.ok()) throw new Error(`The host refused ${name}'s table: ${made.status()}`);
  const [seat] = (await made.json()).seats;
  const room = await inviter.request.get(`${walk.set.game}/api/seat/${seat.seat}`, { headers: { 'X-Seat-Token': seat.token } });
  const join = (await room.json()).waiting?.seats?.[0]?.join;
  if (!join) throw new Error(`${name}'s table names no link for its open place.`);
  return `${walk.set.game}${join}`;
}

// A picture below the first screen, brought into view and waited on: a scene is lazy, so the walk's settling leaves it
// out until it has arrived (#1044).
async function sceneInView(page, selector) {
  await page.locator(selector).first().scrollIntoViewIfNeeded({ timeout: 30000 });
  // A picture inside a closed entry has a box but is not drawn: only the ones the visitor sees are waited on.
  await page.waitForFunction(() => [...document.images].filter(image => {
    const box = image.getBoundingClientRect();
    return image.checkVisibility({ contentVisibilityAuto: true, visibilityProperty: true }) && box.width > 0 && box.bottom > 0 && box.top < innerHeight;
  }).every(image => image.complete && image.naturalWidth > 0), null, { timeout: 30000 });
}

// The pop-ups offer, as it arrives on its moment, read and put off with Not now (#1126): a week before it asks again,
// so the screens after it are as they were.
async function pushOffer(walk, page, name) {
  await page.locator('#push-offer').waitFor({ timeout: 30000 });
  await walk.screen(name);
  await page.locator('#push-offer-later').click();
  await page.locator('#push-offer').waitFor({ state: 'hidden' });
}

const doors = {
  id: 'doors',
  title: 'The two doors',
  screens: ['showcase-home', 'showcase-home-story', 'showcase-home-modes', 'showcase-round', 'showcase-home-fr', 'showcase-mutations',
    'showcase-mutations-family', 'showcase-mutations-entry', 'showcase-rules', 'showcase-404', 'showcase-404-fr', 'sign-in-panel', 'invitation-sign-in', 'name-window', 'invitation-take-the-place', 'first-lesson-brief', 'sign-out', 'signed-out-panel',
    'sign-in-panel-lesson', 'guest-lesson-brief', 'guest-lesson-complete', 'guest-home', 'guest-sign-out', 'guest-name-window', 'guest-upgraded-home'],
  strips: {
    // The seam: the showcase's Play now, pressed.
    'play-now': { at: 'showcase-home', act: ({ page }) => page.getByRole('link', { name: 'Play now' }).first().dispatchEvent('pointerdown') },
  },
  async walk(walk) {
    const { page, set } = walk;
    // The first door: a visitor who knows nothing opens the showcase.
    await page.goto(`${set.showcase}/`);
    await walk.screen('showcase-home');
    const play = await page.getByRole('link', { name: 'Play now' }).first().getAttribute('href');
    if (new URL(play).origin !== new URL(GAME_ORIGIN).origin) throw new Error(`Play now leads to ${play}, not to the game.`);
    // The home's two scenes, far below its first screen (S5): the Predator at rest, then the broken charge (#1044).
    await sceneInView(page, '#story .story-scene');
    await walk.screen('showcase-home-story', { asSeen: true });
    await sceneInView(page, '#modes .modes-scene');
    await walk.screen('showcase-home-modes', { asSeen: true });
    await page.evaluate(() => window.scrollTo(0, 0));
    // How to play: the round seen played, each move a picture of the battle view (UX6).
    await page.getByRole('link', { name: 'How to play' }).click();
    // The moves' pictures are lazy, so the walk's settling leaves them out: wait until each has arrived.
    await page.waitForFunction(() => {
      const images = [...document.querySelectorAll('#duel .move-frame img')];
      return images.length === 3 && images.every(image => image.complete && image.naturalWidth > 0);
    });
    await walk.screen('showcase-round', { asSeen: true });
    await page.goto(`${set.showcase}/fr/`);
    await walk.screen('showcase-home-fr');
    await page.goto(`${set.showcase}/mutations/`);
    await walk.screen('showcase-mutations');
    // The field guide's scenes (S4): a family scrolled to its head, then an entry opened by its link (#1044).
    await page.goto(`${set.showcase}/mutations/#family-predator`);
    await sceneInView(page, '#family-predator');
    await walk.screen('showcase-mutations-family', { asSeen: true });
    await page.goto(`${set.showcase}/mutations/#form-parasite`);
    await page.locator('#form-parasite[open]').waitFor();
    await sceneInView(page, '#form-parasite');
    await walk.screen('showcase-mutations-entry', { asSeen: true });
    await page.goto(`${set.showcase}/rules/`);
    await walk.screen('showcase-rules');
    // A path that does not exist: the static server answers 404 with nothing, so the walk opens the page itself.
    await page.goto(`${set.showcase}/404.html`);
    await walk.screen('showcase-404');
    await page.goto(`${set.showcase}/fr/404.html`);
    await walk.screen('showcase-404-fr');
    // Play now opens the game's origin in the same tab: here, the host of the set.
    await page.goto(`${set.game}${new URL(play).pathname}`);
    await page.locator('#sign-in-panel').waitFor();
    await walk.screen('sign-in-panel');
    // The second door: a friend's invitation, followed signed out. The friend opens a table with a place for
    // another player and passes on its link, as the waiting room shows it, so the panel can name who invites.
    await page.goto(await invitationFrom(walk, FRIEND));
    await page.locator('#sign-in-panel[data-door="invited"]').waitFor();
    await page.getByRole('heading', { name: `${FRIEND} invited you` }).waitFor();
    await walk.screen('invitation-sign-in');
    // A new Google account names itself in this window, over the panel; a developer account is named by its
    // form. Opened by the walk so the window is seen, and marked staged.
    await page.evaluate(() => document.getElementById('name-dialog').showModal());
    await walk.screen('name-window', { staged: true });
    await page.evaluate(() => document.getElementById('name-dialog').close());
    await signIn(page);
    await page.getByRole('button', { name: /Take the place/ }).waitFor({ timeout: 30000 });
    await walk.screen('invitation-take-the-place');
    // Signed in and named, a new player's first plain visit opens tutorial 1's brief.
    await page.goto(`${set.game}/`);
    await page.locator('#practice-brief[open]').waitFor({ timeout: 30000 });
    await walk.screen('first-lesson-brief');
    // The way out: the lesson skipped, the player signs out from the account menu, and the panel says goodbye.
    await page.locator('#practice-brief-skip').click();
    await page.waitForURL(`${set.game}/`);
    await home(page, set.game);
    await page.locator('#account-menu summary').click();
    await page.getByRole('button', { name: 'Sign out', exact: true }).click();
    await page.locator('#action-dialog[open] #action-heading img[data-scene]').waitFor();
    await walk.screen('sign-out');
    await page.locator('#action-confirm').click();
    await page.locator('#sign-in-panel[data-door="left"]').waitFor({ timeout: 30000 });
    await walk.screen('signed-out-panel');
    // A lesson before an account (UX15): the showcase's Try a tutorial first, the lesson door, a guest made on the press.
    // The showcase's link names the published game; the walk opens the same address on its own host.
    await page.goto(`${set.game}/lobby?try=lesson`);
    await page.locator('#sign-in-panel[data-door="lesson"]').waitFor({ timeout: 30000 });
    await walk.screen('sign-in-panel-lesson');
    await page.getByRole('button', { name: 'Play the first tutorial' }).click();
    await page.locator('#practice-brief[open]').waitFor({ timeout: 30000 });
    await page.locator('body[data-guest="true"]').waitFor();
    await walk.screen('guest-lesson-brief');
    await page.locator('#practice-brief-begin').click();
    await playLesson(page, lessons[0], { blocked: walk.blocked });
    await page.locator('#coach-leave.keep-tutorials').waitFor({ timeout: 30000 });
    await walk.screen('guest-lesson-complete');
    // Sign in to keep them: the guest's home, at its keep panel.
    await page.locator('#coach-leave').click();
    await page.waitForURL(`${set.game}/#keep-tutorials`);
    await page.locator('#sign-in-panel[data-door="guest"]').waitFor({ timeout: 30000 });
    await page.locator('#featured[data-featured="lesson"]').waitFor({ timeout: 30000 });
    await walk.screen('guest-home');
    // Sign out asks first: a guest's tutorials are lost with it. Stay signed in keeps them.
    await page.locator('#account-menu summary').click();
    await page.getByRole('button', { name: 'Sign out', exact: true }).click();
    await page.locator('#action-dialog[open]').waitFor();
    await walk.screen('guest-sign-out');
    await page.locator('#action-cancel').click();
    // A way in with an identity no account holds asks a name, which upgrades the guest; Google's window is staged,
    // and the developer's form, which names itself, upgrades it the same way.
    await page.evaluate(() => document.getElementById('name-dialog').showModal());
    await walk.screen('guest-name-window', { staged: true });
    await page.evaluate(() => document.getElementById('name-dialog').close());
    await page.locator('#dev-username').fill(LEARNER);
    await page.locator('#dev-sign-in button[type="submit"]').click();
    await page.locator('body[data-home="member"]:not([data-guest])').waitFor({ timeout: 30000 });
    await page.getByText(`Signed in as ${LEARNER}. Your tutorials are kept.`).waitFor({ timeout: 30000 });
    await walk.screen('guest-upgraded-home');
  },
};

const hasCards = lesson => lesson.file.tutorial.steps.some(step => step.concepts.length);

const theLessons = {
  id: 'lessons',
  title: 'The lessons',
  screens: [...lessons.flatMap(lesson => [`lesson-${lesson.order}-brief`, `lesson-${lesson.order}-first-step`,
    ...(hasCards(lesson) ? [`lesson-${lesson.order}-concept-card`] : []), ...(lesson.order === 1 ? ['lesson-1-push-offer'] : []),
    lesson.endsTheMatch ? 'lesson-result' : `lesson-${lesson.order}-complete`]), 'home-after-the-lessons'],
  strips: {
    'brief-begins': { at: 'lesson-1-brief', act: ({ page }) => page.locator('#practice-brief-begin').click() },
    // The first cast (#978): the coach and its ring by the spell choice, then the spell chosen, and the frames after it
    // show the coach stepping aside while the targets are chosen. Film it with `--frames 12 --interval 40`. On a phone
    // the spell choice docks under the field and the coach leaves with its sheet; its fade on the field plays only from
    // 760 px wide, so the strip is filmed on a desktop too (#1140).
    'first-cast': { at: 'lesson-1-first-step', viewports: [...VIEWPORTS, DESKTOP], act: ({ page, blocked }) => chooseRecordedSpell(page, lessons[0], blocked) },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER, { skipLessons: false });
    // A browser never asked for notifications, so lesson 1's end offers pop-ups (#1126).
    await page.context().addInitScript(neverAskedInPage);
    for (const lesson of lessons) {
      const name = part => `lesson-${lesson.order}-${part}`;
      // The first lesson opens by itself on a new player's home; the next ones are opened from Learn to play.
      if (lesson.order === 1) await page.goto(`${set.game}/`);
      else {
        await home(page, set.game);
        await page.locator(`#tutorial-cards [data-tutorial="${lesson.id}"] a`).click();
      }
      await page.locator('#practice-brief[open]').waitFor({ timeout: 30000 });
      await walk.screen(name('brief'));
      await page.locator('#practice-brief-begin').click();
      await playLesson(page, lesson, {
        blocked: walk.blocked,
        look: async now => { if (now.cards && !walk.seen(name('concept-card'))) await walk.screen(name('concept-card')); },
        step: async () => { if (!walk.seen(name('first-step'))) await walk.screen(name('first-step')); },
      });
      if (lesson.endsTheMatch) {
        // The lesson that plays to the end ends on the match's result, over the coach's last card, and is left from there.
        if (!await page.locator('#match-dialog').isVisible()) await page.locator('#match-open').click();
        await page.locator('#match-dialog').waitFor();
        await walk.screen('lesson-result');
        await page.locator('#match-leave').click();
      } else {
        if (lesson.order === 1) await pushOffer(walk, page, 'lesson-1-push-offer');
        await walk.screen(name('complete'));
        await page.locator('#coach-leave').click();
      }
      await page.waitForURL(`${set.game}/`);
    }
    await home(page, set.game);
    await walk.screen('home-after-the-lessons');
  },
};

// The link to the open seat of the walker's table for another player, as its waiting room passes it on: where the
// friend takes the seat and the walker's turn arrives (UX9). Set by the lobby's walk before the screen it is used at.
let openSeat = null;

// The friend takes the open seat from their own browser: the match begins, and the walker is asked to choose.
async function friendTakesTheSeat(walk) {
  const friend = await walk.person();
  await account(walk, friend, FRIEND);
  await takeSeat(friend, openSeat);
  return friend;
}

// The seat's part of the question both players are asked, answered from `page` with the first choices the host
// offers, until the seat waits on the other player: a mutation is asked a creature at a time.
async function answerOurPart(walk, page) {
  let view = await asked(page), answers = 0;
  while (view?.waitingFor && !view.over) {
    if (answers === 12) throw new Error(`The seat was still asked ${view.waitingFor} after twelve answers.`);
    await readUntilAsked(page);
    await answer(page, view, { blocked: walk.blocked, ahead: true });
    answers += 1;
    view = await latest(page);
  }
}

const lobby = {
  id: 'lobby',
  title: 'The lobby',
  screens: ['home-new', 'home-menu', 'create-window', 'create-window-bot', 'home-your-turn', 'waiting-room', 'home-waiting', 'home-push-offer', 'home-game-ended', 'home-ended-ground', 'home-waiting-ground', 'home-turn-arrives', 'home-answer-arrives'],
  strips: {
    // The turn that arrives: the friend takes the open seat while the walker's home is open, a bot's turn on show.
    'turn-arrives': { at: 'home-game-ended', act: walk => friendTakesTheSeat(walk) },
    'create-window': { at: 'home-menu', act: async ({ page }) => { await page.keyboard.press('Escape'); await page.locator('#new-table').click(); } },
    'menu': { at: 'home-new', act: ({ page }) => page.locator('#home-menu-toggle').click() },
  },
  async walk(walk) {
    const { page, set } = walk;
    // A player with nothing: signed in, the first lesson skipped from its brief.
    await account(walk, page, WALKER, { skipLessons: false });
    await page.goto(`${set.game}/`);
    await page.locator('#practice-brief-skip').click({ timeout: 30000 });
    await page.waitForURL(`${set.game}/`);
    await home(page, set.game);
    await walk.screen('home-new');
    await page.locator('#home-menu-toggle').click();
    await walk.screen('home-menu');
    await page.keyboard.press('Escape');
    await page.locator('#new-table').click();
    await page.locator('#create-dialog[open]').waitFor();
    await walk.screen('create-window');
    await page.locator('#create-solo').click();
    await walk.screen('create-window-bot');
    // A game against a bot: created, its first question left unanswered, and the home says a turn waits.
    await page.locator('#create-submit').click();
    await page.waitForURL(/\/battle/);
    const game = page.url();
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await home(page, set.game);
    await walk.screen('home-your-turn');
    // A table with an open seat, and the room where its maker waits for another player.
    await page.locator('#new-table').click();
    await page.locator('#create-dialog[open]').waitFor();
    await page.locator('#create-submit').click();
    await page.waitForURL(/\/(battle|play)/);
    await page.locator('#battle[data-connection="live"], #waiting-room, [data-waiting]').first().waitFor({ timeout: 30000 });
    const [seat, token] = [...new URL(page.url()).searchParams][0];
    const room = await page.request.get(`${set.game}/api/seat/${seat}`, { headers: { 'X-Seat-Token': token } });
    openSeat = `${set.game}${(await room.json()).waiting?.seats?.[0]?.join ?? ''}`;
    if (openSeat === set.game) throw new Error('The waiting room names no link for its open seat.');
    await walk.screen('waiting-room');
    await home(page, set.game);
    await walk.screen('home-waiting');
    // A game against a person waits on the other player: the moment of the pop-ups offer, in a browser never asked
    // (#1126). Put off with Not now, it does not ask again in this walk.
    await page.context().addInitScript(neverAskedInPage);
    await home(page, set.game);
    await pushOffer(walk, page, 'home-push-offer');
    // The game against the bot, conceded: a game that ended.
    await page.goto(game);
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await concede(walk);
    // A second game against a bot, left in progress: two games, the bot's turn featured while the friend's table waits.
    await home(page, set.game);
    await page.locator('#new-table').click();
    await page.locator('#create-dialog[open]').waitFor();
    await page.locator('#create-solo').click();
    await page.locator('#create-submit').click();
    await page.waitForURL(/\/battle/);
    const botGame = page.url();
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await home(page, set.game);
    await page.locator('#mine-finished').click();
    await walk.screen('home-game-ended');
    // In view, so the walk's contrast reads the words on each ground (S8 of docs/art/scene-paintings-plan.md): the game
    // that ended last on the stomp's aftermath, then the table that waits for a player on the Warped listening.
    await page.locator('#my-tables .game-row.scene-ground').first().scrollIntoViewIfNeeded({ timeout: 30000 });
    await walk.screen('home-ended-ground');
    await page.locator('#mine-ongoing').click();
    await page.locator('#my-tables .game-card.scene-ground').first().scrollIntoViewIfNeeded({ timeout: 30000 });
    await walk.screen('home-waiting-ground');
    // Back as home-game-ended left it: Finished pressed, the featured space in view.
    await page.locator('#mine-finished').click();
    await page.evaluate(() => window.scrollTo(0, 0));
    // The friend takes the open seat: a person's turn arrives on the open home, and outranks the bot's on show.
    const friend = await friendTakesTheSeat(walk);
    await page.locator('#welcome', { hasText: FRIEND }).waitFor({ timeout: 30000 });
    await walk.screen('home-turn-arrives');
    // Its action opens the friend's game, not the bot's, and the battle's first frame names it.
    await page.locator('#featured-action').click();
    await page.waitForURL(new RegExp(`[?&]${seat}=${token}`));
    // The other form (#988): a game begun, its turn delivered by an answer inside a round. The walker answers its
    // question in the friend's game, and concedes the bot's, so the friend's game is featured as their turn; then Rook
    // answers through the host, and the walker's turn arrives on the open home, in place.
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await answerOurPart(walk, page);
    await page.goto(botGame);
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await concede(walk);
    await home(page, set.game);
    await page.locator('#featured-eyebrow', { hasText: /their turn/i }).waitFor({ timeout: 30000 });
    await answerOurPart(walk, friend);
    await page.locator('#featured-eyebrow', { hasText: /your turn/i }).waitFor({ timeout: 30000 });
    await page.locator('#welcome', { hasText: FRIEND }).waitFor({ timeout: 30000 });
    await walk.screen('home-answer-arrives');
  },
};

const pages = {
  id: 'pages',
  title: "The player's pages",
  screens: ['friends-empty', 'friends-asked', 'friends-with-a-friend', 'friend-invited', 'home-invitation', 'leaderboard', 'statistics', 'leaderboard-ranked', 'statistics-ranked', 'rulebook', 'offline'],
  strips: {
    // The window closed and opened again from the menu, which closes behind the window.
    'friends-window': { at: 'friends-empty', act: async ({ page }) => {
      await page.locator('[data-home-close]:visible').click();
      if (!await page.locator('#friends-open').isVisible()) await page.locator('#home-menu-toggle').click();
      await page.locator('#friends-open').click();
    } },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER);
    const friend = await walk.person();
    await account(walk, friend, FRIEND);
    await home(page, set.game);
    // Friends is in the menu once the host says who is signed in: opening the menu before that finds it hidden.
    await page.locator('#friends-open:not([hidden])').waitFor({ state: 'attached' });
    await page.locator('#home-menu-toggle').click();
    await page.locator('#friends-open').click();
    await page.locator('#friends-dialog[open]').waitFor();
    await walk.screen('friends-empty');
    await page.locator('#friend-username').fill(FRIEND);
    await page.locator('#friend-ask-submit').click();
    await page.locator('#friend-outgoing-list li').first().waitFor();
    await walk.screen('friends-asked');
    // The friend answers from their own browser.
    const accepted = await friend.request.post(`${set.game}/api/friends/requests`, { data: { username: WALKER }, headers: { Origin: set.game } });
    if (!accepted.ok()) throw new Error(`The host refused the friend's answer: ${accepted.status()}`);
    await page.locator('#friend-list li').first().waitFor({ timeout: 30000 });
    await walk.screen('friends-with-a-friend');
    await page.locator('#friend-list li button').first().click();
    await page.locator('#create-dialog[open]').waitFor();
    await walk.screen('friend-invited');
    // The other way round: Rook keeps a seat for the walker, and the walker's home shows the invitation's card.
    await page.keyboard.press('Escape');
    await friendInvites(walk, friend);
    await home(page, set.game);
    // In view, so the walk's contrast reads the card's words on its ground: at 320 it is below the first screen.
    await page.locator('#invitation-cards .invitation').first().scrollIntoViewIfNeeded({ timeout: 30000 });
    await walk.screen('home-invitation');
    await page.goto(`${set.game}/leaderboard`);
    await page.locator('main').waitFor();
    await walk.screen('leaderboard');
    await page.goto(`${set.game}/stats`);
    await page.locator('main').waitFor();
    await walk.screen('statistics');
    // The same pages once something is on them: one ranked game against the friend, played through its first round
    // by both, then conceded by the walker, so the ladder has two players and the statistics a record.
    await rankedGame(walk, friend);
    await page.goto(`${set.game}/leaderboard`);
    await page.locator('#ladder-rows tr').first().waitFor();
    await walk.screen('leaderboard-ranked');
    await page.goto(`${set.game}/stats`);
    await page.locator('#stats-body:not([hidden])').waitFor();
    await walk.screen('statistics-ranked');
    // The rulebook from the menu's Rules, read once the book is drawn.
    await page.goto(`${set.game}/rules`);
    await page.locator('#rules-book h2').first().waitFor({ timeout: 30000 });
    await walk.screen('rulebook');
    // The page the service worker shows when a page cannot be loaded at all, opened by its own address: online, so
    // the walk reads it as a navigation offline draws it.
    await page.goto(`${set.game}/offline.html`);
    await page.locator('main').waitFor();
    await walk.screen('offline');
  },
};

// The friend opens a table whose second seat is kept for the walker (ADR 0129), from their own browser.
async function friendInvites(walk, friend) {
  const headers = { Origin: walk.set.game };
  const list = await (await friend.request.get(`${walk.set.game}/api/friends`, { headers })).json();
  const walker = list.friends?.find(one => one.username === WALKER);
  if (!walker) throw new Error(`${FRIEND} does not count ${WALKER} among their friends.`);
  const made = await friend.request.post(`${walk.set.game}/api/lobby/tables`, { data: { opponent: 'friend', friend: walker.id }, headers });
  if (!made.ok()) throw new Error(`The host refused ${FRIEND}'s table for ${WALKER}: ${made.status()}`);
}

// A 3v3 game against a person, opened from the Create window, its open seat taken by the friend: both seats live.
async function gameWithFriend(walk, friend) {
  const { page, set } = walk;
  await home(page, set.game);
  await page.locator('#new-table').click();
  await page.locator('#create-dialog[open]').waitFor();
  await page.locator('#opponent').selectOption('person');
  await page.locator('#create-submit').click();
  await page.waitForURL(/\/(battle|play)/);
  await page.locator('#battle[data-connection="live"], #waiting-room, [data-waiting]').first().waitFor({ timeout: 30000 });
  const [seat, token] = [...new URL(page.url()).searchParams][0];
  const room = await page.request.get(`${set.game}/api/seat/${seat}`, { headers: { 'X-Seat-Token': token } });
  const join = (await room.json()).waiting?.seats?.[0]?.join;
  if (!join) throw new Error('The waiting room names no link for its open seat.');
  await takeSeat(friend, `${set.game}${join}`);
  await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
}

// A ranked 3v3 game between the walker and the friend: a first round played by both with the first choices offered,
// then conceded by the walker.
export async function rankedGame(walk, friend) {
  const { page } = walk;
  await gameWithFriend(walk, friend);
  const second = view => view.board.roundNumber > 1;
  await Promise.all([playUntil(page, second, { blocked: walk.blocked }), playUntil(friend, second, { blocked: walk.blocked })]);
  await concede(walk);
}

async function concede(walk) {
  const { page } = walk;
  // The phase tip a new game opens is read first: it stands over the match's settings.
  await readUntilAsked(page);
  await choose(page, '#settings', walk.blocked);
  await page.locator('#concede-open').click();
  await page.locator('#concede-confirm').click();
  await page.locator('#match-dialog').waitFor({ timeout: 30000 });
}

// The windows a first round opens, each pictured the first time the game opens it. An opponent's action shows Next
// from its windup, quiet until the result is in (`resolved`, table/battle-playback.js): `result-read` is pictured
// once Next is usable, the outcome and the health in. Pictured as soon as Next showed, the screen was the windup, the
// impact or the result as the walk's reads fell, and its words and controls differed from one film to the next (#1308).
export const SIGHTS = [
  ['phase-tip', '#help-dialog'], ['mutation-tree', '#planning-dialog[data-kind="Evolution"]'], ['tempo-sheet', '#planning-dialog[data-kind="Speed"]'],
  ['order-of-play', '#order-continue'], ['spell-choice', '#combat-dialog'], ['targets', '#battle[data-combat="targets"]'],
  ['result-read', '#playback-next[aria-disabled="false"]'], ['upkeep', '#upkeep-dialog'],
];

// The round whose end the walk reads, the first of the game that has something to say (#1308). A condition laid in a
// round counts its rounds from the next one (`Condition.Tick`, src/DownfallArena.Domain), so nothing ends with round
// 1 in any game, and the page reads no end that has nothing to say (`receipts`, table/battle-playback.js). The
// walker's Braces of round 1 lay a Defense Buff of two rounds, which ends with round 3.
const ROUND_WHOSE_END_IS_READ = 3;

/**
 * The walker's cast past the first round: the first spell offered that is aimed at the opponent alone. A Brace cast
 * again would start its Defense Buff's rounds again, and no round's end would have anything to say.
 */
export function attack(offered, view) {
  const opponents = new Set(view.board.enemies.map(one => one.id));
  return offered.find(one => one.legalTargets.candidates.length > 0 && one.legalTargets.candidates.every(id => opponents.has(id))) ?? null;
}

// The game played on from round 2 until a round's end is read, pictured while its reading waits for its OK: the
// dock's card says the end and the round's bar says what ended. The walk then reads on to the next question.
async function roundEnd(walk) {
  const { page } = walk;
  const look = async () => {
    if (walk.seen('round-end')) return;
    const read = await roundEndRead(page);
    if (!read) return;
    // The dock and the bar are drawn together: an end read under a phase's word is the page's fault, said as one.
    if (!END_WORD.test(read.word)) throw new Error(`The round's end is read (${read.detail}) while the round's bar says "${read.word}".`);
    await walk.screen('round-end');
  };
  await playUntil(page, view => walk.seen('round-end') || view.board.roundNumber > ROUND_WHOSE_END_IS_READ, { look, blocked: walk.blocked, cast: attack });
  if (!walk.seen('round-end')) throw new Error(`No round's end was read by round ${ROUND_WHOSE_END_IS_READ}'s: no condition ended there, so the walk's casts no longer lay one that does.`);
}

const match = {
  id: 'match',
  title: 'The match',
  screens: [...SIGHTS.map(([name]) => name), 'second-round', 'round-end', 'settings', 'concede', 'match-result'],
  strips: {
    // Pressed as the walk presses it (#1021): where something covers Settings, the strip is still filmed and says so.
    'settings-window': { at: 'second-round', act: ({ page, blocked }) => choose(page, '#settings', blocked) },
    // One exchange read across the field and the frame (UX11): the opponent's next action played from its OK, the
    // blow and the target's health on one beat, then the next actor. Film it with `--frames 24 --interval 120`.
    exchange: { at: 'result-read', act: ({ page }) => page.locator('#playback-next').click() },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER);
    await takeSeat(page, set.invitation);
    // One round, each question answered as it is asked, with the first choice the host offers.
    const look = async () => {
      for (const [name, selector] of SIGHTS) {
        if (!walk.seen(name) && await page.locator(selector).isVisible()) await walk.screen(name);
      }
    };
    await playUntil(page, view => view.board.roundNumber > 1, { look, blocked: walk.blocked });
    await walk.screen('second-round');
    await roundEnd(walk);
    await choose(page, '#settings', walk.blocked);
    await page.locator('#settings-dialog[open]').waitFor();
    await walk.screen('settings');
    await page.locator('#concede-open').click();
    await page.locator('#concede-dialog[open]').waitFor();
    await walk.screen('concede');
    await page.locator('#concede-confirm').click();
    await page.locator('#match-dialog').waitFor({ timeout: 30000 });
    await walk.screen('match-result');
  },
};

// The journal as a player opens it: from the bar, or under 360 px, where History leaves the bar (table/battle.css),
// from Help's window.
async function openHistory(page, blocked) {
  if (await page.locator('#history-open').isVisible()) return choose(page, '#history-open', blocked);
  await choose(page, '#help-open', blocked);
  await choose(page, '#help-dialog [data-history-open]', blocked);
}

// The match's windows whose pictures docs/art/ux12-pictures-plan.md settles: the pick and the Mutation Tree opened
// from it, the Tempo sheet at its spell reference, the spellbook at its top and at its foot, the order of play once
// a spell is turned face up, the journal once a round is played, and the result once Walker concedes. UX12 adds a
// creature inspected, the family's centred window, opened from the order strip's card. The team's first creature
// plays at Instinct, so its spellbook shows a crit chip that does not hold this turn (P8).
// A creature the order strip names, one already played, as a player taps it to read it.
const INSPECTED = '#timeline li[data-state="Completed"] [data-order-creature]';

const windows = {
  id: 'windows',
  title: "The match's windows",
  screens: ['mutation-pick', 'mutation-tree', 'mutation-followed', 'tempo-reference', 'spell-choice', 'spell-choice-foot', 'order-revealed', 'inspect', 'history', 'match-result', 'match-won'],
  strips: {
    // The journal closed by its button: how a window leaves (UX12). The match's `settings-window` films one entering.
    'history-window': { at: 'history', act: ({ page }) => page.locator('#history-close').click() },
    // The inspection closed by Escape: it fades out faster than it came, and the focus is back on the card's Inspect
    // (UX12). Closed and opened again in one act, a frozen exit leaves nothing to fade in.
    'inspect-window': { at: 'inspect', act: ({ page }) => page.keyboard.press('Escape') },
    // A pick confirmed: the sheet goes on to the next creature, and the field answers on the one that took it (UX12b).
    'pick-confirmed': { at: 'mutation-pick', act: async ({ page }) => { await page.locator('[data-mutation]').first().click(); await page.locator('#planning-confirm').click(); } },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER);
    await takeSeat(page, set.invitation);
    const look = async () => {
      if (!walk.seen('mutation-pick') && await page.locator('#planning-dialog[data-kind="Evolution"]').isVisible()) {
        await walk.screen('mutation-pick');
        // The Mutation Tree as the pick opens it, a package's detail open: the tree and the detail on one screen.
        await choose(page, '#mutation-atlas-open', walk.blocked);
        await page.locator('#atlas-dialog[open]').waitFor();
        await choose(page, '#atlas-board [data-package]', walk.blocked);
        await page.locator('#atlas-detail:not([hidden])').waitFor();
        await walk.screen('mutation-tree');
        await page.locator('#atlas-close').click();
        await page.locator('#atlas-dialog').waitFor({ state: 'hidden' });
      }
      // The team's last pick confirmed, the sheet closed: the field, where the picks are followed back (UX12b).
      if (walk.seen('mutation-tree') && !walk.seen('mutation-followed') && await page.locator('#growth-receipt').isVisible()
        && !await page.locator('#planning-dialog').isVisible()) await walk.screen('mutation-followed');
      if (!walk.seen('tempo-reference') && await page.locator('#planning-dialog[data-kind="Speed"]').isVisible()) {
        // The sheet's spell reference, under the pinned creature and its two tempos.
        // The creature and its tempos stay pinned: the reference is brought just under them, as a thumb leaves it.
        await page.locator('.tempo-reference').evaluate(node => {
          const pinned = document.querySelector('.tempo-pinned').getBoundingClientRect();
          let scroller = node.parentElement;
          while (scroller && scroller.scrollHeight <= scroller.clientHeight) scroller = scroller.parentElement;
          if (scroller) scroller.scrollTop += node.getBoundingClientRect().top - pinned.bottom;
        });
        await walk.screen('tempo-reference');
        await page.locator('#planning-choices').evaluate(node => node.scrollIntoView({ block: 'start' }));
      }
      // A later turn of the round: the strip read from its start, where the spells already cast are face up.
      if (walk.seen('spell-choice') && !walk.seen('order-revealed') && await page.locator('#combat-dialog').isVisible()
        && await page.locator('#timeline li[data-state="Completed"]').count()) {
        await page.locator('#timeline').evaluate(node => { node.scrollLeft = 0; });
        await walk.screen('order-revealed');
        // A creature the strip names, read in its window: the family's centred window (UX12).
        await choose(page, INSPECTED, walk.blocked);
        await choose(page, '#order-why-inspect', walk.blocked);
        await page.locator('#inspect-dialog[open]').waitFor();
        await walk.screen('inspect');
        await page.keyboard.press('Escape');
        await page.locator('#inspect-dialog').waitFor({ state: 'hidden' });
        await page.keyboard.press('Escape');
      }
      if (!walk.seen('spell-choice') && await page.locator('#combat-dialog').isVisible()) {
        await walk.screen('spell-choice');
        // The foot of the spellbook: its last spell, where a docked sheet on a short phone must keep its foot.
        await page.locator('#combat-browser').evaluate(node => { node.scrollTop = node.scrollHeight; });
        await walk.screen('spell-choice-foot');
        await page.locator('#combat-browser').evaluate(node => { node.scrollTop = 0; });
      }
    };
    await playUntil(page, view => view.board.roundNumber > 1, { look, blocked: walk.blocked, tempo: index => index === 0 ? 'Quick' : null });
    // A round played, the journal holds its casts.
    await readUntilAsked(page);
    await openHistory(page, walk.blocked);
    await page.locator('#history-dialog[open]').waitFor();
    await walk.screen('history');
    // The result, on the ground the last blow left (S11 of docs/art/scene-paintings-plan.md): Walker concedes.
    await page.locator('#history-close').click();
    await concede(walk);
    await walk.screen('match-result');
    // A match won (UX12b): a game against the friend, who concedes it once the first round is asked.
    await wonGame(walk);
    await walk.screen('match-won');
  },
};

// A game against the friend, who concedes as soon as it is asked its first question: the walker's result is a win.
async function wonGame(walk) {
  const { page } = walk;
  const friend = await walk.person();
  await account(walk, friend, FRIEND);
  await page.locator('#match-dialog').evaluate(dialog => dialog.close());
  await gameWithFriend(walk, friend);
  await readUntilAsked(friend);
  await choose(friend, '#settings', walk.blocked);
  await friend.locator('#concede-open').click();
  await friend.locator('#concede-confirm').click();
  // The walker reads what the round showed so far (its upkeep), and the result opens by itself.
  await readUntilAsked(page);
  await page.locator('#match-dialog[open]').waitFor({ timeout: 30000 });
}

export const JOURNEYS = [doors, theLessons, lobby, pages, match, windows];
