import { arrow, layout, routes, SHOWCASE_ART } from './layout.mjs';
import { pictureHtml } from '../table/picture.js';

/**
 * The 404, one a language (docs/art/scene-paintings-plan.md, S5): Cloudflare Pages serves the closest `404.html` up
 * the path of what was asked, so `/fr/…` finds the French one. Built, not written by hand, because the claw's file
 * is named by its bytes. The layout's paths are absolute, so the page holds at any depth; it has no route of its own,
 * so it claims no address.
 */
export function notFoundPage(c, app) {
  const l = c.lost;
  return layout(c, 'not-found', app, l.title, l.title, `<section class="section contain lost" aria-labelledby="lost-title">
    <p class="kicker">${l.kicker}</p><h1 id="lost-title">${l.title}</h1>
    ${pictureHtml('latch-under-the-arch', { page: 'showcase/404', base: SHOWCASE_ART, class: 'lost-scene', alt: l.scene, sizes: '(max-width: 850px) min(90vw, 640px), 640px' })}
    <a class="gold-button" href="${routes[c.lang].home}">${l.back} ${arrow}</a>
  </section>`);
}
