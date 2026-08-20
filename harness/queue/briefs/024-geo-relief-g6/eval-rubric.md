# Eval Rubric — Brief 024 : le relief (G6)

**Authored**: 2026-08-20T08:15:00Z
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
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans
  une copie de travail hors du dépôt. Si le contrôle reste vert sous
  sabotage, la condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire : voir la section « Vocabulaire » du brief — non reproduit ici
(Single Source of Instruction).

---

## SC1 — Cache DEM complet et vérifié avant toute lecture (`G6-A`)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_g6.py
```
Lire `tuiles_verifiees` (doit être 179/179), `empreinte_collective_egale`
(doit être vrai), `G6-A` dans `logs/v1_052_qa.json`.

**Reconstruction indépendante** : pour un échantillon d'au moins 10 tuiles
(pas les 179 — coûteux), recalculer soi-même le SHA256 du fichier présent
dans `pipeline/geo/sources/dem_cache/` et le comparer à
`sources.lock`'s `dem.tiles.<nom>.sha256`, lu directement, jamais recopié.
Vérifier que `pipeline/geo/tools/fetch_dem_tiles.py` ne modifie ni ne
contourne `sources.lock`.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, altérer un
octet d'une tuile du cache (ou renommer une tuile absente comme présente) —
`G6-A` doit rougir, empreinte individuelle **et** collective. Supprimer une
tuile du cache — `G6-A` doit rougir avant toute lecture d'altitude (pas un
`elev_mean_m` calculé sur les 178 tuiles restantes en silence).

**Résultat attendu** : `G6-A` vert, `tuiles_verifiees = 179`,
`empreinte_collective_egale = true`. Aucune tuile Copernicus DEM committée
dans Git (`git status --porcelain --ignored
pipeline/geo/sources/dem_cache/` montre le répertoire ignoré, `git
ls-files pipeline/geo/sources/dem_cache/` vide).

---

## SC2 — Toute cellule terrestre échantillonnée, altitudes plausibles (`G6-B`, `G6-C`)

**Vérification** : même exécution que SC1, lire `G6-B` et `G6-C` dans
`logs/v1_052_qa.json`, `cellules_sans_echantillon` et
`echantillons_exclus_hors_plage` dans `stats_g6.json`.

**Reconstruction indépendante** : pour un échantillon de cellules pris dans
`artifacts/cells_relief_g6.json`, vérifier que `elev_mean_m`, `elev_min_m`,
`elev_max_m` sont bien dans `[G6_ELEV_PLAUSIBLE_MIN_M,
G6_ELEV_PLAUSIBLE_MAX_M]` lus de `constants.py` (jamais un littéral en dur
dans le test de l'Évaluateur), et que `sample_count > 0` pour chacune.
Vérifier que le nombre total de cellules dans `cells_relief_g6.json` égale
exactement celui de `cells_g3.json`.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, forcer
`sample_count = 0` sur une cellule qui en a réellement — `G6-B` doit
rougir. Forcer `elev_mean_m` d'une cellule à une valeur hors plage (par
exemple 6000) — `G6-C` doit rougir.

**Résultat attendu** : `G6-B` et `G6-C` verts, chacun avec un `red_proof`
non vide dans `test_qa_red_g6.py`.

---

## SC3 — Barrières et cols cohérents, l'invariant `pass_count == barrier_count` tient (`G6-D`) — la condition centrale de ce brief

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source relief
```
Lire la ligne `barriers=X passes=Y` et `stats_g6.json`.

**Reconstruction indépendante — la plus importante de ce brief** :

1. Vérifier `pass_count == barrier_count` exactement (D7 du brief : un col
   par barrière, jamais plus, jamais moins).
2. Prendre un échantillon d'arêtes `relief_barrier=true` dans
   `artifacts/adjacency_g6.json` et vérifier, pour chacune, que
   `crossing_elev_m` dépasse strictement les deux `centroid_elev_m` des
   cellules `a`/`b` lues dans `artifacts/cells_relief_g6.json` — exactement
   la condition D6 du brief et la logique de `g6d_barrier_above_both_cells`.
3. Vérifier `barrier_count > 0` : au moins une barrière réellement dérivée
   sur la fenêtre pilote — les Pyrénées et les Alpes y sont, un `0` est
   disqualifiant (voir la table des Waivers du brief, dernière ligne : ce
   n'est **pas** un cas accepté comme celui des fleuves nommés en G5).
4. Pour un échantillon de cols dans `artifacts/passes_g6.json`, vérifier
   l'appariement aux 9 cols de `G6_KNOWN_PASSES` : calculer soi-même la
   distance entre le point de franchissement et chaque col connu, comparer
   à `G6_KNOWN_PASS_MATCH_M` lu de `constants.py`. Vérifier que les cols
   non appariés portent `pass_id` au format `g6_derived_<min>_<max>` (D7)
   et `nom = null`, jamais un nom inventé.
5. Vérifier `adjacency_g5.json` **inchangé** (`git status --porcelain`
   vide) et **différent** de `artifacts/adjacency_g6.json` (couple
   `must_differ_from` du manifeste).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, marquer une
arête comme `relief_barrier=true` avec un `crossing_elev_m` inférieur à un
des deux centroïdes — `G6-D` doit rougir. Retirer un enregistrement de
`passes_g6.json` correspondant à une barrière réelle (créant
`pass_count < barrier_count`) — la condition SC3 doit être détectée en
échec par la reconstruction indépendante de l'Évaluateur, même si aucun
contrôle mécanique nommé ne porte spécifiquement cet invariant (c'est une
condition du brief, à vérifier manuellement si `run_g6_green` ne la couvre
pas explicitement).

**Résultat attendu** : `barrier_count > 0`, `pass_count == barrier_count`
exactement, `G6-D` vert avec preuve rouge non vide, `adjacency_g5.json`
intact.

**Note pour l'Évaluateur** : si le Générateur documente, dans
`deliverables/generator-log.md`, une divergence factuelle entre la lecture
D6/D7 de ce brief et ce que le code de `qa/checks.py` exige réellement une
fois qu'il l'a lu en détail, ne pas rejeter automatiquement — vérifier la
table des Waivers du brief (ligne dédiée) avant de conclure à un échec.

---

## SC4 — La maille est inchangée (`G6-E`)

**Vérification** : lire `G6-E` dans `logs/v1_052_qa.json`.

**Reconstruction indépendante** : comparer l'ensemble trié des `cell_id` de
`artifacts/cells_g3.json` à celui de `artifacts/cells_relief_g6.json` —
doivent être strictement identiques (même compte, mêmes valeurs).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, retirer une
cellule de `cells_relief_g6.json` — `G6-E` doit rougir.

**Résultat attendu** : `G6-E` vert.

---

## SC5 — Déterminisme, six contrôles verts et mordants

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_g6.py
```
Lire `logs/v1_052_qa.json` : `determinism.sha256` (paires égales, non
vides), `checks` (6 entrées, `passed=true`, `red_proof` non vide chacune).

**Reconstruction indépendante** : relancer `run_proof_g6.py` une deuxième
fois de façon indépendante (l'Évaluateur, pas le Générateur, le cache DEM
étant déjà présent et vérifié) et comparer les empreintes de sortie à
celles déjà committées — elles doivent être identiques à celles produites
par le Générateur.

**Contre-preuve disqualifiante** : introduire un horodatage courant ou une
graine non fixée dans une copie hors dépôt — le déterminisme doit rougir
(deux passes produisant des empreintes différentes).

**Résultat attendu** : `code_sortie_run_proof_g6 == 0`, 6/6 verts, 6/6 avec
preuve rouge.

---

## SC6 — Crochet `pipeline.py` satisfait, preuves committées, DEM non committée, README honnête

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source relief
```
et
```
git status --porcelain pipeline/geo/pipeline.py pipeline/geo/qa/checks.py \
  pipeline/geo/constants.py pipeline/geo/io_util.py pipeline/geo/projection.py \
  pipeline/geo/steps/02_coastline.py pipeline/geo/steps/02b_corrections_1400.py \
  pipeline/geo/steps/03_cells.py pipeline/geo/steps/03b_align_coastline_provenance.py \
  pipeline/geo/steps/04_adjacency.py pipeline/geo/steps/05_rivers.py
```
(doit être vide — onze fichiers, D13) et
```
git ls-files pipeline/geo/artifacts/*g6* pipeline/geo/logs/*052* \
  pipeline/geo/capture/*052* pipeline/geo/registry/relief_registry.json \
  pipeline/geo/steps/06_relief.py pipeline/geo/tools/fetch_dem_tiles.py \
  pipeline/geo/tests/*g6*
```
et
```
git status --porcelain --ignored pipeline/geo/sources/dem_cache/
```

**Reconstruction indépendante** : lire le `README.md` publié et vérifier
qu'il énonce G6 comme livré, climat/ressources/G7-G10/A12 comme non
livrés, sans affirmer qu'un de ces lots futurs est prêt. Comparer au
`deliverables/pre-edit/pipeline-geo-README.md.orig` committé — doit
différer. Regarder réellement `capture/v1_052_elevation_window.png` et
`capture/v1_052_barriers_passes.png` (règle n° 11) — vérifier que le relief
Pyrénées/Alpes est visuellement plausible (altitudes croissantes vers les
massifs) et que les barrières/cols affichés correspondent à des zones de
montagne réelles, pas à un artefact de rendu.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, modifier
`pipeline.py` de façon triviale (un commentaire) —
`fichiers_partages_modifies` doit détecter le changement et passer à une
valeur > 0. Committer une tuile DEM de force (`git add -f`) dans une copie
hors dépôt — `dem_cache_non_suivi` doit détecter l'anomalie.

**Résultat attendu** : commande de crochet en code 0, ligne de résumé G6
affichée, onze fichiers partagés intacts, toutes les preuves déclarées
suivies par git, cache DEM exclusivement local et ignoré, README honnête
sur le périmètre réellement livré.

**Note pour l'Évaluateur — suite du harnais (`tests_harness_passed_024`)** :
`pytest` n'est déclaré dans aucun fichier de dépendances du dépôt et n'est
installé dans aucun venv de cette machine au moment où ce brief est écrit
(vérifié : `.venv/bin/python -m pytest --version` échoue avec `No module
named pytest`). Le brief autorise le Générateur à l'installer comme
outillage de test (pas du code produit). Si `tests_harness_passed_024`
vaut `-1` avec, dans `deliverables/generator-log.md`, la commande
d'installation réellement tentée et son erreur exacte (Waivers du brief),
ne pas rejeter le lot sur ce seul point — c'est un waiver honoré, pas un
échec dissimulé. Rejeter en revanche tout `tests_harness_passed_024 = 0`
ou toute valeur `PASSED` non accompagnée d'une sortie `pytest` réellement
rejouable par l'Évaluateur : la sentinelle `-1` est la seule forme
acceptable d'un échec de provisionnement, jamais un zéro silencieux.

---

## Échecs disqualifiants (toute la rubrique, transversal)

- Un `red_proof` vide sur n'importe lequel des six contrôles.
- `pass_count != barrier_count`.
- `barrier_count == 0` (contrairement à `fleuves_nommes_trouves` en G5,
  ceci est disqualifiant sur cette fenêtre pilote — voir la table des
  Waivers du brief).
- `adjacency_g5.json` ou `cells_g3.json` modifiés (même un octet).
- Toute valeur de `constants.py` modifiée.
- Une tuile Copernicus DEM committée dans Git, sous quelque forme que ce
  soit (`git add -f` compris).
- Un compteur mesuré rapporté comme `0` alors qu'il n'a en réalité jamais
  été calculé (sentinelle attendue : `-1`, règle n° 8) — distinct d'un `0`
  ou `0.0` réellement mesuré (par exemple `below_0_land_km2`,
  `echantillons_exclus_hors_plage`).
- Une empreinte SHA256 ou un ETag S3 recopié par valeur dans un test, un
  document ou un commentaire (règle n° 12).
- Une vérification de tuile DEM qui se contente de l'ETag S3 ou de la
  taille en octets sans recalculer et comparer le SHA256 déclaré dans
  `sources.lock`.
