import { copyrightLine, escape, layout } from './layout.mjs';

const notices = {
  en: {
    intro: 'The authors and other rights holders reserve their rights in the original engine code, the written rules and the stories told in Downfall Instinct.',
    reuse: 'Reusing game material',
    scope: 'You can explore the rules and play for free. Reproducing, distributing or adapting the original code or writing requires permission unless the law or a separate licence allows the use.',
    contact: 'Have a passage, illustration or piece of code in mind? Tell us which one and how you would like to use it.',
    third: 'Third-party material',
    licences: 'Third-party software, fonts and other material remain subject to their own licences. The game uses Phaser under the MIT licence. The showcase uses Marcellus under the SIL Open Font License 1.1.',
    font: 'Read the Marcellus licence',
  },
  fr: {
    intro: 'Les auteurs et autres titulaires réservent leurs droits sur le code original du moteur, les textes de règles et les récits de Downfall Instinct.',
    reuse: 'Réutiliser un élément du jeu',
    scope: 'Tu peux découvrir les règles et jouer gratuitement. Pour reproduire, distribuer ou adapter le code ou les textes originaux, il faut une permission, sauf si la loi ou une licence distincte autorise cet usage.',
    contact: 'Tu aimerais reprendre un passage, une illustration ou une partie du code ? Écris-nous en précisant ce que tu souhaites utiliser et de quelle façon.',
    third: 'Contenus de tiers',
    licences: 'Les logiciels, les polices et les autres contenus de tiers conservent leurs propres licences. Le jeu utilise Phaser sous licence MIT. La vitrine utilise la police Marcellus sous licence SIL Open Font License 1.1.',
    font: 'Lire la licence de Marcellus',
  },
};

export function copyrightPage(c, app) {
  const n = notices[c.lang];
  return layout(c, 'copyright', app, c.copyright, n.intro, `<article class="rights policy contain">
    <header><p class="kicker">${escape(copyrightLine)}</p><h1>${c.copyright}</h1><p class="lead">${escape(n.intro)}</p></header>
    <div class="policy-body">
      <section><h2>${n.reuse}</h2><p>${escape(n.scope)}</p><p>${escape(n.contact)}</p><a class="editorial-link" href="mailto:contact@downfallinstinct.com">contact@downfallinstinct.com</a></section>
      <section><h2>${n.third}</h2><p>${escape(n.licences)}</p><a class="editorial-link" href="/fonts/OFL.txt">${n.font}</a></section>
    </div></article>`);
}
