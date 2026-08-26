# Brief 039 — Le commerce cesse de ne connaître que la nourriture

**Authored**: 2026-08-26T09:50:00Z
**Author**: Claude
**Risque**: R2 — changement structurel d'un maillon du tick, sans changement de comportement observable.

## But unique

Généraliser le maillon commerce : il ne transporte plus « de la nourriture »,
il transporte **une marchandise**, et il est joué pour chacune de celles que le
monde contient.

Ce lot ne change **rien** au comportement du monde. C'est sa condition de succès
principale : à graine égale, le jeu doit rendre exactement les mêmes nombres
avant et après.

Ce lot ne fait apparaître aucune marchandise, n'en fait consommer aucune, et ne
change ni la capacité des arêtes, ni la règle d'allocation.

## Dépendance

**Ce lot suppose les lots 037 et 038 fusionnés.** Sans panier, il n'y a rien à
généraliser ; sans extraction, il n'y a qu'une marchandise dans le monde et la
généralisation ne se voit pas.

## Pourquoi le minerai ne bougera pas, et pourquoi c'est juste

Le besoin d'une cellule pour une marchandise est, dans ce moteur, **ce qu'elle
va en consommer ce tick moins ce qu'elle en a**. Aujourd'hui, rien ne consomme
de minerai. Le besoin en minerai est donc nul partout, et le commerce
généralisé n'en transportera pas un kilogramme.

Ce n'est pas un manque du lot : c'est le résultat correct. Un minerai qui se
déplacerait sans que personne ne le demande serait une règle de gameplay
plaquée sur le monde — exactement ce que la philosophie du projet interdit. Le
minerai s'entassera au carreau de la mine jusqu'à ce qu'un consommateur existe :
c'est ce que les lots 044 et 045 apporteront, et le commerce les servira alors
sans une ligne de plus.

Ce que ce lot achète, c'est précisément cela : **que ces lots-là n'aient rien à
écrire dans le commerce.**

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/test_commerce.py -q
grep -n "MARCHANDISE_NOURRITURE\|nourriture" sim/engine.py
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si le maillon commerce prend déjà
la marchandise en paramètre et ne nomme plus la nourriture, il n'y a rien à
faire ici.

## Règle du monde

**Fidélité : sans objet.** Ce lot ne touche à aucune donnée du monde,
n'introduit aucun paramètre physique et ne lit aucune valeur nouvelle de la
carte. Le seul critère est l'identité du comportement.

Les cinq règles du commerce sont conservées **mot pour mot**, avec « la
nourriture » remplacé par « la marchandise » :

1. le besoin d'une cellule est ce qu'elle consommera ce tick, moins ce qu'elle
   a ; le surplus est l'excédent au-delà de ce qu'elle consommera ;
2. les transferts sont calculés sur un instantané pris avant tout mouvement : un
   kilogramme ne traverse qu'une arête par tick, et une cellule qui vient de
   recevoir ne redistribue pas le même tick ;
3. l'allocation entre demandeurs concurrents est proportionnelle aux besoins et
   parcourue dans l'ordre croissant des identifiants de cellule ;
4. chaque transfert est borné par la capacité de l'arête ;
5. le total reçu par une cellule est écrêté à son besoin, l'excédent restant
   chez la source.

L'invariant conservé, et le plus important : **le maillon commerce ne touche
jamais à la dette alimentaire.** Il ne modifie que des stocks.

L'ensemble des marchandises jouées est **dérivé du monde** : celles qui
apparaissent dans au moins un panier, plus la nourriture. Il n'est jamais écrit
dans le code. L'ordre dans lequel elles sont jouées est stable et dérivé de leur
nom, sans quoi le déterminisme ne tiendrait pas.

## Source de vérité et raccord au moteur

Il reste **un seul** maillon commerce dans `sim/`, une seule fois écrit, appelé
une fois par marchandise. Aucune copie spécialisée pour la nourriture.

La consommation par tête d'une marchandise vient d'un accès nommé : celle de la
nourriture est la ration existante, celle de toute autre marchandise est nulle
tant qu'aucun lot ne l'a définie. Cet accès est le **seul** endroit du moteur qui
distingue une marchandise d'une autre, et c'est là que les lots suivants
brancheront leurs consommateurs.

Le total transporté que rend le tick devient la somme sur toutes les
marchandises. Comme aucune autre que la nourriture ne circule aujourd'hui, cette
somme est celle d'hier — et SC1 le vérifie au bit près plutôt que de le
supposer.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py`, uniquement si un nom de marchandise ou un accès nommé doit
  y être déclaré ;
- `sim/tests/test_commerce.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible ; les assertions déjà présentes restent
  inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/039-le-commerce-porte-tout/deliverables/manifest.json` ;
- `harness/queue/briefs/039-le-commerce-porte-tout/deliverables/generator-log.md` ;
- `harness/queue/briefs/039-le-commerce-porte-tout/deliverables/measure_039.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/__main__.py`, ni
`sim/aggregation.py`, ni `sim/tests/test_survie.py`, ni `sim/tests/test_monde.py`,
ni la carte figée, ni le visualiseur, ni l'outil de fabrication de la carte, ni
ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Le monde ne bouge pas d'un octet

`.venv/bin/python -m sim --ticks 20 --seed 0 --json` et
`.venv/bin/python -m sim --ticks 365 --seed 0 --json` rendent, après changement,
des sorties **byte-identiques** à celles rejouées sur le SHA de base.

Le mesureur archive les sorties de base **avant** l'édition, les relit, et
compare champ par champ. Le nombre de champs comparés est dérivé du contenu. Il
ne recopie aucun nombre du présent brief.

Une seule différence fait échouer le lot.

### SC2 — Le maillon ne nomme plus la nourriture

Un contrôle parcourt l'arbre syntaxique du maillon commerce et échoue si le nom
de la marchandise « nourriture » y apparaît, sous forme de constante lue ou de
littéral. Le nombre de fonctions inspectées est dérivé du module.

**Le rouge est prouvé avant la correction** : ce contrôle, lancé sur le SHA de
base, échoue en nommant les occurrences.

### SC3 — Une deuxième marchandise consommée circule, sans code nouveau

Sur un micro-monde déterministe construit à la main, on déclare — dans le seul
accès nommé prévu pour cela — qu'une marchandise d'essai est consommée par
habitant. Elle circule alors entre cellules adjacentes selon les cinq règles,
**sans qu'une ligne du maillon commerce ait été ajoutée**.

C'est la démonstration que la généralisation est réelle et pas seulement
renommée.

### SC4 — Une marchandise que personne ne consomme ne bouge pas

Sur le monde réel, après un nombre de ticks dérivé, la quantité totale de chaque
marchandise minière portée par chaque cellule est **exactement** celle produite
par sa propre extraction. Aucun kilogramme n'a changé de cellule.

Ce zéro de transport est une **mesure réelle** : le mesureur a joué les ticks et
comparé. La sentinelle « non calculé » du projet est `-1`, jamais `0`.

### SC5 — La masse se conserve, marchandise par marchandise

Sur un micro-monde où le commerce agit, la somme des stocks d'une marchandise
avant et après le maillon est identique : le commerce déplace, il ne crée ni ne
détruit. Vérifié pour la nourriture et pour la marchandise d'essai de SC3.

### SC6 — La dette alimentaire reste hors d'atteinte du commerce

Le maillon commerce ne modifie jamais `food_deficit_kg`, pour aucune
marchandise. Le contrôle existant qui porte cet invariant reste vert **sans être
modifié**.

### SC7 — Le déterminisme tient malgré plusieurs marchandises

Deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
strictement identiques. Sur le micro-monde de SC3, l'ordre dans lequel les
marchandises sont jouées ne change pas le résultat : le contrôle le vérifie en
présentant les paniers dans deux ordres d'insertion différents.

### SC8 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_conservation_masse_transport`, `test_invariance_ordre_aretes`,
  `test_recepteur_pas_sur_livre` et `test_kg_transportes_egal_deltas_positifs`
  restent verts **sans être modifiés** ;
- `test_adjacency_is_read_by_engine` reste vert ;
- `test_no_hardcoded_numeric_literals` reste vert ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- le nombre de tests collectés dans `sim/tests/` est au moins celui du SHA de
  base.

## Compteurs exigés

Le mesureur `deliverables/measure_039.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `marchandises_du_monde` | paniers du monde chargé après un tick, plus la nourriture | nombre de cellules réellement parcourues |
| `maillons_commerce_dans_sim` | parcours de l'arbre syntaxique des modules de `sim/` hors tests | nombre de modules réellement parcourus |
| `occurrences_nourriture_dans_le_maillon_avant` | même parcours joué sur le SHA de base | nombre de fonctions du maillon à ce SHA |
| `occurrences_nourriture_dans_le_maillon_apres` | même parcours après changement | nombre de fonctions du maillon |
| `champs_cli_identiques` | comparaison des sorties CLI archivées et d'après | nombre de champs réellement présents dans la sortie |
| `kg_mineraux_ayant_change_de_cellule` | monde réel joué, extraction cumulée comparée aux stocks | nombre de cellules minières réellement mesurées |
| `ecart_de_masse_par_marchandise` | somme des stocks avant et après le maillon, sur le micro-monde | nombre de marchandises réellement jouées |
| `modifications_de_dette_par_le_commerce` | micro-monde instrumenté sur le maillon | nombre d'appels au maillon réellement joués |
| `ordres_d_insertion_essayes` | micro-monde de SC3 joué dans deux ordres | nombre d'ordres réellement essayés |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

`maillons_commerce_dans_sim` doit valoir **1**.
`occurrences_nourriture_dans_le_maillon_apres`,
`kg_mineraux_ayant_change_de_cellule`, `ecart_de_masse_par_marchandise` et
`modifications_de_dette_par_le_commerce` doivent valoir **0**, et ces zéros sont
des mesures réelles. La sentinelle « non calculé » du projet est `-1`, jamais
`0`. `occurrences_nourriture_dans_le_maillon_avant` doit être strictement
positif, sans quoi le rouge n'a pas été prouvé.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC2, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_039.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les comparaisons
avant/après passent par la référence Git du SHA de base, pas par une copie
`.orig` fabriquée après coup.

Attention : `sim/engine.py` doit **différer** du SHA de base, tandis que la
sortie CLI doit lui être **identique**.

## Hors périmètre

- faire consommer une marchandise autre que la nourriture ;
- la capacité des arêtes, le coût du transport, le relief sur les routes ;
- le prix, le marché, la monnaie, le troc ;
- l'épuisement, la transformation ou la perte en cours de route ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
