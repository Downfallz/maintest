# Cas limites {#edge-cases}

Voici les règles que l’on lit de travers. Chacune est une règle, pas une exception.

## Un tour sauté {#skipped-turn}

Une créature qui ne peut pas agir quand vient son tour est **sautée** : rien ne se résout pour elle, elle ne dépense
rien, et rien n’est demandé à son propriétaire. Le tour suivant vient.

Une créature ne peut pas agir quand elle a été **vaincue** ou **étourdie** plus tôt dans le round. Une créature qui est
dans le round peut toujours lancer un sort : Focus coûte {spell:wait:v1.energyCost} d’Énergie et cible son
lanceur{?spell:wait:v1.origin = 'Self'}. Un sort qu’elle ne peut pas payer maintenant, ou qui n’a aucune cible légale, n’est pas proposé.

Un tour tardif est donc un risque : avant qu’il vienne, votre créature peut être vaincue ou étourdie, et la cible que
vous visiez peut avoir disparu. C’est le prix d’agir tard, et la raison d’être d’Instinct.

*Exemple.* La Créature 1 est en Précision et la Créature 5 en Instinct. La Créature 5 agit en premier et lance
Crushing Stomp sur la Créature 1, ce qui l’étourdit{?spell:crushing_stomp:v1.stun}. Quand vient le tour de la Créature 1, elle est sautée, et ne
dépense pas d’Énergie.

sources: ADR 0083, ADR 0110, ADR 0112, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: fizzle
title: Un tour sauté
Une créature qui ne peut pas agir quand vient son tour est sautée : rien ne se résout, et rien n’est dépensé.
Cela arrive quand la créature a été vaincue ou étourdie plus tôt dans le round.
> Une créature étourdie par un tour précédent est sautée quand vient le sien, et garde toute son Énergie.
```

## Le plateau change entre deux tours {#the-board-changes}

Chaque sort se résout avant que vienne le tour suivant, donc **vous choisissez toujours sur le plateau tel qu’il
est**, et rien ne se passe entre votre confirmation et le résultat : une cible ne peut pas être vaincue entre-temps.

Ce qui change, c’est le plateau avant votre tour. Un drain a pu laisser votre créature à court du sort sur lequel
vous comptiez, et un ennemi vaincu laisse votre sort avec moins de cibles. Vous choisissez parmi ce qui est
proposé.

*Exemple.* La Créature 1 a 3 points d’Énergie et compte sur Emberstorm, qui coûte {spell:meteor:v1.energyCost}.
Avant son tour, la Créature 5 lance Soul Feast sur elle, qui réduit son Énergie de
{spell:soul_devourer:v1.energyDrain} : l’Énergie de la Créature 1 tombe à
{max(0, 3 - spell:soul_devourer:v1.energyDrain)}. À son tour, l’application ne propose que les sorts qu’elle peut
payer maintenant.

sources: ADR 0035, ADR 0083, ADR 0112, game-rules#round-sequence-adr-0010

## Une créature étourdie passe le round {#stunned}

Une créature sous **Stun** ne prend pas de tempo, n’a pas de tour, et ne lance rien pendant chaque round que dure
le Stun. Elle gagne encore son Énergie à l’Entretien et subit encore son Bleed et sa Regeneration. Elle peut encore
acquérir une mutation, puisque les mutations viennent avant les tempos. Elle peut encore être ciblée, soignée et
vaincue.

Un Stun qui arrive **pendant** le combat coûte aussi à cette créature son tour de ce round, si son tour n’est pas
encore venu : elle est sautée.

**L’immunité au Stun.** Quand un Stun se termine au Nettoyage, la créature **ne peut pas être étourdie au round
suivant**. Un Stun qui atteint une créature déjà étourdie, ou immunisée, est ignoré : les autres effets du sort
s’appliquent quand même, et le Stun en cours ni ne s’allonge ni ne recommence. Aucune créature ne peut donc être
étourdie sans interruption : chaque Stun est suivi d’un round où elle ne peut pas être étourdie.

*Exemple.* Au round 5, la Créature 4 est touchée par Crushing Stomp : Stun pendant
{spell:crushing_stomp:v1.stun.durationRounds|round|rounds}. Elle passe ensuite
{spell:crushing_stomp:v1.stun.durationRounds|round|rounds} en entier. Au Nettoyage qui termine le Stun, elle devient
immunisée pour un round. Un autre Crushing Stomp sur elle pendant qu’elle est étourdie, ou immunisée, inflige ses
dégâts{?spell:crushing_stomp:v1.damage > 0} et n’étourdit rien.

sources: ADR 0041, ADR 0072, game-rules#round-sequence-adr-0010

## Combien de temps dure un état {#durations}

Lisez les rounds d’un état comme **autant de rounds suivants**. Un état appliqué ce round dure jusqu’à la fin de ce
round, puis pendant les rounds qu’il indique : le premier décompte après son arrivée ne compte pas. Un Bleed d’un
round inflige ses dégâts une fois, à l’Entretien suivant. Un Stun de 2 rounds retire les deux rounds suivants.

Un état ne recommence jamais : un second du même type se place à côté du premier, et chacun décompte de son côté.
Seul le Stun est différent : un second est [ignoré](#stunned).

*Exemple.* Au round 5, la Créature 3 lance Infestation sur
{spell:summon_minions:v1.targets|ennemi|ennemis}{?spell:summon_minions:v1.origin = 'Enemy'} : un Bleed de
{spell:summon_minions:v1.bleed} pendant {spell:summon_minions:v1.bleed.durationRounds|round|rounds} sur chacun. Il
n’inflige rien au round 5. Chaque cible subit ensuite {spell:summon_minions:v1.bleed} à l’Entretien de chacun des
{spell:summon_minions:v1.bleed.durationRounds|round|rounds} suivants, quelle que soit sa Défense :
{spell:summon_minions:v1.bleed * spell:summon_minions:v1.bleed.durationRounds} en tout.

sources: ADR 0041, game-rules#implemented, game-rules#round-sequence-adr-0010

## Le jet de critique {#the-critical-roll}

Quand le sort d’une créature en **Précision** se résout et que le sort indique une chance de critique, un d20 est
lancé **une fois pour tout le lancer**. Le sort indique la face qu’il lui faut : d20 : 11+ signifie 11 ou plus. Une
créature en **Instinct** ne lance jamais le dé, et un sort qui n’indique aucune chance ne lance jamais le dé. La
chance propre à la créature, {creature.criticalChance * 100} %, s’ajoute à celle du sort.

Un critique multiplie par {rules.criticalMultiplier}, en laissant tomber toute fraction, **ce que le lancer met
maintenant sur la Santé d’une cible**, et rien d’autre :

- **Multiplié :** les dégâts sur une cible, son bonus de dégâts compris, et un soin sur une cible.
- **Non multiplié :** tout ce qui est sur la ligne du lanceur, comme les
  {spell:hateful_sacrifice:v1.caster.damage} dégâts que Blood Price inflige à son lanceur ; l’Énergie, donnée ou prise ; un effet durable, un
  Bleed, une Regeneration, un Stun, un renfort ou un malus ; et une brèche, qui prend sur la Défense, pas sur le
  coup.

L’ordre est celui de [la résolution d’un sort](#resolving-a-spell) : ajoutez le bonus de dégâts, multipliez, puis
soustrayez la Défense.

*Exemple.* La Créature 2 est en Précision et lance Vital Surge sur la Créature 3, dont la Santé est de
{creature.health - 12} sur {creature.health} : soin {spell:restorative_gush:v1.heal}, d20 :
{21 - (creature.criticalChance + spell:restorative_gush:v1.criticalChance) * 20}+. Le d20 donne un critique : le
soin devient {floor(spell:restorative_gush:v1.heal * rules.criticalMultiplier)}, et la Créature 3 passe à
{min(creature.health, creature.health - 12 + floor(spell:restorative_gush:v1.heal * rules.criticalMultiplier))}.
Un soin ne dépasse jamais la Santé avec laquelle une créature a commencé.

sources: ADR 0033, ADR 0042, ADR 0100, ADR 0154, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: critical
title: Critique
Une créature en Précision lance un d20 quand son sort se résout, contre le nombre que le sort indique.
Sur un critique, les dégâts et les soins du sort sur ses cibles sont multipliés.
Une créature en Instinct ne lance jamais le dé.
> Un sort indique d20 : 11+. Un jet de 14 est un critique : ses 7 dégâts deviennent {floor(7 * rules.criticalMultiplier)}.
```
