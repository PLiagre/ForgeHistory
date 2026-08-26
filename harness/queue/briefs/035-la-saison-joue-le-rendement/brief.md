# Brief 035 — La saison joue dans le rendement

**Authored**: 2026-08-26T09:10:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire jouer le **cycle des saisons** dans la production alimentaire du tick. Une
cellule produit moins en hiver qu'en été, et le contraste entre les deux est
d'autant plus violent qu'elle est loin de l'équateur.

C'est la couche `climat` de la carte qui entre enfin dans le jeu.

Ce lot ne fait jouer ni les gisements, ni la pluie, ni la température, ni la
continentalité, ni le littoral. Il ne refait pas la carte.

## Dépendance

**Ce lot suppose le lot 034 fusionné.** Il apporte au tick une deuxième donnée
de contexte, le jour de l'année ; l'introduire tant que le moteur porte encore
une variable de module reviendrait à en poser une seconde. Si `sim/engine.py`
contient encore une instruction `global`, ce lot est bloqué, pas à adapter.

## Fondement dans le modèle

`sim/MODELE.md`, § « Le rendement agricole et sa variabilité » — la formule de
production que ce lot module — et § « Ce que le moteur ne fait pas encore »,
qui mesure que le climat n'est pas consommé et dit ce que la sonde ne peut pas
voir. Si l'une de ces deux sections a changé depuis la rédaction de ce brief,
le relire avant de le lancer.

`sim/MODELE.md` est hors périmètre de ce lot. La mise à jour de la section
citée après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/ -q
```

Et l'état de la couche, mesuré par la sonde existante :

```bash
.venv/bin/python -c "from sim.world import World; from sim.snapshot_export import build_snapshot_document; print(build_snapshot_document(World.charger(0),0,0)['couches'])"
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si cette sonde déclare déjà
`climat.utilisee_par_le_moteur == true`, le tick lit déjà le climat et ce brief
n'a plus d'objet.

Aujourd'hui, le tick produit la même quantité au 15 janvier qu'au 15 juillet :
il n'a aucune notion de jour de l'année.

## Règle du monde

**Fidélité niveau 2** : la sensibilité du rendement à la durée du jour est un
paramètre plausible, généré, jamais sourcé. Un rendement local surprenant à une
date donnée n'est pas un défaut historique et n'ouvre ni correctif, ni brief.

Le mécanisme, en trois pas.

**1. Le tick connaît le jour de l'année.** Il se dérive du numéro de tick et de
la base de temps déjà nommée (`TICK_DURATION_DAYS`, `CALENDAR_DAYS_PER_YEAR`).
Aucun second littéral de durée n'apparaît.

**2. La durée du jour de chaque cellule se déduit de ses deux solstices.** La
carte porte, pour chaque cellule,
`climat.duree_jour_solstice_ete_h` et `climat.duree_jour_solstice_hiver_h`. La
durée du jour à une date quelconque oscille entre ces deux bornes sur un cycle
annuel :

```
moyenne_h   = (ete_h + hiver_h) / 2
amplitude_h = (ete_h - hiver_h) / 2
duree_jour_h(jour) = moyenne_h + amplitude_h * cos(2π × (jour - JOUR_SOLSTICE_ETE) / annee)
```

`JOUR_SOLSTICE_ETE` est une constante nommée : le rang du solstice d'été dans
l'année calendaire. C'est du niveau 2 — sa valeur exacte ne change rien à ce que
le lot démontre, seul le fait qu'il y ait un maximum et un minimum en compte.

**3. La production suit la durée du jour, en écart à l'équinoxe.**

```
facteur_saison = max(0, 1 + SENSIBILITE_SAISON × (duree_jour_h - DUREE_JOUR_EQUINOXE_H) / DUREE_JOUR_EQUINOXE_H)
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `DUREE_JOUR_EQUINOXE_H` | 12.0 | un jour d'équinoxe dure douze heures partout — niveau 1 |
| `SENSIBILITE_SAISON` | 0.5 | de combien le rendement suit l'écart relatif à l'équinoxe — **niveau 2** |
| `JOUR_SOLSTICE_ETE` | 172 | rang du solstice d'été dans l'année — niveau 2 |

Ces constantes vivent dans `sim/constants.py`, avec un commentaire disant qu'il
s'agit d'ordres de grandeur plausibles de niveau 2. Aucun nombre de réglage
n'est écrit dans une fonction du moteur.

**Motif 033 — constantes invisibles pour le monde d'épreuve.**
`_MondeEpreuve` de `sim/tests/test_write_coverage.py` n'a pas de carte, donc
pas de climat. `test_chaque_constante_du_moteur_change_le_monde` ne dérive
son dénominateur que des noms d'attributs **présents dans** `sim/engine.py`.
Un nom `SENSIBILITE_SAISON`, `JOUR_SOLSTICE_ETE` ou `DUREE_JOUR_EQUINOXE_H`
écrit dans `engine.py` y figurerait, ne bougerait pas l'empreinte, et le
contrôle — hors périmètre — rougirait.

Donc : ces trois constantes ne sont **pas** lues par leur nom dans
`sim/engine.py`. Elles vivent dans `sim/constants.py`. Le moteur consulte le
facteur saisonnier via une **fonction relue à chaque appel**, le même motif
que `facteurs_production_par_relief()` du lot 033 :

```
def facteur_saison(duree_jour_h) -> float:
    # relit SENSIBILITE_SAISON et DUREE_JOUR_EQUINOXE_H
    ...
def duree_jour_h(jour, ete_h, hiver_h) -> float:
    # relit JOUR_SOLSTICE_ETE et CALENDAR_DAYS_PER_YEAR
    ...
```

Interdit dans `engine.py` : `_constantes.SENSIBILITE_SAISON`,
`_constantes.JOUR_SOLSTICE_ETE`, `_constantes.DUREE_JOUR_EQUINOXE_H`.
Autorisé : `_constantes.facteur_saison(...)` et `_constantes.duree_jour_h(...)`.
`test_aucune_constante_terminale` continue de voir ces noms : les fonctions
les lisent.

**Ce que cette forme produit, et pourquoi elle a été choisie.** L'écart est pris
par rapport à une durée de référence **absolue**, pas par rapport à la moyenne
propre de la cellule. Une cellule dont les jours d'été durent dix-huit heures et
les jours d'hiver cinq voit donc un contraste bien plus fort qu'une cellule
dont les jours varient entre quatorze et dix heures. La saison du nord est
violente, celle du sud est douce, et personne n'a écrit cette règle : elle sort
de la carte.

**Le plancher `max(0, …)` est un invariant physique**, pas une commodité : une
production négative fabriquerait de la nourriture à l'envers.

### Le piège de la sonde, et la raison d'être de la forme retenue

La sonde de `sim/snapshot_export.py` mesure la consommation d'une couche en
**multipliant** toutes les valeurs numériques du climat par un même facteur,
puis en comparant deux mondes.

Une formule qui rapporterait la durée du jour à la moyenne propre de la cellule
serait **invariante à cette multiplication** : numérateur et dénominateur
seraient multipliés ensemble, le facteur saisonnier serait inchangé, et la sonde
déclarerait le climat non consommé alors qu'il le serait. La forme ci-dessus
n'a pas ce défaut : `DUREE_JOUR_EQUINOXE_H` est une constante, elle ne suit pas
la multiplication.

Ce n'est pas un détail d'implémentation : c'est la raison pour laquelle SC3
peut passer.

## Source de vérité et raccord au moteur

Les deux durées de solstice viennent **uniquement** de
`world.carte[cell_id]["climat"]`. Le moteur ne duplique pas ces valeurs dans une
seconde base de données et ne lit pas l'outil de fabrication de la carte.

La formule qui applique le facteur saisonnier reste **unique** et vit dans la
même fonction de production que le facteur de relief : il n'y a toujours qu'une
formule de production dans `sim/`.

Le jour de l'année atteint cette fonction par la même voie explicite que la
carte depuis le lot 034 — jamais par une variable de module.

`tick(world, rng)` reste appelable tel quel : le numéro de tick est un paramètre
**facultatif**, dont l'absence vaut le premier jour de l'année. Les appelants
qui ne le fournissent pas — la sonde des couches, les contrôles unitaires —
continuent de fonctionner sans modification.

Un champ `climat` absent, une durée de solstice manquante ou non numérique
**dans le chemin réel du tick** est une donnée invalide : lever une erreur qui
nomme le `cell_id` et la clé fautive. Ne pas deviner, ne pas rabattre
silencieusement vers un facteur neutre.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/__main__.py`, **uniquement** pour transmettre le numéro de tick à la
  boucle ;
- `sim/tests/test_monde.py`, uniquement pour **ajouter** les cas qui protègent
  cette règle visible ; les assertions déjà présentes restent inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/035-la-saison-joue-le-rendement/deliverables/manifest.json` ;
- `harness/queue/briefs/035-la-saison-joue-le-rendement/deliverables/generator-log.md` ;
- `harness/queue/briefs/035-la-saison-joue-le-rendement/deliverables/measure_035.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/tests/test_survie.py`,
ni `sim/aggregation.py`, ni la carte figée, ni le visualiseur, ni l'outil de
fabrication de la carte, ni ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Le rendement dépend de la date

À cellule, surface et rendement aléatoire identiques, la production au solstice
d'été et la production au solstice d'hiver diffèrent. La cellule d'essai est
**dérivée de la carte** : celle dont l'amplitude des durées de jour est la plus
grande. Un échantillon vide fait échouer le contrôle.

**Le rouge est prouvé avant la correction** : sur le SHA de base, ces deux
appels rendent la même valeur.

### SC2 — Le nord a une saison plus violente que le sud

Le rapport entre la production d'été et celle d'hiver est **strictement plus
grand** pour la cellule de plus grande amplitude de durée de jour que pour celle
de plus petite amplitude. Les deux cellules sont dérivées de la carte, jamais
nommées par leur `cell_id` dans le code du contrôle.

Ce critère découle de la forme fixée avant l'exécution. Il ne doit pas être
obtenu en ajustant une constante après avoir vu la mesure.

### SC3 — La couche climat devient réellement consommée

Après le changement, `build_snapshot_document(World.charger(0), 0, 0)` rend :

- `couches.relief.utilisee_par_le_moteur == true` ;
- `couches.climat.utilisee_par_le_moteur == true`.

L'état de la couche **gisements** est celui que la sonde mesure au SHA de base,
**inchangé** par ce lot — quel qu'il soit. Le mesureur le relève avant et après
et vérifie l'égalité ; il ne le fixe pas à une valeur écrite ici. Le lot 038,
s'il est fusionné d'abord, la rend consommée, et ce lot-ci n'a pas à le savoir.

Ces valeurs restent celles de la sonde existante ; aucune déclaration manuelle
n'est ajoutée ou retournée, et `sim/snapshot_export.py` n'est pas modifié.

### SC4 — Le plafond de survie reste dérivé du moteur

`production_moyenne_kg_par_tick()` continue de servir de plafond physique à
`sim/tests/test_survie.py`, et elle reste dérivée de la **même** formule de
production que le tick.

Le facteur saisonnier qu'elle emploie est la **moyenne du facteur sur l'année
calendaire, calculée** — la somme du facteur sur tous les jours de l'année,
divisée par leur nombre, l'un et l'autre dérivés des constantes de temps. Ce
n'est pas la valeur 1 supposée : un contrôle vérifie que cette moyenne calculée
et le plafond employé coïncident.

Conséquence : les trois propriétés de `sim/tests/test_survie.py` restent vertes
**sans que ce fichier soit modifié**.

### SC5 — Le monde ne gagne ni ne perd de nourriture sur l'année

Sur une année complète et à cellule donnée, la somme des productions
saisonnières et la somme des productions au facteur moyen coïncident à la
précision du flottant près. La saison **redistribue** la production dans
l'année ; elle n'en crée ni n'en détruit.

Le nombre de jours sommés est dérivé des constantes de temps, jamais écrit.

### SC6 — Le refus de l'incomplet

Une carte en mémoire dont le `climat` d'une cellule est retiré, ou dont une
durée de solstice est remplacée par une valeur non numérique, provoque l'erreur
explicite exigée, avec le `cell_id` et la clé fautive. Aucun repli silencieux
n'est admis.

### SC7 — Effet visible et déterministe

Deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
strictement identiques entre elles, et différentes de la référence rejouée sur
le SHA de base sur au moins un des champs dérivés `population_arrivee`,
`cellules_affamees`, `kg_transportes`, `stock_kg_arrivee`.

Le mesureur archive la sortie de base **avant** l'édition et la relit ; il ne
recopie aucun nombre du présent brief.

### SC8 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- aucune deuxième formule de production alimentaire n'apparaît dans `sim/` ;
- aucune instruction `global` ne réapparaît dans `sim/engine.py` ;
- aucun des noms `SENSIBILITE_SAISON`, `JOUR_SOLSTICE_ETE`,
  `DUREE_JOUR_EQUINOXE_H` n'apparaît comme attribut lu dans `sim/engine.py`
  — le motif 033 tient.

## Compteurs exigés

Le mesureur `deliverables/measure_035.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `cellules_avec_deux_solstices` | agrégation Python des valeurs `climat` rendues par `World.lire_carte()` | nombre total de cellules réellement mesurées |
| `amplitudes_distinctes_mesurees` | amplitudes de durée de jour dérivées de la carte | `cellules_avec_deux_solstices` |
| `jours_de_l_annee_evalues` | facteur saisonnier calculé jour par jour sur une année | nombre de jours dérivé des constantes de temps |
| `ecart_ete_hiver_avant` | production d'été et d'hiver rejouées sur le SHA de base | nombre de couples réellement comparés |
| `ecart_ete_hiver_apres` | mêmes appels après changement | nombre de couples réellement comparés |
| `couches_consommees_par_tick` | sonde existante du snapshot | nombre de couches déclarées dans le snapshot |
| `cellules_climat_incomplet_refusees` | mutations en mémoire retirant ou corrompant le climat d'une cellule | nombre de mutations réellement exécutées |
| `ecart_relatif_somme_annuelle` | somme des productions saisonnières contre somme au facteur moyen | nombre de cellules réellement sommées |
| `sorties_cli_deterministes` | deux exécutions à 365 ticks, graine 0 | nombre d'exécutions réellement lancées |
| `champs_cli_modifies` | comparaison avec la sortie de base rejouée et archivée avant édition | nombre de champs dérivés réellement comparés |
| `noms_de_constantes_saison_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de noms du motif 033 réellement cherchés |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

Aucun compteur d'affirmation réelle ne prend `-1` comme résultat final. Un zéro
mesuré reste possible seulement si la condition correspondante l'autorise ; il
ne remplace jamais « non calculé ». `ecart_ete_hiver_avant` doit être nul et
`ecart_ete_hiver_apres` non nul : c'est la preuve du rouge puis du vert.
`noms_de_constantes_saison_dans_engine` doit valoir **0**.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : rouge avant correction, fichiers
  modifiés, commandes jouées, résultats et limites ;
- `measure_035.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- les gisements, la pluie, la température, la continentalité, le littoral, la
  distance à la mer ;
- la natalité, la migration, les marchandises autres que la nourriture ;
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
