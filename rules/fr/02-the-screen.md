# Ce que montre l’écran {#the-screen}

L’application est la table. Elle tient le compte de chaque nombre, tranche chaque chance et joue chaque étape que personne
ne décide. Vous prenez les décisions ; ce chapitre dit ce que vous voyez pendant que vous les prenez.

## Vos créatures et les leurs {#the-creatures}

Chaque équipe a {rules.teamSize} créatures. Celles du premier joueur sont numérotées de 1 à {rules.teamSize},
celles du second de {rules.teamSize + 1} à {rules.teamSize * 2}. Les numéros ne changent jamais, et l’historique
nomme chaque créature par son numéro : Créature 4.

Chaque créature montre, aux deux joueurs :

- sa **Santé**, et la Santé avec laquelle elle a commencé ;
- son **Énergie**, qui paie ses sorts ;
- sa **Défense**, qui réduit chaque coup qu’elle reçoit ;
- son **initiative**, qui la place sur la frise ;
- ses **états**, chacun avec les rounds qu’il lui reste ;
- ses **mutations**, et les sorts qu’elles lui ont appris : son grimoire.

Rien d’une créature n’est caché. Ce qui est caché, c’est un choix pas encore révélé : une mutation choisie ce round,
un tempo, un ordre d’égalité. Chacun est révélé aux deux joueurs à la fois.

Chaque créature commence la partie de la même façon : une Santé de {creature.health}, une Énergie de
{creature.energy}, une Défense de {creature.defense} et une initiative de {creature.initiative}, et elle connaît deux
sorts, Strike{?creature.knows = 'spell:heavy_strike:v1'} et Focus{?creature.knows = 'spell:wait:v1'}{?creature.knows.count = 2}. Tout ce qui les distinguera est une décision que vous n’avez pas encore prise.

sources: ADR 0011, ADR 0042, ADR 0089, game-rules#match-lifecycle

## Ce que l’application fait pour vous {#what-the-app-does}

Les étapes que personne ne décide sont jouées pour vous, et l’historique enregistre chacune d’elles :

- **L’Entretien.** Le gain d’Énergie de chaque créature, sa Regeneration et son Bleed (saignement).
- **La frise.** L’ordre des tours, construit à partir des tempos et des initiatives. Une égalité entre les équipes
  est tranchée au hasard, et la frise dit quelle équipe a obtenu la place la plus tôt.
- **Les critiques.** Chaque chance de critique, quand son sort se résout. Un lancer qui obtient un critique montre
  **Crit!** à côté de son lanceur ; un lancer qui n’en obtient pas n’y montre rien. L’historique dit la chance
  qu’avait le lancer.
- **Le décompte.** Chaque état décompte pendant les Retombées, et se termine à zéro.
- **La fin.** La partie se termine à l’instant où une équipe est vaincue, ou quand le dernier round se termine.

Quand l’application vous demande quelque chose, elle ne propose que ce que les règles permettent : les mutations
qu’une créature peut acquérir, les sorts qu’elle peut payer maintenant, les cibles qu’un sort peut prendre. Un
choix qu’elle ne propose pas n’est pas un choix légal.

La fenêtre **Help** parcourt le round où vous êtes. L’historique explique chaque résultat après sa résolution.

sources: ADR 0010, ADR 0063, ADR 0083, ADR 0112, ADR 0154, ADR 0208, game-rules#round-sequence-adr-0010

## Les nombres de cette table {#the-numbers}

La table joue un seul jeu de règles. Ses nombres ne sont pas des règles : ils bougent quand le jeu est équilibré,
et le jeu se joue de la même façon quand ils bougent. Ce livre tire chaque nombre du jeu de règles et du contenu,
et bouge donc avec eux.

| Valeur | Ce qu’elle décide | Cette table |
| --- | --- | --- |
| Taille d’équipe | Les créatures que chaque joueur commande | {rules.teamSize} |
| Énergie par round | L’Énergie que chaque créature vivante gagne à l’Entretien | {rules.energyPerRound} |
| Choix par round de mutation | Les mutations qu’une équipe peut acquérir à un round de mutation | {rules.evolutionPicksPerOpportunity} |
| Premier round de mutation | Le premier round qui propose des mutations | {rules.firstEvolutionRound} |
| Rounds entre deux rounds de mutation | L’écart d’un round de mutation au suivant | {rules.evolutionInterval} |
| Dernier round | Le round après lequel la partie se termine sur la Santé | {rules.roundCap} |
| Multiplicateur de critique | Ce par quoi un critique multiplie les dégâts et les soins | {rules.criticalMultiplier} |

Un sort indique sa chance de critique en pourcentage : Incinerate indique Crit
{(creature.criticalChance + spell:engulfing_flames:v1.criticalChance) * 100} %. Chaque chance de critique du jeu est un multiple de 5 %.

sources: ADR 0011, ADR 0056, ADR 0100, ADR 0105, ADR 0208, game-rules#implemented
