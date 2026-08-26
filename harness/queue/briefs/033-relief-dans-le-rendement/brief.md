# Brief 033 — Le relief joue dans le rendement alimentaire

**Authored**: 2026-08-25T18:12:50Z  
**Author**: Hermes  
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire jouer le relief de la carte figée dans la production alimentaire du tick.
À surface et rendement aléatoire identiques, une cellule de haute montagne ne
produit plus autant qu'une plaine.

Ce lot ne fait jouer ni le climat ni les gisements. Il ne refait pas la carte.

## État de départ mesuré

Sur `master` au SHA `448aa2a6c733331aebfb031e217a5c68f4c02c07` :

- la mesure bornée `World.lire_carte()` trouve 596 cellules : 322 `plaine`,
  162 `colline`, 77 `montagne`, 15 `haute_montagne`, 20 `marais` ; aucun
  agent ne lit ni ne recopie l'artefact brut de carte ;
- `.venv/bin/python -m pytest sim/tests/ -q` donne `62 passed` ;
- `.venv/bin/python -m sim --ticks 20 --seed 0 --json` donne notamment :
  population finale `66649442`, `3` cellules affamées, `800.0` kg transportés ;
- la sonde dérivée du snapshot déclare aujourd'hui le relief non utilisé par le
  moteur.

Ces valeurs sont une référence avant changement, pas une cible à recopier.

## Règle du monde

Le facteur de rendement lié au relief est un paramètre de **fidélité niveau 2** :
plausible, généré, jamais sourcé. Une valeur locale surprenante n'est pas un
défaut historique.

Les facteurs nominaux sont :

| classe de relief | facteur de production |
|---|---:|
| `plaine` | 1.00 |
| `colline` | 0.80 |
| `montagne` | 0.45 |
| `haute_montagne` | 0.15 |
| `marais` | 0.50 |

Ils vivent sous des constantes nommées dans `sim/constants.py`, avec un
commentaire expliquant qu'il s'agit d'ordres de grandeur plausibles de niveau 2.
Aucun nombre de réglage n'est écrit dans une fonction du moteur.

## Source de vérité et raccord au moteur

La classe de relief provient uniquement de
`world.carte[cell_id]["relief"]`, alimentée par le chargeur de carte existant.
Le moteur ne duplique pas cette classe dans une seconde base de données et ne
lit pas l'outil de fabrication de la carte. Les agents interrogent cette carte
par une mesure Python bornée (`World.lire_carte()` puis agrégation) ; ils ne
lisent jamais l'artefact brut dans leur contexte.

La formule qui applique le facteur reste unique dans `production_kg()` de
`sim/engine.py`. Le raccord nécessaire entre `tick(world, rng)`,
`_apply_production` et `production_kg()` peut évoluer dans ce même fichier,
mais aucune deuxième formule de production n'est créée.

Les appels unitaires historiques qui construisent une `Cell` sans `World`
doivent rester utilisables sans modifier leurs tests. Le chemin réel du tick,
lui, doit toujours fournir la classe issue de `world.carte` : aucun facteur
neutre implicite ne doit masquer une classe absente sur une cellule du monde
chargé.

Une classe manquante ou inconnue dans le chemin réel du tick est une donnée
invalide : lever une erreur qui nomme le `cell_id` et la valeur fautive. Ne pas
deviner, ne pas rabattre silencieusement vers `plaine`.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/tests/test_monde.py`, uniquement pour **ajouter** les cas qui protègent
  cette règle visible ; les assertions déjà présentes restent inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/033-relief-dans-le-rendement/deliverables/manifest.json` ;
- `harness/queue/briefs/033-relief-dans-le-rendement/deliverables/generator-log.md` ;
- `harness/queue/briefs/033-relief-dans-le-rendement/deliverables/measure_033.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/tests/test_survie.py`,
ni la carte figée, ni le visualiseur, ni l'outil de fabrication de la carte,
ni le brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Une seule formule, cinq facteurs effectifs

`production_kg()` applique exactement un facteur correspondant à chacune des
cinq classes fermées de la carte. Sur cinq appels de même surface et de même
rendement aléatoire, les rapports de production suivent les constantes du
tableau. Les cinq classes testées sont dérivées de la carte ; un échantillon
vide échoue.

Le cas rouge est prouvé avant la correction : sur `master`, ces cinq appels ne
se distinguent pas par le relief.

### SC2 — Le tick lit la carte et refuse l'inconnu

Un tick sur un monde chargé lit la classe dans `world.carte` pour chaque
cellule. Une carte en mémoire dont une classe est remplacée par une valeur
inconnue provoque l'erreur explicite exigée, avec le `cell_id` et la valeur.
Aucun repli silencieux n'est admis.

### SC3 — La couche relief devient réellement consommée

Après le changement, `build_snapshot_document(World.charger(0), 0, 0)` rend :

- `couches.relief.utilisee_par_le_moteur == true` ;
- `couches.climat.utilisee_par_le_moteur == false` ;
- `couches.gisements.utilisee_par_le_moteur == false`.

Ces trois valeurs restent celles de la sonde existante ; aucune déclaration
manuelle n'est ajoutée ou retournée.

### SC4 — Effet visible et déterministe

Deux exécutions de `.venv/bin/python -m sim --ticks 20 --seed 0 --json` sont
strictement identiques entre elles et différentes de la référence avant
changement sur au moins un des champs dérivés suivants :
`population_arrivee`, `cellules_affamees`, `kg_transportes`,
`stock_kg_arrivee`.

Le changement doit être matériel : `cellules_affamees` après changement est
strictement supérieur à la même grandeur rejouée sur le SHA de base par la
même commande. Le mesureur archive cette sortie de base avant l'édition et la
relit ; il ne recopie aucun nombre du présent brief. Ce critère découle des
facteurs nominaux fixés avant l'exécution ; il ne doit pas être obtenu en
ajustant un test ou une tolérance après mesure.

### SC5 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- les trois propriétés de régime dans `sim/tests/test_survie.py` restent vertes
  sans modification de ce fichier ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde` et
  `test_aucune_constante_terminale` restent verts ;
- aucune deuxième formule de production alimentaire n'apparaît dans `sim/`.

## Compteurs exigés

Le mesureur `deliverables/measure_033.py` reconstruit les compteurs ; il ne
porte pas leurs résultats en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `classes_relief_carte` | agrégation Python des valeurs `relief` rendues par `World.lire_carte()` | nombre de classes distinctes réellement mesurées |
| `cellules_par_classe` | même agrégation bornée, sans lecture de l'artefact par un agent | nombre total de cellules réellement mesurées |
| `classes_avec_facteur_effectif` | appels de production à surface et rendement identiques, une fois par classe dérivée | `classes_relief_carte` |
| `classes_inconnues_refusees` | une mutation en mémoire vers une valeur absente de l'ensemble dérivé | nombre de mutations réellement exécutées |
| `couches_consommees_par_tick` | sonde existante du snapshot | nombre de couches déclarées dans le snapshot |
| `sorties_cli_deterministes` | deux exécutions à 20 ticks, graine 0 | nombre d'exécutions réellement lancées |
| `champs_cli_modifies` | comparaison avec la sortie de base rejouée et archivée avant édition | nombre de champs dérivés réellement comparés |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

Aucun compteur d'affirmation réelle ne prend `-1` comme résultat final. Un zéro
mesuré reste possible seulement si la condition correspondante l'autorise ; il
ne remplace jamais « non calculé ».

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : rouge avant correction, fichiers
  modifiés, commandes jouées, résultats et limites ;
- `measure_033.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git,
pas une copie `.orig` inventée après coup.

## Hors périmètre

- climat, précipitations, température et gisements ;
- changement des données ou reconstruction de la carte ;
- nouvelle dataclass, champ persistant ou cache géographique parallèle ;
- modification des règles de survie, du commerce, de la consommation ou de la
  mortalité ;
- calibration d'un test existant après observation ;
- modification du snapshot pour forcer son indicateur ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.