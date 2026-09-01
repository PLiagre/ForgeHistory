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

## Ordre

Un lot d'atelier à la fois. Le prochain qui vaut le coup : **faire
revenir le numéro de PR dans la carte**, pour que `relire` sache quoi
relire sans que tu le lui dises. Ensuite seulement, la reprise de juge.
