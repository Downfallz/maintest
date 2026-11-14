import { escape, layout } from './layout.mjs';
import { lore } from './lore.mjs';

const families = ['brute', 'predator', 'warped'];
const romans = ['', 'I', 'II', 'III', 'IV'];
const key = id => id.split(':')[1];
const name = (entry, lang) => lang === 'fr' ? entry.fr : entry.name;

const effectNames = {
  en: { Damage: 'Damage', Heal: 'Healing', EnergyGain: 'Energy gained', EnergyDrain: 'Energy lost', DefenseBuff: 'Defence gained',
    DamageBuff: 'Damage bonus', DefenseDebuff: 'Defence lost', InitiativeDebuff: 'Initiative lost', Bleed: 'Bleeding',
    Regeneration: 'Regeneration', Stun: 'Stun' },
  fr: { Damage: 'Dégâts', Heal: 'Soins', EnergyGain: 'Gain d’énergie', EnergyDrain: 'Perte d’énergie', DefenseBuff: 'Gain de défense',
    DamageBuff: 'Bonus de dégâts', DefenseDebuff: 'Perte de défense', InitiativeDebuff: 'Perte d’initiative', Bleed: 'Saignement',
    Regeneration: 'Régénération', Stun: 'Étourdissement' },
};

function describeEffect(e, lang) {
  const fr = lang === 'fr';
  const parts = [effectNames[lang][e.kind]];
  if (!parts[0]) throw new Error(`No field guide label for effect ${e.kind}`);
  if (e.amount != null) parts.push(String(e.amount));
  if (e.amountPerRound != null) parts.push(fr ? `${e.amountPerRound} par round` : `${e.amountPerRound} per round`);
  if (e.durationRounds != null) parts.push(`(${e.durationRounds} round${e.durationRounds > 1 ? 's' : ''})`);
  if (e.permanent) parts.push('(permanent)');
  return parts.join(' ');
}

function targetText(s, lang) {
  const fr = lang === 'fr';
  if (s.target === 'Self') return fr ? 'Soi-même' : 'Self';
  const ally = s.target === 'Ally';
  if (s.count === 1) return fr ? (ally ? '1 allié' : '1 ennemi') : (ally ? '1 ally' : '1 enemy');
  return fr ? `Jusqu’à ${s.count} ${ally ? 'alliés' : 'ennemis'}` : `Up to ${s.count} ${ally ? 'allies' : 'enemies'}`;
}

function spell(s, lang) {
  const l = lore[lang];
  const effects = list => list.map(e => escape(describeEffect(e, lang))).join(' · ');
  return `<li id="spell-${key(s.id)}" data-spell-name="${escape(name(s, lang))}">
    <strong>${escape(name(s, lang))}</strong><span class="spell-facts">${targetText(s, lang)} · ${s.cost} ${l.energy}</span>
    <p>${effects(s.effects)}</p>${s.casterEffects.length ? `<p class="caster"><b>${l.caster}</b> · ${effects(s.casterEffects)}</p>` : ''}
  </li>`;
}

function passive(t, lang) {
  const fr = lang === 'fr', p = t.passive;
  if (p.sunder) return fr ? `Chaque coup direct ignore jusqu’à ${p.sunder} points de défense.` : `Each direct hit bypasses up to ${p.sunder} defence.`;
  if (p.damageBonus) return fr ? `Chaque coup direct inflige ${p.damageBonus} dégâts supplémentaires.` : `Every direct hit deals ${p.damageBonus} additional damage.`;
  if (p.upkeepEnergy && t.shared) return fr ? `Chaque créature alliée gagne ${p.upkeepEnergy} énergie de plus à chaque entretien.` : `Each allied creature gains ${p.upkeepEnergy} extra energy at each upkeep.`;
  throw new Error(`No field note for passive ${t.id}`);
}

function entry(t, lang, byId) {
  const l = lore[lang], n = escape(name(t, lang)), id = 'form-' + key(t.id);
  const parentLinks = ids => ids.map(id => `<a href="#form-${key(id)}">${escape(name(byId.get(id), lang))}</a>`).join(' · ');
  const need = t.anyOf.length ? `${l.afterAny} · ${parentLinks(t.anyOf)}` : t.prerequisites.length ? `${l.next} · ${parentLinks(t.prerequisites)}` : l.roots;
  return `<details class="form-entry" id="${id}" data-form="${n}">
    <summary><span class="roman">${romans[t.level]}</span><strong>${n}</strong><span class="form-teaser">${t.passive ? l.passive : t.spells.map(s => escape(name(s, lang))).join(' · ')}</span></summary>
    <div class="field-entry ${t.passive ? 'passive-entry' : ''}">
      ${t.visual.artwork ? `<div class="field-art"><img src="/assets/evolutions/${t.visual.artwork}.webp" alt="" width="450" height="300" loading="lazy" decoding="async"></div>` : ''}
      <div><p class="requirement">${need}</p><p class="anatomy">${escape(t.notes[lang])}</p>
      ${t.passive ? `<p class="passive-rule">${passive(t, lang)}</p>` : `<ul class="spell-list">${t.spells.map(s => spell(s, lang)).join('')}</ul>`}</div>
    </div></details>`;
}

function familySection(family, lang, byId, i) {
  const l = lore[lang], nodes = [...byId.values()].filter(t => t.visual.family === family);
  const root = nodes.find(t => t.level === 1), cap = nodes.find(t => t.level === 4);
  const pairs = nodes.filter(t => t.level === 2).map(t => [t, nodes.find(child => child.level === 3 && child.prerequisites.includes(t.id))]);
  const link = t => `<a href="#form-${key(t.id)}">${escape(name(t, lang))}</a>`;
  return `<section class="family-record" id="family-${family}" aria-labelledby="title-${family}">
    <header><div><p class="kicker">${l.route}</p><h2 id="title-${family}">${l.familyNames[i]}</h2></div>
      <nav class="path-map" aria-label="${l.route} · ${l.familyNames[i]}">${pairs.map(([middle, last]) => `<div>${link(middle)} <span aria-hidden="true">→</span> ${link(last)}</div>`).join('')}</nav></header>
    ${[root, ...pairs.flat(), cap].map(t => entry(t, lang, byId)).join('')}
  </section>`;
}

export function guidePage(c, catalogue, app) {
  const lang = c.lang, l = lore[lang];
  const byId = new Map(catalogue.nodes.map(t => [t.id, t]));
  const countSpells = new Set([...catalogue.baseSpells, ...catalogue.nodes.flatMap(t => t.spells)].map(s => s.id)).size;
  const forms = catalogue.nodes.filter(t => t.visual.artwork).length;
  return layout(c, 'guide', app, l.guideTitle, l.guideIntro, `
    <div class="contain guide-page">
      <header class="guide-head"><div><p class="kicker">${c.guide}</p><h1>${l.guideTitle}</h1><p class="lead">${l.guideIntro}</p>
        <p class="catalogue-counts">${forms} ${l.formTotal} <span>·</span> ${catalogue.nodes.length - forms} ${l.passiveTotal} <span>·</span> ${countSpells} ${l.spellTotal}</p></div>
        <img src="/assets/evolutions/base-creature.webp" width="1536" height="1024" alt="${escape(l.altBase)}"></header>
      <div class="guide-tools"><nav class="family-jumps" aria-label="${l.familyLabel}">${families.map((family, i) => `<a href="#family-${family}">${l.familyNames[i]}</a>`).join('')}</nav>
        <div class="field-search" data-search hidden><label for="field-query">${l.search}</label><div><input id="field-query" type="search" placeholder="${escape(l.placeholder)}" autocomplete="off"><button type="button" data-clear hidden>${l.clear}</button></div>
        <span data-search-status role="status" class="visually-hidden"></span></div></div>
      <div data-results hidden><h2>${l.results}</h2><p data-empty hidden>${l.none}</p><ul class="search-results"></ul></div>
      <article class="base-record" id="form-ancestor" data-form="${l.base}"><p class="kicker">${l.innate}</p><h2>${l.base}</h2><p>${l.baseNote}</p>
        <ul class="spell-list">${catalogue.baseSpells.map(s => spell(s, lang)).join('')}</ul></article>
      <p class="reading-hint">${l.pathHint}</p>
      ${families.map((family, i) => familySection(family, lang, byId, i)).join('')}
    </div>`, ['guide.js']);
}
