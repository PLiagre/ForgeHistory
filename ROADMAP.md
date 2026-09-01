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

## Ce que l'atelier ne sait pas encore faire

- **Reprendre un juge.** Après un JSON de revue illisible, changer
  de relecteur sans relancer le lot.
- **Lire un quota vivant.** L'atelier consomme `llmquota` s'il est
  là, mais ne sonde aucun fournisseur lui-même. Reste une référence,
  pas une dépendance.
- **Ouvrir la PR depuis l'atelier.** C'est Composer qui l'ouvre ; le
  numéro ne revient pas encore dans la carte tout seul.
- **Convoquer un conseil.** Le protocole FACT / INFERENCE / ASSUMPTION
  / UNKNOWN est décrit ; il n'est pas une commande.
- **Un second produit.** Seul ForgeHistory a un profil.
- **Retirer la main qui écrit à tous les relecteurs.** Seul `claude` a
  un drapeau connu ; pour `agent` et `codex`, l'atelier déclare qu'il ne
  sait pas, il ne fait pas semblant.

## Ordre

Un lot d'atelier à la fois. Le prochain qui vaut le coup : **faire
revenir le numéro de PR dans la carte**, pour que `relire` sache quoi
relire sans que tu le lui dises. Ensuite seulement, la reprise de juge.
