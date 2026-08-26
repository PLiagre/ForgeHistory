# Brief 040 — Franchir une montagne coûte plus cher qu'une plaine

**Authored**: 2026-08-26T10:00:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire dépendre la **capacité d'une arête** du relief des deux cellules qu'elle
relie. Aujourd'hui, un col à deux mille mètres transporte autant qu'une route de
plaine : la même capacité plate, pour toutes les arêtes du monde.

Ce lot ne touche ni à la production, ni à la consommation, ni aux règles
d'allocation du commerce. Il ne fait circuler aucune marchandise nouvelle et ne
touche pas aux arêtes maritimes.

## Fondement dans le modèle

`sim/MODELE.md`, § « Le commerce entre cellules » — la capacité par arête que
ce lot fait dépendre du relief. Si cette section a changé depuis la rédaction
de ce brief, le relire avant de le lancer.

`sim/MODELE.md` est hors périmètre de ce lot. La mise à jour de la section
citée après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 365 --seed 0 --json
grep -n "TRADE_CAPACITY_KG_PER_EDGE_PER_TICK" sim/
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si la capacité d'une arête dépend
déjà du **relief**, il n'y a rien à faire ici. Qu'elle dépende d'autre chose que
porte la carte — la longueur de frontière partagée, par exemple — ne rend pas ce
lot caduc : le facteur de terrain vient alors **multiplier** cette base, il ne
la remplace pas.

## Règle du monde

**Fidélité niveau 2** : les facteurs de transport par classe de relief sont des
ordres de grandeur plausibles, générés, jamais sourcés. Un flux local
surprenant n'est pas un défaut historique et n'ouvre ni correctif, ni brief.

Le mécanisme, en deux pas.

**1. C'est le bout le plus difficile qui commande.**

```
capacite(arete) = TRADE_CAPACITY_KG_PER_EDGE_PER_TICK
                × min(facteur_transport[relief_a], facteur_transport[relief_b])
```

Le minimum, et non la moyenne : une route de plaine qui débouche sur un col ne
fait pas passer plus que le col. C'est une contrainte de goulot, pas une
moyenne de confort.

| classe de relief | facteur de transport |
|---|---:|
| `plaine` | 1.00 |
| `colline` | 0.70 |
| `marais` | 0.40 |
| `montagne` | 0.30 |
| `haute_montagne` | 0.10 |

Ces constantes vivent dans `sim/constants.py`, distinctes de celles de
production — un marais se traverse mal et produit mal, une montagne se traverse
très mal et produit mal, et rien ne garantit que les deux échelles coïncident.
Elles portent un commentaire disant qu'il s'agit d'ordres de grandeur plausibles
de niveau 2.

**Motif 033 — constantes invisibles pour le monde d'épreuve.**
`_MondeEpreuve` n'a pas de carte, donc pas de relief. Un nom
`FACTEUR_TRANSPORT_PLAINE` (ou l'une des quatre autres classes) écrit dans
`sim/engine.py` entrerait dans le dénominateur de
`test_chaque_constante_du_moteur_change_le_monde` et n'y bougerait rien.

Donc : les cinq facteurs de transport ne sont **pas** lus par leur nom dans
`sim/engine.py`. Ils vivent dans `sim/constants.py` et le moteur les
consulte via une **table relue à chaque appel**, le même motif que
`facteurs_production_par_relief()` du lot 033 :

```
def facteurs_transport_par_relief() -> dict[str, float]:
    return {
        "plaine": FACTEUR_TRANSPORT_PLAINE,
        "colline": FACTEUR_TRANSPORT_COLLINE,
        "marais": FACTEUR_TRANSPORT_MARAIS,
        "montagne": FACTEUR_TRANSPORT_MONTAGNE,
        "haute_montagne": FACTEUR_TRANSPORT_HAUTE_MONTAGNE,
    }
```

Interdit dans `engine.py` : `_constantes.FACTEUR_TRANSPORT_PLAINE` et les
quatre autres. Autorisé : `_constantes.facteurs_transport_par_relief()`.

**Sans carte, la capacité reste inchangée.** `_MondeEpreuve` n'a pas
`world.carte` (absent ou vide). Dans ce cas, le facteur de terrain vaut 1 :
la capacité d'une arête est la capacité de base, exactement comme aujourd'hui.
C'est le même repli que le lot 033 pour la production : « sans carte, le
chemin unitaire historique reste inchangé ». Ce n'est pas un repli vers
`plaine` sur un monde chargé.

**Sur un monde chargé** (`world.carte` non vide) : une classe de relief
absente ou inconnue sur une arête entre deux cellules du monde lève, avec
les deux `cell_id` et la valeur. Pas de facteur neutre, pas de crash du
monde d'épreuve.

**Composition avec une capacité de base dérivée.** Ce lot multiplie la capacité
de base par un facteur de terrain, quelle que soit la façon dont cette base est
obtenue. Si le lot 043 est déjà fusionné, la base n'est plus la constante plate
mais le débit dérivé de la longueur de frontière : le facteur de terrain la
multiplie, et la constante plate supprimée par 043 **n'est pas réintroduite**.
L'ordre du produit ne change rien, et il n'existe toujours qu'un seul endroit où
la capacité se calcule.

L'ordre recommandé reste **040 avant 043** : ce lot est plus simple à prouver
sur une base constante.

**2. Rien d'autre ne change.** Les cinq règles du commerce — instantané pris
avant tout mouvement, un kilogramme par arête et par tick, allocation
proportionnelle dans l'ordre croissant des identifiants, écrêtage côté receveur,
dette jamais touchée — sont conservées mot pour mot. Seule la valeur du plafond
par arête change.

**Ce que cette règle produit sans être écrite.** Une vallée fertile cernée de
montagnes cesse de secourir ses voisines aussi facilement ; une plaine continue
de faire circuler ses surplus. La géographie devient une contrainte logistique
au lieu d'un décor, et personne n'a codé « les montagnes isolent ».

**Une classe de relief inconnue ou absente sur une arête d'un monde chargé**
est une donnée invalide : lever, avec les deux `cell_id` et la valeur. Ne
pas deviner, ne pas rabattre vers `plaine`. Sans carte, voir plus haut :
facteur 1, capacité de base inchangée.

## Source de vérité et raccord au moteur

Le relief vient **uniquement** de `world.carte[cell_id]["relief"]`, la même
source que la production. Le moteur ne duplique pas cette classe et ne lit pas
l'outil de fabrication de la carte.

La capacité d'une arête se calcule à **un seul endroit** dans le maillon
commerce, et ce maillon reste unique.

Les arêtes dont l'un des bouts n'est pas une cellule du monde — la carte en
porte, elles relient la terre à la mer — continuent d'être **ignorées**,
exactement comme aujourd'hui. Le transport maritime n'existe pas encore ; ce lot
ne l'invente pas.

La carte atteint le maillon commerce par la voie explicite mise en place au lot
034 — jamais par une variable de module.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/tests/test_commerce.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible ; les assertions déjà présentes restent
  inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/040-franchir-une-montagne-coute/deliverables/manifest.json` ;
- `harness/queue/briefs/040-franchir-une-montagne-coute/deliverables/generator-log.md` ;
- `harness/queue/briefs/040-franchir-une-montagne-coute/deliverables/measure_040.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/__main__.py`, ni
`sim/aggregation.py`, ni `sim/tests/test_survie.py`, ni `sim/tests/test_monde.py`,
ni la carte figée, ni le visualiseur, ni l'outil de fabrication de la carte, ni
ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Cinq facteurs effectifs, dérivés de la carte

Sur un micro-monde déterministe où une même source approvisionne des voisines
identiques par des arêtes de relief différent, la quantité transférée suit
strictement l'ordre des cinq facteurs. Les cinq classes sont **dérivées de la
carte figée** ; si l'une manque, le contrôle échoue au lieu de la sauter.

**Le rouge est prouvé avant la correction** : sur le SHA de base, ces cinq
arêtes transportent la même quantité.

### SC2 — C'est le bout le plus difficile qui commande

Une arête plaine–haute montagne transporte exactement autant qu'une arête haute
montagne–haute montagne, et strictement moins qu'une arête plaine–plaine. Le
contrôle essaie les deux ordres d'extrémités et obtient le même résultat : la
capacité ne dépend pas du sens de lecture de l'arête.

### SC3 — Effet visible et déterministe sur le monde réel

Deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
strictement identiques entre elles, et le champ `kg_transportes` est
**strictement inférieur** à celui rejoué sur le SHA de base par la même
commande.

Le sens de l'inégalité découle des facteurs, tous inférieurs ou égaux à un,
fixés avant l'exécution. Il ne doit pas être obtenu en ajustant un facteur après
avoir vu la mesure.

Le mesureur archive la sortie de base **avant** l'édition et la relit ; il ne
recopie aucun nombre du présent brief.

### SC4 — La masse se conserve toujours

Sur un micro-monde, la somme des stocks avant et après le maillon commerce est
identique. Réduire une capacité déplace moins de kilogrammes ; cela n'en détruit
aucun. Le contrôle existant qui porte cet invariant reste vert **sans être
modifié**.

### SC5 — Le monde ne meurt pas

Les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
**sans modification de ce fichier**, y compris le plancher : couper les convois
de montagne ne doit pas éteindre le monde.

Une vérification supplémentaire est faite à un horizon cinq fois plus long que
celui du contrôle existant, pour montrer que l'effet se stabilise au lieu de
s'aggraver indéfiniment.

### SC6 — Le refus de l'inconnu, sur un monde chargé

Une **carte en mémoire** dont la classe de relief d'une cellule reliée par une
arête est remplacée par une valeur inconnue provoque l'erreur explicite
exigée, avec les deux `cell_id` et la valeur. Aucun repli silencieux n'est
admis. Ce contrôle se joue sur un monde **chargé** (`world.carte` non vide),
pas sur `_MondeEpreuve`.

Un tick sur un monde sans carte — le monde d'épreuve de
`test_write_coverage.py`, ou un `World` construit sans `carte` — **ne lève
pas**. La capacité reste la capacité de base.

### SC7 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_conservation_masse_transport`, `test_invariance_ordre_aretes`,
  `test_recepteur_pas_sur_livre` et `test_kg_transportes_egal_deltas_positifs`
  restent verts **sans être modifiés** ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- il n'y a toujours qu'un seul maillon commerce dans `sim/` ;
- aucun nom `FACTEUR_TRANSPORT_*` n'apparaît comme attribut lu dans
  `sim/engine.py` — le motif 033 tient.

## Compteurs exigés

Le mesureur `deliverables/measure_040.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `classes_relief_carte` | agrégation Python des valeurs `relief` rendues par `World.lire_carte()` | nombre de classes distinctes réellement mesurées |
| `aretes_entre_deux_cellules` | parcours de l'adjacence du monde chargé | nombre total d'arêtes réellement présentes dans la carte |
| `aretes_ignorees_hors_monde` | même parcours | `aretes_entre_deux_cellules` plus les ignorées |
| `aretes_par_facteur_limitant` | même parcours, facteur minimal des deux bouts | `aretes_entre_deux_cellules` |
| `classes_avec_capacite_effective` | transferts mesurés sur le micro-monde, une arête par classe dérivée | `classes_relief_carte` |
| `capacite_independante_du_sens` | même arête essayée dans les deux ordres d'extrémités | nombre d'arêtes réellement essayées |
| `kg_transportes_avant` | sortie de base rejouée et archivée avant édition | nombre d'exécutions réellement lancées |
| `kg_transportes_apres` | même commande après changement | nombre d'exécutions réellement lancées |
| `ecart_de_masse_micro_monde` | somme des stocks avant et après le maillon | nombre de cellules réellement sommées |
| `reliefs_inconnus_refuses` | mutations en mémoire vers une valeur absente de l'ensemble dérivé | nombre de mutations réellement exécutées |
| `fraction_survie_horizon_long` | monde réel joué à cinq fois l'horizon du contrôle existant | population de départ réellement mesurée |
| `noms_de_constantes_transport_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de noms du motif 033 réellement cherchés |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`ecart_de_masse_micro_monde` doit valoir **0**, et ce zéro est une mesure réelle.
La sentinelle « non calculé » du projet est `-1`, jamais `0`.
`kg_transportes_apres` doit être strictement inférieur à `kg_transportes_avant`,
et `fraction_survie_horizon_long` strictement positive.
`noms_de_constantes_transport_dans_engine` doit valoir **0**.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_040.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- le transport maritime, les arêtes terre–mer, les ports, les fleuves ;
- la longueur d'une arête, déjà présente dans la carte mais laissée de côté ici ;
- les routes, les ponts, l'investissement dans une infrastructure ;
- le coût du transport en nourriture, en temps ou en pertes ;
- la production, la consommation, la mortalité, la natalité ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
