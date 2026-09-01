# Vision — infrastructure dont les agents ont besoin

Ce document est gelé. Il prime sur tout autre document de ce dépôt
en cas de conflit.

## Ce que nous construisons

Pas un agent de plus. Pas une démo IA. Une **infrastructure** pour que
des agents déjà choisis — Claude, Cursor, Codex, Hermes, ou un autre —
travaillent de manière fiable sur un dépôt produit.

Le premier client est [ForgeHistory](https://github.com/PLiagre/ForgeHistory).
Ce n'est pas le seul client possible. L'atelier ne connaît pas le jeu ;
il connaît un **lot**, un **périmètre**, et des **rôles**.

## Une phrase

Un lot entre, une PR sort, le propriétaire fusionne.

## Les sept couches

Le schéma est plus vaste que n'importe quel agent unique. Chaque
composant de ce dépôt déclare **une** couche. Un composant qui en
occupe deux est un défaut.

| couche | question | ce qu'elle n'est pas |
|---|---|---|
| **intelligence** | qui raisonne | pas le pilote, pas le juge de fusion |
| **outils** | quelles compétences se rejouent | pas une seconde source d'instruction |
| **mémoire** | ce qui survit aux sessions | pas une base parallèle au dépôt produit |
| **exécution** | où le code s'écrit | pas le dépôt commun de tout le monde |
| **orchestration** | dans quel ordre, avec quelle reprise | pas un agent, pas une fusion |
| **coordination** | qui a le quota, qui tient quel fichier | pas un verrou magique qui remplace la parole |
| **vérification** | quoi est mesuré, par qui | pas un verdict qui fusionne |

## Ce qui ne se négocie pas

1. **Celui qui a écrit le code ne dit pas s'il est recevable.**
2. **Le brief est la seule source d'instruction d'un lot.** Tout le reste
   y renvoie ; rien ne le paraphrase.
3. **Le propriétaire fusionne.** Aucune commande de l'atelier ne fusionne.
   Aucun cron ne fusionne.
4. **Une absence se déclare.** L'atelier refuse de deviner un quota, un
   rôle, un fichier, un verdict.
5. **Sans `--run`, rien n'est écrit.** Un aperçu n'est pas une dépense.

## Ce que nous ne construisons pas

- un cinquième agent de codage
- un tableau de bord qui devient une base de données parallèle
- une mémoire vectorielle qui invente ce que le git ne contient pas
- une fusion automatique
- un niveau de risque qui choisit le modèle à la place du propriétaire
