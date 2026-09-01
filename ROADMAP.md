# ROADMAP — où l'atelier en est

## v0 — le contrat, livré

Les sept couches sont nommées. Le cycle tient sans invoquer d'agent.
La porte mécanique refuse un brief infirme. Le verrou refuse deux
lots sur le même fichier. Le canal d'échange existe. `fusionner`
sort en erreur. Un quota manquant vaut `-1`.

C'est volontairement incomplet : lancer Cursor hors d'un contrat
durable a déjà coûté un lot. v0 *prépare* ; il ne dépense pas.

## Ce que l'atelier ne sait pas encore faire

- **Invoquer.** Les adaptateurs nomment Claude, Cursor, Codex, Hermes.
  Personne ne les lance depuis l'atelier.
- **Reprendre un juge.** Après un JSON de revue illisible, changer
  de relecteur sans relancer le lot.
- **Lire un quota vivant.** v0 refuse l'inconnu ; il ne sonde pas
  les fournisseurs. llmquota reste une référence, pas une dépendance.
- **Convoquer un conseil.** Le protocole FACT / INFERENCE / ASSUMPTION
  / UNKNOWN est décrit ; il n'est pas une commande.
- **Un second produit.** Seul ForgeHistory a un profil.

## Ordre

Un lot d'atelier à la fois. Le prochain qui vaut le coup : **invoquer
Cursor dans un worktree déjà créé par `--run`**, et rien d'autre.
Tant que ça n'existe pas, le propriétaire colle le prompt que
`start` imprime.
