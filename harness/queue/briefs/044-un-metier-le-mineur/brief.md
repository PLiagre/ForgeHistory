# Brief 044 — Un métier : le mineur

**Authored**: 2026-08-26T10:40:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de modèle structurel.

## But unique

Faire qu'une part des habitants d'une cellule à gisement **cesse de cultiver**
pour extraire. Ce qu'ils sortent de la mine, ils ne le sortent pas des champs.

C'est la première division du travail du jeu : jusqu'ici tout le monde faisait
tout, et la mine tournait en plus de l'agriculture, gratuitement.

Ce lot ne crée aucun métier autre que celui-là, n'invente ni salaire, ni marché,
ni classe sociale, et ne fait pas circuler le minerai.

## Dépendance

**Ce lot suppose le lot 038 fusionné.** Sans extraction, il n'y a pas de mineur.

## Fondement dans le modèle

`sim/MODELE.md`, § « Le rendement agricole et sa variabilité » — la production
que ce lot atténue — et § « Ce qui dit que le monde vit », qui explique pourquoi
le plafond de survie suit tout seul. Si l'une de ces sections a changé depuis la
rédaction de ce brief, le relire avant de le lancer.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
grep -rn "EXTRACTION_KG_PAR_HABITANT_PAR_TICK" sim/
.venv/bin/python -m pytest sim/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si la production agricole d'une
cellule dépend déjà du nombre de ses habitants affectés à autre chose, il n'y a
rien à faire ici.

## Règle du monde

**Fidélité niveau 2** : la part de la population qu'un gisement occupe est un
ordre de grandeur plausible, généré, jamais sourcé. Une répartition locale
surprenante n'est pas un défaut historique et n'ouvre ni correctif, ni brief.

Le mécanisme, en trois pas.

**1. Un gisement occupe des bras.**

```
part_miniere = min(PART_MINIERE_MAXIMALE,
                   somme sur les gisements de la cellule de
                       PART_MINIERE_PAR_GISEMENT × facteur_richesse)
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `PART_MINIERE_PAR_GISEMENT` | 0.05 | part de la population qu'un gisement notable occupe — niveau 2 |
| `PART_MINIERE_MAXIMALE` | 0.30 | plafond : une cellule ne devient jamais entièrement minière — niveau 2 |

Les facteurs de richesse sont ceux, déjà nommés, que le lot 038 a introduits :
un gisement majeur occupe plus de bras qu'un mineur, et il rend plus. Ils ne
sont **pas** dupliqués.

**Le plafond est un invariant, pas un réglage de confort** : sans lui, une
cellule chargée de gisements majeurs verrait toute sa population descendre à la
mine et mourir de faim au-dessus d'un filon d'argent. Ce n'est pas invraisemblable
— c'est arrivé — mais le modèle n'a pas encore de quoi le raconter, et un monde
où c'est le cas par construction n'apprend rien.

**2. Les bras à la mine ne sont pas aux champs.**

```
production agricole de la cellule = production_actuelle × (1 - part_miniere)
```

Cette atténuation entre dans l'unique formule de production, à côté du facteur
de relief et du facteur de saison. Il n'y a toujours **qu'une** formule de
production alimentaire dans `sim/`.

**3. Ce qu'ils extraient est proportionnel à ceux qui extraient.** Le débit
d'extraction du lot 038, aujourd'hui calculé sur la population entière, se
calcule désormais sur la **part minière** de cette population. Le même gisement,
sur une cellule deux fois plus peuplée, rend deux fois plus — c'était déjà vrai
— mais il rend maintenant ce que ses mineurs, et eux seuls, produisent.

**Ce que cette règle produit sans être écrite.** Une cellule à gisement majeur
devient un endroit qui produit beaucoup de métal et pas assez de grain : elle
dépend de ses voisines pour manger. C'est la première fois qu'un endroit du
monde a besoin des autres pour exister — et c'est de cela qu'une ville est
faite. Aucune règle ne dit « les villes minières importent leur nourriture » :
cela sort du fait que les mineurs ne labourent pas.

## Source de vérité et raccord au moteur

Les gisements, leur richesse et leur ressource viennent **uniquement** de
`world.carte[cell_id]["gisements"]`, la même source que le lot 038.

La part minière se calcule à **un seul endroit**, lu à la fois par la production
agricole et par l'extraction. Deux calculs séparés dériveraient l'un de l'autre
au premier changement.

Le plafond de survie, `production_moyenne_kg_par_tick()`, reste dérivé de la
**même** formule de production que le tick : il tient compte de l'atténuation
minière sans qu'on ait à le lui dire.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/tests/test_monde.py`, uniquement pour **ajouter** les cas qui protègent
  cette règle visible ; les assertions déjà présentes restent inchangées.

Livrables du lot autorisés :

- `harness/queue/briefs/044-un-metier-le-mineur/deliverables/manifest.json` ;
- `harness/queue/briefs/044-un-metier-le-mineur/deliverables/generator-log.md` ;
- `harness/queue/briefs/044-un-metier-le-mineur/deliverables/measure_044.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/world.py`,
ni `sim/model.py`, ni `sim/snapshot_export.py`, ni `sim/__main__.py`, ni
`sim/aggregation.py`, ni `sim/tests/test_survie.py`, ni `sim/tests/test_commerce.py`,
ni la carte figée, ni le visualiseur, ni l'outil de fabrication de la carte, ni
ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Une cellule à gisement cultive moins

À surface, relief, date et rendement aléatoire identiques, une cellule porteuse
d'un gisement produit **strictement moins** de nourriture qu'une cellule sans
gisement. Les deux cellules sont **dérivées de la carte** : une porteuse et une
non porteuse de même classe de relief. Si aucune paire de ce genre n'existe, le
contrôle échoue au lieu de la fabriquer.

**Le rouge est prouvé avant la correction** : sur le SHA de base, ces deux
cellules produisent exactement la même chose.

### SC2 — La richesse ordonne l'atténuation

À population égale, la part minière suit strictement l'ordre des richesses :
majeure au-dessus de notable, notable au-dessus de mineure. Les trois classes
sont dérivées de la carte ; si l'une manque, le contrôle échoue au lieu de la
sauter.

### SC3 — Le plafond de part minière tient

Une cellule à laquelle on ajoute, en mémoire, assez de gisements majeurs pour
dépasser le plafond garde une part minière **exactement** égale au plafond, et
continue de produire de la nourriture. Le nombre de gisements ajoutés est dérivé
des constantes, jamais écrit.

### SC4 — Une seule définition de la part minière

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si plus d'une fonction calcule la part minière. Le nombre de modules
parcourus est dérivé du répertoire.

De même, il n'y a toujours qu'une seule formule de production alimentaire.

### SC5 — L'extraction suit les mineurs, pas la population

À gisement identique, la quantité extraite est **proportionnelle à la part
minière** de la population, et non à la population entière. Le contrôle le
vérifie en comparant deux cellules dont les parts minières diffèrent d'un facteur
dérivé de leurs gisements respectifs.

Une cellule dont la part minière serait nulle n'extrait rien. Ce zéro est une
**mesure réelle** : le maillon a été joué et a compté zéro. La sentinelle « non
calculé » du projet est `-1`, jamais `0`.

### SC6 — La cellule minière devient dépendante

Sur le monde réel joué à un horizon dérivé, la quantité de nourriture reçue par
le commerce par les cellules porteuses de gisement est **strictement supérieure**
à celle rejouée sur le SHA de base par la même commande. La mesure de base est
archivée avant l'édition et relue ; aucun nombre du présent brief n'est recopié.

C'est le fait que ce lot achète, et il est mesuré, pas raconté : un endroit du
monde vit maintenant en partie de ce que les autres lui apportent.

### SC7 — Le plafond de survie reste dérivé, et il tient

Les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
**sans modification de ce fichier**. Le plafond employé descend tout seul, parce
qu'il appelle la même formule de production que le tick : c'est ce que ce
couplage a été écrit pour garantir.

Une vérification supplémentaire est faite à un horizon cinq fois plus long, pour
montrer que l'effet se stabilise.

### SC8 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_chaque_constante_du_moteur_change_le_monde`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- les facteurs de richesse du lot 038 ne sont pas dupliqués : un contrôle échoue
  si un second jeu de facteurs apparaît ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques entre elles, et différentes de la référence rejouée sur
  le SHA de base ;
- aucune instruction `global` n'apparaît dans `sim/engine.py`.

## Compteurs exigés

Le mesureur `deliverables/measure_044.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `cellules_avec_gisement_carte` | agrégation Python des `gisements` rendus par `World.lire_carte()` | nombre total de cellules réellement mesurées |
| `paires_comparables_derivees` | cellules porteuses et non porteuses de même classe de relief | `cellules_avec_gisement_carte` |
| `production_agricole_avec_gisement` | production mesurée sur la cellule porteuse de chaque paire | `paires_comparables_derivees` |
| `production_agricole_sans_gisement` | production mesurée sur la cellule témoin de chaque paire | `paires_comparables_derivees` |
| `parts_minieres_ordonnees` | part minière calculée, une fois par classe de richesse dérivée | nombre de classes de richesse réellement mesurées |
| `part_miniere_au_plafond` | cellule surchargée de gisements en mémoire | plafond nommé, lu par le module |
| `definitions_de_part_miniere` | parcours de l'arbre syntaxique des modules de `sim/` hors tests | nombre de modules réellement parcourus |
| `formules_de_production_agricole` | même parcours | nombre de modules réellement parcourus |
| `jeux_de_facteurs_de_richesse` | même parcours | nombre de modules réellement parcourus |
| `extraction_part_miniere_nulle` | cellule dont la part minière est nulle, un tick joué | nombre de ticks réellement joués |
| `nourriture_recue_par_les_minieres_avant` | monde réel rejoué sur le SHA de base | nombre de cellules minières réellement mesurées |
| `nourriture_recue_par_les_minieres_apres` | même mesure après changement | nombre de cellules minières réellement mesurées |
| `fraction_survie_horizon_long` | monde réel joué à cinq fois l'horizon du contrôle existant | population de départ réellement mesurée |
| `tests_sim_verts` | collecte pytest après changement | nombre de tests collectés |

`definitions_de_part_miniere`, `formules_de_production_agricole` et
`jeux_de_facteurs_de_richesse` doivent valoir **1**.
`extraction_part_miniere_nulle` doit valoir **0**, et ce zéro est une mesure
réelle. La sentinelle « non calculé » du projet est `-1`, jamais `0`.
`fraction_survie_horizon_long` doit être strictement positive.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_044.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- tout métier autre que le mineur ;
- le salaire, le prix, le marché, la propriété, la classe sociale ;
- la transformation du minerai, la fonte, l'artisanat ;
- le déplacement des mineurs vers les gisements ;
- l'épuisement d'un gisement ;
- la définition d'un bourg ou d'une ville ;
- le schéma du snapshot, sa version, et le visualiseur ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
