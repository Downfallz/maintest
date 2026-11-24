import { escape, layout, routes } from './layout.mjs';

const address = 'contact@downfallinstinct.com';
// What infra/main.bicep configures (the log workspace's retention, the blobs' soft delete); the test holds them equal.
const retention = { logDays: 30, deletedBlobDays: 7 };
const policies = {
  en: {
    label: 'Player information', date: 'Updated November 23, 2026',
    intro: 'You can read the showcase and explore mutations without an account. When you sign in to play, the game needs a small account record to recognise you, keep your seats and run your matches.',
    index: 'On this page', contact: 'Privacy contact', responsibility: 'The person responsible for privacy at Downfall Instinct handles questions, access requests, corrections and complaints at this address.',
    sections: [
      ['showcase', 'When you visit the showcase', [
        'The pages, artwork and fonts on downfallinstinct.com are served as static files. The mutation guide searches in your browser. The showcase sets no application cookies, creates no player profile and sends no analytics events.',
        'The game and the showcase carry no advertising or advertising trackers. We do not sell personal information or email addresses, send newsletters or marketing emails, or track opens and clicks in email. There are no in-game purchases or payment details to collect.',
      ]],
      ['account', 'Your account and browser', [
        'On play.downfallinstinct.com, you sign in with Google or, where offered, with Discord or a personal Microsoft account. Google Identity Services provides the Google button and verifies your Google account; Discord verifies your Discord account and shares only your Discord user ID with the game; Microsoft verifies your Microsoft account and shares only an identifier it makes for this game. The game checks the sign-in and stores the provider’s account identifier, a separate internal player ID and the player name you choose. It does not store your password, email address, real name or profile photo from either provider.',
        'The game issues its own signed, HttpOnly session cookie (downfall_session). It expires after 30 days and can be renewed when you use the game. Signing out removes the session from this browser. Signing out everywhere invalidates every copy of it, on all your devices.',
        'Your browser may keep seat links, display preferences and cached game files so you can return to a match or use the installed web app. You can clear those items in your browser’s site-data settings. They are separate from the account stored by the game.',
      ]],
      ['matches', 'What playing records and who can see it', [
        'The game keeps your seats, invitations, match actions, results and the last few preset reactions under your player ID. These records let participants replay their finished games, let you see your personal statistics and help the developer improve the bots.',
        'Other players can see your player name when they play or interact with you. The public ranked leaderboard shows player names, ratings, results and last-played dates. Your detailed personal statistics and your individual lobby are for your account.',
        'Friendships and requests are kept so the two players can respond or remove the connection. The game saves your settings, your last sign-in and a recent activity time updated at most once an hour. The operator can see account activity and game counts, administer accounts and investigate problems. If an account is suspended, the reason and time of suspension are recorded.',
        'If you send a bug report or an idea with Report or suggest, the game keeps your words, the page you sent it from, your browser’s name and, when sent from a game, the game and its round. A bug report also keeps the page’s last errors and, if you attach it, the position your seat could see. Only the operator reads what you send. To fix a bug, the operator may describe it in a public issue on GitHub, in their own words, with the position stripped of anything that names you, your account or the game. It is deleted 180 days after it is closed. The operator can stop you from sending more for 30 days. If you are signed in, that pause is kept with your account, and dropped the first time you use the site after it ends.',
      ]],
      ['notifications', 'Optional notifications', [
        'If you enable notifications on a device, the game stores that browser’s push endpoint and encryption keys. Your browser’s push service delivers events such as a turn or an invitation. This is optional and uses no email address. Turning on pop-ups in a game’s settings while signed in registers that browser with your account too. The account also keeps which kinds pop up (your turn, invitations and friends, results). Turn off pop-ups in the game to remove that subscription, sign out on that browser to remove it from your account, or use Sign out everywhere to remove the account’s registered devices. When a push service says a subscription no longer exists, the game keeps its address without the keys (the five latest), so that browser is asked to subscribe again; Sign out everywhere and deleting the account remove them too. A notice that could not be delivered is kept for a week at most, with your internal ID and its kind only.',
        'Whether or not you enable them, the game keeps the latest 100 notices from the last 90 days for your account’s bell, such as a friend who accepted your request, an invited friend who took the seat, a player who left or a match result. Each notice keeps the other player’s name as it was when it was sent, even if they rename or delete their account later. A notice is erased within the hour once it is 90 days old, or when you delete your account.',
      ]],
      ['providers', 'Service providers and retention', [
        'Cloudflare handles traffic for the public domains. Microsoft Azure hosts the game and stores account and game records. Google, Discord and Microsoft handle sign-in, and your browser’s push provider handles notifications if you enable them. In delivering or protecting a request, a provider can process technical information such as an IP address, the time, the requested page and browser information. Providers may process information outside Québec.',
        `Account and activity records stay until the account is deleted. Settings can be changed, friendships removed and notifications disabled earlier. The configured retention for the game host’s console logs is ${retention.logDays} days. Deleted blobs can remain recoverable in Azure storage for ${retention.deletedBlobDays} days. Finished match records are kept for replays, results and bot improvement.`,
      ]],
      ['choices', 'Access, corrections and deletion', [
        'To delete your account, open Settings → Account on the game site and confirm with your player name. Your name is released, linked devices, notices, friends, settings and account activity are removed, and seats in unfinished games become available to other players. Your public leaderboard entry disappears. Finished game records keep their internal player ID.',
        'You can ask what information we hold about you, request a copy or correction, ask about deletion or withdrawing consent, or raise a privacy concern. Email the privacy contact below with your player name and the request. We may need to verify account ownership before disclosing or changing a record. Please do not send passwords or seat links.',
        'If you email us, we receive your email address and the contents you send so we can answer and follow up on that request. Changes to this policy will be posted here with a new update date.',
      ]],
    ],
  },
  fr: {
    label: 'Renseignements des joueurs', date: 'Mise à jour le 23 novembre 2026',
    intro: 'Tu peux lire la vitrine et explorer les mutations sans compte. Quand tu te connectes pour jouer, le jeu a besoin de quelques renseignements pour te reconnaître, te redonner tes places et gérer tes parties.',
    index: 'Dans cette politique', contact: 'Nous joindre', responsibility: 'La personne responsable de la protection des renseignements personnels chez Downfall Instinct répond aux questions, aux demandes d’accès ou de rectification et aux plaintes à cette adresse.',
    sections: [
      ['showcase', 'Quand tu visites la vitrine', [
        'Les pages, illustrations et polices de downfallinstinct.com sont servies sous forme de fichiers statiques. La recherche dans le guide des mutations s’effectue dans ton navigateur. La vitrine ne dépose aucun témoin applicatif, ne crée aucun profil de joueur et n’envoie aucun événement de mesure d’audience.',
        'Le jeu et la vitrine sont sans publicité ni traceur publicitaire. Nous ne vendons ni adresses courriel ni renseignements personnels. Nous n’envoyons aucune infolettre ni aucun courriel promotionnel et ne suivons ni les ouvertures ni les clics dans les courriels. Il n’y a aucun achat dans le jeu et aucune donnée de paiement à recueillir.',
      ]],
      ['account', 'Ton compte et ton navigateur', [
        'Sur play.downfallinstinct.com, tu te connectes avec Google ou, quand c’est offert, avec Discord ou un compte Microsoft personnel. Le bouton de Google Identity Services sert à vérifier ton compte Google; Discord vérifie ton compte Discord et ne transmet au jeu que ton identifiant d’utilisateur Discord; Microsoft vérifie ton compte Microsoft et ne transmet au jeu qu’un identifiant qu’il crée pour ce jeu. Le jeu vérifie la connexion et conserve l’identifiant transmis par le fournisseur, un identifiant de joueur interne distinct et le nom de joueur que tu choisis. Il ne conserve, d’aucun des deux fournisseurs, ni ton mot de passe, ni ton adresse courriel, ni ton vrai nom, ni ta photo de profil.',
        'Le jeu dépose son propre témoin de session signé, inaccessible au JavaScript (downfall_session). Il expire après 30 jours et peut être renouvelé quand tu utilises le jeu. Te déconnecter retire la session de ce navigateur. L’option « Sign out everywhere » invalide toutes ses copies, sur tous tes appareils.',
        'Ton navigateur peut conserver des liens vers tes places dans les parties, des préférences d’affichage et des fichiers du jeu en cache pour que tu puisses reprendre une partie ou utiliser l’application Web installée. Tu peux les effacer dans les réglages des données de site de ton navigateur. Le compte est conservé séparément par le jeu.',
      ]],
      ['matches', 'Les parties et les renseignements visibles', [
        'Le jeu associe à ton identifiant de joueur tes places, tes invitations, tes actions en partie, tes résultats et les dernières réactions prédéfinies. Ces enregistrements permettent aux participants de revoir leurs parties terminées, de te présenter tes statistiques personnelles et d’aider le développeur à améliorer les adversaires automatisés.',
        'Les autres joueurs peuvent voir ton nom de joueur quand ils jouent ou interagissent avec toi. Le classement public affiche les noms de joueur, les cotes, les résultats et la date de la dernière partie jouée. Tes statistiques personnelles détaillées et ton salon individuel sont réservés à ton compte.',
        'Les liens d’amitié et les demandes sont conservés pour permettre aux deux joueurs de répondre ou de retirer le lien. Le jeu conserve tes préférences, ta dernière connexion et une heure d’activité récente mise à jour au plus une fois par heure. La personne qui exploite le jeu peut voir l’activité des comptes et le nombre de parties, administrer les comptes et examiner les problèmes. En cas de suspension, le motif et le moment de la suspension sont consignés.',
        'Si tu signales un problème ou proposes une idée avec le bouton « Report or suggest », le jeu conserve tes mots, la page d’où tu l’envoies, le nom de ton navigateur et, s’il est envoyé d’une partie, la partie et son round. Un signalement garde aussi les dernières erreurs de la page et, si tu la joins, la position que ta place voyait. Seule la personne qui exploite le jeu lit ce que tu envoies. Pour corriger un problème, elle peut le décrire dans un ticket public sur GitHub, avec ses propres mots, et la position débarrassée de tout ce qui te nomme, nomme ton compte ou la partie. Tout est supprimé 180 jours après la fermeture du message. Cette personne peut aussi t’empêcher d’en envoyer d’autres pendant 30 jours. Si tu es connecté, cette pause est conservée avec ton compte, puis effacée la première fois que tu utilises le site après son terme.',
      ]],
      ['notifications', 'Les notifications facultatives', [
        'Si tu actives les notifications sur un appareil, le jeu conserve l’adresse de livraison du navigateur et ses clés de chiffrement. Le service de notification du navigateur peut alors t’aviser lors de ton tour ou d’une invitation. Cette option est facultative et ne requiert aucune adresse courriel. Activer les notifications dans les réglages d’une partie, une fois connecté, inscrit aussi ce navigateur à ton compte. Le compte conserve aussi les types de notifications que tu acceptes (ton tour, les invitations et les amis, les résultats). Désactive les notifications dans le jeu pour retirer cet abonnement, déconnecte-toi sur ce navigateur pour le retirer de ton compte, ou utilise « Sign out everywhere » pour retirer les appareils inscrits à ton compte. Quand un service de notification indique qu’un abonnement n’existe plus, le jeu garde son adresse sans les clés (les cinq plus récentes), pour que ce navigateur soit invité à s’abonner de nouveau ; « Sign out everywhere » et la suppression du compte les retirent aussi. Une notification qui n’a pas pu être livrée est gardée une semaine au plus, avec seulement ton identifiant interne et son type.',
        'Que tu les actives ou non, le jeu conserve pour la cloche de ton compte les 100 derniers avis des 90 derniers jours, par exemple un ami qui a accepté ta demande, un ami invité qui a pris la place, un joueur qui a quitté la table ou le résultat d’une partie. Chaque avis garde le nom de l’autre joueur tel qu’il était à l’envoi, même s’il change de nom ou supprime son compte par la suite. Un avis est effacé dans l’heure qui suit ses 90 jours, ou à la suppression de ton compte.',
      ]],
      ['providers', 'Les fournisseurs et la conservation', [
        'Cloudflare achemine le trafic des domaines publics. Microsoft Azure héberge le jeu et conserve les comptes et les parties. Google, Discord et Microsoft assurent la connexion. Le service de notification de ton navigateur intervient si tu actives cette option. Pour acheminer ou protéger une requête, un fournisseur peut traiter des renseignements techniques comme l’adresse IP, l’heure, la page demandée et des informations sur le navigateur. Les fournisseurs peuvent traiter des renseignements à l’extérieur du Québec.',
        `Les renseignements du compte et de son activité sont conservés jusqu’à sa suppression. Tu peux d’ici là modifier tes préférences, retirer un lien d’amitié ou désactiver les notifications. Les journaux de console de l’hébergement du jeu sont conservés ${retention.logDays} jours. Les objets supprimés du stockage Azure peuvent demeurer récupérables pendant ${retention.deletedBlobDays} jours. Les parties terminées sont conservées pour les reprises, les résultats et l’amélioration des adversaires automatisés.`,
      ]],
      ['choices', 'Accéder, corriger ou supprimer', [
        'Pour supprimer ton compte, ouvre Settings → Account dans le jeu et confirme avec ton nom de joueur. Ton nom redevient disponible. Les appareils associés, les avis, les liens d’amitié, les préférences et l’activité de ton compte sont retirés. Tes places dans les parties en cours sont offertes à d’autres joueurs et ton entrée disparaît du classement public. Les parties terminées conservent leur identifiant de joueur interne.',
        'Tu peux demander quels renseignements nous détenons à ton sujet, en obtenir une copie, demander une rectification, te renseigner sur la suppression ou le retrait de ton consentement, ou soulever une préoccupation. Écris à l’adresse ci-dessous avec ton nom de joueur et ta demande. Nous pouvons avoir à vérifier que le compte t’appartient avant de communiquer ou de modifier un renseignement. Évite d’envoyer un mot de passe ou un lien donnant accès à une place en partie.',
        'Si tu nous écris, nous recevons ton adresse courriel et le contenu de ton message pour te répondre et assurer le suivi de ta demande. Toute modification à cette politique sera affichée ici avec une nouvelle date de mise à jour.',
      ]],
    ],
  },
};

const paragraph = text => `<p>${escape(text)}</p>`;
const section = ([id, title, paragraphs]) => `<section id="${id}"><h2>${title}</h2>${paragraphs.map(paragraph).join('')}</section>`;
const indexLink = ([id, title]) => `<a href="#${id}">${title}</a>`;

export function privacyPage(c, app) {
  const p = policies[c.lang];
  return layout(c, 'privacy', app, c.privacy, p.intro, `<div class="policy contain">
    <header><p class="kicker">${p.label}</p><h1>${c.privacy}</h1><p class="policy-date"><time datetime="2026-11-23">${p.date}</time></p><p class="lead">${escape(p.intro)}</p></header>
    <div class="policy-grid"><nav aria-label="${p.index}"><h2>${p.index}</h2>
      ${[...p.sections, ['contact', p.contact]].map(indexLink).join('')}</nav>
      <div class="policy-body">${p.sections.map(section).join('')}
        <section id="contact"><h2>${p.contact}</h2><p>${p.responsibility}</p><a class="editorial-link" href="mailto:${address}">${address}</a></section>
        <a class="editorial-link" href="${routes[c.lang].home}">← Downfall Instinct</a>
      </div></div></div>`);
}
