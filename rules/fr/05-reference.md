# Référence {#reference}

Chaque état, chaque façon dont une partie se termine, chaque passif, et le classement, en un seul endroit.

## Les états {#conditions}

Un **état** est un effet durable sur une créature, avec une valeur et un nombre de rounds. Chacun, sauf un état
permanent, décompte au Nettoyage et se termine à zéro, et le premier décompte après son arrivée ne compte pas
([les durées](#durations)). Les états se cumulent : un second se place à côté du premier, sauf un Stun.

| État | Ce qu’il fait | Quand |
| --- | --- | --- |
| **Bleed** (saignement) | Inflige sa valeur, **en ignorant la Défense** | Début du round, après la Regeneration |
| **Regeneration** (régénération) | Soigne sa valeur, jamais au-dessus de la Santé de départ | Début du round, avant le Bleed |
| **Régénération d’Énergie** | Donne sa valeur en Énergie | Début du round, en premier |
| **Stun** (étourdissement) | Pas de tempo, pas de tour ; un second est ignoré, tout comme celui qui arrive au round suivant la fin d’un Stun | Tout le round |
| **Renfort de Défense** | Augmente la Défense ; tous ensemble comptent pour {engine.defenseBuffCeiling} au plus | Chaque coup que la créature reçoit |
| **Malus de Défense** | Diminue la Défense | Chaque coup que la créature reçoit |
| **Renfort d’initiative** | Augmente l’initiative | Quand la frise est construite |
| **Malus d’initiative** | Diminue l’initiative | Quand la frise est construite |
| **Renfort de dégâts** | S’ajoute au bonus de dégâts de la créature | Chaque coup que la créature porte |

Trois sommes, chacune ramenée à zéro **après** la soustraction si elle tombe en dessous :

- **Défense** = Défense de base + renforts de Défense, {engine.defenseBuffCeiling} au plus, − malus de Défense.
- **Initiative** = initiative de base + renforts d’initiative − malus d’initiative.
- **Bonus de dégâts** = ce que donnent ses passifs + ses renforts de dégâts.

Un état **permanent** ne décompte jamais : le
malus{?spell:infectious_blast:v1.defenseDebuff.permanent} de Défense de Contagion, et le durable des
deux{?spell:revenant_guards:v1.defenseBuff.count = 2} renforts de
Défense{?spell:revenant_guards:v1.defenseBuff1.permanent} de Wraithguard.
**L’immunité au Stun** n’est pas un état : aucun sort ne la donne, et elle dure du Nettoyage qui termine un Stun
jusqu’au suivant. Une créature vaincue perd ses états et n’en prend aucun nouveau.

sources: ADR 0019, ADR 0020, ADR 0035, ADR 0036, ADR 0041, ADR 0072, ADR 0076, ADR 0101, game-rules#implemented

```card
id: conditions
title: États
Un état, effet durable, a une valeur et un nombre de rounds.
Il décompte en fin de round, jusqu’à zéro.
Une créature étourdie passe le Stun, puis ne peut pas l’être pendant un round.
> Le Bleed de Latch inflige {spell:latch:v1.bleed} par round pendant {spell:latch:v1.bleed.durationRounds|round|rounds}, malgré toute Défense.
```

## La fin d’une partie {#the-end-of-a-match}

Une partie se termine de l’une de ces façons, chacune vérifiée à son propre moment :

1. **Une équipe est vaincue**, aussitôt, sur le sort ou le Bleed qui vainc sa dernière créature. Rien de ce qui
   suit ne se résout : plus aucun tour, pas de Nettoyage. L’autre équipe gagne.
2. **Les deux équipes à la fois**, c’est une partie nulle. Cela peut arriver : une seule passe de Bleed peut vider
   les deux équipes, et la ligne du lanceur d’un sort peut vaincre son lanceur au moment où il vainc le dernier
   ennemi.
3. **Le dernier round**, le round {rules.roundCap}, se termine avec les deux équipes debout : l’équipe à qui il
   reste le plus de Santé gagne, et à totaux égaux la partie est nulle.
4. **Un joueur abandonne** : l’autre gagne.
5. **Un joueur laisse s’écouler son temps pour répondre** : il perd. Quand les deux le font, la partie est nulle.

*Exemple.* Round 11. Il ne reste au second joueur que la Créature 4, avec une Santé de
{spell:heavy_strike:v1.damage} et sans Défense. Le premier tour est celui de la Créature 2, qui lance Strike{?spell:heavy_strike:v1.origin = 'Enemy'} sur elle : la Créature 4 est
vaincue, et **le premier joueur gagne tout de suite**. Aucun autre tour ne vient, et rien ne décompte.

sources: ADR 0011, ADR 0083, ADR 0087, ADR 0105, ADR 0169, game-rules#match-lifecycle

## Les passifs {#passives}

Un **passif** est ce que donne une clé de voûte, de la révélation qui l’acquiert jusqu’à la fin de la partie. Il ne
décompte jamais. Ceux de Titan et d’Apex vont à la créature qui les acquiert ; celui d’Archmage va à chaque créature
de son équipe. Une créature détient chaque passif que donnent ses propres clés de voûte, et celui d’Archmage une fois
de plus pour chaque exemplaire que possèdent ses coéquipières.

- **Titan : chaque coup ignore {tier:titan:v1.sunder} de Défense.** Sa [brèche](#sunder) : chaque coup rencontre
  la Défense de la cible diminuée de {tier:titan:v1.sunder}, jamais en dessous de zéro.
- **Archmage, partagé : Énergie +{tier:archmage:v1.upkeepEnergy} au début de chaque round.** Le gain d’Énergie de
  chaque créature vivante de l’équipe augmente de {tier:archmage:v1.upkeepEnergy}, une fois pour chaque exemplaire
  que possède l’équipe, à partir du round suivant. Un exemplaire continue de donner après que la créature qui l’a acquis
  est vaincue.
- **Apex : Dégâts +{tier:apex:v1.damageBonus} à chaque coup.** Il ajoute {tier:apex:v1.damageBonus} à chaque coup,
  avant le critique et avant la Défense : voir [le bonus de dégâts](#damage-bonus).

Ni le passif de Titan ni celui d’Apex ne touchent un Bleed, un soin, l’Énergie ou la ligne du lanceur.

Un **coup**, ce sont les dégâts qu’un sort inflige à une cible. Un Bleed n’en est pas un, la ligne du lanceur non
plus.

sources: ADR 0101, ADR 0106, ADR 0114, game-rules#implemented

## Le classement {#the-ladder}

Le classement ordonne les joueurs qui ont joué une partie classée, chacun selon sa cote.

Après une partie classée, le classement fait bouger les cotes des deux joueurs selon le degré de surprise du
résultat : battre un joueur mieux coté fait bouger les cotes davantage que battre un joueur moins bien coté. Une
partie nulle compte pour une demi-victoire. Moins le classement est sûr d’une cote, plus une partie la fait bouger.

Une partie classée est une partie en 3v3 entre deux places tenues par deux comptes différents. Une partie contre un
bot, sur un appareil partagé ou ouverte hors classement ne compte jamais. Une partie que la table annule ne compte
jamais non plus, même une fois terminée.

À côté de chaque cote, le ± dit à quel point le classement en est sûr. Tant que son ± est élevé, une cote est
**provisoire** : la page du classement la place après les cotes établies. Un nouveau joueur
commence provisoire. Un joueur qui cesse de jouer garde sa cote, mais son ± grandit chaque jour sans partie : sa
cote redevient peu à peu provisoire.

sources: ADR 0124, ADR 0155, ADR 0171, ADR 0176
