import { escape, layout, routes } from './layout.mjs';

const address = 'contact@downfallinstinct.com';
const policies = {
  en: {
    label: 'Player information', date: 'Updated November 14, 2026',
    intro: 'You can read the showcase and explore mutations without an account. When you sign in to play, the game needs a small account record to recognise you, keep your seats and run your matches.',
    index: 'On this page', contact: 'Privacy contact', responsibility: 'The person responsible for privacy at Downfall Instinct handles questions, access requests, corrections and complaints at this address.',
    sections: [
      ['showcase', 'When you visit the showcase', [
        'The pages, artwork and fonts on downfallinstinct.com are served as static files. The mutation guide searches in your browser. The showcase sets no application cookies, creates no player profile and sends no analytics events.',
        'The game and the showcase carry no advertising or advertising trackers. We do not sell personal information or email addresses, send newsletters or marketing emails, or track opens and clicks in email. There are no in-game purchases or payment details to collect.',
      ]],
      ['account', 'Your account and browser', [
        'On play.downfallinstinct.com, Google Identity Services provides the sign-in button and verifies your Google account. The game checks the sign-in credential and stores Google’s account identifier, a separate internal player ID and the player name you choose. It does not store your Google password, email address, real name or profile photo.',
        'The game issues its own signed, HttpOnly session cookie (downfall_session). It expires after 30 days and can be renewed when you use the game. Signing out closes that session. Signing out everywhere invalidates the sessions on all your devices.',
        'Your browser may keep seat links, display preferences and cached game files so you can return to a match or use the installed web app. You can clear those items in your browser’s site-data settings. They are separate from the account stored by the game.',
      ]],
      ['matches', 'What playing records and who can see it', [
        'The game keeps your seats, invitations, match actions, results and the last few preset reactions under your player ID. These records let participants replay their finished games, let you see your personal statistics and help the developer improve the bots.',
        'Other players can see your player name when they play or interact with you. The public ranked leaderboard shows player names, ratings, results and last-played dates. Your detailed personal statistics and your individual lobby are for your account.',
        'Friendships and requests are kept so the two players can respond or remove the connection. The game saves your settings, your last sign-in and a recent activity time updated at most once an hour. The operator can see account activity and game counts, administer accounts and investigate problems. If an account is suspended, the reason and time of suspension are recorded.',
      ]],
      ['notifications', 'Optional notifications', [
        'If you enable notifications on a device, the game stores that browser’s push endpoint and encryption keys. Your browser’s push service delivers events such as a turn or an invitation. This is optional and uses no email address. Turn off notifications in the game to remove that subscription, or use Sign out everywhere to remove the account’s registered devices.',
      ]],
      ['providers', 'Service providers and retention', [
        'Cloudflare handles traffic for the public domains. Microsoft Azure hosts the game and stores account and game records. Google handles sign-in, and your browser’s push provider handles notifications if you enable them. In delivering or protecting a request, a provider can process technical information such as an IP address, the time, the requested page and browser information. Providers may process information outside Québec.',
        'Account and activity records stay until the account is deleted. Settings can be changed, friendships removed and notifications disabled earlier. The configured retention for the game host’s console logs is 30 days. Deleted blobs can remain recoverable in Azure storage for seven days. Finished match records are kept for replays, results and bot improvement.',
      ]],
      ['choices', 'Access, corrections and deletion', [
        'To delete your account, open Settings → Account on the game site and confirm with your player name. Your name is released, linked devices, friends, settings and account activity are removed, and seats in unfinished games become available to other players. Your public leaderboard entry disappears. Finished game records keep their internal player ID.',
        'You can ask what information we hold about you, request a copy or correction, ask about deletion or withdrawing consent, or raise a privacy concern. Email the privacy contact below with your player name and the request. We may need to verify account ownership before disclosing or changing a record. Please do not send passwords or seat links.',
        'If you email us, we receive your email address and the contents you send so we can answer and follow up on that request. Changes to this policy will be posted here with a new update date.',
      ]],
    ],
  },
  fr: {
    label: 'Renseignements des joueurs', date: 'Mise à jour le 14 novembre 2026',
    intro: 'Tu peux lire la vitrine et explorer les mutations sans compte. Quand tu te connectes pour jouer, le jeu a besoin de quelques renseignements pour te reconnaître, te redonner tes places et gérer tes parties.',
    index: 'Dans cette politique', contact: 'Nous joindre', responsibility: 'La personne responsable de la protection des renseignements personnels chez Downfall Instinct répond aux questions, aux demandes d’accès ou de rectification et aux plaintes à cette adresse.',
    sections: [
      ['showcase', 'Quand tu visites la vitrine', [
        'Les pages, illustrations et polices de downfallinstinct.com sont servies sous forme de fichiers statiques. La recherche dans le guide des mutations s’effectue dans ton navigateur. La vitrine ne dépose aucun témoin applicatif, ne crée aucun profil de joueur et n’envoie aucun événement de mesure d’audience.',
        'Le jeu et la vitrine sont sans publicité ni traceur publicitaire. Nous ne vendons ni adresses courriel ni renseignements personnels. Nous n’envoyons aucune infolettre ni aucun courriel promotionnel et ne suivons ni les ouvertures ni les clics dans les courriels. Il n’y a aucun achat dans le jeu et aucune donnée de paiement à recueillir.',
      ]],
      ['account', 'Ton compte et ton navigateur', [
        'Sur play.downfallinstinct.com, le bouton de connexion de Google Identity Services sert à vérifier ton compte Google. Le jeu vérifie le justificatif de connexion et conserve l’identifiant transmis par Google, un identifiant de joueur interne distinct et le nom de joueur que tu choisis. Il ne conserve ni ton mot de passe Google, ni ton adresse courriel, ni ton vrai nom, ni ta photo de profil.',
        'Le jeu dépose son propre témoin de session signé, inaccessible au JavaScript (downfall_session). Il expire après 30 jours et peut être renouvelé quand tu utilises le jeu. Te déconnecter ferme cette session. L’option « Sign out everywhere » invalide les sessions sur tous tes appareils.',
        'Ton navigateur peut conserver des liens vers tes places dans les parties, des préférences d’affichage et des fichiers du jeu en cache pour que tu puisses reprendre une partie ou utiliser l’application Web installée. Tu peux les effacer dans les réglages des données de site de ton navigateur. Le compte est conservé séparément par le jeu.',
      ]],
      ['matches', 'Les parties et les renseignements visibles', [
        'Le jeu associe à ton identifiant de joueur tes places, tes invitations, tes actions en partie, tes résultats et les dernières réactions prédéfinies. Ces enregistrements permettent aux participants de revoir leurs parties terminées, de te présenter tes statistiques personnelles et d’aider le développeur à améliorer les adversaires automatisés.',
        'Les autres joueurs peuvent voir ton nom de joueur quand ils jouent ou interagissent avec toi. Le classement public affiche les noms de joueur, les cotes, les résultats et la date de la dernière partie classée. Tes statistiques personnelles détaillées et ton salon individuel sont réservés à ton compte.',
        'Les liens d’amitié et les demandes sont conservés pour permettre aux deux joueurs de répondre ou de retirer le lien. Le jeu conserve tes préférences, ta dernière connexion et une heure d’activité récente mise à jour au plus une fois par heure. La personne qui exploite le jeu peut voir l’activité des comptes et le nombre de parties, administrer les comptes et examiner les problèmes. En cas de suspension, le motif et le moment de la suspension sont consignés.',
      ]],
      ['notifications', 'Les notifications facultatives', [
        'Si tu actives les notifications sur un appareil, le jeu conserve l’adresse de livraison du navigateur et ses clés de chiffrement. Le service de notification du navigateur peut alors t’aviser lors de ton tour ou d’une invitation. Cette option est facultative et ne requiert aucune adresse courriel. Désactive les notifications dans le jeu pour retirer cet abonnement, ou utilise « Sign out everywhere » pour retirer les appareils inscrits à ton compte.',
      ]],
      ['providers', 'Les fournisseurs et la conservation', [
        'Cloudflare achemine le trafic des domaines publics. Microsoft Azure héberge le jeu et conserve les comptes et les parties. Google assure la connexion. Le service de notification de ton navigateur intervient si tu actives cette option. Pour acheminer ou protéger une requête, un fournisseur peut traiter des renseignements techniques comme l’adresse IP, l’heure, la page demandée et des informations sur le navigateur. Les fournisseurs peuvent traiter des renseignements à l’extérieur du Québec.',
        'Les renseignements du compte et de son activité sont conservés jusqu’à sa suppression. Tu peux d’ici là modifier tes préférences, retirer un lien d’amitié ou désactiver les notifications. Les journaux de console de l’hébergement du jeu sont conservés 30 jours. Les objets supprimés du stockage Azure peuvent demeurer récupérables pendant sept jours. Les parties terminées sont conservées pour les reprises, les résultats et l’amélioration des adversaires automatisés.',
      ]],
      ['choices', 'Accéder, corriger ou supprimer', [
        'Pour supprimer ton compte, ouvre Settings → Account dans le jeu et confirme avec ton nom de joueur. Ton nom redevient disponible. Les appareils associés, les liens d’amitié, les préférences et l’activité de ton compte sont retirés. Tes places dans les parties en cours sont offertes à d’autres joueurs et ton entrée disparaît du classement public. Les parties terminées conservent leur identifiant de joueur interne.',
        'Tu peux demander quels renseignements nous détenons à ton sujet, en obtenir une copie, demander une rectification, te renseigner sur la suppression ou le retrait de ton consentement, ou soulever une préoccupation. Écris à l’adresse ci-dessous avec ton nom de joueur et ta demande. Nous pouvons avoir à vérifier que le compte t’appartient avant de communiquer ou de modifier un renseignement. Évite d’envoyer un mot de passe ou un lien donnant accès à une place en partie.',
        'Si tu nous écris, nous recevons ton adresse courriel et le contenu de ton message pour te répondre et assurer le suivi de ta demande. Toute modification à cette politique sera affichée ici avec une nouvelle date de mise à jour.',
      ]],
    ],
  },
};

export function privacyPage(c, app) {
  const p = policies[c.lang];
  return layout(c, 'privacy', app, c.privacy, p.intro, `<div class="policy contain">
    <header><p class="kicker">${p.label}</p><h1>${c.privacy}</h1><p class="policy-date"><time datetime="2026-11-14">${p.date}</time></p><p class="lead">${escape(p.intro)}</p></header>
    <div class="policy-grid"><nav aria-label="${p.index}"><h2>${p.index}</h2>
      ${[...p.sections.map(([id, title]) => [id, title]), ['contact', p.contact]].map(([id, title]) => `<a href="#${id}">${title}</a>`).join('')}</nav>
      <div class="policy-body">${p.sections.map(([id, title, paragraphs]) => `<section id="${id}"><h2>${title}</h2>${paragraphs.map(s => `<p>${escape(s)}</p>`).join('')}</section>`).join('')}
        <section id="contact"><h2>${p.contact}</h2><p>${p.responsibility}</p><a class="editorial-link" href="mailto:${address}">${address}</a></section>
        <a class="editorial-link" href="${routes[c.lang].home}">← Downfall Instinct</a>
      </div></div></div>`);
}
