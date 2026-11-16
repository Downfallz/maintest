# Ce que vous cherchez à faire {#the-goal}

Deux joueurs. Chacun commande une équipe de {rules.teamSize} créatures, et joue pour vaincre l’autre équipe.

## Gagner {#winning}

Une créature commence avec une Santé de {creature.health}. À zéro point de Santé, elle est **vaincue**, pour le
reste de la partie. Vous gagnez quand chaque créature de l’autre équipe est vaincue. La partie se termine à l’instant
où cela arrive, même au milieu d’un round : rien de ce qui suit ne se résout. Si les deux équipes tombent en même
temps, la partie est nulle.

Si le round {rules.roundCap} se termine avec les deux équipes encore debout, l’équipe à qui il reste le plus de
Santé, additionnée sur ses créatures, gagne. À totaux égaux, la partie est nulle.

Un joueur peut aussi abandonner à tout moment : l’autre joueur gagne. Une partie entre deux joueurs donne à chacun
un temps pour répondre. Un joueur qui le laisse s’écouler perd ; quand les deux le font, la partie est nulle.

sources: ADR 0011, ADR 0083, ADR 0087, ADR 0105, ADR 0169, game-rules#match-lifecycle

```card
id: health
title: Santé
À zéro point de Santé, une créature est vaincue, pour le reste de la partie.
Quand une équipe n’a plus de créature, la partie se termine aussitôt, même au milieu d’un round.
Un soin ne porte jamais une créature au-dessus de la Santé avec laquelle elle a commencé la partie.
> Une créature commence avec une Santé de {creature.health}. Un coup de 7 la laisse à {max(0, creature.health - max(0, 7 - creature.defense))}.
```

## Un round {#a-round}

Une partie est une suite de rounds. Chaque round se lit **Entretien → Commandement → Combat → Retombées**, et
l’application vous y guide :

- **Entretien.** L’Énergie de chaque créature vivante augmente de {rules.energyPerRound}. La Regeneration
  (régénération) soigne. Les dégâts dans la durée blessent.
- **Commandement.** Aux rounds qui le proposent, vous acquérez d’abord des mutations pour vos créatures. Puis vous
  choisissez un tempo pour chacune d’elles, Instinct ou Précision, et, là où vos créatures sont à égalité sur la
  frise, leur ordre.
- **Combat.** La frise est parcourue une fois, place par place. Quand vient la place d’une de vos créatures, vous
  choisissez son sort et ses cibles sur le plateau tel qu’il est. Le sort se résout aussitôt, avant que la
  créature suivante agisse.
- **Retombées.** Les états décomptent, et le round suivant commence.

sources: ADR 0010, ADR 0083, ADR 0112, game-rules#round-sequence-adr-0010

## Ce qui fait le jeu {#what-makes-the-game}

- **Vous vous engagez avant de voir.** Vos mutations, vos tempos et vos ordres d’égalité se choisissent en secret,
  en même temps que ceux de votre adversaire. Vos sorts et leurs cibles viennent plus tard, à la place de chaque
  créature, quand vous savez tout ce qui s’est déjà passé dans ce round.
- **Vous grandissez en mutant.** Une mutation apprend à une créature un ensemble de sorts, tous à la fois, et
  augmente son initiative du bonus de la mutation pour le reste de la partie. La dernière mutation d’une famille,
  sa clé de voûte, n’apprend aucun sort : elle donne un passif à la place.
- **Rien n’est réservé.** L’Énergie d’un sort est dépensée quand il se résout. Une créature peut être vaincue,
  étourdie ou drainée avant que sa place vienne, et alors elle ne lance rien ce round-là.

sources: ADR 0056, ADR 0063, ADR 0089, ADR 0101, ADR 0112
