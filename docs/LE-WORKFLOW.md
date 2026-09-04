# Le workflow, de bout en bout

> **Ce document décrit la conduite d'aujourd'hui.** Le 4 septembre 2026,
> la vision a changé : la fusion devient mécanique, plusieurs lots
> avancent de front, et une relecture terminée n'est plus une
> approbation ([VISION.md](../VISION.md)). Rien de tout cela n'est
> encore écrit ici, parce que rien de tout cela ne tourne encore. La
> série 009-023 de [ROADMAP.md](../ROADMAP.md) l'apporte lot par lot, et
> chaque lot corrige la section qu'il change. Une phrase de ce fichier
> qui décrirait le cycle à venir serait un mensonge daté.

Ce document décrit ce que la machine fait toute seule, et où elle
s'arrête. Il tient en une phrase : **la boucle ne s'arrête jamais sur un
état qu'elle pouvait lire, et n'attend une personne que là où une
personne décide.**

Les commandes citées ici sont vérifiées par un contrôle : une commande
inventée fait rougir `tests/test_cli.py`. C'est la seule façon qu'un
document ait de vieillir en se faisant remarquer.

## Le tour d'un rôle

Une ligne de cron appelle `crons/repartiteur.sh` chaque minute. Il lit le
profil actif — un fichier que `hermes` écrit — et n'en tire qu'une
question : *quel rôle se réveille à cette minute ?* Presque toujours
aucun, et il sort en quelques millisecondes.

Quand un rôle se réveille, `crons/reveil.sh` ouvre son journal et appelle
`crons/tour.sh`. Ce tour, dans l'ordre :

1. **Un verrou par rôle.** Deux tours du même rôle ne se marchent pas
   dessus ; deux rôles différents tournent ensemble.
2. **Le rappel.** `atelier rappeler` remet en circulation ce qui se
   retente tout seul (voir plus bas). Il ne fait jamais échouer le tour.
3. **La carte.** `atelier prochain` rend la première carte admissible de
   la boîte du rôle, ou `RIEN` — et alors le tour sort 0 sans rien
   dépenser.
4. **Le drapeau.** Sans `ATELIER_INVOQUER=1`, le tour imprime ce qu'il
   ferait et s'arrête. C'est le mode à sec, et il est fait pour voir.
5. **Le quota.** Un quota à zéro laisse la carte intacte. Un quota
   inconnu vaut -1, jamais 0 : il ne se compte pas comme épuisé.
6. **Les portes.** Ce qui doit exister avant de dépenser : le brief pour
   tout rôle qui n'est pas le briefer, le verdict de la CI pour le
   relecteur, la branche du lot et son verrou pour le coder.
7. **L'agent.** `atelier invocation` construit l'`argv` ; le script
   l'exécute, sans clé d'API dans l'environnement et sous un délai
   maximum.
8. **La sortie.** Une carte prise ne reste jamais en place : elle avance,
   ou elle tombe dans `echec/` avec une cause.

## Les boîtes, et le chemin d'une carte

Les cartes vivent dans `.atelier/boite/` du produit. Une carte, un lot,
un fichier JSON.

```
a-briefer → brief-a-fusionner → (fusion du propriétaire)
                                        ↓
                              a-coder → a-relire → faite → (fusion)
                                                             ↓
                                                        fusionnee
```

- **`a-briefer`** — le pilote y dépose un lot dont la fiche dit
  `a-briefer`. Le briefer écrit le brief et ouvre une PR.
- **`brief-a-fusionner`** — le brief existe et attend le propriétaire.
  Une carte n'y entre que si le fichier du brief existe *et* qu'un numéro
  de PR a été déposé : une carte ne passe pas sur parole.
- **`a-coder`** — le pilote y dépose un lot `pret` dont le brief est
  fusionné, dont les dépendances sont livrées, dont le périmètre est
  libre, et **dont aucune PR n'est ouverte**.
- **`a-relire`** — le coder y a mis un lot avec le numéro de sa PR. Le
  relecteur ne s'y met que si les contrôles obligatoires de cette PR sont
  au vert.
- **`faite`** — relu, en attente de la fusion du propriétaire.
- **`fusionnee`** — la fiche dit `livre` : la carte est rangée, son
  verrou levé.
- **`echec`** — tout ce qui est tombé, avec sa cause.

`atelier prochain --projet P --role R` dit ce qu'un rôle prendrait.
`atelier verrous --projet P` dit quels fichiers sont tenus, et par quel
lot.

## Ce qui revient tout seul, et ce qui attend une personne

La question n'est pas « grave / pas grave », c'est : **refaire le même
geste peut-il donner un autre résultat ?**

| Cause | Reprises | Pourquoi |
|---|---|---|
| `timeout` | 2 | Le tirage change, même si l'entrée ne change pas. |
| `agent` | 1 | Un agent qui plante deux fois ne plante pas par hasard. |
| `brief-absent`, `perimetre`, `branche`, `pr`, `ci`, `verrou`, `worktree`, `avancer`, `inconnue` | 0 | Refaire à l'identique brûle un quota pour arriver au même endroit. |

`atelier rappeler --projet P --role R` est appelé au début de chaque
tour : le rôle se rattrape lui-même au réveil suivant. Ce qu'il ne
rappelle pas, il le dit sur stderr, avec la raison.

Ce qui reste demande une décision, pas une réparation :
`atelier reprendre --projet P --lot L` sort une carte de n'importe quelle
boîte et lève son verrou.

## Ce que la machine lit, et ne devine pas

Trois sondes, une doctrine : **un inconnu n'est ni un oui ni un non.**

- `atelier ci --pr N` rend le verdict des contrôles **obligatoires** —
  la liste qui gouverne le bouton de fusion de GitHub. 0 vert, 1 rouge
  (et il nomme les fautifs), 2 inconnu. Devant l'inconnu, le relecteur
  **attend** : la carte reste dans `a-relire`, et l'un des quatre réveils
  du jour redemandera. Une porte qui s'ouvre toute seule ne se voit
  nulle part.
- `atelier pr-etat --pr N` rend `ouverte`, `fusionnee`, `fermee` ou
  `inconnue`. Devant l'inconnu, le pilote **retient** son dépôt :
  déposer est ce qui fait dépenser. Mais il ne **libère** rien : ranger
  sur une sonde muette rangerait des cartes vivantes.
- La branche d'un lot est demandée à GitHub avant tout dépôt de code.
  Une fiche dit ce que le propriétaire a décidé ; une PR dit ce qui
  existe. Quand les deux se contredisent — fiche `pret`, PR ouverte —
  c'est la PR qui décrit le monde.

Les deux commandes sont injectables : `ATELIER_CI_CMD` et
`ATELIER_PR_CMD` nomment la commande qui répond, et valent `gh` en leur
absence. Aucun test de ce dépôt n'a besoin d'un compte GitHub.

## Les deux profils, et la bascule

Le crontab n'a qu'une ligne et ne change plus. Tout ce qui change vit
dans un profil, sous `crons/profils/`, et la bascule est l'écriture d'un
fichier — pas une modification système.

```bash
atelier-boucle jour      # ForgeHistory, treize réveils, du vrai quota
atelier-boucle atelier   # un produit d'épreuve, de faux agents, aucun quota
atelier-boucle arret     # plus aucun réveil ne démarre
atelier-boucle etat      # quel profil tourne, depuis quand, et le prochain réveil
```

`atelier-boucle etat` est le seul juge de ce qui tourne. Il lit l'heure
*dans* le fuseau du profil, et il dément « ça tourne » quand le crontab
n'appelle pas le répartiteur.

Le profil `atelier` monte son banc par `crons/banc.sh --neuf` : un
produit jetable, ses worktrees, et de faux agents pilotés par
`FAUX_CODE`, `FAUX_DORT`, `FAUX_PR`, `FAUX_SANS_PR`, `FAUX_SALIT` et
`FAUX_COMMIT`. Son `PATH` commence par eux : un vrai agent n'est pas
*choisi de ne pas être appelé*, il est **hors de portée**. C'est là qu'on
éprouve la plomberie, jamais sur le produit.

## Ce qui attend le propriétaire, et rien d'autre

L'atelier ne fusionne pas — `atelier fusionner` refuse, toujours. Trois
gestes restent des décisions, et ce sont les seules :

1. **Fusionner une PR**, celle d'un brief ou celle d'un lot. C'est ce qui
   fait avancer la feuille de route, et c'est ce qui passe une fiche à
   `livre`.
2. **Reprendre une carte tombée** dont la cause ne se retente pas.
3. **Écrire ou corriger un brief** que la porte mécanique refuse.

Tout le reste, la machine le lit.
