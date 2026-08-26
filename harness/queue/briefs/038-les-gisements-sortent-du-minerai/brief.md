# Brief 038 — Les gisements sortent enfin quelque chose

**Authored**: 2026-08-26T09:40:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire **extraire** : une cellule qui porte un gisement nommé de la carte produit,
à chaque tick, des kilogrammes de la ressource de ce gisement, et les range dans
son panier.

C'est la couche `gisements` de la carte qui entre enfin dans le jeu.

Ce lot ne transporte rien — le minerai extrait reste où il est produit. Il ne
consomme rien, ne vend rien, ne fabrique rien, et ne détourne personne de
l'agriculture.

## Dépendance

**Ce lot suppose le lot 037 fusionné, avec un `World.to_dict()` qui porte le
panier.** Sans panier de marchandises, il faudrait inventer un champ de cas
particulier par ressource. Sans panier dans `to_dict()`, la sonde des couches
ne verrait pas l'extraction (l'empreinte ignore alors tout stock autre que la
nourriture) et SC4 serait infaisable. Si `Cell` ne porte pas encore de panier,
ou si `to_dict()` ne l'expose pas, ce lot est bloqué, pas à adapter.

## Fondement dans le modèle

`sim/MODELE.md`, § « Ce que le moteur ne fait pas encore » — la mesure qui dit
que la couche gisements n'est pas consommée. Aucune section ne décrit encore
l'extraction : ce lot est le premier à en poser une. Si cette section a changé
depuis la rédaction de ce brief, le relire avant de le lancer.

`sim/MODELE.md` est hors périmètre de ce lot. La mise à jour de la section
citée après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -c "from sim.world import World; from sim.snapshot_export import build_snapshot_document; print(build_snapshot_document(World.charger(0),0,0)['couches'])"
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si la sonde déclare déjà
`gisements.utilisee_par_le_moteur == true`, le tick lit déjà cette couche et ce
brief n'a plus d'objet.

## Règle du monde

**Fidélité mixte, et la distinction compte.**

- Les gisements eux-mêmes — leur existence, leur emplacement, leur ressource —
  sont de **niveau 1** : ils sont dans la carte figée, nommés, et ils sont là où
  l'histoire les met. Ce lot ne les invente pas et n'en ajoute aucun.
- Le **débit** d'extraction, lui, est de **niveau 2** : plausible, généré,
  jamais sourcé. Un tonnage local surprenant n'est pas un défaut historique et
  n'ouvre ni correctif, ni brief.

Le mécanisme, en trois pas.

**1. Ce qu'on extrait vient de la carte.** Chaque enregistrement de gisement de
`world.carte[cell_id]["gisements"]` porte une `ressource` et une `richesse`. La
ressource devient le nom de la marchandise rangée dans le panier ; l'ensemble des
noms de marchandises minières est donc **dérivé de la carte**, jamais écrit dans
le code.

**2. Le débit dépend de la richesse et des bras disponibles.**

```
extraction_kg = population × EXTRACTION_KG_PAR_HABITANT_PAR_TICK × facteur_richesse
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `EXTRACTION_KG_PAR_HABITANT_PAR_TICK` | 0.02 | kilogrammes extraits par habitant et par tick sur un gisement notable — niveau 2 |
| `FACTEUR_RICHESSE_MAJEURE` | 2.0 | un gisement majeur rend le double d'un notable — niveau 2 |
| `FACTEUR_RICHESSE_NOTABLE` | 1.0 | la référence — niveau 2 |
| `FACTEUR_RICHESSE_MINEURE` | 0.4 | un gisement mineur rend moins de la moitié — niveau 2 |

Ces constantes vivent dans `sim/constants.py`, avec un commentaire disant qu'il
s'agit d'ordres de grandeur plausibles de niveau 2. Aucun nombre de réglage
n'est écrit dans une fonction du moteur.

**Motif 033 — constantes invisibles pour le monde d'épreuve.**
`_MondeEpreuve` de `sim/tests/test_write_coverage.py` n'a ni carte ni gisement.
`test_chaque_constante_du_moteur_change_le_monde` ne dérive son dénominateur
que des noms d'attributs **présents dans** `sim/engine.py`. Un nom de constante
d'extraction écrit dans `engine.py` y figurerait, ne bougerait pas l'empreinte
(nourriture, population, faim, dette), et le contrôle — hors périmètre —
rougirait.

Donc : les constantes de ce lot (`EXTRACTION_KG_PAR_HABITANT_PAR_TICK`,
`FACTEUR_RICHESSE_MAJEURE`, `FACTEUR_RICHESSE_NOTABLE`,
`FACTEUR_RICHESSE_MINEURE`) ne sont **pas** lues par leur nom dans
`sim/engine.py`. Elles vivent dans `sim/constants.py` et le moteur les
consulte via une **table relue à chaque appel**, le même motif que
`facteurs_production_par_relief()` du lot 033 :

```
def facteurs_richesse_extraction() -> dict[str, float]:
    return {
        "majeure": FACTEUR_RICHESSE_MAJEURE,
        "notable": FACTEUR_RICHESSE_NOTABLE,
        "mineure": FACTEUR_RICHESSE_MINEURE,
    }
```

Le débit se calcule en appelant cette table, et le kilogramme par habitant
passe de même par une fonction de `constants.py` qui relit
`EXTRACTION_KG_PAR_HABITANT_PAR_TICK` à chaque appel. Interdit dans
`engine.py` : `_constantes.FACTEUR_RICHESSE_MAJEURE` ou
`_constantes.EXTRACTION_KG_PAR_HABITANT_PAR_TICK`. Autorisé :
`_constantes.facteurs_richesse_extraction()` et l'équivalent pour le débit.
`test_aucune_constante_terminale` continue de voir ces noms : la table les
lit.

**La dépendance à la population n'est pas décorative** : c'est elle qui fait
qu'une cellule qui meurt de faim cesse d'extraire, sans qu'aucune règle ne le
dise. Le minerai a une origine physique — des gens qui creusent — et non un
robinet attaché à une case de la carte.

**3. Plusieurs gisements sur une cellule s'additionnent, ressource par
ressource.** Une cellule qui porterait deux gisements de fer produit la somme des
deux ; une cellule qui porterait fer et sel range les deux séparément dans son
panier. Rien ne se mélange, rien ne s'écrase.

**Une richesse présente et hors des trois classes est une donnée invalide** :
lever une erreur qui nomme le `cell_id`, l'identifiant du gisement et la
valeur fautive. Ne pas deviner, ne pas rabattre silencieusement vers
`notable`.

**Un gisement sans clé `ressource` ou sans clé `richesse` n'est pas un
gisement** : l'ignorer, ne pas lever. C'est le cas de l'enregistrement que
la sonde de `sim/snapshot_export.py` substitue
(`{"nature": "sonde", "classe": "sonde"}`). Lever dessus ferait exploser
`build_snapshot_document`, que ce lot n'a pas le droit de modifier. La sonde
voit quand même l'extraction : elle **remplace** la liste des vrais gisements,
donc le panier du monde altéré ne contient plus les ressources de la carte, et
`to_dict()` — qui porte le panier depuis le 037 — diffère.

Une ressource inconnue, en revanche, est **normale** : c'est simplement une
marchandise de plus, et le panier l'accepte sans code nouveau — c'est ce que
le lot 037 a rendu possible.

## Source de vérité et raccord au moteur

Les gisements viennent **uniquement** de `world.carte[cell_id]["gisements"]`. Le
moteur ne duplique pas cette liste dans une seconde base de données, ne lit pas
l'outil de fabrication de la carte, et n'écrit jamais dans la carte.

L'extraction est un **maillon du tick**, pas une variante de la production
alimentaire : elle ne passe pas par la formule de production agricole, qui reste
unique et inchangée.

Sa place dans la chaîne est la première, à côté de la production : ce qui est
extrait ce tick est disponible ce tick. L'ordre devient extraction et production
→ commerce → consommation → faim → mortalité → natalité.

La carte atteint ce maillon par la voie explicite mise en place au lot 034 —
jamais par une variable de module.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/tests/test_monde.py`, uniquement pour **ajouter** les cas qui protègent
  cette règle visible ; les assertions déjà présentes restent inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/038-les-gisements-sortent-du-minerai/deliverables/manifest.json` ;
- `harness/queue/briefs/038-les-gisements-sortent-du-minerai/deliverables/generator-log.md` ;
- `harness/queue/briefs/038-les-gisements-sortent-du-minerai/deliverables/measure_038.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/tests/test_survie.py`,
ni `sim/aggregation.py`, ni la carte figée, ni le visualiseur, ni l'outil de
fabrication de la carte, ni ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Chaque gisement de la carte produit sa ressource

Après un tick sur le monde chargé, le nombre de cellules dont le panier contient
au moins une marchandise minière est **égal** au nombre de cellules que la carte
déclare porteuses d'au moins un gisement. Les deux nombres sont dérivés : l'un
du panier, l'autre de la carte. Un échantillon vide fait échouer le contrôle.

L'ensemble des ressources extraites est **égal** à l'ensemble des ressources
déclarées par la carte — ni plus, ni moins.

**Le rouge est prouvé avant la correction** : sur le SHA de base, aucun panier
ne contient de marchandise minière après un tick.

### SC2 — La richesse ordonne les débits

À population et ressource égales, la quantité extraite suit strictement l'ordre
des trois facteurs de richesse : majeure au-dessus de notable, notable au-dessus
de mineure. Les trois classes sont dérivées de la carte ; si l'une d'elles n'y
figure pas, le contrôle échoue au lieu de la sauter.

### SC3 — Sans bras, pas de minerai

Une cellule porteuse d'un gisement dont la population est nulle n'extrait rien.
Ce zéro est une **mesure réelle** : le maillon a été joué et a compté zéro. La
sentinelle « non calculé » du projet est `-1`, jamais `0`.

Sur le monde réel joué longtemps, la quantité extraite par tick par une cellule
minière affamée décroît quand sa population décroît. Aucune règle ne le dit :
cela découle de la formule.

### SC4 — La couche gisements devient réellement consommée

Après le changement, `build_snapshot_document(World.charger(0), 0, 0)` rend
`couches.gisements.utilisee_par_le_moteur == true`, sans que
`sim/snapshot_export.py` soit modifié et sans qu'aucune déclaration manuelle
soit ajoutée ou retournée.

C'est faisable parce que `to_dict()` porte le panier : extraire du minerai
change l'empreinte même si la nourriture ne bouge pas (SC5). La sonde
substitue un enregistrement sans `ressource` ni `richesse`, ignoré (SC6) ;
les vrais gisements disparaissent du monde altéré, le panier diffère, la
sonde passe à vrai.

L'état des deux autres couches est celui que la sonde mesure au moment du lot ;
ce brief ne demande de le changer ni dans un sens ni dans l'autre.

### SC5 — Le minerai n'est pas de la nourriture

Le stock de nourriture d'une cellule minière est **exactement** celui qu'elle
aurait sans gisement, à graine et à tick égaux. Aucun kilogramme de minerai
n'entre dans la consommation, ne rembourse une dette alimentaire, ni ne nourrit
personne.

Un contrôle compare, sur le monde réel, le stock de nourriture cellule par
cellule avec celui rejoué sur le SHA de base. Le nombre de cellules comparées est
dérivé du monde chargé.

C'est le contrôle qui empêche la faute la plus coûteuse de ce lot : une
marchandise nouvelle qui se retrouverait comptée comme de la nourriture ferait
disparaître les famines sans que rien ne rougisse.

### SC6 — Le refus de l'invalide, et l'ignorance de l'incomplet

Une carte en mémoire dont la `richesse` d'un gisement est remplacée par une
valeur inconnue — la clé est **présente**, la valeur hors des trois classes —
provoque l'erreur explicite exigée, avec le `cell_id`, l'identifiant du
gisement et la valeur.

Une carte dont un gisement **n'a plus** la clé `ressource` ou la clé
`richesse` **ne provoque aucune erreur** : cet enregistrement est ignoré, les
autres gisements de la même cellule s'extraient normalement.

Une carte dont la `ressource` d'un gisement est remplacée par un nom inconnu
**ne provoque aucune erreur** : la marchandise correspondante apparaît
simplement dans le panier.

### SC7 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
  **sans modification de ce fichier** : le plafond de survie ne parle que de
  nourriture, et l'extraction n'en produit pas ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques entre elles ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- la formule de production alimentaire reste unique et inchangée ;
- aucun des noms `EXTRACTION_KG_PAR_HABITANT_PAR_TICK`,
  `FACTEUR_RICHESSE_MAJEURE`, `FACTEUR_RICHESSE_NOTABLE`,
  `FACTEUR_RICHESSE_MINEURE` n'apparaît comme attribut lu dans
  `sim/engine.py` — le motif 033 tient.

## Compteurs exigés

Le mesureur `deliverables/measure_038.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `cellules_avec_gisement_carte` | agrégation Python des `gisements` rendus par `World.lire_carte()` | nombre total de cellules réellement mesurées |
| `gisements_declares` | même agrégation, gisements comptés un par un | `cellules_avec_gisement_carte` |
| `ressources_distinctes_carte` | même agrégation | `gisements_declares` |
| `classes_de_richesse_carte` | même agrégation | `gisements_declares` |
| `cellules_extractrices_apres_un_tick` | paniers du monde chargé après un tick | `cellules_avec_gisement_carte` |
| `ressources_distinctes_extraites` | mêmes paniers | `ressources_distinctes_carte` |
| `richesses_ordonnees` | extractions à population et ressource égales, une par classe dérivée | `classes_de_richesse_carte` |
| `extraction_population_nulle` | cellule minière à population nulle, un tick joué | nombre de ticks réellement joués |
| `cellules_dont_la_nourriture_a_change` | comparaison cellule par cellule avec le monde rejoué sur le SHA de base | nombre de cellules réellement chargées |
| `richesses_inconnues_refusees` | mutations en mémoire vers une richesse absente de l'ensemble dérivé | nombre de mutations réellement exécutées |
| `gisements_incomplets_ignores` | mutations en mémoire retirant `ressource` ou `richesse` ; ticks joués sans erreur | nombre de mutations réellement exécutées |
| `ressources_inconnues_acceptees` | mutations en mémoire vers une ressource absente de l'ensemble dérivé | nombre de mutations réellement exécutées |
| `noms_de_constantes_extraction_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de noms du motif 033 réellement cherchés |
| `couches_consommees_par_tick` | sonde existante du snapshot | nombre de couches déclarées dans le snapshot |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`cellules_dont_la_nourriture_a_change` et `extraction_population_nulle` doivent
valoir **0**, et ces zéros sont des mesures réelles. La sentinelle « non
calculé » du projet est `-1`, jamais `0`.
`cellules_extractrices_apres_un_tick` doit égaler `cellules_avec_gisement_carte`,
et `ressources_distinctes_extraites` doit égaler `ressources_distinctes_carte`.
`noms_de_constantes_extraction_dans_engine` doit valoir **0**.
`gisements_incomplets_ignores` doit égaler le nombre de mutations réellement
exécutées : chacune a été ignorée, aucune n'a levé.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_038.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les comparaisons
avant/après passent par la référence Git du SHA de base, pas par une copie
`.orig` fabriquée après coup.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- le transport, la vente, le prix, la transformation ou la consommation du
  minerai ;
- l'épuisement d'un gisement, la profondeur, la qualité du filon ;
- toute division du travail — personne ne cesse de cultiver dans ce lot ;
- l'ajout ou le déplacement d'un gisement dans la carte ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
