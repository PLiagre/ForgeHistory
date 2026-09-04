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

## Lot 005 — une carte ne passe pas sur parole

Le 3 septembre 2026, un agent a écrit « 164 passent, 3 échecs identiques
à `master` (préexistants) ». La CI disait `sim` rouge et trois
régressions. Le relecteur a été payé pour relire du code cassé. Le
4 septembre, le briefer a été bloqué sur une demande d'accord, est sorti
0 sans rien écrire, et la carte 049 est entrée dans `brief-a-fusionner`
alors qu'aucun brief ni aucune PR n'existait nulle part.

- **Le poste d'écriture peut écrire.** Un backend déclare son
  `accord_ecriture` : ce qu'il faut poser pour qu'un tour non interactif
  puisse aller jusqu'à la PR. Sans lui, `claude -p` refuse chaque outil
  qui mute et rend 0 — l'échec le plus coûteux, celui qui ressemble à un
  succès. Rien de plus large que nécessaire : les règles `deny` du
  produit continuent de fermer ce qu'elles ferment.
- **La carte du briefer ne bouge que si le brief existe** et qu'un
  numéro de PR a été déposé. `brief-a-fusionner` promet une fusion au
  propriétaire : la promesse est vérifiée avant d'être faite.
- `atelier ci --pr N` rend le verdict des contrôles **obligatoires** —
  la liste qui gouverne le bouton de fusion de GitHub. 0 vert, 1 rouge
  (il nomme les fautifs), 2 inconnu.
- **Le relecteur ne relit pas une PR rouge** : la carte tombe dans
  `echec` avec la cause `ci` et les contrôles fautifs dans sa note,
  aucun agent n'est lancé. Devant un verdict illisible, la carte
  **attend** dans `a-relire` et le tour sort 0 — une porte qui s'ouvre
  quand la sonde se tait cède exactement quand elle ne répond plus.
- La commande est injectable (`ATELIER_CI_CMD`) : aucun test de ce dépôt
  n'a besoin d'un compte GitHub.

## Lot 006 — la boucle ne s'arrête que sur une décision

Le 4 septembre 2026 à 7h00, le pilote a redéposé le lot 046 alors que la
PR 206 était ouverte sur sa branche. À 7h30, Composer l'a recodé en
entier : un tiers de la capacité de codage du jour, pour refaire ce qui
existait. La fiche disait `pret · PR : —`, parce que la ligne qui la
passe à `livre` vit dans la PR non fusionnée.

- `atelier pr-etat --pr N` rend `ouverte`, `fusionnee`, `fermee` ou
  `inconnue`, et l'inconnu rend 2 — jamais 0.
- **Le pilote ne dépose pas un lot dont une PR est ouverte**, et devant
  l'inconnu il retient : déposer est ce qui fait dépenser. Il nomme sur
  stderr ce qu'il a retenu et pourquoi. Un lot neuf n'a pas de branche :
  il ne coûte aucun appel.
- **Une PR fermée sans fusion libère son lot** : la carte quitte `faite`
  ou `a-relire` pour `echec` avec la cause `pr`, et son verrou tombe.
  Devant l'inconnu, rien ne bouge — ranger sur une sonde muette rangerait
  des cartes vivantes.
- `docs/LE-WORKFLOW.md` décrit la boucle de bout en bout, et un contrôle
  vérifie que chaque commande qu'il cite existe.
- **`atelier-boucle etat` lit l'heure dans le fuseau du profil.** Le cron
  de ce VPS suit UTC ; lue avant de sourcer le profil, l'heure était
  celle du shell, et `etat` annonçait un réveil deux heures faux. C'est
  pourtant lui qu'on interroge pour démentir le tableau de bord.

## La série 009-024 — le cycle automatique

Le 4 septembre 2026, le propriétaire a retiré la fusion de ses mains.
`VISION.md` a changé le même jour : la fusion devient mécanique, une
relecture terminée n'est plus une approbation, l'intégration est
séquentielle et retestée, et une huitième couche est ouverte.

Seize briefs portent cette évolution. Aucun n'est une PR globale : chacun
se livre et se juge seul, et la série est faite pour qu'ils se croisent.

### Les préalables — enlever ce qui sérialise

- **009 — Une commande vit dans son propre fichier.** `atelier/__main__.py`
  porte les vingt-cinq `add_parser` du programme. Chacun des lots suivants
  apporte une commande : tant que le point d'entrée est une table
  centrale, *aucun d'eux n'est parallélisable*. Pas par accident, par
  construction.
- **010 — Un module déclare sa couche chez lui.** Même défaut, autre
  fichier : `couches.MODULES` est une seconde source que six lots doivent
  éditer.

### Le verdict — ce qui remplace l'œil du propriétaire

- **011 — Le verdict est une donnée, pas une prose.** Format, validation,
  lien au SHA relu et à l'auteur du code. Quatre refus : absent, illisible,
  périmé, interdit.
- **012 — Le verdict devient un contrôle sur la révision relue.** Un état
  de commit, posé sur le SHA. « Périmé bloque » cesse d'être une garde
  qu'on écrit : c'est une propriété du support.
- **013 — Les contrôles requis se déclarent et se vérifient.** Aujourd'hui
  `master` exige `gitleaks`, `sim`, `viewer` ; `feuille` n'est pas requis
  et `enforce_admins` est à `false`. Les deux manques sont la porte
  dérobée de tout le reste.

### La relecture qui refuse

- **014 — Une relecture terminée n'est pas une approbation.** `PASS`
  avance, `FAIL` renvoie la carte à son auteur avec ses motifs, le reste
  attend. La boîte `faite` disparaît — elle ne se renomme pas, parce que
  les cartes qui y dorment sont exactement celles qui ont avancé sur
  parole.
- **015 — Le brief se relit comme le diff.** Un rôle de plus, la même
  mécanique, et un brief refusé qui retourne à son auteur.

### Le parallélisme

- **016 — La prise d'une carte et de ses fichiers est un seul geste.**
  `prochain` puis `verrouiller` laisse un intervalle ; avec plusieurs
  tours du même rôle, c'est le cas nominal, pas une course rare.
- **017 — Un lot actif a son worktree.** Un worktree par rôle ne peut pas
  être sur deux branches à la fois, et il n'y a pas de contre-mesure à ça.
- **018 — La fiche d'un lot n'est pas tout le fichier.** Le défaut le plus
  cher de la série et le moins visible : tant que la fiche est « le
  fichier de la feuille », *aucun lot n'est jamais disjoint d'aucun
  autre*.
- **019 — Le pilote dépose autant de lots que de périmètres disjoints.**

### L'intégration

- **020 — L'intégration est mécanique, séquentielle et retestée.** Elle
  occupe la huitième couche. Elle ne lit ni brief, ni diff, ni verdict :
  elle lit la liste des contrôles requis. Elle ferme aussi le trou écrit
  noir sur blanc dans `atelier/backends.py` — l'accord d'écriture du
  codeur porte `Bash(gh:*)`, qui contient `gh pr merge`.

### Ce qui reste au propriétaire, et qui le sert

- **021 — Une direction du propriétaire devient plusieurs lots.** Une
  phrase, et N fiches `a-briefer` arrivent par une PR relue et intégrée
  comme les autres.
- **022 — L'éclaireur lit et propose, il ne touche à rien.** Aucun accord
  d'écriture, aucune boîte, aucune carte. Un tour d'éclaireur ne laisse
  aucune trace dans le produit.

### La preuve, avant d'armer quoi que ce soit

- **023 — Un faux GitHub sur le banc.** Un état sur le disque, pas une
  logique. Il refuse ce que GitHub refuse, une mise à jour y change le
  SHA, et il journalise tout.
- **024 — Le banc joue les huit conditions.** Huit scénarios, chacun avec
  son rouge prouvé. Rien n'est armé sur le produit avant qu'il passe.

### L'ordre, et ce qui se croise

Les dépendances : 012 après 011 ; 013 après 012 ; 014 après 011 et 012 ;
015 après 014 ; 019 après 016, 017 et 018 ; 020 après 012 et 013 ; 021
après 015, 019 et 020 ; 024 après 023, et après tout le reste puisqu'il
le mesure. 009, 010, 011, 016, 017, 018, 022 et 023 n'attendent personne.

Le reste n'est pas un planning : c'est ce que `atelier piloter` calcule
en lisant les périmètres. Deux exemples, vérifiables aujourd'hui :

- **011 et 016 sont disjoints** — le verdict et la prise ne partagent
  aucun fichier. Ils s'écrivent ensemble.
- **014 et 015 sont en collision** — `atelier/boite.py` et
  `crons/tour.sh`. Ils ne s'écrivent jamais ensemble, et c'est le verrou
  qui le dit, pas une consigne.

Deux fichiers sérialisent cette série et il faut le savoir avant de
commencer : `crons/tour.sh`, que cinq lots touchent, et
`docs/LE-WORKFLOW.md`, que six lots corrigent. Le shell se découpe moins
bien que le Python et la série l'assume : ces lots-là passent l'un après
l'autre.

**Aucun de ces seize briefs n'écrit dans `ROADMAP.md`, sauf le dernier.**
C'est le prix de leur parallélisme, et c'est exactement le défaut que le
lot 018 corrige dans le dépôt produit — ici, l'atelier n'a pas de
registre de lots, donc la fiche n'existe pas et le fichier entier serait
tenu par chacun.

### Ce que cette série ne fait pas

Elle n'arme rien. Poser la protection de branche sur `master` et basculer
le profil sont deux gestes d'exploitation, une fois, après que 024 est
passé.

Elle ne donne pas de registre de lots à l'atelier lui-même : `ROADMAP.md`
d'ici reste de la prose, et les lots de l'atelier se suivent à la main.
Le jour où l'atelier tournera sur lui-même, ce sera un lot, et il
commencera par un `atelier.toml`.

## Ce que l'atelier ne sait pas encore faire

- **Exécuter les tests du produit.** `[projet].tests` est déclaré
  obligatoire dans `atelier.toml` et exécuté nulle part. Le lot 005 lit
  le verdict que la CI rend ; il ne le calcule pas sur la machine.
- **Dire la vérité sur son horaire dans le tableau de bord.**
  `~/bin/atelier-vue` n'est pas dans ce dépôt et lit encore
  `/etc/cron.d/forgeatelier` pour savoir s'il est armé et ce qui vient :
  le crontab n'y porte plus ni réveils ni `ATELIER_INVOQUER`, alors il
  annonce une boucle désarmée pendant qu'elle tourne. C'est un lot à
  lui, et il commence par verser l'outil dans le dépôt.
- **Reprendre un juge.** Après un JSON de revue illisible, changer
  de relecteur sans relancer le lot.
- **Lire un quota vivant.** L'atelier consomme `llmquota` s'il est
  là, mais ne sonde aucun fournisseur lui-même. Reste une référence,
  pas une dépendance.
- **Exiger `gh` pour valider une PR.** Le numéro vient du canal
  d'échange, au format strict. Si `gh` répond et que le remote est
  GitHub, on refuse un désaccord de branche ; si la sonde échoue, on
  se tait — pas d'authentification obligatoire dans le cron. Les sondes
  du 005 et du 006 suivent la même règle : sans `gh`, le verdict et
  l'état sont inconnus, et l'inconnu retient sans jamais bloquer le cron.
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

Un lot d'atelier à la fois — **jusqu'au 4 septembre 2026**. La série
009-024 lève cette règle pour elle-même : ses lots se croisent quand
leurs périmètres sont disjoints, et le verrou dit lesquels. Ce qui suit
raconte comment on en est arrivé là.

Les quatre premiers lots ont fermé le sujet du workflow : le contrat (v0), l'invocation (001), la source unique des
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

Le 4 septembre au matin, la boucle a tourné seule pour la première
journée entière, et elle a mesuré ce qui lui manquait. À 7h00 le pilote
a redéposé un lot dont la PR était ouverte ; à 7h30 Composer l'a recodé
en entier ; à 8h30 le briefer a avancé une carte sans avoir écrit une
ligne. Deux réveils de coder sur trois, aucun lot neuf livré. Les deux
pannes sont celles que les briefs 005 et 006 nommaient — elles ont donc
été reprises ensemble, dans un seul lot d'exploitation : le 005 d'abord,
le 006 par-dessus, chacun avec ses contrôles. Les deux écrivent dans
`atelier/echange.py`, et c'est le verrou de périmètre — pas la
plomberie — qui demandait qu'ils soient séparés.

**Reste la porte manquante que ni l'un ni l'autre ne pose** : les tests
du produit ne tournent nulle part sur la machine. L'atelier lit le
verdict que GitHub rend ; il ne le calcule pas.

**Reprendre un juge** attend toujours ce qui le justifierait — une
revue illisible.

Le 4 septembre après-midi, le propriétaire a tranché autrement : il ne
fusionne plus. La porte manquante ne se ferme donc plus en exécutant les
tests sur la machine — elle se ferme en rendant la CI **obligatoire** et
en confiant la fusion à un composant qui ne lit qu'elle (lots 013 et
020). Et « reprendre un juge » cesse d'attendre : un verdict illisible
est un des quatre refus du lot 011, et il bloque au lieu de laisser
passer.
