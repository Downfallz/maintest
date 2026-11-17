import { SITE_ORIGIN } from '../table/site-origins.js';

export const arrow = '<span class="arrow" aria-hidden="true"></span>';
export const copyrightLine = '© 2026';
const rune = '<img class="rune" src="/assets/brand-mark.svg" alt="" width="25" height="38">';
export const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
export const routes = {
  en: { home: '/', guide: '/mutations/', rules: '/rules/', privacy: '/privacy/', copyright: '/copyright/' },
  fr: { home: '/fr/', guide: '/fr/mutations/', rules: '/fr/regles/', privacy: '/fr/confidentialite/', copyright: '/fr/droit-auteur/' },
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
  const current = name => kind === name ? ' aria-current="page"' : '';
  return `<!doctype html>
<html lang="${c.lang}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#0b1519">
  <title>${escape(title)} — Downfall Instinct</title>
  <meta name="description" content="${escape(description)}">
  <link rel="canonical" href="${new URL(path[kind], SITE_ORIGIN).href}">
  ${Object.entries(routes).map(([lang, paths]) => `<link rel="alternate" hreflang="${lang}" href="${new URL(paths[kind], SITE_ORIGIN).href}">`).join('\n  ')}
  <meta property="og:type" content="website"><meta property="og:title" content="${escape(title)} — Downfall Instinct">
  <meta property="og:description" content="${escape(description)}">
  <meta property="og:url" content="${new URL(path[kind], SITE_ORIGIN).href}">
  <meta property="og:image" content="${new URL('assets/eclipse-battle.webp', SITE_ORIGIN).href}">
  <link rel="icon" href="/assets/icon.svg" type="image/svg+xml">
  <link rel="preload" href="/fonts/marcellus-latin-400.woff2" as="font" type="font/woff2" crossorigin>
  <link rel="stylesheet" href="/branding.css"><link rel="stylesheet" href="/showcase.css">
  ${scripts.map(file => `<script type="module" src="/${file}"></script>`).join('')}
</head>
<body id="top">
  <a class="skip" href="#main">${c.skip}</a>
  <header class="mast"><div class="contain">
    <a class="wordmark" href="${path.home}">${rune}<span>Downfall Instinct</span></a>
    <nav aria-label="${c.nav}"><a href="${section('duel')}">${c.game}</a><a href="${section('battlefield')}">${c.world}</a><a href="${path.guide}"${current('guide')}>${c.guide}</a><a href="${path.rules}"${current('rules')}>${c.rules}</a><a href="${section('modes')}">${c.modesNav}</a><a href="${section('story')}">${c.storyLink}</a></nav>
    <a class="language" hreflang="${other}" lang="${other}" aria-label="${c.switchLabel}" href="${localized}">${other.toUpperCase()}</a>
    <a class="nav-play" href="${app}">${c.play} ${arrow}</a>
  </div></header>
  <main id="main">${body}</main>
  <footer class="foot contain"><div class="foot-brand"><a class="wordmark" href="${path.home}">${rune}Downfall Instinct</a>
    <small>${copyrightLine}</small></div>
    <div class="foot-links"><a href="${path.guide}">${c.guide}</a><a href="${path.rules}"${current('rules')}>${c.rules}</a><a href="${section('modes')}">${c.modesNav}</a><a href="${section('story')}">${c.storyLink}</a><a href="${path.privacy}"${current('privacy')}>${c.privacy}</a><a href="${path.copyright}"${current('copyright')}>${c.copyright}</a><a href="#top">${c.footerTop} ↑</a></div>
  </footer>
</body></html>\n`;
}
