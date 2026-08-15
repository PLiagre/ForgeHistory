# Eval Rubric — Brief 021 : les fleuves (G5)

**Authored**: 2026-08-14T21:15:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

---

## Guide de lecture

Pour chaque condition de succès du brief :

- **Vérification** : commandes rejouables, depuis la racine avec
  `.venv/bin/python`, ou depuis `pipeline/geo/` avec `../../.venv/bin/python`.
  Jamais l'alias nu de l'interpréteur (règle n° 1).
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur
  lui-même depuis les fichiers du dépôt, sans reprendre un nombre du
  manifeste.
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans une
  copie de travail hors du dépôt. Si le contrôle reste vert sous sabotage, la
  condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire : voir la section « Vocabulaire » du brief — non reproduit ici
(Single Source of Instruction).

---

## SC1 — Tronçons valides, classés en trois classes de navigabilité

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_g5.py
```
Lire `troncons_valides` (doit être vrai, `Q1` vert), `navigability_counts`
(trois clés `navigable`/`indeterminate`/`non_navigable` sommant à
`segment_count`), `fleuves_nommes_trouves`.

**Reconstruction indépendante** : charger `artifacts/rivers_g5.json`,
recalculer soi-même `scalerank <= 5` / `>= 9` / entre les deux sur les
tronçons bruts de la couche source, comparer aux trois comptes déclarés.
Vérifier que `G5_NAV_SCALE_NAVIGABLE_MAX` et `G5_NAV_SCALE_NON_NAV_MIN` sont
bien **lus** de `constants.py` dans `steps/05_rivers.py` (pas de littéral `5`
ou `9` en dur).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, invalider la
géométrie d'un tronçon (coordonnée NaN ou anneau non fermé) ; `Q1` doit
rougir. Forcer `scalerank` d'un tronçon à une valeur hors des trois bandes
déclarées (impossible si les bandes couvrent tout l'espace — vérifier que
c'est bien le cas, sinon c'est un défaut de conception, pas du Générateur).

**Résultat attendu** : `Q1` vert, `navigability_counts` mesuré et cohérent.

---

## SC2 — Rattachement (`G5-A`) et absence de fleuve en pleine mer (`G5-B`)

**Vérification** : même exécution que SC1, lire `G5-A` et `G5-B` dans
`logs/v1_060_qa.json`.

**Reconstruction indépendante** : pour un échantillon de tronçons rattachés,
recalculer l'intersection tronçon/cellule soi-même (shapely) et vérifier
qu'elle dépasse la tolérance `G5_INTERSECT_EPS_M` lue de `constants.py`. Pour
`G5-B`, prendre un tronçon `featurecla != "Lake Centerline"` proche du bord
de la fenêtre pilote et vérifier à l'œil sur la capture
`capture/v1_060_rivers_window.png` qu'il n'est pas entièrement en mer.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, retirer une
cellule de la liste `attachments` d'un tronçon qui la traverse réellement —
`G5-A` doit rougir. Forcer `sea_only_fraction` calculé d'un tronçon terrestre
à 1.0 sans changer sa géométrie réelle — `G5-B` doit rougir.

**Résultat attendu** : `G5-A` et `G5-B` verts, chacun avec un `red_proof` non
vide dans `test_qa_red_g5.py`.

---

## SC3 — Classification artère / croisement / mixte (D3) — la condition centrale de ce brief

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source rivers
```
Lire la ligne `artery=X crossing=Y both=Z` et
`aretes_terre_terre_avec_fleuve` dans `stats_g5.json`.

**Reconstruction indépendante — la plus importante de ce brief** :

1. Vérifier `artery_count + crossing_count + both_count ==
   aretes_terre_terre_avec_fleuve` exactement (aucune arête comptée deux
   fois, aucune omise).
2. Prendre un échantillon d'arêtes de chaque classe dans
   `artifacts/adjacency_g5.json` et vérifier à la main, contre
   `artifacts/rivers_g5.json`'s `navigability` par tronçon, que la classe
   assignée correspond exactement à la table de D3 du brief (tous navigables
   → `artery` ; aucun navigable → `crossing` ; mélange → `both`).
3. Vérifier `G5-C` sur `artifacts/adjacency_g5.json` : chaque arête
   `fluvial_artery=true` (donc `artery` ou `both`) porte au moins un tronçon
   `navigable` dans `artery_rivers`. Aucune arête `crossing` ne porte
   `fluvial_artery=true`.
4. Vérifier que `artery_count > 0` : au moins une arête réellement classée
   artère, pas seulement un script qui se termine sans erreur.
5. Vérifier `adjacency_g4.json` **inchangé** (`git status --porcelain` vide)
   et **différent** de `artifacts/adjacency_g5.json` (couple
   `must_differ_from` du manifeste).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, marquer une
arête `crossing` comme `fluvial_artery=true` sans `artery_rivers` navigable —
`G5-C` doit rougir. Modifier `adjacency_g4.json` directement dans le
répertoire de travail du Générateur pendant l'exécution — `git status
--porcelain` sur ce fichier doit détecter la modification et
`adjacency_g4_inchange` doit tomber à 0.

**Résultat attendu** : la partition en trois classes est exacte, `G5-C` vert
avec preuve rouge non vide, `adjacency_g4.json` intact.

**Note pour l'Évaluateur** : si le Générateur documente, dans
`deliverables/generator-log.md`, une divergence factuelle entre la lecture
D3 de ce brief et ce que le code de `qa/checks.py` exige réellement une fois
qu'il l'a lu en détail, ne pas rejeter automatiquement — vérifier la table
des Waivers du brief (ligne dédiée) avant de conclure à un échec.

---

## SC4 — Embouchures (`G5-D`)

**Vérification** : lire `G5-D` dans `logs/v1_060_qa.json`, `embouchures_mesurees`
dans `stats_g5.json`.

**Reconstruction indépendante** : pour chaque embouchure dans
`artifacts/mouths_g5.json`, vérifier que la zone `sea_zone_id` déclarée
partage bien une arête `land-sea` (dans `adjacency_g4.json`) avec au moins une
cellule de `attachments` du tronçon correspondant.

**Contre-preuve disqualifiante** : forcer `sea_zone_adjacent_to_river_cells`
à `false` sur une embouchure valide dans une copie hors dépôt — `G5-D` doit
rougir.

**Résultat attendu** : `G5-D` vert. `embouchures_mesurees = 0` est acceptable
si mesuré et rapporté (aucun plancher), mais alors `G5-D` doit rester vert
sur un ensemble vide (vacuously true) — vérifier que ce n'est pas confondu
avec un contrôle qui n'a jamais tourné.

---

## SC5 — Déterminisme, six contrôles verts et mordants

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_g5.py
```
Lire `logs/v1_060_qa.json` : `determinism.sha256` (paires égales, non
vides), `checks` (6 entrées, `passed=true`, `red_proof` non vide chacune).

**Reconstruction indépendante** : relancer `run_proof_g5.py` une deuxième
fois de façon indépendante (l'Évaluateur, pas le Générateur) et comparer les
empreintes de sortie à celles déjà committées — elles doivent être
identiques à celles produites par le Générateur.

**Contre-preuve disqualifiante** : introduire un horodatage courant ou une
graine non fixée dans une copie hors dépôt — le déterminisme doit rougir
(deux passes produisant des empreintes différentes).

**Résultat attendu** : `code_sortie_run_proof_g5 == 0`, 6/6 verts, 6/6 avec
preuve rouge.

---

## SC6 — Crochet `pipeline.py` satisfait, preuves committées, README honnête

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source rivers
```
et
```
git status --porcelain pipeline/geo/pipeline.py pipeline/geo/qa/checks.py \
  pipeline/geo/constants.py pipeline/geo/io_util.py pipeline/geo/projection.py \
  pipeline/geo/steps/02_coastline.py pipeline/geo/steps/02b_corrections_1400.py \
  pipeline/geo/steps/03_cells.py pipeline/geo/steps/04_adjacency.py
```
(doit être vide — neuf fichiers, D12) et
```
git ls-files pipeline/geo/artifacts/*g5* pipeline/geo/logs/*060* \
  pipeline/geo/capture/*060* pipeline/geo/registry/river_registry.json \
  pipeline/geo/steps/05_rivers.py pipeline/geo/tests/*g5*
```

**Reconstruction indépendante** : lire le `README.md` publié et vérifier
qu'il énonce G5 comme livré, G5-bis/G5-ter/relief/climat/ressources/villes
comme non livrés, sans affirmer que la fusion Europe (G5-ter) est prête —
elle ne l'est pas, la couche source manque (D10 du brief). Comparer au
`deliverables/pre-edit/pipeline-geo-README.md.orig` committé — doit différer.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, modifier
`pipeline.py` de façon triviale (un commentaire) — `fichiers_partages_modifies`
doit détecter le changement et passer à une valeur > 0.

**Résultat attendu** : commande de crochet en code 0, ligne de résumé G5
affichée, neuf fichiers partagés intacts, toutes les preuves déclarées
suivies par git, README honnête sur le périmètre réellement livré.

---

## Échecs disqualifiants (toute la rubrique, transversal)

- Un `red_proof` vide sur n'importe lequel des six contrôles.
- `artery_count + crossing_count + both_count != aretes_terre_terre_avec_fleuve`.
- `adjacency_g4.json` modifié (même un octet).
- Toute valeur de `constants.py` modifiée.
- Un compteur mesuré rapporté comme `0` alors qu'il n'a en réalité jamais été
  calculé (sentinelle attendue : `-1`, règle n° 8).
- Une empreinte SHA256 recopiée par valeur dans un test, un document ou un
  commentaire (règle n° 12).
