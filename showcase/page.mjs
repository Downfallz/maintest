import { arrow, escape, layout, routes, selector, SHOWCASE_ART } from './layout.mjs';
import { lore } from './lore.mjs';
import { pictureHtml, stripHtml } from '../table/picture.js';
import { PAGE_ART } from '../table/page-art.js';

/** The page the declaration names. */
const HOME = 'showcase/home';
const art = (name, options) => pictureHtml(name, { page: HOME, base: SHOWCASE_ART, ...options });

/** A specimen's picture, declared in docs/art/page-pictures.json under its artwork's id. */
export function specimenPicture(id, pictures = PAGE_ART) {
  const name = `specimen-${id}`;
  if (!pictures[name]?.pages[HOME]) throw new Error(`The specimen ${id} has no picture ${name} for ${HOME} in docs/art/page-pictures.json`);
  return name;
}

// The widths each picture is drawn at, as showcase.css lays it out; the declaration's `drawn` holds the same.
const SIZES = {
  // Under 600 px the ground is 68% of the hero's height, its width following: about 710 px on a phone.
  ground: '(max-width: 600px) 720px, 100vw',
  beast: '(max-width: 600px) 79vw, min(54vw, 850px)',
  field: '(max-width: 850px) 90vw, min(1250px, 88vw)',
  specimen: '(max-width: 650px) 315px, 465px',
  move: '(max-width: 600px) 90vw, (max-width: 1420px) 26vw, 368px',
  // A strip's file holds its six frames side by side: it is drawn six times as wide.
  strip: '(max-width: 600px) 540vw, (max-width: 1420px) 156vw, 2208px',
};
// The round's three moves, one Brute followed through them, each a picture of the real battle view
// (scripts/ux/round.mjs); Cast is a strip.
const MOVES = ['round-mutate', 'round-tempo', 'round-cast'];

function movePicture(name, alt) {
  if (PAGE_ART[name].frames === 1) return `<figure class="move-frame">${art(name, { alt, sizes: SIZES.move })}</figure>`;
  if (PAGE_ART[name].frames !== 6) throw new Error(`${name}: the strip's sizes are written for six frames`);
  // Under less motion the strip rests on the blow's contact, its fourth frame, so it still shows what its name says.
  return `<figure class="move-frame">${stripHtml(name, { page: HOME, base: SHOWCASE_ART, alt, sizes: SIZES.strip, still: 4 })}</figure>`;
}

const rounds = [1, 6, 12];
const roman = ['I', 'II', 'III'];

function mutationStory(c, specimen) {
  const l = lore[c.lang];
  const ids = ['ancestral', 'warped', 'necrotic', 'revenant', 'hybrid'].map(id => 'specimen-' + id);
  return `<section id="mutations" class="mutation-story" aria-labelledby="mutation-title"><div class="section contain" id="bestiary">
    <div class="section-head"><div><p class="kicker">${c.mutations}</p><h2 id="mutation-title">${l.title}</h2></div></div>
    <div class="lore-columns"><p>${l.opening}</p><p>${l.choice}</p></div>
    <div class="case" data-gallery><header class="case-header"><p class="kicker">${l.caseKicker}</p><h3>${l.caseTitle}</h3></header>
    ${selector(l.select, ids.map((id, i) => [id, l.steps[i][0]]))}
    ${specimen.map(({ id }, i) => `<figure id="${ids[i]}" class="specimen-step" data-panel>
      <div class="specimen-stage">${art(specimenPicture(id), { sizes: SIZES.specimen })}</div>
      <figcaption><p class="kicker">${l.steps[i][0]}</p><h4>${l.steps[i][1]}</h4><p>${l.steps[i][2]}</p></figcaption>
    </figure>`).join('')}</div>
    <div class="guide-invite"><div><h3>${l.invite}</h3><p>${l.guideNote}</p></div><a class="outline-button" href="${routes[c.lang].guide}">${l.guideLink} <span aria-hidden="true">→</span></a></div>
  </div></section>`;
}

function battlefield(c) {
  const choices = rounds.map(round => [`round-${round}`, `${c.round} ${round}`]);
  return `<section id="battlefield" class="section contain" aria-labelledby="world-title" data-gallery>
    <div class="section-head"><div><p class="kicker">${c.world}</p><h2 id="world-title">${c.worldTitle}</h2></div>
      <p class="lead">${c.worldLead}</p></div>
    ${selector(c.roundPicker, choices)}
    ${rounds.map((round, index) => `<figure class="moment" id="round-${round}" data-panel>
      <div class="landscape ${round === 12 ? 'shattered' : ''}">
        ${art(['battlefield-eclipse', 'battlefield-breach', 'battlefield-broken'][index], { sizes: SIZES.field })}
        ${round === 12 ? art('battlefield-sky', { class: 'sky', sizes: SIZES.field }) : ''}
      </div>
      <figcaption><strong>${choices[index][1]}</strong><span>${c.moments[index]}</span></figcaption>
    </figure>`).join('')}
  </section>`;
}

export function page(c, specimen, app) {
  const play = (label, style) => `<a class="${style}" href="${app}">${label} ${arrow}</a>`;
  return layout(c, 'home', app, c.kicker, c.description, `
    <section class="hero" aria-labelledby="title">
      <div class="hero-ground">${art('showcase-ground', { sizes: SIZES.ground })}</div>
      ${art('colossus', { class: 'hero-beast motion-breath', sizes: SIZES.beast })}
      <div class="hero-inner contain"><div class="hero-copy">
        <p class="kicker">${c.kicker}</p>
        <h1 id="title">${c.title[0]}<br><em>${c.title[1]}</em></h1>
        <p class="pitch">${c.pitch}</p>
        <div class="actions">${play(c.play, 'gold-button')}<a class="discover" href="#duel">${c.explore} <span aria-hidden="true">↓</span></a></div>
        <p class="play-note">${c.playNote}</p>
      </div><div class="specimen"><strong>${c.specimen}</strong><span>${c.specimenLine}</span></div></div>
    </section>
    <section id="duel" class="duel" aria-labelledby="duel-title"><div class="section contain">
      <p class="kicker">${c.game}</p><h2 id="duel-title">${c.duelTitle}</h2><p class="lead">${c.duelLead}</p>
      <ol class="moves">${c.moves.map(([name, description], index) => `<li>${movePicture(MOVES[index], c.movePictures[index])}<span aria-hidden="true">${roman[index]}</span><h3>${name}</h3><p>${description}</p></li>`).join('')}</ol>
      <a class="editorial-link" href="${routes[c.lang].rules}">${c.readRules} <span aria-hidden="true">→</span></a>
    </div></section>
    ${battlefield(c)}
    ${mutationStory(c, specimen)}
    <section id="story" class="story section contain" aria-labelledby="story-title">
      <div class="story-copy">
        <p class="kicker">${c.storyKicker}</p><h2 id="story-title">${c.storyTitle}</h2>
        ${c.story.map(paragraph => `<p>${escape(paragraph)}</p>`).join('')}
        <div class="tabletop-note"><h3>${c.tabletopTitle}</h3><p>${c.tabletop}</p></div>
      </div>
      <aside class="commitments" aria-labelledby="commitments-title">
        <h3 id="commitments-title">${c.commitmentsTitle}</h3>
        <dl>${c.commitments.map(([title, detail]) => `<div><dt>${title}</dt><dd>${detail}</dd></div>`).join('')}</dl>
        <a class="editorial-link" href="${routes[c.lang].privacy}">${c.privacy} <span aria-hidden="true">→</span></a>
      </aside>
    </section>
    <section id="modes" class="play-modes" aria-labelledby="modes-title">${art('battlefield-breach', { class: 'modes-ground', sizes: '100vw' })}<div class="section contain">
      <p class="kicker">${c.platform}</p><h2 id="modes-title">${c.modesTitle}</h2>
      <ul class="mode-list">${c.modes.map(([name, description]) => `<li><h3>${escape(name)}</h3><p>${escape(description)}</p></li>`).join('')}</ul>
      <div class="actions">${play(c.play, 'outline-button')}<a class="discover" href="${new URL('leaderboard', app).href}">${c.leaderboardLink} ${arrow}</a></div>
      <p class="play-note">${c.playNote}</p>
    </div></section>
`, ['gallery.js', 'motion.js']);
}
