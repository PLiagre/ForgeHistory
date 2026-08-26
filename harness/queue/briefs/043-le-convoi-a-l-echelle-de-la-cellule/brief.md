# Brief 043 — Le convoi est à l'échelle de la cellule

**Authored**: 2026-08-26T10:30:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire dériver la capacité d'une arête de la **longueur de frontière partagée**
entre les deux cellules, une donnée que la carte porte déjà et que personne ne
lit.

Aujourd'hui la capacité vaut un convoi de mulets par jour, quelle que soit
l'arête. Une cellule médiane du monde compte environ cent mille habitants et
consomme près de deux cent mille kilogrammes par tick ; une arête en transporte
deux cents. **Le commerce est mille fois trop petit pour l'échelle des cellules**
: il ne peut, en pratique, sauver personne.

Ce lot ne change ni les règles d'allocation du commerce, ni la production, ni la
consommation, et ne crée aucune marchandise.

## Dépendance

**Ce lot se lance après le lot 040.** Le facteur de terrain se prouve plus
simplement sur une capacité constante ; il multiplie ensuite la capacité
dérivée. Si 040 n'est pas fusionné, ce lot est **bloqué**, pas à adapter.

## Pourquoi c'est le lot qui ouvre la couche 2

Une ville est un endroit qui **ne produit pas ce qu'il mange**. Tant qu'une
cellule ne peut recevoir que deux cents kilogrammes par arête et par tick, aucun
endroit du monde ne peut être nourri par ses voisins : la densité de population
reste bornée par ce que chaque cellule cultive elle-même.

Mesuré, avant d'écrire ce brief : ni la natalité, ni la migration de famine, ni
une migration d'attraction ne concentrent la population. La densité de la
cellule la plus peuplée ne dépasse jamais celle de la médiane d'un facteur
notable, à trois cent soixante-cinq comme à mille ticks. Ce n'est pas une
faiblesse des maillons de population : c'est que rien ne peut nourrir une
concentration.

**Le brief « le bourg est une agrégation dérivée » ne peut donc pas être écrit
avant celui-ci**, et il n'a pas été écrit.

## Fondement dans le modèle

`sim/MODELE.md`, § « Le mur qui sépare la couche 1 de la couche 2 » — la mesure
qui montre que le commerce est de trois ordres de grandeur trop petit — et
§ « Le commerce entre cellules », qui porte la capacité que ce lot remplace. Si
l'une de ces sections a changé depuis la rédaction de ce brief, le relire avant
de le lancer.

`sim/MODELE.md` est hors périmètre de ce lot. La mise à jour de la section
citée après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
grep -rn "shared_length_m" sim/ tools/
.venv/bin/python -c "import statistics;from sim.world import World;w=World.charger(0);print(statistics.median([c.population for c in w.cells.values()]), statistics.median([e['shared_length_m'] for e in w.adjacency if e['a'] in w.cells and e['b'] in w.cells]))"
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si `sim/` lit déjà
`shared_length_m`, il n'y a rien à faire ici.

## Règle du monde

**Fidélité mixte, et la distinction compte.**

- La longueur de frontière partagée est de **niveau 1** : elle est dans la carte
  figée, calculée sur la géométrie, et ce lot ne la recalcule pas.
- Le **débit par kilomètre de frontière** est de **niveau 2** : plausible,
  généré, jamais sourcé. Un flux local surprenant n'est pas un défaut historique
  et n'ouvre ni correctif, ni brief.

Le mécanisme, en un pas :

```
capacite(arete) = DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK × (shared_length_m / METRES_PAR_KM)
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` | 400.0 | kilogrammes traversant un kilomètre de frontière par tick — niveau 2 |
| `METRES_PAR_KM` | 1000.0 | conversion d'unité, pas un réglage |

`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` — la capacité plate — est **supprimée**,
pas conservée à côté. Une constante morte laissée en place piège le brief
suivant, et une capacité plate qui coexisterait avec une capacité dérivée serait
un deuxième chemin de décision.

**Ce que cette forme dit du monde.** Une longue frontière commune laisse passer
plus de convois qu'un contact ponctuel : il y a plus de chemins, plus de gués,
plus de cols. Ce n'est pas une route — le jeu n'a pas de routes — c'est la
perméabilité brute d'une frontière. Le jour où des routes existeront, elles
multiplieront ce débit ; elles ne le remplaceront pas.

**Composition avec le relief.** Si le lot 040 est fusionné, son facteur de
terrain **multiplie** cette capacité dérivée : une longue frontière de haute
montagne reste une mauvaise frontière. Les deux règles se composent, aucune ne
remplace l'autre, et l'ordre du produit ne change rien.

**Une longueur de frontière absente ou non numérique est une donnée invalide** :
lever une erreur qui nomme les deux `cell_id`. Ne pas deviner, ne pas rabattre
silencieusement vers une longueur par défaut. Une longueur nulle, en revanche,
est **valide** : deux cellules qui ne se touchent qu'en un point ne laissent rien
passer, et ce zéro est une mesure.

Ce refus vaut partout, y compris sur un micro-monde. Pour que
`test_write_coverage.py` — hors assertions, hors périmètre de calibration —
ne crashe pas : les deux arêtes de `_MondeEpreuve.adjacency` n'ont
aujourd'hui **pas** de `shared_length_m`. Ce lot **ajoute** cette clé à ces
arêtes, et rien d'autre dans ce fichier. La valeur est une longueur
strictement positive, identique sur les deux arêtes, **lue** du premier
littéral `shared_length_m` déjà présent dans `sim/tests/test_commerce.py` —
pas inventée, pas calibrée après mesure. Aucune assertion de
`test_write_coverage.py` ne change.

## Source de vérité et raccord au moteur

La longueur vient **uniquement** de l'arête de `world.adjacency`, telle que la
carte figée la porte. Le moteur ne la recalcule pas depuis la géométrie, ne la
duplique pas, et n'écrit jamais dans la carte.

La capacité se calcule à **un seul endroit** dans le maillon commerce, et ce
maillon reste unique.

Les arêtes dont l'un des bouts n'est pas une cellule du monde continuent d'être
ignorées.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/tests/test_commerce.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible, et pour la substitution du nom de la constante
  supprimée selon la règle ci-dessous ;
- `sim/tests/test_survie.py`, **uniquement** pour la même substitution de nom, si
  ce fichier importe la constante supprimée ;
- `sim/tests/test_write_coverage.py`, **uniquement** pour ajouter `shared_length_m`
  aux arêtes de `_MondeEpreuve.adjacency`, selon la règle ci-dessous. Aucune
  assertion de ce fichier ne change.

Livrables du lot autorisés :

- `harness/queue/briefs/043-le-convoi-a-l-echelle-de-la-cellule/deliverables/manifest.json` ;
- `harness/queue/briefs/043-le-convoi-a-l-echelle-de-la-cellule/deliverables/generator-log.md` ;
- `harness/queue/briefs/043-le-convoi-a-l-echelle-de-la-cellule/deliverables/measure_043.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/__main__.py`, ni
`sim/aggregation.py`, ni `sim/tests/test_monde.py`, ni la carte figée, ni le
visualiseur, ni l'outil de fabrication de la carte, ni ce brief, ni sa grille,
ni un `verdict.md`.

### La règle de substitution du nom de la constante

La constante supprimée est nommée par des contrôles existants. Pour chaque ligne
modifiée d'un fichier de test, la ligne d'origine à laquelle on applique **la
seule substitution du nom de la constante par l'expression de capacité dérivée**
doit être identique à la ligne d'arrivée. Aucune valeur attendue, aucun seuil,
aucun nom de test ne change. Le mesureur compte les lignes qui violent cette
règle ; ce compte doit être **nul**.

Un contrôle dont la valeur attendue dépendait de la capacité plate doit tirer sa
nouvelle valeur de la **même expression que le moteur**, jamais d'un nombre
recopié après avoir vu la mesure.

### La règle d'ajout de `shared_length_m` au monde d'épreuve

`_MondeEpreuve.adjacency` dans `sim/tests/test_write_coverage.py` reçoit la
clé `shared_length_m` sur chaque arête. Pour chaque ligne modifiée de ce
fichier, la ligne d'origine à laquelle on applique **la seule addition de
cette clé** doit être identique à la ligne d'arrivée. Aucune valeur
attendue, aucun seuil, aucun nom de test, aucune assertion ne change. Le
mesureur compte les lignes qui violent cette règle ; ce compte doit être
**nul**.

Sans cet ajout, le refus de la longueur absente ferait crasher
`test_chaque_constante_du_moteur_change_le_monde`, que ce lot n'a pas le
droit de recalibrer. Ce n'est pas une calibration : c'est la même clé que
la carte porte déjà sur toutes les arêtes terre–terre.

## Conditions de succès

### SC1 — La capacité suit la longueur de frontière

Sur un micro-monde déterministe où une même source approvisionne des voisines
identiques par des arêtes de longueurs différentes, les quantités transférées
sont dans le **même rapport** que les longueurs, tant qu'aucune n'est bornée par
le besoin ou par le surplus. Les longueurs de l'échantillon sont dérivées de la
carte figée : la plus courte, la médiane et la plus longue arête réellement
présentes entre deux cellules du monde.

**Le rouge est prouvé avant la correction** : sur le SHA de base, ces arêtes
transportent la même quantité.

### SC2 — La constante plate a disparu

Un contrôle parcourt les modules de `sim/` hors tests et échoue si le nom de la
constante supprimée y apparaît encore. Le nombre de modules parcourus est dérivé
du répertoire ; un parcours vide fait échouer le contrôle.

### SC3 — Une frontière ponctuelle ne laisse rien passer

Une arête de longueur nulle transporte zéro. Ce zéro est une **mesure réelle** :
le maillon a été joué et a compté zéro. La sentinelle « non calculé » du projet
est `-1`, jamais `0`.

### SC4 — Le commerce cesse d'être décoratif

Sur le monde réel, le champ `kg_transportes` de
`.venv/bin/python -m sim --ticks 365 --seed 0 --json` est **strictement
supérieur** à celui rejoué sur le SHA de base, d'au moins un ordre de grandeur.

Le facteur minimal exigé est dérivé, avant l'exécution, du rapport entre la
capacité médiane dérivée et la capacité plate qu'elle remplace — l'une et
l'autre mesurées, jamais recopiées d'ici. Le mesureur archive la sortie de base
**avant** l'édition et la relit.

### SC5 — Une cellule peut désormais être nourrie par ses voisines

Sur un micro-monde déterministe, une cellule qui ne produit **rien** — surface
nulle — et dont les voisines ont un surplus suffisant traverse un nombre de
ticks dérivé sans que sa population diminue.

C'est le fait que ce lot achète, et qui n'était pas atteignable avant lui : un
endroit peut vivre de ce qu'on lui apporte. C'est la condition d'existence d'une
ville, et elle est ici démontrée sur un cas construit, pas supposée.

Le même contrôle, joué avec la capacité plate remplacée en mémoire par le
module, montre que cette cellule dépérit : la garde est payée, le résultat
dépend bien de la constante.

### SC6 — La masse se conserve toujours

Sur un micro-monde, la somme des stocks avant et après le maillon commerce est
identique. Le contrôle existant qui porte cet invariant reste vert **sans être
modifié**.

### SC7 — Le monde ne nourrit toujours pas plus de monde qu'il ne produit

Les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes, à
la substitution de nom près. Le plafond employé est celui que le moteur dérive.

Un commerce plus large déplace mieux la nourriture ; il n'en crée pas. Si le
plafond était dépassé, c'est que le maillon duplique des kilogrammes — et c'est
exactement ce que ce contrôle est là pour attraper.

### SC8 — Le refus de l'invalide

Une adjacence en mémoire dont la longueur de frontière d'une arête est retirée
ou remplacée par une valeur non numérique provoque l'erreur explicite exigée,
avec les deux `cell_id`. Aucun repli silencieux n'est admis.

### SC9 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_conservation_masse_transport`, `test_invariance_ordre_aretes`,
  `test_recepteur_pas_sur_livre` et `test_kg_transportes_egal_deltas_positifs`
  restent verts, à la substitution de nom près ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques entre elles ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- il n'y a toujours qu'un seul maillon commerce dans `sim/` ;
- le nombre de tests collectés dans `sim/tests/` est au moins celui du SHA de
  base.

## Compteurs exigés

Le mesureur `deliverables/measure_043.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `aretes_entre_deux_cellules` | parcours de l'adjacence du monde chargé | nombre total d'arêtes réellement présentes dans la carte |
| `longueurs_distinctes_mesurees` | même parcours | `aretes_entre_deux_cellules` |
| `capacite_mediane_derivee` | capacité calculée sur chaque arête, médiane prise | `aretes_entre_deux_cellules` |
| `capacite_plate_remplacee` | valeur de la constante supprimée, lue sur le SHA de base | nombre de constantes réellement lues |
| `rapport_de_capacite_attendu` | rapport des deux précédentes, fixé avant l'exécution | — |
| `rapports_transferts_sur_longueurs` | micro-monde, arêtes courte, médiane et longue dérivées de la carte | nombre d'arêtes réellement essayées |
| `transfert_sur_arete_de_longueur_nulle` | micro-monde, arête de longueur nulle | nombre de ticks réellement joués |
| `occurrences_constante_plate_apres` | parcours des modules de `sim/` hors tests | nombre de modules réellement parcourus |
| `kg_transportes_avant` | sortie de base rejouée et archivée avant édition | nombre d'exécutions réellement lancées |
| `kg_transportes_apres` | même commande après changement | nombre d'exécutions réellement lancées |
| `ticks_survecus_cellule_sans_production` | micro-monde de SC5, capacité dérivée | borne de ticks dérivée du contrôle |
| `ticks_survecus_cellule_sans_production_capacite_plate` | même micro-monde, constante remplacée en mémoire | même borne |
| `ecart_de_masse_micro_monde` | somme des stocks avant et après le maillon | nombre de cellules réellement sommées |
| `longueurs_invalides_refusees` | mutations en mémoire retirant ou corrompant une longueur | nombre de mutations réellement exécutées |
| `aretes_monde_epreuve_avec_longueur` | arêtes de `_MondeEpreuve.adjacency` portant `shared_length_m` après changement | nombre d'arêtes du fixture |
| `lignes_de_test_hors_substitution` | diff des fichiers de test contre le SHA de base, hors l'ajout de clé au fixture | nombre de lignes du diff réellement examinées |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

`transfert_sur_arete_de_longueur_nulle`, `occurrences_constante_plate_apres`,
`ecart_de_masse_micro_monde` et `lignes_de_test_hors_substitution` doivent valoir
**0**, et ces zéros sont des mesures réelles. La sentinelle « non calculé » du
projet est `-1`, jamais `0`.

Le rapport de `kg_transportes_apres` sur `kg_transportes_avant` doit atteindre
`rapport_de_capacite_attendu`. `ticks_survecus_cellule_sans_production` doit être
strictement supérieur à sa contrepartie à capacité plate.
`aretes_monde_epreuve_avec_longueur` doit égaler le nombre d'arêtes du fixture.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_043.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- les routes, les ponts, les ports, les fleuves et tout investissement dans une
  infrastructure ;
- le transport maritime et les arêtes terre–mer ;
- le coût du transport en nourriture, en temps ou en pertes en route ;
- la définition d'un bourg ou d'une ville — elle vient après ce lot, pas avec ;
- la production, la consommation, la mortalité, la natalité, la migration ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
