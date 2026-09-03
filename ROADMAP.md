# ROADMAP — où l'atelier en est

## v0 — le contrat, livré

Les sept couches sont nommées. Le cycle tient sans invoquer d'agent.
La porte mécanique refuse un brief infirme. Le verrou refuse deux
lots sur le même fichier. Le canal d'échange existe. `fusionner`
sort en erreur. Un quota manquant vaut `-1`. La **boîte aux lettres**
existe : un rôle dont la file est vide sort `RIEN` (code 0), et le
planificateur n'est pas sur le chemin du coder.

## Lot 001 — l'invocation, sous drapeau

`ATELIER_INVOQUER=1` existe et fait quelque chose. Un cron sait
lancer Claude ou Cursor, borné par un `timeout`, gardé par un `flock`
par rôle, et ranger sa carte : `avancer` ou `echouer`, jamais la
laisser en place. L'`argv` de chaque rôle est construit en Python
(`atelier invocation`), pas composé dans le shell — le prompt cite le
chemin du brief et refuse toute autre consigne.

Ce qui a changé de forme, et pourquoi
([docs/CRITIQUE-001.md](docs/CRITIQUE-001.md)) :

- les clés d'API sont retirées de l'environnement de l'agent : une
  variable oubliée fait payer l'unité au lieu de l'abonnement ;
- `planifier` et `coder` tirent le même Cursor Pro — le rôle
  facultatif garde une réserve pour le rôle critique ;
- une carte ne change plus de brief en avançant ;
- le `coder` pose le verrou avant d'écrire, `prochain` saute une carte
  déjà tenue, et `atelier lever` rend les fichiers après ta fusion ;
- la veille ne connaît plus le jeu : sa commande de fumée vient de
  l'`atelier.toml` du produit.

Le drapeau reste baissé par défaut. Lancer Cursor hors d'un contrat
durable a déjà coûté un lot.

## Lot 002 — les rôles viennent du branchement

Le lot 001 avait laissé trois endroits qui répondaient à « qui relit » :
l'`atelier.toml` du produit, une table `POSTES_DU_ROLE` dans l'atelier,
et un `case` dans `tour.sh`. Il en reste un : le produit.

- `atelier poste --projet … --role relire` dit le binaire, l'abonnement,
  le modèle et l'état de la garde de lecture seule. `tour.sh` le lit au
  lieu de tenir sa propre table.
- La validation des rôles cesse d'être plus stricte que la règle :
  `ecriture == controle` est permis (écrire un brief n'est pas écrire du
  code), `execution == controle` reste refusé.
- Un relecteur dont le binaire ne sait pas qu'on lui retire les outils
  qui écrivent est déclaré `non-tenue`, sur stderr, avant l'invocation.
- La veille sort en erreur quand le branchement est introuvable, au lieu
  de sortir 0 sans rien avoir mesuré.
- Le gabarit `profiles/forgehistory.toml` dit `controle = "claude"` :
  c'est ce qui tient avec trois abonnements. Codex tire le même quota
  ChatGPT que Hermes.

## Lot 003 — le tour boucle, et on peut basculer

Le sujet « workflow » est fermé : le tour se referme sans le
propriétaire, et une commande dit si on peut l'armer.

- Le **numéro de PR** fait le dernier saut. L'exécutant l'écrit dans
  `atelier-echange/pr.txt`, le cron le range dans la carte et efface le
  fichier. Le relecteur reçoit « la PR 44, sur la branche
  `agent/044-mineur` » — ou la branche seule, jamais un numéro inventé.
- Une **file bloquée se déclare** : `RIEN` sur stdout, et sur stderr qui
  tient quel fichier. Une file vide reste silencieuse.
- `atelier pret --projet …` répond avant la bascule : branchement,
  binaires des quatre rôles, `flock`, `timeout`, dossier des verrous,
  quota, boîtes, état du drapeau. `PASS` / `FAIL` / `?`, et `?` n'est
  pas un feu vert. Il ne lance aucun agent.

Ce qui reste au propriétaire n'a pas changé : il lit la PR, il fusionne,
et il rend les fichiers avec `atelier lever`.

## Lot 004 — la feuille de route se lit, le pilote reçoit sa décision

Le pilote lisait `ROADMAP.md` comme du texte libre et devait deviner
quel lot manquait de brief ; rien ne mettait la feuille à jour après une
fusion ; le brief du briefer partait vers un coder qui ne le trouvait
pas sur `master`.

- Le dépôt produit tient un **registre des lots** dans sa feuille de
  route (`[projet].feuille`) : une fiche par lot, six états écrits
  (`idee`, `a-briefer`, `pret`, `livre`, `abandonne`, `archive`),
  dépendances, PR. Tout ce qui est entre — en file, en relecture,
  bloqué, en échec — se **dérive** des cartes, des verrous et des briefs.
- `atelier feuille valider` refuse une fiche mal formée, un numéro
  dupliqué, un brief attendu absent ou orphelin, une dépendance fantôme
  ou circulaire, une carte d'un lot inconnu ; avec `--base`, une
  transition interdite, et pour une branche `agent/NNN-slug`, une fiche
  qui ne passe pas à `livre` avec le numéro de la PR. La CI du produit
  la joue sur chaque PR.
- `atelier piloter` **calcule** la décision du matin : rapprocher les
  cartes des lots fusionnés (et rendre leurs verrous), puis déposer au
  plus une carte par rôle, la première fiche admissible dans l'ordre de
  la feuille. Hermes reçoit cette décision dans son prompt ; il n'invente
  ni numéro, ni statut, ni chemin, et n'est appelé que s'il y a quelque
  chose à dire.
- **La fiche voyage dans la PR** : le briefer la passe à `pret`, le
  coder à `livre` (`atelier feuille marquer`). `master` ne dit livré qu'à
  l'instant de la fusion, jamais avant, sans correction après coup.
- Le briefer ouvre la PR de son brief ; sa carte attend la fusion dans
  `brief-a-fusionner`. `atelier reprendre` retire une carte de `echec/`
  pour que le pilote la redépose.
- Le lecteur de périmètre écarte les fichiers qu'un brief **interdit**
  (le 046 aurait tenu `sim/aggregation.py`, périmètre du 047).

## Ce que l'atelier ne sait pas encore faire

- **Mesurer ce qu'un agent affirme.** Entre la fin du tour du coder
  et la boîte `a-relire`, l'atelier ne demande qu'un code de sortie
  nul et un entier positif dans `atelier-echange/pr.txt`. Le lot 046
  de ForgeHistory est arrivé au relecteur avec trois régressions et
  un compte rendu qui les disait préexistantes ; la CI a démenti.
  `[projet].tests` est déclaré obligatoire et exécuté nulle part.
  C'est le brief 005.
- **Savoir où en est une PR.** L'atelier connaît le numéro de la PR
  d'un lot et ne lui demande jamais son état. Le 3 septembre au soir,
  `atelier piloter` proposait de redéposer le lot 046 alors que la PR
  206 était ouverte sur sa branche et attendait la fusion : Composer
  aurait recodé un lot déjà en relecture. Symétriquement, une PR fermée
  sans fusion laisse sa carte dans `faite` et son verrou posé. C'est le
  brief 006.
- **Reprendre un juge.** Après un JSON de revue illisible, changer
  de relecteur sans relancer le lot.
- **Lire un quota vivant.** L'atelier consomme `llmquota` s'il est
  là, mais ne sonde aucun fournisseur lui-même. Reste une référence,
  pas une dépendance.
- **Exiger `gh` pour valider une PR.** Le numéro vient du canal
  d'échange, au format strict. Si `gh` répond et que le remote est
  GitHub, on refuse un désaccord de branche ; si la sonde échoue, on
  se tait — pas d'authentification obligatoire dans le cron. Une PR
  fermée sans fusion se range toujours à la main (`atelier reprendre`,
  qui sort la carte de n'importe quelle boîte et lève son verrou) —
  c'est ce que le brief 006 doit rendre automatique.
- **Reconnaître une PR de lot qui ne suit pas `prefixe_branche`.** La
  CI ne vérifie la fiche que sur une branche `agent/NNN-slug` ; ailleurs,
  c'est l'œil du propriétaire au moment de fusionner.
- **Convoquer un conseil.** Le protocole FACT / INFERENCE / ASSUMPTION
  / UNKNOWN est décrit ; il n'est pas une commande.
- **Un second produit.** Seul ForgeHistory a un profil.
- **Retirer la main qui écrit à tous les relecteurs.** Seul `claude` a
  un drapeau connu ; pour `agent` et `codex`, l'atelier déclare qu'il ne
  sait pas, il ne fait pas semblant.

## Ordre

Un lot d'atelier à la fois. Les quatre premiers ont fermé le sujet du
workflow : le contrat (v0), l'invocation (001), la source unique des
rôles (002), la boucle et la bascule (003), la feuille de route lue par
une machine (004).

La suite n'est plus du code, c'est de l'exploitation : poser
`atelier.toml` (avec `feuille`) dans le produit, faire passer `atelier
pret` et `atelier feuille valider`, puis armer **un** cron — le `coder`
d'un brief que tu as relu. Le pilote dépose à partir de là.

L'exploitation a parlé le 3 septembre 2026. Le premier tour complet —
pilote, coder, relire — a tourné sans personne. Il a aussi livré au
relecteur du code que la CI a refusé, parce que l'atelier avance une
carte sur la parole de l'agent.

Le soir du même jour, la plomberie a été reprise sans brief — deux PR
d'exploitation — parce que les pannes ne se voyaient pas à la lecture :
il a fallu exécuter chaque scénario sur un produit jetable avec de faux
agents pour les trouver. Le tour **nominal** salissait le worktree du
rôle et faisait échouer le lot suivant ; `echec/` n'avait pas de porte
de sortie, si bien qu'un délai dépassé coûtait une commande tapée ; un
second échec du même lot laissait la carte dans sa file, que le réveil
suivant repayait. Depuis, `/etc/cron.d/forgeatelier` n'appelle plus
qu'un répartiteur et le profil actif vit dans un fichier que `hermes`
écrit : basculer entre la boucle de production et la boucle d'épreuve
ne demande plus root ([docs/BOUCLES.md](docs/BOUCLES.md)).

Il reste deux lots pour que la boucle tourne sans qu'on la rattrape, et
dans cet ordre. **La porte des tests**
(`briefs/005-la-carte-ne-passe-pas-sur-parole.md`) : le tour de `relire`
lit le verdict des contrôles obligatoires avant d'invoquer qui que ce
soit. Puis **l'état des PR**
(`briefs/006-la-boucle-ne-s-arrete-que-sur-une-decision.md`), qui en
dépend — les deux écrivent dans `atelier/echange.py`, et le verrou
refuserait le second si les deux partaient ensemble.

**Reprendre un juge** attend toujours ce qui le justifierait — une
revue illisible.
