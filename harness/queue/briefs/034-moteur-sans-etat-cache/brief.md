# Brief 034 — Le moteur cesse de porter un état caché pendant le tick

**Authored**: 2026-08-26T09:00:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données, sans changement de comportement observable.

## But unique

Supprimer la variable de module `_carte_du_tick` de `sim/engine.py`. La carte
figée arrive par la **signature** des fonctions qui la lisent, comme n'importe
quelle autre donnée.

Ce lot ne change rien au monde. C'est sa condition de succès principale : à
graine égale, le jeu doit rendre exactement les mêmes nombres avant et après.

Ce lot ne fait jouer ni le climat, ni les gisements, et n'ajoute aucun maillon
au tick.

## État de départ mesuré

Sur `master` au SHA `8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1`.

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
grep -n "_carte_du_tick" sim/engine.py sim/tests/test_monde.py
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m sim --ticks 20 --seed 0 --json
```

Le lot 033 a fait jouer le relief dans `production_kg()`. Pour y parvenir sans
changer la signature d'une fonction appelée par des tests unitaires historiques,
il a posé la carte dans une variable de module, que `tick()` et
`production_moyenne_kg_par_tick()` affectent puis restaurent autour de leur
travail.

**Le fait qualitatif qui rend ce lot caduc** : si `sim/engine.py` ne contient
plus aucune instruction `global`, il n'y a rien à faire ici.

## Pourquoi ce n'est pas un détail de style

Trois raisons mesurables, et aucune n'est esthétique.

1. **La présence n'est pas la fonction** (règle 7). Une fonction dont le
   résultat dépend d'une variable absente de sa signature ne peut pas être
   raisonnée depuis son appel. `production_kg(cell, rendement)` rend deux
   résultats différents pour les mêmes arguments selon ce qui a été posé
   ailleurs.
2. **Le prochain lot empilerait le deuxième.** Le lot 035 doit apporter au tick
   le jour de l'année, et le lot 038 les gisements. Trois variables de module au
   lieu d'une, et une restauration à ne pas oublier à chaque fois.
3. **L'état survit à l'exception.** `_carte_du_tick` est restauré dans un
   `finally`, ce qui tient aujourd'hui ; mais toute sortie anticipée écrite sans
   y penser laisserait la carte d'un monde posée pour le suivant. Un test qui
   charge deux mondes verrait le premier.

## Règle du monde

**Aucune.** Ce lot ne touche à aucune donnée du monde, n'introduit aucun
paramètre et ne lit aucune valeur nouvelle de la carte. La question du niveau de
fidélité (AGENTS.md, « Vraisemblable, pas véridique ») ne se pose donc pas : il
n'y a rien ici dont on puisse dire que c'est juste ou plausible. Le seul critère
est l'identité du comportement.

## Source de vérité et raccord au moteur

La classe de relief continue de venir **uniquement** de
`world.carte[cell_id]["relief"]`. Rien d'autre ne change de source.

La forme du raccord est libre dans `sim/engine.py` — un paramètre explicite, un
petit objet de contexte passé au tick, ou tout autre moyen — sous trois
contraintes :

- il reste **une seule** formule de production dans `sim/` ;
- `tick(world, rng)` garde sa signature publique actuelle : `sim/__main__.py`,
  `sim/snapshot_export.py` et les tests l'appellent ainsi, et ce lot ne les
  modifie pas ;
- le chemin unitaire historique — construire une `Cell` sans `World` et appeler
  la production — reste utilisable, et rend alors la production sans facteur de
  relief, comme aujourd'hui.

Une classe de relief manquante ou inconnue **dans le chemin réel du tick** reste
une donnée invalide : l'erreur qui nomme le `cell_id` et la valeur fautive est
conservée telle quelle. Aucun repli silencieux vers `plaine`.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/tests/test_monde.py`, **uniquement** pour ce que décrit la section
  ci-dessous.

Livrables du lot autorisés :

- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/manifest.json` ;
- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/generator-log.md` ;
- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/measure_034.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni
`sim/constants.py`, ni `sim/world.py`, ni `sim/model.py`, ni
`sim/snapshot_export.py`, ni `sim/__main__.py`, ni `sim/tests/test_survie.py`,
ni la carte figée, ni le visualiseur, ni l'outil de fabrication de la carte, ni
ce brief, ni sa grille, ni un `verdict.md`.

### La seule modification de test autorisée, et pourquoi

`sim/tests/test_monde.py::test_production_kg_modulée_par_le_relief` **pose et
retire `engine._carte_du_tick` lui-même** pour appeler la production. Ces deux
lignes de mise en place disparaissent avec la variable ; elles sont remplacées
par le passage explicite que ce lot introduit.

**Aucune assertion de ce test ne change.** Les ratios attendus, l'échantillon
dérivé de la carte, le refus d'un échantillon vide, la comparaison à la plaine :
tout reste identique, caractère pour caractère.

Ce n'est pas une calibration après mesure — le défaut n° 4 que cherche le
relecteur — et la différence est vérifiable mécaniquement. Une calibration
consiste à **desserrer un critère** après avoir vu une mesure qui le dépasse.
Ici le critère est inchangé, et il n'a jamais échoué : ce qui change est la
façon dont le test atteint la fonction qu'il mesure. Le mesureur du lot produit
le diff de ce fichier et montre qu'aucune ligne contenant `assert` n'y apparaît.

Les deux autres contrôles de relief (`test_tick_refuse_relief_inconnu`,
`test_tick_refuse_relief_absent`) passent par `tick()` et ne touchent pas la
variable de module : ils restent **strictement inchangés**, et ce sont eux qui
prouvent que le refus n'a pas été perdu au passage.

L'ajout du contrôle de SC1 dans ce même fichier est un **ajout**, pas une
modification : il ne touche aucune assertion existante.

## Conditions de succès

### SC1 — Plus aucun état global mutable dans le moteur

Un contrôle ajouté à `sim/tests/test_monde.py` parcourt l'arbre syntaxique de
`sim/engine.py` et échoue si une fonction quelconque du module contient une
instruction `global`. Le nombre de fonctions inspectées est dérivé du module
lui-même ; un module sans fonction fait échouer le contrôle au lieu de passer.

**Le rouge est prouvé avant la correction** : ce contrôle, écrit et lancé sur le
SHA de base, échoue en nommant les fonctions fautives. Le journal du lot cite
cette sortie rouge.

### SC2 — La carte arrive par la signature

Sur le monde chargé, la fonction de production reçoit la carte par ses
arguments. Un contrôle appelle la production deux fois avec les mêmes arguments
et obtient deux fois le même résultat, sans qu'aucune mise en place hors de
l'appel ne soit nécessaire.

### SC3 — Le monde ne bouge pas d'un octet

`.venv/bin/python -m sim --ticks 20 --seed 0 --json` rend, après changement, une
sortie **byte-identique** à celle rejouée sur le SHA de base.

Le mesureur archive la sortie de base **avant** l'édition, la relit, et compare
champ par champ. Le nombre de champs comparés est celui de la sortie, dérivé de
son propre contenu. Il ne recopie aucun nombre du présent brief.

Une différence, si petite soit-elle, fait échouer le lot : elle voudrait dire
que le raccord a changé l'ordre des tirages ou la valeur d'un facteur, ce que ce
lot n'a pas le droit de faire.

La même identité vaut pour 200 ticks à la graine 42, pour attraper une
divergence qui ne se verrait pas en 20 pas.

### SC4 — Le chemin unitaire historique survit

Construire une `Cell` sans `World` et appeler la production rend la production
sans facteur de relief, sans lever d'erreur et sans exiger de carte. Les
contrôles de `sim/tests/test_survie.py` qui empruntent ce chemin restent verts
**sans être modifiés**.

### SC5 — Le refus de l'inconnu est intact

`test_tick_refuse_relief_inconnu` et `test_tick_refuse_relief_absent` restent
verts sans une seule ligne changée. Une carte en mémoire dont une classe est
remplacée par une valeur inconnue provoque toujours l'erreur explicite portant
le `cell_id` et la valeur.

### SC6 — La sonde des couches ne bouge pas

`build_snapshot_document(World.charger(0), 0, 0)` rend toujours
`couches.relief.utilisee_par_le_moteur == true`,
`couches.climat.utilisee_par_le_moteur == false` et
`couches.gisements.utilisee_par_le_moteur == false`. Ces valeurs viennent de la
sonde existante ; aucune déclaration manuelle n'est ajoutée.

### SC7 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
  sans modification de ce fichier ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde` et
  `test_aucune_constante_terminale` restent verts ;
- aucune deuxième formule de production alimentaire n'apparaît dans `sim/`.

## Compteurs exigés

Le mesureur `deliverables/measure_034.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `fonctions_moteur_inspectees` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de fonctions réellement trouvées dans le module |
| `fonctions_avec_global` | mêmes fonctions, instructions `global` comptées | `fonctions_moteur_inspectees` |
| `fonctions_avec_global_avant` | même parcours joué sur le fichier du SHA de base | nombre de fonctions du module à ce SHA |
| `champs_cli_identiques` | comparaison de la sortie CLI archivée avant édition et de celle d'après | nombre de champs réellement présents dans la sortie |
| `sorties_cli_comparees` | 20 ticks graine 0, puis 200 ticks graine 42 | nombre d'exécutions réellement lancées |
| `appels_unitaires_sans_carte` | appels de production sur une `Cell` construite hors `World` | nombre d'appels réellement essayés |
| `classes_inconnues_refusees` | mutations en mémoire vers une valeur absente de l'ensemble dérivé de la carte | nombre de mutations réellement exécutées |
| `assertions_modifiees_dans_les_tests` | diff de `sim/tests/test_monde.py` contre le SHA de base, lignes contenant `assert` | nombre de lignes du diff réellement examinées |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`fonctions_avec_global` doit valoir **0**, et ce zéro est une mesure réelle : le
mesureur a inspecté chaque fonction. La sentinelle « non calculé » du projet est
`-1`, jamais `0`. `assertions_modifiees_dans_les_tests` doit valoir **0** pour
la même raison. `fonctions_avec_global_avant` doit être strictement positif,
sans quoi le rouge n'a pas été prouvé.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : la sortie rouge du contrôle SC1 avant
  correction, les fichiers modifiés, les commandes jouées, les résultats et les
  limites ;
- `measure_034.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. La comparaison
avant/après passe par la référence Git du SHA de base, pas par une copie `.orig`
fabriquée après coup.

Attention : `sim/engine.py` doit **différer** du SHA de base, tandis que la
sortie CLI doit lui être **identique**. Ce sont les deux faits que le lot a à
prouver ensemble.

## Hors périmètre

- le climat, les gisements, la saison, la natalité, la migration ;
- toute modification du comportement du monde, si petite soit-elle ;
- toute constante nouvelle ou modifiée ;
- la signature publique de `tick()` ;
- le snapshot, le visualiseur, la carte figée, l'outil de fabrication ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
