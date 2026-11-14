import { arrow, escape, layout, routes, selector } from './layout.mjs';
import { lore } from './lore.mjs';

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
    ${specimen.map((art, i) => `<figure id="${ids[i]}" class="specimen-step" data-panel>
      <div class="specimen-stage"><img src="/assets/${art.file}" width="${art.width}" height="${art.height}" loading="lazy" decoding="async" alt=""></div>
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
        <img src="/assets/${['eclipse-battle', 'breach', 'broken-floor'][index]}.webp" alt="" loading="lazy" width="1672" height="${round === 12 ? '542' : '941'}">
        ${round === 12 ? '<img class="sky" src="/assets/shattered-sky.webp" alt="" loading="lazy" width="1672" height="480">' : ''}
      </div>
      <figcaption><strong>${choices[index][1]}</strong><span>${c.moments[index]}</span></figcaption>
    </figure>`).join('')}
  </section>`;
}

export function page(c, specimen, app) {
  const play = (label, style) => `<a class="${style}" href="${app}">${label} ${arrow}</a>`;
  return layout(c, 'home', app, c.kicker, c.description, `
    <section class="hero" aria-labelledby="title">
      <div class="hero-ground"></div>
      <img class="hero-beast" src="/assets/evolutions/brute-stage3-colossus.webp" alt="" width="1536" height="1024" fetchpriority="high">
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
      <ol class="moves">${c.moves.map(([name, description], index) => `<li><span aria-hidden="true">${roman[index]}</span><h3>${name}</h3><p>${description}</p></li>`).join('')}</ol>
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
    <section id="modes" class="play-modes" aria-labelledby="modes-title"><div class="section contain">
      <p class="kicker">${c.platform}</p><h2 id="modes-title">${c.modesTitle}</h2>
      <ul class="mode-list">${c.modes.map(([name, description]) => `<li><h3>${escape(name)}</h3><p>${escape(description)}</p></li>`).join('')}</ul>
      <div class="actions">${play(c.play, 'gold-button')}<a class="discover" href="${new URL('leaderboard', app).href}">${c.leaderboardLink} ${arrow}</a></div>
      <p class="play-note">${c.playNote}</p>
    </div></section>
`, ['gallery.js']);
}
