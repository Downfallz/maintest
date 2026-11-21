import { escape, layout, SHOWCASE_ART } from './layout.mjs';
import { cuePath, STROKED } from '../battle/src/cue-art.js';
import { lore } from './lore.mjs';
import { stageNumeral } from '../table/terms.js';
import { pictureHtml } from '../table/picture.js';

const families = ['brute', 'predator', 'warped'];
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

const effectIcons = {
  Damage: 'health-loss', Bleed: 'health-loss', Heal: 'health-gain', Regeneration: 'health-gain',
  EnergyGain: 'energy-gain', EnergyDrain: 'energy-loss', DefenseBuff: 'defense-gain', DefenseDebuff: 'defense-loss',
  DamageBuff: 'damage-gain', InitiativeDebuff: 'initiative-loss', Stun: 'stun',
};

function icon(key) {
  const style = STROKED.has(key) ? 'class="stroked"' : 'fill-rule="evenodd"';
  return `<svg class="cue-icon cue-${key}" viewBox="0 0 16 16" aria-hidden="true" focusable="false"><path ${style} d="${cuePath(key)}"/></svg>`;
}

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

// Who a spell reaches, by origin: one target, then several.
const targetWords = {
  en: { Self: ['Self'], Ally: ['1 ally', n => `Up to ${n} allies`], Enemy: ['1 enemy', n => `Up to ${n} enemies`] },
  fr: { Self: ['Soi-même'], Ally: ['1 allié', n => `Jusqu’à ${n} alliés`], Enemy: ['1 ennemi', n => `Jusqu’à ${n} ennemis`] },
};

function targetText(s, lang) {
  const words = targetWords[lang];
  const [one, several] = words[s.target] ?? words.Enemy;
  return s.target === 'Self' || s.count === 1 ? one : several(s.count);
}

// A spell's or a capstone's portrait, as the battle draws it beside the name (docs/art/spell-vfx.md#portraits): the
// kit's picture, declared for this page, and decoration, since the name beside it is the text.
const portrait = name => name
  ? pictureHtml(`spell-${name}`, { page: 'showcase/mutations', base: SHOWCASE_ART, class: 'spell-portrait', sizes: '40px' })
  : '';

// The scenes (docs/art/scene-paintings-plan.md, S4): each family opens on the one that shows what it is, and three
// forms carry theirs under their picture, inside the closed entry, so it loads only when the entry opens.
const familyScenes = { brute: 'brute-moult', predator: 'predator-and-deathstalker', warped: 'warped-listening' };
const formScenes = { ironbound: 'ironhide-scar', blightweaver: 'blightweaver-pressure-sac', parasite: 'parasite-under-slate' };
const scene = (picture, kind, alt, sizes) =>
  pictureHtml(picture, { page: 'showcase/mutations', base: SHOWCASE_ART, class: `${kind}-scene`, alt, sizes });

function spell(s, lang) {
  const l = lore[lang];
  const effects = list => list.map(e => `<li>${icon(effectIcons[e.kind])}<span>${escape(describeEffect(e, lang))}</span></li>`).join('');
  const critical = lang === 'fr' ? 'Critique en Précision' : 'Precision crit chance';
  return `<li id="spell-${key(s.id)}" data-spell-name="${escape(name(s, lang))}">
    <div class="spell-head"><strong>${portrait(s.portrait)}${escape(name(s, lang))}</strong>
      <div class="spell-stats"><span class="spell-chip cost">${icon('energy-gain')}${s.cost} ${l.energy}</span>
      ${s.critical > 0 ? `<span class="spell-chip crit" title="${critical}"><span aria-hidden="true">✦</span>${Math.round(s.critical * 100)}% <span aria-hidden="true">crit.</span><span class="visually-hidden"> ${critical}</span></span>` : ''}</div></div>
    <p class="spell-target">${targetText(s, lang)}</p><ul class="spell-effects">${effects(s.effects)}</ul>
    ${s.casterEffects.length ? `<div class="caster"><b>${l.caster}</b><ul class="spell-effects">${effects(s.casterEffects)}</ul></div>` : ''}
  </li>`;
}

function passive(t, lang) {
  const fr = lang === 'fr', p = t.passive;
  if (p.sunder) return fr ? `Chaque coup direct ignore jusqu’à ${p.sunder} points de défense.` : `Each direct hit bypasses up to ${p.sunder} defence.`;
  if (p.damageBonus) return fr ? `Chaque coup direct inflige ${p.damageBonus} dégâts supplémentaires.` : `Every direct hit deals ${p.damageBonus} additional damage.`;
  if (p.upkeepEnergy && t.shared) return fr ? `Chaque créature alliée gagne ${p.upkeepEnergy} énergie de plus à chaque entretien.` : `Each allied creature gains ${p.upkeepEnergy} extra energy at each upkeep.`;
  throw new Error(`No field note for passive ${t.id}`);
}

function initiativeChip(initiative) {
  if (!initiative) return '';
  const gain = initiative > 0;
  return `<span class="spell-chip initiative">${icon(gain ? 'initiative-gain' : 'initiative-loss')}${gain ? '+' : ''}${initiative} initiative</span>`;
}

// A form's picture, and its scene under it in the same column when it has one.
function figure(art, form, l) {
  const picture = formScenes[form];
  return picture ? `<div class="field-figure">${art}${scene(picture, 'form', l.scenes[form], '(min-width: 1421px) 421px, (min-width: 651px) 30vw, 81vw')}</div>` : art;
}

function entry(t, lang, byId) {
  const l = lore[lang], n = escape(name(t, lang)), id = 'form-' + key(t.id);
  const parentLinks = ids => ids.map(id => `<a href="#form-${key(id)}">${escape(name(byId.get(id), lang))}</a>`).join(' · ');
  let need = l.roots;
  if (t.anyOf.length) need = `${l.afterAny} · ${parentLinks(t.anyOf)}`;
  else if (t.prerequisites.length) need = `${l.next} · ${parentLinks(t.prerequisites)}`;
  return `<details class="form-entry" id="${id}" data-form="${n}">
    <summary><span class="roman">${stageNumeral(t.level)}</span><strong>${n}</strong><span class="form-teaser">${t.passive ? l.passive : t.spells.map(s => escape(name(s, lang))).join(' · ')}</span></summary>
    <div class="field-entry ${t.passive ? 'passive-entry' : ''}">
      ${t.visual.artwork ? figure(`<div class="field-art"><img src="/assets/evolutions/${t.visual.artwork}.webp" alt="" width="450" height="300" loading="lazy" decoding="async"></div>`, key(t.id), l) : ''}
      <div><div class="mutation-facts"><p class="requirement">${need}</p>
        ${initiativeChip(t.initiative)}</div><p class="anatomy">${escape(t.notes[lang])}</p>
      ${t.passive ? `<p class="passive-rule">${portrait(t.visual.portrait)}${passive(t, lang)}</p>` : `<ul class="spell-list">${t.spells.map(s => spell(s, lang)).join('')}</ul>`}</div>
    </div></details>`;
}

function familySection(family, lang, byId, i) {
  const l = lore[lang], nodes = [...byId.values()].filter(t => t.visual.family === family);
  const root = nodes.find(t => t.level === 1), cap = nodes.find(t => t.level === 4);
  if (!root) throw new Error(`No root for family ${family}`);
  if (!cap) throw new Error(`No capstone for family ${family}`);
  const pairs = nodes.filter(t => t.level === 2).map(t => [t, nodes.find(child => child.level === 3 && child.prerequisites.includes(t.id))]);
  const link = t => `<a href="#form-${key(t.id)}">${escape(name(t, lang))}</a>`;
  return `<section class="family-record" id="family-${family}" aria-labelledby="title-${family}">
    ${scene(familyScenes[family], 'family', l.scenes[family], '(min-width: 1421px) 1250px, 88vw')}
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
        ${pictureHtml('specimen-base-creature', { page: 'showcase/mutations', base: SHOWCASE_ART, alt: l.altBase, sizes: '(max-width: 650px) 218px, 435px' })}</header>
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
