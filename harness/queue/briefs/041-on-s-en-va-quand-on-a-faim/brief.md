# Brief 041 — On s'en va quand on a faim

**Authored**: 2026-08-26T10:10:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, un champ ajouté à une entité existante, sans migration de données.

## But unique

Ajouter au tick un maillon de **migration** : une part des habitants d'une
cellule qui a manqué de nourriture ce tick part vers une cellule voisine qui,
elle, en avait de reste.

Aujourd'hui, un habitant naît, mange et meurt là où le monde l'a posé. Il ne
peut pas partir, même quand la cellule d'à côté regorge de grain et que la
sienne est vide.

Ce lot ne fait pas déménager de marchandises avec les gens, ne crée aucune
ville, et ne touche ni à la production, ni au commerce, ni à la mortalité.

## Dépendance

Aucune dépendance dure. Ce lot peut être lancé dès que le lot 034 est fusionné,
sans attendre les lots 035 à 040.

## Fondement dans le modèle

`sim/MODELE.md`, § « Ce que veut dire “affamée” » — la pénurie du tick, qui est
la cause du départ — et § « Le report de la fraction de mortalité », dont ce
lot reprend le mécanisme. Si l'une de ces sections a changé depuis la rédaction
de ce brief, le relire avant de le lancer.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
grep -rn "migration\|migrant" sim/
.venv/bin/python -m pytest sim/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si le tick comporte déjà un
maillon qui déplace des habitants d'une cellule à une autre, il n'y a rien à
faire ici.

## Règle du monde

**Fidélité niveau 2** : la fraction de la population qui part quand elle a faim
est un ordre de grandeur plausible, généré, jamais sourcé. Un mouvement local
surprenant n'est pas un défaut historique et n'ouvre ni correctif, ni brief.

Le mécanisme, en quatre pas.

**1. On ne part que si l'on a manqué.** Le maillon s'applique à une cellule dont
la pénurie du tick est strictement positive. Ce n'est pas « si misère alors
migration » : ils ont eu faim, donc certains s'en vont. La cause est la même
grandeur que celle qui fait la faim et la dette, calculée une seule fois par le
maillon de consommation.

**2. On ne va que là où il reste à manger.** Les destinations d'une cellule sont
ses voisines par une arête d'adjacence dont le surplus du tick est strictement
positif. S'il n'y en a aucune, personne ne part : on ne s'exile pas au hasard.

**3. Combien partent.**

```
brut     = population × FRACTION_MIGRANTE_PAR_TICK + migration_remainder
partants = int(brut)
migration_remainder = brut - partants
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `FRACTION_MIGRANTE_PAR_TICK` | 0.01 | part de la population d'une cellule affamée qui s'en va en un tick — niveau 2 |

Le report de la fraction est là pour la même raison que pour la mortalité et la
natalité : sans lui, une cellule de cinquante habitants ne verrait **jamais**
partir personne, tandis que sa voisine de cinquante mille en verrait partir cinq
cents. L'immobilité par arrondi est le pendant exact de l'immortalité par
arrondi que `mortality_remainder` a été écrit pour supprimer.

Le champ `Cell.migration_remainder` conserve la fraction non appliquée.
Sentinelle `-1.0` = non calculé, jamais `0.0` (règle 8).

**4. Comment ils se répartissent.** Les partants d'une cellule se répartissent
entre ses destinations **proportionnellement à leur surplus**, calculé sur un
instantané pris avant tout mouvement. Les habitants étant entiers, la
répartition proportionnelle laisse un reste : il est attribué par plus fort
reste, départagé par identifiant de cellule croissant. Aucune dépendance à
l'ordre de parcours des arêtes.

Comme pour le commerce, le mouvement est **atomique** : une personne ne traverse
qu'une arête par tick, et une cellule qui vient de recevoir des arrivants ne les
renvoie pas ailleurs le même tick.

**Les migrants ne portent rien.** Ils partent les mains vides : aucun kilogramme
ne change de cellule avec eux. C'est une simplification assumée, déclarée ici, et
non un oubli ; un lot ultérieur pourra leur donner un baluchon.

**5. La place dans la chaîne : à la fin.** Après la natalité. Une cellule est
d'abord nourrie, affamée, décimée et repeuplée, puis ceux qui restent décident de
partir. L'ordre du tick devient : extraction et production → commerce →
consommation → faim → mortalité → natalité → **migration**.

## Source de vérité et raccord au moteur

Les conditions se lisent sur des grandeurs que le tick calcule déjà : la pénurie
retournée par le maillon de consommation, le stock et la population. Aucune
nouvelle lecture de la carte, aucune source nouvelle.

L'adjacence vient de `world.adjacency`, la même que le commerce. Les arêtes dont
l'un des bouts n'est pas une cellule du monde continuent d'être ignorées.

Le champ `migration_remainder` est ajouté à `Cell` avec sa sentinelle, amorcé au
chargement, et ajouté à la sérialisation canonique `World.to_dict()` — sans quoi
l'empreinte qui sert au déterminisme ignorerait une part de l'état du monde.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/model.py`, uniquement pour ajouter le champ `migration_remainder` et sa
  documentation ;
- `sim/world.py`, uniquement pour amorcer ce champ au chargement et l'inclure
  dans `to_dict()` ;
- `sim/tests/test_commerce.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible ; les assertions déjà présentes restent
  inchangées.

C'est bien `test_commerce.py` qui reçoit ces cas : il porte déjà les invariants
de déplacement entre cellules adjacentes — conservation de la masse, invariance
à l'ordre des arêtes, atomicité — et le dépôt n'ajoute pas un fichier de test par
lot.

Livrables du lot autorisés :

- `harness/queue/briefs/041-on-s-en-va-quand-on-a-faim/deliverables/manifest.json` ;
- `harness/queue/briefs/041-on-s-en-va-quand-on-a-faim/deliverables/generator-log.md` ;
- `harness/queue/briefs/041-on-s-en-va-quand-on-a-faim/deliverables/measure_041.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni
`sim/snapshot_export.py`, ni `sim/aggregation.py`, ni `sim/__main__.py`, ni
`sim/tests/test_survie.py`, ni `sim/tests/test_monde.py`, ni la carte figée, ni
le visualiseur, ni l'outil de fabrication de la carte, ni ce brief, ni sa
grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — La population totale ne change pas d'une personne

Sur un micro-monde déterministe, la somme des populations avant et après le
maillon de migration est **exactement** égale. La migration déplace, elle ne
crée ni ne détruit personne.

C'est l'invariant physique central du lot, l'équivalent pour les gens de la
conservation de la masse pour les kilogrammes. Un écart d'une seule personne
fait échouer le lot.

**Le rouge est prouvé avant la correction** : sur le SHA de base, aucun habitant
ne change de cellule, et le contrôle qui mesure un mouvement échoue.

### SC2 — On part d'une cellule affamée vers une cellule en surplus

Sur un micro-monde à trois cellules — une affamée, une voisine en surplus, une
témoin sans adjacence — la population de l'affamée décroît, celle de la voisine
croît d'autant, et le témoin ne bouge pas.

### SC3 — On ne part pas quand il n'y a nulle part où aller

Une cellule affamée dont aucune voisine n'a de surplus ne perd personne. Ce zéro
est une **mesure réelle** : le maillon a été joué et a compté zéro. La sentinelle
« non calculé » du projet est `-1`, jamais `0`.

Une cellule rassasiée ne perd personne non plus, même entourée de surplus.

### SC4 — Pas d'immobilité par arrondi

Une cellule affamée dont `population × fraction` est strictement inférieur à 1
finit par voir partir un habitant, en au plus `ceil(1 / (population × fraction))`
ticks affamés. Cette borne est **dérivée** de la constante et de la population de
l'échantillon, jamais recopiée.

Le rouge correspondant est prouvé : une migration sans report de fraction laisse
cette cellule immobile indéfiniment, et le contrôle échoue.

### SC5 — Le mouvement est atomique et déterministe

Une cellule qui reçoit des arrivants ne les renvoie pas ailleurs le même tick.
Le résultat ne dépend pas de l'ordre dans lequel les arêtes sont parcourues : le
contrôle rejoue le même micro-monde avec l'adjacence présentée dans l'ordre
inverse et obtient un état identique.

Deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
strictement identiques entre elles.

### SC6 — La sentinelle reste une sentinelle

`migration_remainder` vaut `-1.0` sur une `Cell` construite sans valeur, et
`0.0` sur une cellule d'un monde amorcé. Un contrôle distingue les deux cas et
échoue si l'un prend la valeur de l'autre.

### SC7 — Les gens bougent vraiment, sur le monde réel

Après un nombre de ticks dérivé, le nombre de cellules dont la population a
changé pour une autre raison que les naissances et les morts est **strictement
positif**. La mesure se fait en comparant deux exécutions du même monde, l'une
avec la fraction migrante nominale, l'autre avec la constante mise à zéro en
mémoire par le module — jamais lue par valeur.

Ce contrôle est la garde payée : un maillon de migration aveugle à sa propre
constante passerait tout le reste sans rien faire.

### SC8 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
  **sans modification de ce fichier** ;
- `test_all_dataclass_fields_have_write_and_read_sites` reste vert ;
- `test_conservation_masse_transport` et `test_invariance_ordre_aretes` restent
  verts sans être modifiés ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- aucune instruction `global` n'apparaît dans `sim/engine.py`.

## Compteurs exigés

Le mesureur `deliverables/measure_041.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `ecart_de_population_micro_monde` | somme des populations avant et après le maillon | nombre de cellules réellement sommées |
| `partants_sans_destination` | cellule affamée sans voisine en surplus, ticks joués | nombre de ticks réellement joués |
| `partants_depuis_cellule_rassasiee` | cellule rassasiée entourée de surplus, ticks joués | nombre de ticks réellement joués |
| `ticks_jusqu_au_premier_depart` | petite cellule affamée jouée jusqu'au premier départ | borne dérivée de la constante et de la population de l'échantillon |
| `renvois_le_meme_tick` | cellule receveuse instrumentée sur un tick | nombre d'arrivées réellement observées |
| `ordres_d_aretes_essayes` | même micro-monde joué dans deux ordres d'adjacence | nombre d'ordres réellement essayés |
| `cellules_deplacees_fraction_nulle` | monde réel, constante mise à zéro en mémoire | nombre de cellules réellement chargées |
| `cellules_deplacees_fraction_nominale` | monde réel, constante du module | nombre de cellules réellement chargées |
| `champs_cli_modifies` | comparaison avec la sortie de base rejouée et archivée avant édition | nombre de champs dérivés réellement comparés |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`ecart_de_population_micro_monde`, `partants_sans_destination`,
`partants_depuis_cellule_rassasiee`, `renvois_le_meme_tick` et
`cellules_deplacees_fraction_nulle` doivent valoir **0**, et ces zéros sont des
mesures réelles. La sentinelle « non calculé » du projet est `-1`, jamais `0`.
`cellules_deplacees_fraction_nominale` doit être strictement positif, sans quoi
SC7 n'est pas démontré.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : les deux rouges prouvés (SC1 et SC4),
  les fichiers modifiés, les commandes jouées, les résultats et les limites ;
- `measure_041.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- emporter des biens, un stock ou une dette en migrant ;
- la distance parcourue, la durée du voyage, la mortalité en route ;
- l'attachement au sol, la culture, la langue, la famille ;
- l'attraction d'une ville ou d'un salaire — rien de tout cela n'existe encore ;
- la production, le commerce, la consommation, la mortalité, la natalité ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
