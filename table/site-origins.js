// The two public origins, named once (#612): the game website at the apex, served by Cloudflare Pages from
// showcase/ (ADR 0185), and the game itself. The table links to the apex for the policy, the copyright notice
// and "About the game"; a developer's table has no apex of its own, so those links open the published site on
// purpose. The showcase build reads both from here; lobby.html, a static page, repeats the apex in its links,
// and showcase/showcase.test.js fails when any of them disagrees.
export const SITE_ORIGIN = 'https://downfallinstinct.com/';
export const GAME_ORIGIN = 'https://play.downfallinstinct.com/';
