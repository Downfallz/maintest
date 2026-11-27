# Le round, étape par étape {#the-round}

Chaque round parcourt les mêmes étapes, dans cet ordre, jamais à rebours. Un round a deux phases, **Commandement →
Combat**. Avant elles, au début du round, l’application donne l’Énergie et applique les effets en cours ; après
elles, à la fin du round, elle fait décompter les états et clôt le round. Personne ne décide rien à l’un ou l’autre
de ces moments. Chaque section ci-dessous est une étape. Elle dit quand elle vient, qui décide, et ce qui se passe.
Chaque exemple utilise les nombres de cette table et les vrais sorts.

## Gain d’Énergie {#energy-gain}

*Début du round · Énergie.* Le round commence, et l’Énergie de chaque créature **vivante** augmente de {rules.energyPerRound},
plus l’Énergie que lui donnent les passifs d’Archmage. Personne ne décide rien. Une créature vaincue ne gagne rien,
ni maintenant ni jamais.

Archmage est partagé avec toute l’équipe : chaque exemplaire que possède une équipe ajoute
{tier:archmage:v1.upkeepEnergy} à l’Énergie que gagne chaque créature vivante de cette équipe au début de chaque round, à partir du round qui
suit son acquisition.

*Exemple.* Le round 8 commence. La Créature 1 a 1 point d’Énergie et la Créature 2 en a 4 ; la Créature 3 est
vaincue. La Créature 1 passe à {1 + rules.energyPerRound} et la Créature 2 à {4 + rules.energyPerRound}. La
Créature 3 ne gagne rien. Si la Créature 2 avait acquis Archmage au round 7, chaque créature vivante de son équipe
gagnerait {rules.energyPerRound + tier:archmage:v1.upkeepEnergy} ; avec un second exemplaire dans l’équipe,
{rules.energyPerRound + 2 * tier:archmage:v1.upkeepEnergy}.

sources: ADR 0010, ADR 0101, ADR 0114, ADR 0132, game-rules#round-sequence-adr-0010

```card
id: energy
title: Énergie
Chaque créature vivante gagne de l’Énergie au début de chaque round.
Un sort coûte de l’Énergie, dépensée quand il se résout, jamais avant.
> Une créature à 1 point d’Énergie en gagne {rules.energyPerRound} au début du round et en a {1 + rules.energyPerRound}, assez pour un sort qui coûte {1 + rules.energyPerRound}.
```

## Effets en cours {#ongoing-effects}

*Début du round · Effets en cours.* Juste après le gain d’Énergie, les états qui agissent au début du round s’appliquent, en trois passes.
L’ordre compte :

1. La **régénération d’Énergie** donne son Énergie.
2. La **Regeneration** soigne, jamais au-dessus de la Santé avec laquelle la créature a commencé.
3. Le **Bleed** inflige ses dégâts. **Un Bleed ignore la Défense** : ce sont les seuls dégâts du jeu à le faire.

Une créature qui a deux états d’un même type subit les deux. Le soin vient exprès avant le saignement : une
Regeneration peut faire passer une créature au travers d’un Bleed qui, sans elle, la vaincrait.

**Un Bleed peut terminer la partie.** Quand la passe du Bleed laisse une équipe sans créature vivante, la partie
se termine là, avant que rien ne soit choisi. Deux équipes vaincues par la même passe, c’est une partie nulle.

*Exemple.* La Créature 5 est à 1 point de Santé, avec une Regeneration de {spell:healing_screech:v1.regeneration}
venue de Vital Echo et un Bleed de {spell:toxic_waves:v1.bleed} venu de Void Pulse. Sa Défense est de 3. La
Regeneration la soigne jusqu’à {1 + spell:healing_screech:v1.regeneration}. Le Bleed inflige
{spell:toxic_waves:v1.bleed}, et sa Défense ne compte pas : sa Santé finit à
{1 + spell:healing_screech:v1.regeneration - spell:toxic_waves:v1.bleed}. Dans l’autre ordre, le
Bleed l’aurait vaincue avant que le soin arrive.

sources: ADR 0019, ADR 0020, ADR 0083, game-rules#round-sequence-adr-0010

## Mutations {#evolution}

*Commandement · Évolution.* Seulement à un round de mutation : le round {rules.firstEvolutionRound}, puis tous les
{rules.evolutionInterval|round|rounds}. À tout autre round, cette étape ne demande rien à personne et le round passe aux
tempos.

Chaque équipe a {rules.evolutionPicksPerOpportunity} choix. Un choix acquiert une mutation pour une de vos
créatures **vivantes**, et **une créature acquiert au plus une mutation par round** : vos choix vont à des créatures
différentes, et aucune créature ne monte de deux Stades en un round. Une équipe réduite à une créature vivante a
un seul choix.

Les deux joueurs choisissent en secret, en même temps. Rien ne change tant que les deux n’ont pas fini ; alors
chaque choix des deux équipes est révélé et acquis à la fois. Un choix dont vous ne voulez pas, vous le passez.
L’Arbre des mutations s’ouvre avec l’étape : choisissez une créature, lisez une mutation, confirmez-la.

**Ce qu’est une mutation.** Un ensemble de sorts qui porte un nom. L’acquérir apprend à la créature chacun de ces
sorts à la fois, et augmente son initiative de base du bonus de la mutation, pour le reste de la partie. Ce bonus
est la seule chose qui fasse jamais bouger une initiative de base : acquérir est la façon dont une créature devient
plus forte, et aussi celle dont elle devient plus rapide.

**Quelle mutation.** Il y a trois familles, Brute, Warped et Predator, chacune un arbre de quatre Stades. Une
mutation de Stade I ne demande rien. Chaque autre demande une mutation nommée de sa famille, un Stade en dessous :
l’Arbre des mutations montre laquelle. Une créature peut acquérir une mutation qu’elle ne possède pas quand elle
possède ce que cette mutation demande, et elle peut mêler les familles en acquérant le Stade I d’une autre
famille. Deux créatures peuvent prendre la même mutation. Ce qu’une
créature peut acquérir se lit d’après ce qu’elle possède à l’ouverture de l’étape : un choix de ce round n’ouvre
rien avant le round de mutation suivant.

*Exemple.* Round {rules.firstEvolutionRound}. Chaque créature connaît Strike{?creature.knows = 'spell:heavy_strike:v1'} et Focus{?creature.knows = 'spell:wait:v1'}{?creature.knows.count = 2} et a une initiative de
{creature.initiative}. Le premier joueur choisit Brute pour la Créature 1 et Warped pour la Créature 2. Il
voudrait aussi Oppressor pour la Créature 1, mais elle demande Brute{?tier:marauder:v1.needs = 'tier:brute:v1'}{?tier:marauder:v1.needs.count = 1}, que la Créature 1 ne possède pas encore, et la
Créature 1 a déjà son choix. En même temps, le second joueur choisit Predator pour la Créature 4 et Brute pour la
Créature 5. Les deux ont fini, et les quatre mutations sont acquises ensemble :

- Créature 1, Brute : Pummel{?tier:brute:v1.teaches = 'spell:pummel:v1'} et Brace{?tier:brute:v1.teaches = 'spell:guard:v1'}{?tier:brute:v1.teaches.count = 2}, et une initiative de {creature.initiative + tier:brute:v1.initiativeBonus}.
- Créature 2, Warped : Shock{?tier:occultist:v1.teaches = 'spell:lightning_bolt:v1'} et Revitalize{?tier:occultist:v1.teaches = 'spell:rejuvenate:v1'}{?tier:occultist:v1.teaches.count = 2}, et une initiative de
  {creature.initiative + tier:occultist:v1.initiativeBonus}.
- Créature 4, Predator : Venom Claw{?tier:prowler:v1.teaches = 'spell:poison_slash:v1'} et Quill{?tier:prowler:v1.teaches = 'spell:throwing_star:v1'}{?tier:prowler:v1.teaches.count = 2}, et une initiative de
  {creature.initiative + tier:prowler:v1.initiativeBonus}.
- Créature 5, Brute : Pummel{?tier:brute:v1.teaches = 'spell:pummel:v1'} et Brace{?tier:brute:v1.teaches = 'spell:guard:v1'}{?tier:brute:v1.teaches.count = 2}, et une initiative de {creature.initiative + tier:brute:v1.initiativeBonus}.

Oppressor attend la Créature 1 jusqu’au round {rules.firstEvolutionRound + rules.evolutionInterval}, le round de
mutation suivant.

sources: ADR 0056, ADR 0057, ADR 0059, ADR 0066, ADR 0089, game-rules#round-sequence-adr-0010

```card
id: mutation
title: Mutation
Une mutation apprend de nouveaux sorts à une créature et élève son initiative pour la partie.
Une clé de voûte ne donne qu’un passif{?tier:titan:v1.initiativeBonus = 0}{?tier:archmage:v1.initiativeBonus = 0}{?tier:apex:v1.initiativeBonus = 0}.
À un round de mutation, votre équipe a {rules.evolutionPicksPerOpportunity|choix|choix}, un par créature.
> Une créature à {creature.initiative} d’initiative acquiert Brute (+{tier:brute:v1.initiativeBonus}) et agit à {creature.initiative + tier:brute:v1.initiativeBonus}.
```

## Clés de voûte {#capstones}

Chaque famille se termine par une mutation de Stade IV, sa **clé de voûte**. Une clé de voûte demande **n’importe
laquelle** des mutations de Stade III de sa famille. Elle n’apprend aucun sort et n’ajoute pas d’initiative. Elle
donne un **passif** à la place, détenu de la révélation qui l’acquiert jusqu’à la fin de la partie :

| Clé de voûte | Famille | Demande l’une de | Son passif |
| --- | --- | --- | --- |
| Titan | Brute | Ravager, Colossus, Tyrant | Chaque coup ignore {tier:titan:v1.sunder} de Défense |
| Archmage | Warped | Cataclysm, Revenant, Transcendent | Énergie +{tier:archmage:v1.upkeepEnergy} au début de chaque round, pour chaque créature de l’équipe |
| Apex | Predator | Blightweaver, Deathstalker, Soulreaver | Dégâts +{tier:apex:v1.damageBonus} à chaque coup |

**Archmage est partagé.** La créature qui l’acquiert le possède, et chaque créature de son équipe détient son
passif. Il se cumule : un coéquipier peut acquérir son propre exemplaire, et chaque exemplaire donne une fois de
plus son passif à chaque créature de l’équipe. Un exemplaire continue de donner après que la créature qui l’a
acquis est vaincue : seule la créature vaincue ne gagne plus rien.

Comment joue chaque passif : voir [les passifs](#passives).

sources: ADR 0101, ADR 0106, ADR 0114, game-rules#implemented

## Tempo {#tempo}

*Commandement · Tempo.* Pour chacune de vos créatures vivantes qui n’est pas étourdie, choisissez un tempo :
**Instinct** ou **Précision**. Les deux joueurs choisissent en secret, et les tempos sont révélés ensemble.

- **Instinct** agit avant chaque créature en Précision, et **ne peut pas obtenir de critique ce round**, quoi que
  son sort indique.
- **Précision** agit après chaque créature en Instinct, et garde sa chance de critique.

Choisissez Instinct pour porter un coup qui n’a pas besoin d’un critique, ou pour agir avant quelque chose qui vous
vaincrait. Choisissez Précision quand le critique est ce que vous espérez, ou quand vous voulez que le plateau
change avant d’agir. Un tempo ne choisit aucun sort : le sort se choisit quand vient le tour de la créature.

**Une créature étourdie ne prend pas de tempo.** Elle n’est pas dans le round : pas de tempo, pas de tour, rien
qu’elle puisse lancer. Une créature vaincue n’est pas dans le round non plus.

*Exemple.* La Créature 4 a été étourdie au round précédent. Le second joueur choisit des tempos pour les
Créatures 5 et 6 seulement ; le premier joueur pour les Créatures 1, 2 et 3. Cinq tempos sont révélés, et le round
aura cinq tours, pas six.

sources: ADR 0010, ADR 0072, ADR 0112, ADR 0208, game-rules#round-sequence-adr-0010

```card
id: tempo
title: Tempo
À chaque round, chaque créature non étourdie prend un tempo, Instinct ou Précision.
Instinct agit avant toute créature en Précision, sans critique ce round.
Précision agit après Instinct et garde sa chance de critique.
> Une créature en Instinct à {creature.initiative} d’initiative agit avant une créature en Précision à {creature.initiative + 4}.
```

## L’ordre des tours {#turn-order}

*Commandement · Résolution de l’ordre des tours.* L’application construit la **frise** du round, un tour pour
chaque créature qui a un tempo :

1. Les créatures en Instinct d’abord, la plus haute initiative en premier.
2. Puis les créatures en Précision, de la même façon.

L’initiative d’une créature est son initiative de base, augmentée de ses renforts d’initiative et diminuée de ses
malus d’initiative, jamais en dessous de zéro. Elle est lue une fois, quand la frise est construite : un malus qui
arrive pendant le combat change l’ordre du round suivant, pas celui-ci.

Instinct vient toujours en premier. Une créature en Instinct à une initiative de 0 agit avant une créature en
Précision à 20.

**Une égalité entre les équipes** est tranchée au hasard : l’application tire quelle équipe prend chaque place de
l’égalité. La frise marque l’égalité comme tranchée au hasard, et toucher une place dit quelle équipe a obtenu
la place la plus tôt. Comment une équipe ordonne ses propres créatures dans ses places, c’est
[l’étape suivante](#tie-order).

*Exemple.* Les Créatures 1, 2, 4 et 5 ont acquis Warped, et les Créatures 1 et 4 ont depuis acquis Stormborn. Les
Créatures 1 et 4 ont une initiative de
{creature.initiative + tier:occultist:v1.initiativeBonus + tier:elementalist:v1.initiativeBonus}, les Créatures 2
et 5 de {creature.initiative + tier:occultist:v1.initiativeBonus}, et les Créatures 3 et 6 de
{creature.initiative}. Au round précédent, un Ice Grip a diminué l’initiative de la Créature 2 de
{spell:frostbite:v1.initiativeDebuff} : elle est de
{max(0, creature.initiative + tier:occultist:v1.initiativeBonus - spell:frostbite:v1.initiativeDebuff)} ce round.
Les Créatures 1 et 4 ont choisi Instinct, les autres Précision. Les Créatures 1 et 4 sont à égalité, et l’égalité
est tranchée au hasard : la Créature 4 agit en premier, puis la Créature 1. Puis les créatures en Précision, par
leur initiative : la Créature 5, puis les Créatures 3 et 6, à égalité entre les équipes, tranchée au hasard elle
aussi, et la Créature 2 en dernier.

sources: ADR 0036, ADR 0059, ADR 0063, ADR 0208, game-rules#round-sequence-adr-0010

```card
id: initiative
title: Initiative
L’initiative ordonne les créatures dans leur tempo, la plus haute en premier.
Chaque mutation ajoute son bonus à l’initiative de base, pour de bon.
Une égalité entre les deux équipes est tranchée au hasard.
> En Précision, la créature à {creature.initiative + 2} d’initiative agit avant celle à {creature.initiative}.
```

## Ordonner vos égalités {#tie-order}

*Commandement · Ordre des égalités.* Là où votre équipe occupe deux places ou plus dans une même égalité, vous
choisissez laquelle de vos créatures à égalité prend quelle place. Les places de l’autre équipe ne bougent pas. Une
égalité que votre équipe occupe seule est aussi à vous d’ordonner, et rien n’est tiré pour elle. Les deux joueurs
ordonnent en secret, et les ordres sont révélés ensemble. Un round sans une telle égalité ne demande rien à personne.

*Exemple.* Les Créatures 3, 5 et 6 sont à égalité dans le groupe Précision. L’égalité est tranchée au hasard, et
les places vont au second joueur, puis au premier, puis au second. Le second joueur en occupe deux et veut que la Créature 6 agisse en premier : la Créature
6 prend la première place, la Créature 3 la deuxième, la Créature 5 la troisième.

sources: ADR 0063, ADR 0208, game-rules#round-sequence-adr-0010

## Le tour de votre créature {#your-turn}

*Combat · Activation.* La frise est parcourue une fois, tour par tour. Quand vient le tour d’une de vos créatures,
vous choisissez son **sort** et ses **cibles**, sur le plateau tel qu’il est, et vous confirmez. Le sort se résout
aussitôt, avant le tour suivant. Le sort et ses cibles deviennent publics ensemble.

Rien n’est déclaré à l’avance. Vous choisissez parmi les sorts que la créature connaît et peut payer maintenant, et
vous choisissez en sachant tout ce qui s’est passé dans ce round. Chaque créature connaît Focus{?creature.knows = 'spell:wait:v1'}, dont le coût en
Énergie est de {spell:wait:v1.energyCost} et qui ne cible que son lanceur{?spell:wait:v1.origin = 'Self'} : une créature dans le round a toujours un
sort à lancer.

**Rien n’est réservé.** L’Énergie est dépensée quand le sort se résout. Jusqu’à ce que vienne son tour, une
créature peut être vaincue, étourdie ou drainée. Une créature vaincue ou étourdie avant son tour est
[sautée](#skipped-turn).

sources: ADR 0083, ADR 0110, ADR 0112, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: intent
title: Choisir un sort
Aucun sort n’est déclaré à l’avance.
Au tour d’une créature, vous choisissez son sort et ses cibles sur le plateau tel qu’il est. Il se résout aussitôt.
> Une créature à {1 + rules.energyPerRound} points d’Énergie peut lancer, à son tour, tout sort qu’elle connaît coûtant {1 + rules.energyPerRound} ou moins.
```

## Cibles {#targets}

Un sort dit qui il peut cibler :

- **Soi** : le lanceur, et seulement le lanceur.
- **Allié** : n’importe quelle créature vivante de votre équipe, **lanceur compris**.
- **Ennemi** : n’importe quelle créature vivante de l’autre équipe.

Un sort pour une cible en prend exactement une. Un sort pour plusieurs en prend **une ou plus, jusqu’à son
maximum** : Emberstorm, pour plusieurs{?spell:meteor:v1.targets > 1}
ennemis{?spell:meteor:v1.origin = 'Enemy'}, peut n’en prendre qu’un. Une créature vaincue ne peut pas être une
cible, et aucune créature ne peut être désignée deux fois par un même lancer.

*Exemple.* Le tour de la Créature 6 vient, et son sort est Brace, pour un{?spell:guard:v1.targets = 1}
allié{?spell:guard:v1.origin = 'Ally'} : elle peut se cibler elle-même ou
cibler la Créature 4, mais pas la Créature 5, vaincue plus tôt dans ce round. Puis la Créature 3 lance Emberstorm
alors que deux ennemis sont debout : elle peut en désigner un ou les deux.

sources: ADR 0083, game-rules#round-sequence-adr-0010

## Résoudre un sort {#resolving-a-spell}

Un sort se résout entièrement avant que vienne le tour suivant, dans cet ordre :

1. **Payer.** L’Énergie du lanceur baisse du coût du sort.
2. **Le critique**, tranché une fois pour tout le lancer, si le lanceur est en Précision et que le sort indique
   une chance. Voir [la chance de critique](#the-critical-roll).
3. **Chaque effet, sur chaque cible.** Un **coup**, ce sont les dégâts du sort, plus le **bonus de dégâts** du lanceur,
   multipliés sur un critique, moins la **Défense** de la cible, jamais en dessous de zéro. Un soin ne dépasse
   jamais la Santé avec laquelle une créature a commencé. Un drain d’Énergie prend au plus ce que la cible a. Un
   effet durable devient un [état](#conditions).
4. **La ligne du lanceur**, si le sort en a une, une fois pour tout le lancer. Les dégâts qu’elle inflige sont
   réduits par la propre Défense du lanceur, et ne prennent jamais de bonus de dégâts ni de critique.

La **Défense** d’une créature est sa Défense de base plus ses renforts de Défense, qui comptent pour
{engine.defenseBuffCeiling} au plus quel que soit leur nombre, moins ses malus de Défense, jamais en dessous de zéro.

Un coup ne va jamais au-delà de la Santé qui reste : une créature à 3 points de Santé touchée pour 10 en perd 3. Un
sort qui ne change rien sur une cible, 0 dégât à travers une Défense ou un soin à Santé pleine, ne lui a rien fait.

*Exemple.* La Créature 2 est en Précision, à 4 points d’Énergie, et lance Incinerate{?spell:engulfing_flames:v1.origin = 'Enemy'} sur la Créature 5 : coût
{spell:engulfing_flames:v1.energyCost}, dégâts {spell:engulfing_flames:v1.damage}, Crit
{(creature.criticalChance + spell:engulfing_flames:v1.criticalChance) * 100} %. La Créature 5 a lancé Carapace
au round précédent : sa Défense est de {creature.defense + spell:full_plate:v1.defenseBuff}.

- Payer : l’Énergie de la Créature 2 passe à {4 - spell:engulfing_flames:v1.energyCost}.
- Le lancer obtient un critique : {spell:engulfing_flames:v1.damage} × {rules.criticalMultiplier} =
  {floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier)}.
- Moins la Défense : la Créature 5 perd
  {max(0, floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier) - (creature.defense + spell:full_plate:v1.defenseBuff))},
  et sa Santé passe de {creature.health} à
  {max(0, creature.health - max(0, floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier) - (creature.defense + spell:full_plate:v1.defenseBuff)))}.

Sans critique, elle aurait perdu
{max(0, spell:engulfing_flames:v1.damage - (creature.defense + spell:full_plate:v1.defenseBuff))}. **Multipliez
d’abord, soustrayez ensuite** : l’autre ordre donne un nombre que le jeu n’inflige jamais.

sources: ADR 0033, ADR 0035, ADR 0076, ADR 0083, ADR 0101, ADR 0208, game-rules#round-sequence-adr-0010

```card
id: defense
title: Défense
La Défense réduit les dégâts de chaque coup, jamais sous zéro.
Ses renforts ajoutent au plus {engine.defenseBuffCeiling} en tout.
Seuls les dégâts dans la durée l’ignorent.
> Une créature commence à {creature.defense} de Défense. Un coup de 7 inflige {max(0, 7 - creature.defense)}, ou {max(0, 5 - creature.defense)} à {creature.defense + 2} de Défense.
```

## Le bonus de dégâts {#damage-bonus}

Le **bonus de dégâts** d’une créature s’ajoute à chaque coup qu’elle porte : Apex donne
{tier:apex:v1.damageBonus}, et chaque renfort de dégâts sur elle ajoute son propre nombre. Il s’ajoute **avant**
que le critique multiplie et avant que la Défense soit soustraite. Il ne touche jamais un Bleed, un soin, ni la
ligne du lanceur.

*Exemple.* La Créature 4 possède Apex, est en Précision, et lance Blood Price{?spell:hateful_sacrifice:v1.origin = 'Enemy'} sur la Créature 1, dont la Défense est
de 3 : dégâts {spell:hateful_sacrifice:v1.damage}, Crit
{(creature.criticalChance + spell:hateful_sacrifice:v1.criticalChance) * 100} %. Ajoutez le bonus :
{spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus}. Sur un critique,
{floor((spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus) * rules.criticalMultiplier)}, moins 3 : la
Créature 1 perd
{max(0, floor((spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus) * rules.criticalMultiplier) - 3)}.
Sans critique, {max(0, spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus - 3)}. **Ajoutez, multipliez,
soustrayez**, dans cet ordre. La ligne du lanceur de Blood Price inflige ensuite
{spell:hateful_sacrifice:v1.caster.damage} dégâts à la Créature 4, moins sa propre Défense de {creature.defense} :
elle perd {max(0, spell:hateful_sacrifice:v1.caster.damage - creature.defense)}, sans bonus ni critique.

*Exemple, renforts de dégâts.* Alpha Roar a donné à la Créature 5 un renfort de dégâts de
{spell:thundering_seal:v1.damageBuff}, et Overdrive un autre de {spell:adrenaline_tonic:v1.damageBuff}. Son
Strike, à {spell:heavy_strike:v1.damage}, inflige
{spell:heavy_strike:v1.damage + spell:thundering_seal:v1.damageBuff + spell:adrenaline_tonic:v1.damageBuff} à une
créature sans Défense. Les renforts de dégâts se cumulent : ajoutez-les tous.

sources: ADR 0101, ADR 0208, game-rules#round-sequence-adr-0010

## La brèche {#sunder}

Le passif de Titan est une **brèche** : chaque coup que porte sa créature rencontre la Défense de la cible diminuée
de {tier:titan:v1.sunder}, jamais en dessous de zéro. Une brèche retire de la Défense ; elle n’ajoute jamais de
dégâts, et contre une cible sans Défense elle ne fait rien. Elle ne déplace aucune Défense : la Défense de la cible
reste la même face à tous les autres.

*Exemple.* La Créature 3 possède Titan et lance Dominate{?spell:chain_slash:v1.origin = 'Enemy'}, dégâts {spell:chain_slash:v1.damage}, sur la Créature 5,
qui détient la Défense d’Alpha Roar, {creature.defense + spell:thundering_seal:v1.defenseBuff}, et sur la Créature
4, à sa Défense de base de {creature.defense}. Sans critique, les deux{?spell:chain_slash:v1.targets > 1} perdent
{max(0, spell:chain_slash:v1.damage - max(0, creature.defense + spell:thundering_seal:v1.defenseBuff - tier:titan:v1.sunder))}.
Un lanceur sans brèche n’aurait infligé à la Créature 5 que
{max(0, spell:chain_slash:v1.damage - creature.defense - spell:thundering_seal:v1.defenseBuff)}.

sources: ADR 0106, game-rules#round-sequence-adr-0010

## Nettoyage {#cleanup}

*Fin du round · Nettoyage.* Chaque tour est venu, et les deux équipes sont encore debout. Chaque état décompte d’un
round, et un état à zéro se termine. Un état appliqué **ce** round ne décompte pas encore : il dure pour le reste
de ce round, puis pour chaque round qu’il indique. Voir [les durées](#durations).

Une créature vivante dont le Stun (étourdissement) se termine ici ne peut pas être étourdie au round suivant. Voir
[l’étourdissement](#stunned).

sources: ADR 0041, ADR 0072, game-rules#round-sequence-adr-0010

## La fin du round {#finalization}

*Fin du round · Clôture.* Si c’était le round {rules.roundCap}, la partie se termine : l’équipe à qui il reste **le
plus de Santé**, additionnée sur ses créatures, gagne, et à totaux égaux la partie est nulle. Sinon, le round
suivant commence.

*Exemple.* Le round {rules.roundCap} se termine avec les deux équipes debout. Les créatures du premier joueur ont
4, 5 et 6 points de Santé : 15. Celles du second ont 18, 0 et 0 : 18. **Le second joueur gagne**, avec moins de
créatures debout. C’est la Santé qui décide, pas les survivants.

sources: ADR 0011, ADR 0105, game-rules#match-lifecycle
