export const arrow = '<span class="arrow" aria-hidden="true"></span>';
const rune = '<img class="rune" src="/assets/brand-mark.svg" alt="" width="25" height="38">';
export const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
export const routes = {
  en: { home: '/', guide: '/mutations/', privacy: '/privacy/' },
  fr: { home: '/fr/', guide: '/fr/mutations/', privacy: '/fr/confidentialite/' },
};

export function selector(label, choices) {
  return `<div class="selector" aria-label="${escape(label)}" data-selector>
    ${choices.map(([id, name]) => `<a href="#${id}" id="choose-${id}">${escape(name)}</a>`).join('')}
  </div>`;
}

export function layout(c, kind, app, title, description, body, scripts = []) {
  const other = c.lang === 'fr' ? 'en' : 'fr';
  const path = routes[c.lang], localized = routes[other][kind];
  const section = id => (kind === 'home' ? '' : path.home) + '#' + id;
  return `<!doctype html>
<html lang="${c.lang}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#0b1519">
  <title>${escape(title)} — Downfall Instinct</title>
  <meta name="description" content="${escape(description)}">
  <link rel="canonical" href="https://downfallinstinct.com${path[kind]}">
  ${Object.entries(routes).map(([lang, paths]) => `<link rel="alternate" hreflang="${lang}" href="https://downfallinstinct.com${paths[kind]}">`).join('\n  ')}
  <meta property="og:type" content="website"><meta property="og:title" content="${escape(title)} — Downfall Instinct">
  <meta property="og:description" content="${escape(description)}">
  <meta property="og:url" content="https://downfallinstinct.com${path[kind]}">
  <meta property="og:image" content="https://downfallinstinct.com/assets/eclipse-battle.webp">
  <link rel="icon" href="/assets/icon.svg" type="image/svg+xml">
  <link rel="preload" href="/fonts/marcellus-latin-400.woff2" as="font" type="font/woff2" crossorigin>
  <link rel="stylesheet" href="/branding.css"><link rel="stylesheet" href="/showcase.css">
  ${scripts.map(file => `<script type="module" src="/${file}"></script>`).join('')}
</head>
<body id="top">
  <a class="skip" href="#main">${c.skip}</a>
  <header class="mast"><div class="contain">
    <a class="wordmark" href="${path.home}">${rune}<span>Downfall Instinct</span></a>
    <nav aria-label="${c.nav}"><a href="${section('battlefield')}">${c.world}</a><a href="${path.guide}"${kind === 'guide' ? ' aria-current="page"' : ''}>${c.guide}</a><a href="${section('duel')}">${c.game}</a><a href="${section('story')}">${c.storyLink}</a></nav>
    <a class="language" hreflang="${other}" lang="${other}" aria-label="${c.switchLabel}" href="${localized}">${other.toUpperCase()}</a>
    <a class="nav-play" href="${app}">${c.play} ${arrow}</a>
  </div></header>
  <main id="main">${body}</main>
  <footer class="foot contain"><a class="wordmark" href="${path.home}">${rune}Downfall Instinct</a>
    <div class="foot-links"><a href="${path.guide}">${c.guide}</a><a href="${section('story')}">${c.storyLink}</a><a href="${path.privacy}"${kind === 'privacy' ? ' aria-current="page"' : ''}>${c.privacy}</a><a href="#top">${c.footerTop} ↑</a></div>
  </footer>
</body></html>\n`;
}
