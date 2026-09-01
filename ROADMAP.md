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

## Ce que l'atelier ne sait pas encore faire

- **Reprendre un juge.** Après un JSON de revue illisible, changer
  de relecteur sans relancer le lot.
- **Lire un quota vivant.** L'atelier consomme `llmquota` s'il est
  là, mais ne sonde aucun fournisseur lui-même. Reste une référence,
  pas une dépendance.
- **Ouvrir ou lire la PR depuis l'atelier.** Composer l'ouvre et écrit
  son numéro dans le canal ; l'atelier le transporte sans jamais parler
  à GitHub.
- **Lever un verrou tout seul après une fusion.** L'atelier ne sait pas
  ce que le propriétaire a fusionné. Il dit quand un verrou bloque ;
  `atelier lever` reste un geste humain.
- **Convoquer un conseil.** Le protocole FACT / INFERENCE / ASSUMPTION
  / UNKNOWN est décrit ; il n'est pas une commande.
- **Un second produit.** Seul ForgeHistory a un profil.
- **Retirer la main qui écrit à tous les relecteurs.** Seul `claude` a
  un drapeau connu ; pour `agent` et `codex`, l'atelier déclare qu'il ne
  sait pas, il ne fait pas semblant.

## Ordre

Un lot d'atelier à la fois. Les trois premiers ont fermé le sujet du
workflow : le contrat (v0), l'invocation (001), la source unique des
rôles (002), la boucle et la bascule (003).

La suite n'est plus du code, c'est de l'exploitation : poser
`atelier.toml` dans le produit, faire passer `atelier pret`, puis armer
**un** cron — le `coder` d'un brief que tu as relu. Hermes pilote à
partir de là.

Le prochain lot de code qui vaudra le coup, quand l'exploitation aura
parlé : **reprendre un juge** après une revue illisible, sans relancer
le lot.
