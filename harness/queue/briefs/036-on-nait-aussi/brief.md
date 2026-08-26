# Brief 036 — On naît aussi : la population cesse de ne faire que mourir

**Authored**: 2026-08-26T09:20:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, un champ ajouté à une entité existante, sans migration de données.

## But unique

Ajouter au tick un maillon de **natalité** : une cellule qui a mangé sa ration
entière et qui ne traîne aucune dette alimentaire gagne des habitants.

Aujourd'hui le monde ne sait que décroître. Une cellule bien nourrie reste
exactement à son effectif de départ, pour toujours ; une cellule affamée
descend. Le monde entier est donc une courbe qui baisse, et aucun endroit ne
peut prospérer.

Ce lot ne fait pas migrer les habitants, ne crée aucune marchandise nouvelle et
ne touche ni au commerce, ni à la mortalité.

## Fondement dans le modèle

`sim/MODELE.md`, § « Le déficit alimentaire et la mortalité » et § « Le report
de la fraction de mortalité » — la formule dont ce lot est le symétrique, et la
raison du report de fraction. Si l'une de ces sections a changé depuis la
rédaction de ce brief, le relire avant de le lancer.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/ -q
grep -rn "natalit\|naissance" sim/
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si le tick comporte déjà un
maillon qui augmente `population`, il n'y a rien à faire ici.

## Règle du monde

**Fidélité niveau 2** : le taux de naissance est un ordre de grandeur plausible,
généré, jamais sourcé. Une démographie locale surprenante n'est pas un défaut
historique et n'ouvre ni correctif, ni brief.

Le mécanisme, en trois pas.

**1. On ne naît que dans une cellule rassasiée.** Le maillon de natalité
s'applique à une cellule si, **ce tick** :

- la pénurie retournée par la consommation est nulle — elle a mangé sa ration
  entière ; et
- sa dette alimentaire cumulée est nulle — elle ne rembourse rien ; et
- il lui reste des habitants.

Les trois conditions viennent de grandeurs que le moteur calcule déjà. Aucune
n'est une règle de gameplay : ce n'est pas « si prospérité alors +X % de
population », c'est « ils ont mangé, donc certains font des enfants ».

**2. La formule est celle de la mortalité, en sens inverse.**

```
brut         = population × NAISSANCES_PAR_HABITANT_PAR_TICK + natalite_remainder
naissances   = int(brut)
natalite_remainder = brut - naissances
population  += naissances
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `NAISSANCES_PAR_HABITANT_PAR_TICK` | 0.0002 | naissances par habitant et par tick, **sur les seuls ticks rassasiés** — niveau 2 |

Le taux est **conditionnel** : il ne s'applique pas tous les jours, mais
seulement les jours où la cellule a mangé son content. Son effet annuel réel est
donc inférieur à son produit par le nombre de jours de l'année, et d'autant plus
inférieur que la cellule est mal nourrie. C'est voulu, et c'est ce qui fait que
la démographie répond à la nourriture sans qu'aucune règle ne le dise.

**3. Le report de la fraction, pour la même raison que la mortalité.**
`int(population × taux)` vaut zéro dès que le produit est inférieur à un. Sans
report, une cellule de cent habitants ne connaîtrait **jamais** une naissance,
tandis que sa voisine de cent mille en aurait vingt par tick : la stérilité par
arrondi, exactement symétrique de l'immortalité par arrondi que
`mortality_remainder` a été écrit pour supprimer (règle payée, `sim/MODELE.md`).

Le champ `Cell.natalite_remainder` conserve la fraction non appliquée.
Sentinelle `-1.0` = non calculé, jamais `0.0` — un zéro est une mesure réelle
(règle 8).

**4. La place dans la chaîne : après la mortalité.** Une cellule qui meurt de
faim ce tick ne donne pas naissance le même tick : la faim est évaluée avant.
L'ordre du tick devient production → commerce → consommation → faim →
mortalité → **natalité**.

**Rien n'est créé de nourriture.** Les nouveaux habitants n'apportent aucun
stock : ils mangent dès le tick suivant, ce qui rapproche leur cellule de sa
limite. La croissance se paie donc elle-même, et c'est ce qui la borne.

## Source de vérité et raccord au moteur

Les trois conditions se lisent sur des grandeurs que le tick calcule déjà : la
pénurie retournée par le maillon de consommation, `food_deficit_kg` et
`population`. Aucune nouvelle lecture de la carte, aucune source nouvelle.

Il n'existe qu'**un seul** endroit dans `sim/` où `population` augmente.

Le champ `natalite_remainder` est ajouté à `Cell` avec sa sentinelle, et il est
ajouté à la sérialisation canonique `World.to_dict()` — sans quoi l'empreinte
qui sert au déterminisme et à la sonde des couches ignorerait une part de l'état
du monde.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/model.py`, uniquement pour ajouter le champ `natalite_remainder` et sa
  documentation ;
- `sim/world.py`, uniquement pour amorcer ce champ au chargement et l'inclure
  dans `to_dict()` ;
- `sim/tests/test_survie.py`, uniquement pour **ajouter** les cas qui protègent
  cette règle visible ; les assertions déjà présentes restent inchangées.

C'est bien `test_survie.py` qui reçoit ces cas : il porte déjà l'invariant
« le monde ne s'éteint pas et ne nourrit pas plus de monde qu'il ne produit », et
le dépôt n'ajoute pas un fichier de test par lot.

Livrables du lot autorisés :

- `harness/queue/briefs/036-on-nait-aussi/deliverables/manifest.json` ;
- `harness/queue/briefs/036-on-nait-aussi/deliverables/generator-log.md` ;
- `harness/queue/briefs/036-on-nait-aussi/deliverables/measure_036.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni
`sim/snapshot_export.py`, ni `sim/aggregation.py`, ni `sim/tests/test_monde.py`,
ni `sim/tests/test_commerce.py`, ni la carte figée, ni le visualiseur, ni
l'outil de fabrication de la carte, ni ce brief, ni sa grille, ni un
`verdict.md`.

## Conditions de succès

### SC1 — Une cellule rassasiée gagne des habitants

Sur un micro-monde déterministe construit à la main, une cellule sans pénurie et
sans dette voit sa population **strictement augmenter** au bout d'un nombre de
ticks dérivé du taux — au plus `ceil(1 / NAISSANCES_PAR_HABITANT_PAR_TICK)`
divisé par sa population, jamais un nombre de ticks écrit en dur.

**Le rouge est prouvé avant la correction** : sur le SHA de base, la même
cellule ne bouge pas d'un habitant, quel que soit le nombre de ticks.

### SC2 — Une cellule affamée n'en gagne aucun

Sur le même micro-monde, une cellule dont la consommation dépasse le stock
n'accroît jamais sa population, et sa fraction de natalité en attente ne
progresse pas. La faim ferme la porte, elle ne fait pas que la ralentir.

### SC3 — Pas de stérilité par arrondi

Une cellule dont `population × taux` est strictement inférieur à 1 finit par
gagner un habitant, en au plus `ceil(1 / (population × taux))` ticks rassasiés.
Cette borne est **dérivée** des constantes et de la population de l'échantillon,
jamais recopiée.

Le rouge correspondant est prouvé : une natalité sans report de fraction laisse
cette cellule immobile indéfiniment, et le contrôle échoue.

### SC4 — La sentinelle reste une sentinelle

`natalite_remainder` vaut `-1.0` sur une `Cell` construite sans valeur, et
`0.0` sur une cellule d'un monde amorcé — parce que ce zéro-là est une mesure :
aucune fraction n'est en attente au premier tick. Un contrôle distingue les deux
cas et échoue si l'un prend la valeur de l'autre.

### SC5 — Le monde ne nourrit toujours pas plus de monde qu'il ne produit

`test_le_monde_ne_meurt_pas_et_ne_nourrit_pas_plus_qu_il_ne_produit` reste vert
**sans être modifié**, à son horizon d'observation actuel.

La même vérification est faite à un horizon cinq fois plus long, pour montrer
que la croissance se borne d'elle-même au lieu de repousser le dépassement plus
loin. Le plafond employé est celui que le moteur dérive, jamais un nombre écrit.

C'est le contrôle central du lot : une natalité qui ferait dépasser ce plafond
fabriquerait des habitants que le monde ne peut pas nourrir.

### SC6 — La démographie répond à la nourriture

À nombre de ticks égal, la population finale du monde réel est **strictement
plus grande** avec un taux de natalité doublé qu'avec le taux nominal, et
strictement plus petite avec un taux nul. La constante est remplacée en mémoire
par le module, jamais lue par valeur.

Ce contrôle est la garde payée : un maillon de natalité aveugle à sa propre
constante passerait tout le reste sans rien faire.

### SC7 — Une région prospère apparaît

Sur le monde réel et à horizon long, le nombre de cellules dont la population
finale dépasse la population de départ est **strictement supérieur** à ce même
nombre rejoué sur le SHA de base. La mesure de base est archivée avant l'édition
et relue ; aucun nombre du présent brief n'est recopié.

### SC8 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_all_dataclass_fields_have_write_and_read_sites` reste vert : le champ
  ajouté a un site d'écriture et un site de lecture ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques, et différentes de la référence rejouée sur le SHA de
  base ;
- aucune instruction `global` n'apparaît dans `sim/engine.py`.

## Compteurs exigés

Le mesureur `deliverables/measure_036.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `sites_d_augmentation_de_population` | parcours de l'arbre syntaxique des modules de `sim/` hors tests | nombre de modules réellement parcourus |
| `ticks_jusqu_a_la_premiere_naissance` | micro-monde rassasié joué jusqu'à la première naissance | borne dérivée du taux et de la population de l'échantillon |
| `naissances_en_cellule_affamee` | micro-monde en pénurie, joué sur le même horizon | nombre de ticks réellement joués |
| `cellules_en_croissance_avant` | monde réel rejoué sur le SHA de base | nombre de cellules réellement chargées |
| `cellules_en_croissance_apres` | même mesure après changement | nombre de cellules réellement chargées |
| `fraction_survie_taux_nul` | monde réel, constante remplacée en mémoire | population de départ réellement mesurée |
| `fraction_survie_taux_nominal` | idem, constante du module | population de départ réellement mesurée |
| `fraction_survie_taux_double` | idem, constante doublée en mémoire | population de départ réellement mesurée |
| `plafond_derive` | `production_moyenne_kg_par_tick` sur le monde chargé | ration du monde de départ, mesurée |
| `champs_cli_modifies` | comparaison avec la sortie de base rejouée et archivée avant édition | nombre de champs dérivés réellement comparés |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`sites_d_augmentation_de_population` doit valoir **1**.
`naissances_en_cellule_affamee` doit valoir **0**, et ce zéro est une mesure
réelle : le mesureur a joué les ticks et compté. La sentinelle « non calculé »
du projet est `-1`, jamais `0`. Les trois fractions de survie doivent être
strictement ordonnées, sans quoi SC6 n'est pas démontré.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : les deux rouges prouvés (SC1 et SC3),
  les fichiers modifiés, les commandes jouées, les résultats et les limites ;
- `measure_036.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- la migration, le vieillissement, les familles, les âges, le sexe ;
- les marchandises autres que la nourriture, les gisements, le climat ;
- toute modification des règles de mortalité, de faim, de commerce ou de
  consommation existantes ;
- toute modification de la carte figée ou du snapshot ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
