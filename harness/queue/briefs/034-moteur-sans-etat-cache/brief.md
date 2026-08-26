# Brief 034 — La carte arrive au tick par les arguments, plus par une variable de module

**Authored**: 2026-08-26T09:00:00Z
**Revised**: 2026-08-26T11:00:00Z — correction des constats D2 et D4 de `brief-review`
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données, sans changement de comportement observable.

## But unique

**Le tick cesse de poser la carte figée dans la variable de module
`_carte_du_tick`.** Le maillon production reçoit la carte par ses arguments,
comme n'importe quelle autre donnée : plus aucune fonction de `sim/engine.py`
n'écrit un nom de module, et pendant un tick `_carte_du_tick` reste `None`.

Ce lot ne change rien au monde. C'est sa condition de succès principale : à
graine égale, le jeu doit rendre exactement les mêmes nombres avant et après.

Ce que ce lot ne fait **pas**, nommément :

- il ne **supprime pas** la variable `_carte_du_tick`, ni la lecture qu'en fait
  `production_kg()` quand aucune carte ne lui est passée. Cette lecture est le
  point d'entrée qu'emploie un test déjà vert (voir « Aucun test existant n'est
  modifié ») ; la retirer obligerait à réécrire ce test, ce qui est interdit. La
  variable survit, écrite par **personne** dans `sim/` ;
- il ne change ni la signature de `tick(world, rng)`, ni celle de
  `production_kg(cell, yield_factor)`, ni celle de
  `production_moyenne_kg_par_tick(world)`. Il **ajoute** une fonction, il n'en
  retouche aucune de l'extérieur ;
- il ne fait jouer ni le climat, ni les gisements, et n'ajoute aucun maillon au
  tick.

## Fondement dans le modèle

**Aucun.** Ce lot ne découle d'aucune affirmation de `sim/MODELE.md` : il ne
touche pas au monde, et l'identité du comportement est son seul critère. La
forme du raccord interne au moteur n'est décrite nulle part ailleurs qu'ici.

## État de départ mesuré

Sur `master` au SHA `8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1`.

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
grep -n "_carte_du_tick\|global " sim/engine.py sim/tests/test_monde.py
grep -n "production_kg" sim/engine.py sim/snapshot_export.py \
                        sim/tests/test_monde.py sim/tests/test_survie.py
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

1. **La présence n'est pas la fonction** (règle 7). Aujourd'hui le maillon
   production du tick n'a pas d'appel qu'on puisse lire : son résultat dépend
   d'une variable posée ailleurs, et deux appels aux mêmes arguments rendent
   deux nombres différents. Après ce lot, le chemin du tick est une fonction
   dont **tout** ce qui entre est dans les arguments. La variable de module
   survit pour le seul chemin unitaire historique, et plus aucune ligne de
   `sim/` ne l'écrit : pendant un tick réel, elle vaut `None`.
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
`world.carte[cell_id]["relief"]`, lue par `_facteur_relief_pour_cellule()`, qui
ne change pas. Rien d'autre ne change de source.

La forme du raccord n'est pas libre : elle est ce que le contrôle de SC2 mesure,
et elle est contrainte par deux tests existants qu'il est interdit de toucher.

**Le point d'entrée du tick.** `sim/engine.py` expose une fonction nouvelle :

```python
production_du_tick_kg(cell, yield_factor, carte)
```

C'est elle que `_apply_production()` appelle pour chaque cellule, et elle que
`production_moyenne_kg_par_tick()` appelle en lui passant `world.carte`. Elle
reçoit la carte par son troisième argument, obligatoire et positionnel. Elle ne
recalcule aucun kilogramme : elle appelle `production_kg(cell, yield_factor)` —
**avec deux arguments, par le nom de module, comme aujourd'hui** — et multiplie
le résultat par le facteur de relief tiré de la `carte` reçue. La formule des
kilogrammes reste unique, et reste dans `production_kg()`.

**Pourquoi la carte ne peut pas entrer dans `production_kg()` elle-même.**
`sim/tests/test_monde.py::test_la_consommation_des_couches_est_mesuree_pas_declaree`
remplace `engine.production_kg` par des fonctions à **deux** paramètres
(`production_qui_lit_le_climat`, `production_qui_lit_le_relief`) et fait jouer de
vrais ticks par-dessus. Un tick qui appellerait `production_kg` avec trois
arguments ferait éclater ce test, qu'il est interdit de modifier. Le maillon
production du tick est donc une fonction **de plus**, jamais la même retouchée.

**Ce qui ne bouge pas.**

- `tick(world, rng)` garde sa signature publique : `sim/__main__.py`,
  `sim/snapshot_export.py` et les tests l'appellent ainsi, et ce lot ne les
  modifie pas ;
- `production_kg(cell, yield_factor)` garde exactement ses deux paramètres et
  son repli sur `_carte_du_tick` quand cette variable est renseignée. Elle
  conserve ainsi le chemin unitaire historique **et** le point d'entrée du test
  de relief du lot 033 ;
- `production_moyenne_kg_par_tick(world)` garde sa signature et reste le plafond
  dérivé de `sim/tests/test_survie.py` ;
- aucune instruction `global` ne subsiste dans `sim/engine.py`.

**Byte pour byte.** La multiplication garde l'ordre qu'elle a aujourd'hui —
la base d'abord, le facteur de relief ensuite, en un seul produit — sans quoi
l'arrondi flottant peut bouger et SC3 rougir. Il n'y a pas d'autre façon de
tenir SC3 : ce n'est pas une préférence de style, c'est la seule qui produise
les mêmes octets.

**Le double facteur est interdit.** Une cellule ne se voit appliquer le facteur
de relief qu'une fois par tick. Comme `_carte_du_tick` vaut `None` pendant un
tick, `production_kg()` ne l'applique pas et `production_du_tick_kg()` l'applique
seule. SC2 le vérifie, SC3 le prouverait de toute façon en rougissant.

Une classe de relief manquante ou inconnue **dans le chemin réel du tick** reste
une donnée invalide : `ReliefInvalideError`, avec le `cell_id` et la valeur
fautive, est conservée telle quelle. Aucun repli silencieux vers `plaine`.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/tests/test_monde.py`, **en ajout seul** : le diff de ce fichier contre le
  SHA de base ne contient aucune ligne supprimée. Voir la section suivante.

Livrables du lot autorisés :

- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/manifest.json` ;
- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/generator-log.md` ;
- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/measure_034.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni
`sim/constants.py`, ni `sim/world.py`, ni `sim/model.py`, ni
`sim/snapshot_export.py`, ni `sim/__main__.py`, ni `sim/tests/test_survie.py`,
ni `sim/tests/test_write_coverage.py`, ni la carte figée, ni le visualiseur, ni
l'outil de fabrication de la carte, ni les livrables d'un lot précédent
(`harness/queue/briefs/033-*/`), ni ce brief, ni sa grille, ni un `verdict.md`.

### Aucun test existant n'est modifié

C'est la contrainte la plus dure du lot, et celle qui a fait échouer sa première
rédaction. Elle se vérifie mécaniquement.

`sim/tests/test_monde.py::test_production_kg_modulée_par_le_relief` pose
`engine._carte_du_tick`, appelle `engine.production_kg(cell, rendement)`, puis
remet la variable à `None`. **Ce test ne bouge pas d'un caractère** : ni ses
assertions, ni son échantillon dérivé de la carte, ni son refus d'un échantillon
vide, ni sa mise en place, ni les deux lignes qui posent et retirent la variable.
C'est lui qui a mesuré l'état de départ ; le réécrire pour qu'il reste vert après
le changement reviendrait à juger le nouveau raccord avec un contrôle calibré
sur lui. C'est pour le laisser intact — et pour lui seul — que `production_kg()`
garde son repli sur `_carte_du_tick`.

Les autres contrôles du fichier sont sous la même interdiction, en particulier
`test_tick_refuse_relief_inconnu`, `test_tick_refuse_relief_absent` et
`test_la_consommation_des_couches_est_mesuree_pas_declaree`. Ce sont eux qui
prouvent que le refus de l'inconnu et la sonde des couches n'ont pas été perdus
au passage : un contrôle qu'on retouche ne prouve plus rien de ce qu'il
prouvait.

Ce que ce lot écrit dans `sim/tests/test_monde.py` est **ajouté à la fin du
fichier** : les contrôles de SC1 et de SC2, et rien d'autre. Le mesureur produit
le diff du fichier contre le SHA de base et compte les lignes supprimées ; ce
compte doit valoir `0`. Un `0` mesuré sur un diff réellement lu, pas une
absence de mesure.

`sim/tests/test_survie.py` doit rester **byte-identique** au SHA de base : son
diff est vide.

## Conditions de succès

Chaque contrôle ajouté vit dans `sim/tests/test_monde.py`, se lance par
`.venv/bin/python -m pytest sim/tests/test_monde.py -q`, et **le journal du lot
cite sa sortie rouge obtenue sur le SHA de base avant toute correction**.

### SC1 — Plus aucun état global mutable dans le moteur

Un contrôle ajouté parcourt l'arbre syntaxique de `sim/engine.py` et échoue si
une fonction quelconque du module contient une instruction `global`. Le nombre
de fonctions inspectées est dérivé du module lui-même ; un module sans fonction
fait échouer le contrôle au lieu de passer.

**Rouge sur le SHA de base** : le contrôle échoue en nommant les fonctions
fautives — celles qui portent aujourd'hui un `global _carte_du_tick`.

### SC2 — La carte arrive par les arguments, et rien d'autre n'entre

Un contrôle ajouté prouve les quatre choses ci-dessous. Il échoue si l'une
manque, et il échoue **entièrement** sur le SHA de base : `sim/engine.py` n'y
expose aucune fonction de production acceptant une carte en argument, et le
tick y pose un état de module pendant qu'il lit la carte. Les deux rouges — le
point d'entrée absent, l'état de module vu pendant le tick — sont cités dans le
journal.

**(a) Seule la carte change, et le résultat change.** L'échantillon est dérivé
de la carte : pour une cellule prise dans le monde chargé, on construit en
mémoire une carte par classe de relief réellement présente dans
`World.lire_carte()`, identique à l'originale sauf la classe de cette cellule.
`production_du_tick_kg(cell, rendement, carte_x)` est appelée avec le **même
objet cellule** et le **même rendement** pour chaque carte. Le rapport de deux
productions doit valoir le rapport des facteurs nominaux correspondants, lus
dans `sim.constants.facteurs_production_par_relief()` — une référence dérivée du
code, jamais un nombre écrit dans ce brief. Si moins de deux classes de facteurs
distincts sont trouvées dans la carte, le contrôle **échoue** : échantillon
insuffisant, jamais passage silencieux.

**(b) Carte identique, résultat identique.** Deux appels consécutifs aux mêmes
trois arguments rendent le même flottant, à l'égalité stricte, pour chacune des
cartes de l'échantillon. Aucune mise en place hors de l'appel n'est faite : le
contrôle affirme `engine._carte_du_tick is None` avant chaque appel et après le
dernier.

**(c) Rien n'est assigné hors des arguments, pendant un vrai tick.** Sur un
monde chargé, `world.carte` est remplacée en mémoire par une carte instrumentée
— une sous-classe de `dict` qui, à chaque lecture faite par le moteur,
enregistre la valeur qu'a `engine._carte_du_tick` à cet instant précis. Un tick
complet est joué. Le contrôle échoue si **une seule** de ces lectures a vu autre
chose que `None`, et il échoue aussi si **aucune** lecture n'a été enregistrée :
zéro lecture voudrait dire que le tick n'a pas lu la carte du tout, ce qui n'est
pas une preuve mais une absence de mesure. Sur le SHA de base, chaque lecture
voit la carte posée dans le module : rouge franc, pour une raison de
comportement et non d'API manquante.

**(d) Les compteurs sont dérivés.** Nombre d'appels joués, nombre de cartes
comparées, nombre de lectures enregistrées : tous comptés sur les exécutions
réellement faites, aucun attendu écrit en dur. Ils figurent au tableau des
compteurs.

### SC3 — Le monde ne bouge pas d'un octet

`.venv/bin/python -m sim --ticks 20 --seed 0 --json` rend, après changement, une
sortie **byte-identique** à celle rejouée sur le SHA de base.

Le mesureur archive la sortie de base **avant** l'édition, la relit, et compare
champ par champ. Le nombre de champs comparés est celui de la sortie, dérivé de
son propre contenu. Il ne recopie aucun nombre du présent brief.

Une différence, si petite soit-elle, fait échouer le lot : elle voudrait dire
que le raccord a changé l'ordre des tirages, la valeur d'un facteur, ou qu'un
facteur de relief s'applique deux fois — ce que ce lot n'a pas le droit de
faire.

La même identité vaut pour 200 ticks à la graine 42, pour attraper une
divergence qui ne se verrait pas en 20 pas.

### SC4 — Le chemin unitaire historique survit

Construire une `Cell` sans `World` et appeler `production_kg(cell, rendement)`
rend la production sans facteur de relief, sans lever d'erreur et sans exiger de
carte. Les contrôles de `sim/tests/test_survie.py` qui empruntent ce chemin
restent verts **sans que ce fichier soit modifié** — son diff contre le SHA de
base est vide.

### SC5 — Le refus de l'inconnu est intact

`test_tick_refuse_relief_inconnu` et `test_tick_refuse_relief_absent` restent
verts sans une seule ligne changée. Une carte en mémoire dont une classe est
remplacée par une valeur inconnue provoque toujours `ReliefInvalideError`,
portant le `cell_id` et la valeur.

### SC6 — La sonde des couches ne bouge pas

`test_la_consommation_des_couches_est_mesuree_pas_declaree` reste vert sans une
seule ligne changée : c'est le contrôle qui remplace `engine.production_kg` par
des fonctions à deux paramètres, et il rougirait le premier si le tick appelait
`production_kg` autrement qu'avec deux arguments.

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
- aucune deuxième formule de production alimentaire n'apparaît dans `sim/` :
  `production_du_tick_kg()` délègue les kilogrammes à `production_kg()` et
  n'écrit ni surface, ni constante de rendement.

## Compteurs exigés

Le mesureur `deliverables/measure_034.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `fonctions_moteur_inspectees` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de fonctions réellement trouvées dans le module |
| `fonctions_avec_global` | mêmes fonctions, instructions `global` comptées | `fonctions_moteur_inspectees` |
| `fonctions_avec_global_avant` | même parcours joué sur le fichier du SHA de base | nombre de fonctions du module à ce SHA |
| `cartes_comparees_sc2` | cartes construites en mémoire, une par classe de relief présente dans la carte figée | nombre de classes réellement trouvées dans la carte |
| `appels_production_du_tick` | appels de `production_du_tick_kg` joués par le contrôle SC2 | nombre d'appels réellement lancés |
| `ratios_conformes_au_facteur_nominal` | rapports mesurés entre deux cartes ne différant que par une classe | nombre de rapports réellement calculés |
| `appels_repetes_stables` | seconds appels aux trois mêmes arguments | nombre de répétitions réellement jouées |
| `lectures_de_carte_pendant_le_tick` | carte instrumentée, tick complet sur monde chargé | nombre de lectures réellement enregistrées |
| `lectures_voyant_un_etat_de_module` | mêmes lectures, valeur de `_carte_du_tick` à l'instant de la lecture | `lectures_de_carte_pendant_le_tick` |
| `lectures_voyant_un_etat_de_module_avant` | même instrumentation jouée sur le SHA de base | nombre de lectures enregistrées à ce SHA |
| `champs_cli_identiques` | comparaison de la sortie CLI archivée avant édition et de celle d'après | nombre de champs réellement présents dans la sortie |
| `sorties_cli_comparees` | 20 ticks graine 0, puis 200 ticks graine 42 | nombre d'exécutions réellement lancées |
| `appels_unitaires_sans_carte` | appels de `production_kg` sur une `Cell` construite hors `World` | nombre d'appels réellement essayés |
| `classes_inconnues_refusees` | mutations en mémoire vers une valeur absente de l'ensemble dérivé de la carte | nombre de mutations réellement exécutées |
| `lignes_supprimees_dans_test_monde` | diff de `sim/tests/test_monde.py` contre le SHA de base | nombre de lignes du diff réellement examinées |
| `lignes_modifiees_dans_test_survie` | diff de `sim/tests/test_survie.py` contre le SHA de base | nombre de lignes du diff réellement examinées |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

Ce que ces compteurs doivent valoir, et pourquoi c'est une mesure :

- `fonctions_avec_global`, `lectures_voyant_un_etat_de_module`,
  `lignes_supprimees_dans_test_monde` et `lignes_modifiees_dans_test_survie`
  valent **0**. Chacun de ces zéros est une mesure réelle : le mesureur a
  inspecté chaque fonction, chaque lecture, chaque ligne de diff. La sentinelle
  « non calculé » du projet est `-1`, jamais `0` ;
- `fonctions_avec_global_avant` et `lectures_voyant_un_etat_de_module_avant`
  sont **strictement positifs**, sans quoi le rouge n'a pas été prouvé ;
- `cartes_comparees_sc2`, `appels_production_du_tick`,
  `lectures_de_carte_pendant_le_tick` et `appels_unitaires_sans_carte` sont
  strictement positifs : un échantillon vide fait échouer le lot, il ne le fait
  jamais passer ;
- `ratios_conformes_au_facteur_nominal` égale le nombre de rapports calculés :
  un seul écart et le lot échoue.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : les sorties rouges obtenues sur le SHA
  de base avant correction — celle de SC1, et les deux de SC2 —, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_034.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Toute comparaison
avant/après passe par la référence Git du SHA de base, pas par une copie `.orig`
fabriquée après coup.

Attention : `sim/engine.py` doit **différer** du SHA de base, la sortie CLI doit
lui être **identique**, et `sim/tests/test_monde.py` ne doit en différer que par
des lignes **ajoutées**. Ce sont les trois faits que le lot a à prouver
ensemble.

## Hors périmètre

- le climat, les gisements, la saison, la natalité, la migration ;
- toute modification du comportement du monde, si petite soit-elle ;
- toute constante nouvelle ou modifiée ;
- la suppression de `_carte_du_tick` et du repli de `production_kg()` sur cette
  variable — c'est un lot ultérieur, et il devra d'abord dire ce qu'il fait du
  test de relief du lot 033 ;
- toute modification, même d'une seule ligne, d'un test existant ;
- la signature publique de `tick()`, de `production_kg()` et de
  `production_moyenne_kg_par_tick()` ;
- le snapshot, le visualiseur, la carte figée, l'outil de fabrication ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
