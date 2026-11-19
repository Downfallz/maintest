// The five journeys of docs/art/ux-craft-plan.md, each walked as a new player would walk it: the screens it
// names, in order, and how to get from one to the next. A walk presses what a player presses; where it cannot
// (Google's own button), it says so with a staged screen. `strips` names the interactions `--strip` films.

import { GAME_ORIGIN } from '../../table/site-origins.js';
import { choose, lessons, playLesson, playUntil, readUntilAsked, takeSeat } from './play.mjs';

const WALKER = 'Walker', FRIEND = 'Rook';
const FIRST_LESSON = 'sc-repo-tutorial-your-first-combat';

// The developer form stands in for Google's button: the one step of the doors a walk cannot press.
async function signIn(page) {
  await page.locator('#dev-username').fill(WALKER);
  await page.locator('#dev-sign-in button[type="submit"]').click();
}

// An account that exists before the walk begins, signed in on `page`'s browser: the walk is of what follows.
async function account(walk, page, username, { skipLessons = true } = {}) {
  const headers = { Origin: walk.set.game };
  const made = await page.request.post(`${walk.set.game}/api/account/dev`, { data: { username }, headers });
  if (!made.ok()) throw new Error(`The host refused the account ${username}: ${made.status()}`);
  if (skipLessons) await page.request.post(`${walk.set.game}/api/tutorials/${FIRST_LESSON}/skip`, { data: {}, headers });
}

// The home once its sections are in: its lists no longer say they are being found.
async function home(page, origin) {
  await page.goto(`${origin}/`);
  await page.locator('body[data-home="member"]').waitFor();
  await page.waitForFunction(() => !/Finding/.test(document.querySelector('#my-tables')?.textContent ?? 'Finding'));
}

const doors = {
  id: 'doors',
  title: 'The two doors',
  screens: ['showcase-home', 'showcase-home-fr', 'showcase-mutations', 'showcase-rules', 'sign-in-panel', 'invitation-sign-in', 'name-window', 'invitation-take-the-place', 'first-lesson-brief'],
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
    await page.goto(`${set.showcase}/fr/`);
    await walk.screen('showcase-home-fr');
    await page.goto(`${set.showcase}/mutations/`);
    await walk.screen('showcase-mutations');
    await page.goto(`${set.showcase}/rules/`);
    await walk.screen('showcase-rules');
    // Play now opens the game's origin in the same tab: here, the host of the set.
    await page.goto(`${set.game}${new URL(play).pathname}`);
    await page.locator('#sign-in-panel').waitFor();
    await walk.screen('sign-in-panel');
    // The second door: a friend's invitation, followed signed out.
    await page.goto(set.invitation);
    await page.locator('#sign-in-panel').waitFor();
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
  },
};

const hasCards = lesson => lesson.file.tutorial.steps.some(step => step.concepts.length);

const theLessons = {
  id: 'lessons',
  title: 'The lessons',
  screens: [...lessons.flatMap(lesson => [`lesson-${lesson.order}-brief`, `lesson-${lesson.order}-first-step`,
    ...(hasCards(lesson) ? [`lesson-${lesson.order}-concept-card`] : []), lesson.endsTheMatch ? 'lesson-result' : `lesson-${lesson.order}-complete`]), 'home-after-the-lessons'],
  strips: {
    'brief-begins': { at: 'lesson-1-brief', act: ({ page }) => page.locator('#practice-brief-begin').click() },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER, { skipLessons: false });
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
        await walk.screen(name('complete'));
        await page.locator('#coach-leave').click();
      }
      await page.waitForURL(`${set.game}/`);
    }
    await home(page, set.game);
    await walk.screen('home-after-the-lessons');
  },
};

const lobby = {
  id: 'lobby',
  title: 'The lobby',
  screens: ['home-new', 'home-menu', 'create-window', 'create-window-bot', 'home-your-turn', 'waiting-room', 'home-waiting', 'home-game-ended'],
  strips: {
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
    await walk.screen('waiting-room');
    await home(page, set.game);
    await walk.screen('home-waiting');
    // The game against the bot, conceded: a game that ended.
    await page.goto(game);
    await page.locator('#battle[data-connection="live"]').waitFor({ timeout: 30000 });
    await concede(walk);
    await home(page, set.game);
    await page.locator('#mine-finished').click();
    await walk.screen('home-game-ended');
  },
};

const pages = {
  id: 'pages',
  title: "The player's pages",
  screens: ['friends-empty', 'friends-asked', 'friends-with-a-friend', 'friend-invited', 'leaderboard', 'statistics'],
  strips: {
    'friends-window': { at: 'friends-empty', act: async ({ page }) => { await page.locator('[data-home-close]:visible').click(); await page.locator('#friends-open').click(); } },
  },
  async walk(walk) {
    const { page, set } = walk;
    await account(walk, page, WALKER);
    const friend = await walk.person();
    await account(walk, friend, FRIEND);
    await home(page, set.game);
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
    await page.goto(`${set.game}/leaderboard`);
    await page.locator('main').waitFor();
    await walk.screen('leaderboard');
    await page.goto(`${set.game}/stats`);
    await page.locator('main').waitFor();
    await walk.screen('statistics');
  },
};

async function concede(walk) {
  const { page } = walk;
  // The phase tip a new game opens is read first: it stands over the match's settings.
  await readUntilAsked(page);
  await choose(page, '#settings', walk.blocked);
  await page.locator('#concede-open').click();
  await page.locator('#concede-confirm').click();
  await page.locator('#match-dialog').waitFor({ timeout: 30000 });
}

// The windows a first round opens, each pictured the first time the game opens it.
const SIGHTS = [
  ['phase-tip', '#help-dialog'], ['mutation-tree', '#planning-dialog[data-kind="Evolution"]'], ['tempo-sheet', '#planning-dialog[data-kind="Speed"]'],
  ['order-of-play', '#order-continue'], ['spell-choice', '#combat-dialog'], ['targets', '#battle[data-combat="targets"]'],
  ['result-read', '#playback-next'], ['upkeep', '#upkeep-dialog'],
];

const match = {
  id: 'match',
  title: 'The match',
  screens: [...SIGHTS.map(([name]) => name), 'second-round', 'settings', 'concede', 'match-result'],
  strips: {
    'settings-window': { at: 'second-round', act: ({ page }) => page.locator('#settings').click() },
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

export const JOURNEYS = [doors, theLessons, lobby, pages, match];
