const rounds = [1, 6, 12];
const roman = ['I', 'II', 'III'];
const home = lang => lang === 'fr' ? '/fr/' : '/';
const arrow = '<span class="arrow" aria-hidden="true"></span>';
const rune = '<img class="rune" src="/assets/brand-mark.svg" alt="" width="25" height="38">';
const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');

function selector(label, choices) {
  return `<div class="selector" aria-label="${label}" data-selector>
    ${choices.map(([id, name]) => `<a href="#${id}" id="choose-${id}">${escape(name)}</a>`).join('\n    ')}
  </div>`;
}

function bestiary(c, families) {
  const choices = families.map((line, index) => [line[0].family, c.families[index].name]);
  return `<section id="bestiary" class="section contain" aria-labelledby="family-title" data-gallery>
    <div class="section-head"><div><p class="kicker">${c.bestiary}</p><h2 id="family-title">${c.familiesTitle}</h2></div>
      <p class="lead">${c.familiesLead}</p></div>
    ${selector(c.familyPicker, choices)}
    ${families.map((line, index) => {
      const family = c.families[index];
      return `<article id="${line[0].family}" class="lineage" data-panel>
        <div class="lineage-copy"><p class="kicker">${escape(family.trait)}</p><h3>${family.name}</h3><p>${family.blurb}</p></div>
        <ol class="stages">${line.map((form, stage) => `<li>
          <img src="/assets/${form.artwork}.webp" loading="lazy" decoding="async" alt="" width="420" height="280">
          <small>${c.stage} ${roman[stage]}</small><strong>${escape(family.stages?.[stage] ?? form.name)}</strong>
        </li>`).join('')}</ol></article>`;
    }).join('')}
  </section>`;
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

export function page(c, families, app) {
  const other = c.lang === 'fr' ? 'en' : 'fr';
  const play = (label, style) => `<a class="${style}" href="${app}">${label} ${arrow}</a>`;
  return `<!doctype html>
<html lang="${c.lang}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#0b1519">
  <title>Downfall Instinct — ${c.kicker}</title>
  <meta name="description" content="${c.description}">
  <link rel="canonical" href="https://downfallinstinct.com${home(c.lang)}">
  <link rel="alternate" hreflang="en" href="https://downfallinstinct.com/">
  <link rel="alternate" hreflang="fr" href="https://downfallinstinct.com/fr/">
  <meta property="og:type" content="website">
  <meta property="og:title" content="Downfall Instinct">
  <meta property="og:description" content="${c.description}">
  <meta property="og:url" content="https://downfallinstinct.com${home(c.lang)}">
  <meta property="og:image" content="https://downfallinstinct.com/assets/eclipse-battle.webp">
  <link rel="icon" href="/assets/icon.svg" type="image/svg+xml">
  <link rel="preload" href="/fonts/marcellus-latin-400.woff2" as="font" type="font/woff2" crossorigin>
  <link rel="stylesheet" href="/branding.css">
  <link rel="stylesheet" href="/showcase.css">
  <script type="module" src="/gallery.js"></script>
</head>
<body id="top">
  <a class="skip" href="#main">${c.skip}</a>
  <header class="mast"><div class="contain">
    <a class="wordmark" href="${home(c.lang)}">${rune}<span>Downfall Instinct</span></a>
    <nav aria-label="${c.nav}"><a href="#bestiary">${c.bestiary}</a><a href="#duel">${c.game}</a><a href="#battlefield">${c.world}</a><a href="#story">${c.storyLink}</a></nav>
    <a class="language" hreflang="${other}" lang="${other}" aria-label="${c.switchLabel}" href="${home(other)}">${other.toUpperCase()}</a>
    ${play(c.play, 'nav-play')}
  </div></header>
  <main id="main">
    <section class="hero" aria-labelledby="title">
      <div class="hero-ground"></div>
      <img class="hero-beast" src="/assets/${families[0][2].artwork}.webp" alt="" width="1536" height="1024" fetchpriority="high">
      <div class="hero-inner contain"><div class="hero-copy">
        <p class="kicker">${c.kicker}</p>
        <h1 id="title">${c.title[0]}<br><em>${c.title[1]}</em></h1>
        <p class="pitch">${c.pitch}</p>
        <div class="actions">${play(c.play, 'gold-button')}<a class="discover" href="#bestiary">${c.explore} <span aria-hidden="true">↓</span></a></div>
        <p class="play-note">${c.playNote}</p>
      </div><div class="specimen"><strong>${c.specimen}</strong><span>${c.specimenLine}</span></div></div>
    </section>
    ${bestiary(c, families)}
    <section id="duel" class="duel" aria-labelledby="duel-title"><div class="section contain">
      <p class="kicker">${c.game}</p><h2 id="duel-title">${c.duelTitle}</h2><p class="lead">${c.duelLead}</p>
      <ol class="moves">${c.moves.map(([name, description], index) => `<li><span aria-hidden="true">${roman[index]}</span><h3>${name}</h3><p>${description}</p></li>`).join('')}</ol>
    </div></section>
    ${battlefield(c)}
    <section id="story" class="story section contain" aria-labelledby="story-title">
      <div class="story-copy">
        <p class="kicker">${c.storyKicker}</p><h2 id="story-title">${c.storyTitle}</h2>
        ${c.story.map(paragraph => `<p>${escape(paragraph)}</p>`).join('')}
        <div class="tabletop-note"><h3>${c.tabletopTitle}</h3><p>${c.tabletop}</p></div>
      </div>
      <aside class="commitments" aria-labelledby="commitments-title">
        <h3 id="commitments-title">${c.commitmentsTitle}</h3>
        <dl>${c.commitments.map(([title, detail]) => `<div><dt>${title}</dt><dd>${detail}</dd></div>`).join('')}</dl>
      </aside>
    </section>
    <section class="closing"><div class="contain"><p class="kicker">${c.platform}</p><h2>${c.closing}</h2><p>${c.modes}</p>${play(c.play, 'gold-button')}<p class="play-note">${c.playNote}</p></div></section>
  </main>
  <footer class="foot contain"><a class="wordmark" href="${home(c.lang)}">${rune}Downfall Instinct</a><a href="#story">${c.storyLink}</a><a href="#top">${c.footerTop} ↑</a></footer>
</body></html>
`;
}
