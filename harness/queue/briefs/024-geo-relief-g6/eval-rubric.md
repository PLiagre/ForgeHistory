# Eval Rubric — Brief 024 : le relief (G6)

**Authored**: 2026-08-20T08:15:00Z
**Author**: forge-planificateur
**Amendé le**: 2026-08-21T17:10:00Z (amendement 001)
**Amendé le**: 2026-08-22 (amendement 002)

> **AMENDEMENT 002 (2026-08-22).** Cette rubrique est amendée une deuxième fois,
> après relecture en lecture seule de la deuxième exécution. Deux faits l'ont
> motivée : une tuile DEM **fabriquée** localement pour remplacer une tuile
> absente du dépôt public, et — trouvés en cherchant à répondre honnêtement à ce
> premier point — des `0,0 m` fabriqués dans **576 cellules sur 596**, invisibles
> pour tous les compteurs de l'amendement 001. La décision et ses preuves sont
> dans
> `harness/queue/briefs/024-geo-relief-g6/amendement-002-frontiere-de-tuile-et-zeros-fabriques.md` ;
> les instructions, elles, sont dans `brief.md` et nulle part ailleurs.
>
> `rubrique_amendee_apres_revue` vaut toujours 1, et l'Évaluateur cite désormais
> **trois** dates : 2026-08-20 (rédaction), 2026-08-21 (amendement 001),
> 2026-08-22 (amendement 002). Un `rubric_predates_deliverables` vert ne vaut ici
> aucune preuve d'antériorité.
>
> Les sections amendées portent la mention **[A2]**, qui prime sur **[A1]**.

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

> **AMENDEMENT 001 (2026-08-21).** Cette rubrique a été amendée **après** le
> verdict `FAIL` de la première relecture, sur décision du propriétaire. La
> décision et ses preuves sont dans
> `harness/queue/briefs/024-geo-relief-g6/amendement-001-couverture-dem-complete.md` ;
> les instructions, elles, sont dans `brief.md` et nulle part ailleurs.
>
> **Conséquence à ne pas dissimuler** : le contrôle mécanique
> `rubric_predates_deliverables` existe pour prouver qu'une rubrique n'a pas
> été écrite après avoir vu les résultats. Pour cette itération, il ne prouve
> pas cela. Les dates d'origine ont été conservées parce que ce sont des faits,
> la date d'amendement est portée ci-dessus, et le compteur
> `rubrique_amendee_apres_revue` porte le fait jusque dans le verdict.
> L'Évaluateur vérifie que ce compteur vaut 1 et cite les deux dates ; un
> `rubric_predates_deliverables` vert ne vaut ici aucune preuve d'antériorité.
>
> Les sections amendées portent la mention **[A1]**.

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

## SC1 — [A1] Cache DEM complet, couvrant et vérifié avant toute lecture (`G6-A`)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_g6.py
```
Lire, dans `logs/v1_052_qa.json` : `tuiles_verifiees` (doit égaler son
dénominateur), `empreinte_collective_egale` (doit être vrai),
`recettes_collectives_essayees` (doit valoir 1),
`tuiles_bornes_nom_vs_raster_egales` (doit égaler son dénominateur),
`tuiles_requises_absentes_du_depot_public` (doit valoir 0), `G6-A`.

**Reconstruction indépendante** :

1. **Le dénominateur d'abord.** Lire `len(dem.tiles)` dans `sources.lock` et
   vérifier que c'est bien lui que le Générateur emploie comme dénominateur.
   `grep -rn "179" pipeline/geo/ harness/queue/briefs/024-geo-relief-g6/`
   ne doit ramener aucun dénominateur de tuiles. Un `179` survivant comme
   dénominateur est disqualifiant.
2. **Empreintes.** Pour un échantillon d'au moins 10 tuiles (pas toutes —
   coûteux), recalculer soi-même le SHA256 du fichier présent dans
   `pipeline/geo/sources/dem_cache/` et le comparer à
   `sources.lock`'s `dem.tiles.<nom>.sha256`, lu directement, jamais recopié.
3. **[A1] Périmètre de l'écriture dans `sources.lock`.** Comparer le
   `sources.lock` publié à
   `deliverables/pre-edit/pipeline-geo-sources.lock.orig` : charger les deux en
   JSON et vérifier que **seul** l'objet `dem` diffère, que `dem.licence` est
   identique, et que le texte d'attribution Copernicus est intact. Toute
   différence hors du bloc `dem` est disqualifiante.
4. **[A1] Aucun hexadécimal saisi à la main.** Pour l'échantillon de tuiles
   ci-dessus, vérifier que chaque `sha256` publié égale l'empreinte recalculée
   sur le fichier. Lire `pipeline/geo/tools/fetch_dem_tiles.py` et vérifier
   qu'aucune empreinte n'y figure en littéral, et que la régénération du bloc
   lit les fichiers du cache.
5. **[A1] Recette collective unique.** Vérifier que la fonction qui essayait
   plusieurs recettes **n'existe plus** dans le dépôt :
   `grep -rn "try_collective_recipes\|candidates" pipeline/geo/tools/` ne doit
   rien ramener de tel. Vérifier que `dem.collective_recipe` porte un **nom**
   de recette, jamais une valeur.
6. **[A1] Convention des bornes.** Recalculer soi-même, pour un échantillon de
   tuiles d'ouest (par exemple un `W001` et un `W011`), les bornes déduites du
   nom, et les comparer aux bornes déclarées dans les métadonnées du fichier
   COG. `W001` doit couvrir `[−1, 0)`. Un `[−2, −1)` est le défaut d'origine et
   est disqualifiant.

**[A2] Reconstructions supplémentaires — la tuile fabriquée et le registrement** :

7. **Aucune tuile fabriquée.** La deuxième exécution a créé de toutes pièces
   `Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif` (3600×3600 pixels à `0 m`),
   l'a mise dans le cache et **inscrite dans `sources.lock`**. Vérifier que la
   fonction et l'option qui le permettaient n'existent plus :
   `grep -rn "synthes" pipeline/geo/` ne doit rien ramener, et
   `grep -rn "from_bounds\|rasterio.open" pipeline/geo/tools/ pipeline/geo/steps/`
   ne doit ramener aucune écriture de raster. Vérifier que le nom de cette tuile
   **n'apparaît plus** dans `sources.lock` ni dans
   `artifacts/dem_required_tiles_g6.json`. Recalculer soi-même
   `fichiers_du_cache_hors_lock` (le cache contient exactement le bloc `dem`) et
   contrôler dans `artifacts/dem_tile_availability_g6.json` que **toutes** les
   tuiles du bloc publié — pas seulement les requises — répondent `200`.
8. **Le registrement est mesuré.** Lire `registrement_dem_mesure` et
   `tuiles_registrement_homogene`. Ouvrir soi-même l'en-tête de deux ou trois
   tuiles du cache (largeur, hauteur, pas, bornes) et vérifier que le nom publié
   décrit ce que les fichiers disent. Vérifier que la tolérance de
   `tuiles_bornes_nom_vs_raster_egales` est dérivée du registrement et non un
   `0,001` écrit en dur — cette tolérance-là est plus large qu'un demi-pixel
   (`0,000417°`) et ne prouve donc pas ce qu'elle prétend. Vérifier que ce
   compteur est bien **publié dans `logs/v1_052_qa.json`** : il en était absent.
9. **La règle d'attribution est prouvée contre le fichier.** Vérifier
   `tuiles_regle_domaine_conforme` = son dénominateur, et refaire le calcul
   soi-même pour une tuile : pour un point à une latitude entière `k`, la tuile
   `N k` doit donner un indice de ligne **hors bornes**, et la tuile `N (k−1)`
   la ligne 0. C'est le fait qui fonde D19 ; s'il est faux, tout le reste de
   l'amendement 002 doit être réexaminé, pas contourné.

**Contre-preuves disqualifiantes** (montées par l'Évaluateur, dans une copie
hors dépôt) :

- altérer un octet d'une tuile du cache, ou renommer une tuile absente comme
  présente — `G6-A` doit rougir, empreinte individuelle **et** collective, et
  **sans qu'aucune recette de repli ne soit essayée** ;
- **[A2]** déposer dans le cache un GeoTIFF fabriqué (rempli de zéros) portant
  le nom d'une tuile du bloc — `G6-A` doit rougir **avant toute lecture
  d'altitude**, par le sondage de disponibilité, en nommant la tuile ;
- supprimer une tuile du cache — `G6-A` doit rougir avant toute lecture
  d'altitude (pas un `elev_mean_m` calculé en silence sur les tuiles
  restantes) ;
- **[A1]** retirer une tuile **requise** du bloc `dem` — la garde de couverture
  doit s'arrêter en la nommant, **avant** le premier échantillonnage ;
- **[A1]** rétablir la convention d'ouest fautive (`W001` = `[−2, −1)`) — la
  comparaison nom-contre-raster doit rougir ;
- **[A1]** modifier un objet de `sources.lock` hors du bloc `dem` — le couple
  de comparaison avec l'instantané pré-édition doit le détecter.

**Résultat attendu** : `G6-A` vert, `tuiles_verifiees` égal à son dénominateur
dérivé, `empreinte_collective_egale = true`, `recettes_collectives_essayees = 1`,
`sha256_saisis_a_la_main = 0`. Aucune tuile Copernicus DEM committée dans Git
(`git status --porcelain --ignored pipeline/geo/sources/dem_cache/` montre le
répertoire ignoré, `git ls-files pipeline/geo/sources/dem_cache/` vide).

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

**[A1] Reconstruction supplémentaire — la donnée fabriquée.** C'est ici que la
première exécution a échoué en restant verte. Vérifier explicitement :

1. **Aucune cellule plate à zéro.** Compter les cellules de
   `cells_relief_g6.json` où `elev_mean_m`, `elev_min_m`, `elev_max_m`,
   `roughness_m` et `slope_mean_deg` valent **tous** `0,0`. Une telle cellule
   est la signature exacte du défaut corrigé (une cellule réelle a du relief,
   même faible). Un compte non nul est disqualifiant tant que le Générateur
   n'a pas prouvé, cellule nommée, qu'il s'agit d'une plaine réellement
   mesurée.
2. **Les cellules autrefois fabriquées.** Reprendre les cellules nommées par la
   relecture d'origine — dont `10225` (centroïde lon −7,077 ; lat 41,104,
   Portugal), `10221`, `10310`, `10346` — et vérifier qu'elles portent
   désormais des altitudes non nulles et plausibles pour leur position.
3. **`nodata`.** Lire `echantillons_nodata_raster` et
   `tuiles_sans_valeur_nodata_declaree` dans `stats_g6.json`. Vérifier dans
   `steps/06_relief.py` que la valeur `nodata` est **lue du fichier**
   (`grep -n "nodata" pipeline/geo/steps/06_relief.py`) et qu'aucune valeur
   sentinelle de raster n'y est écrite en dur.

**[A2] Reconstruction supplémentaire — les zéros fabriqués, deuxième prise.**
C'est ici que la **deuxième** exécution a échoué en restant verte, et c'est le
point le plus important de cette itération avec SC7. Les compteurs de
l'amendement 001 sont aveugles à ce défaut : `echantillons_hors_couverture_dem`
et `echantillons_nodata_raster` valaient tous deux `0` pendant que des altitudes
de `0,0 m` étaient inventées.

1. **Le compte global.** Recalculer soi-même, sur `cells_relief_g6.json`, le
   nombre de cellules dont `elev_min_m` est ≤ 0,0. Il valait **576 sur 596**.
   Une valeur restée de cet ordre signale que le défaut n'a pas été corrigé.
   Comparer à `cellules_altitude_min_nulle` publié.
2. **Les cellules impossibles.** Vérifier nommément **9797** (centroïde
   1,534°O / 33,531°N, Maroc oriental, altitude moyenne 1 149 m, rugosité
   164 m), **9854** et **9872** : elles publiaient toutes trois `elev_min_m =
   0,0` à plus de 150 km de la mer et à plus de 1 100 m de moyenne. Elles
   doivent porter un minimum plausible pour leur position. Un `0,0` qui
   subsisterait doit être prouvé pixel par pixel, ou c'est disqualifiant.
3. **La cellule 1492.** Elle publiait `sample_count = 3` et `0,0` sur **tous**
   ses champs (centroïde 34,8170°E / 45,8262°N, Sivach, Crimée). Vérifier que le
   journal publie ses **trois** lectures — coordonnées, tuile servante, indices
   de pixel, valeur brute — et que la conclusion tirée correspond à ce que ces
   trois lectures montrent. Trois pixels valides d'une tuile réelle : ce sont des
   mesures, et le fait est déclaré. Autre chose : c'est un défaut. Une cellule
   entièrement nulle publiée sans explication reste disqualifiante.
4. **Le compteur qui mord.** Vérifier `lectures_hors_bornes_du_fichier = 0` avec
   pour dénominateur le total des lectures, et
   `cellules_sans_littoral_avec_echantillon_a_zero = 0` avec pour dénominateur
   les cellules sans arête `land-sea` dans `adjacency_g5.json` — recalculer soi-
   même ce dénominateur, ne pas le reprendre du manifeste.
5. **Le code ne s'en remet plus au silence.** Lire `steps/06_relief.py` :
   vérifier que les indices de pixel sont calculés et **contrôlés** avant la
   lecture, et qu'aucun chemin ne s'appuie sur le remplissage par défaut de
   `rasterio.sample()` (`dataset.nodata or 0`, donc `0.0` quand aucune valeur
   `nodata` n'est déclarée — et `tuiles_sans_valeur_nodata_declaree` valait
   1 108 sur 1 108).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, forcer
`sample_count = 0` sur une cellule qui en a réellement — `G6-B` doit
rougir. Forcer `elev_mean_m` d'une cellule à une valeur hors plage (par
exemple 6000) — `G6-C` doit rougir. **[A1]** Forcer un échantillon à la valeur
`nodata` du fichier — il ne doit ni devenir `0,0`, ni compter comme valide, et
`echantillons_nodata_raster` doit augmenter. **[A2]** Demander une altitude à
une latitude exactement entière en forçant l'ancienne règle d'attribution (tuile
du nord) — la lecture doit **lever** en nommant les indices calculés, jamais
rendre `0,0`. **[A2]** Demander une altitude avec des indices de pixel hors du
tableau — la lecture doit lever, pas rendre une valeur.

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
3-bis. **[A1] Où sont réellement les barrières.** La première exécution a
   produit des « barrières » à 14,5 m de franchissement dans les Fens du
   Lincolnshire, parce que les deux centroïdes valaient `0,0` par fabrication.
   Lire `barrieres_par_zone_nommee` et vérifier soi-même : pour chaque
   franchissement de `passes_g6.json`, dans quelle boîte de
   `A12_RELIEF_ZONES` (lue de `constants.py`, jamais recopiée) il tombe, et à
   quelle altitude. Recalculer `zones_hautes_sous_une_zone_basse` : le plus
   grand `elev_max_m` des cellules rencontrant chaque zone déclarée haute doit
   dépasser celui de chaque zone déclarée basse. Une zone haute qui passe sous
   une zone basse est disqualifiante — c'est le contrôle qui aurait fait
   rougir la première exécution.
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

**[A2] Sept cas rouges d'amendement, pas quatre.** Vérifier
`cas_rouges_amendement_non_vides = 7` sur 7, et lire les trois cas ajoutés dans
`tests/test_qa_red_g6.py` : ligne de degré, lecture hors bornes, tuile
fabriquée. Pour chacun, vérifier que la preuve rouge est **non vide** et qu'elle
décrit un échec réel (une erreur levée, une tuile nommée), pas un booléen
retourné. Un cas rouge qui vérifierait qu'une valeur est rendue au lieu d'une
erreur ne prouve rien.

**Résultat attendu** : `code_sortie_run_proof_g6 == 0`, 6/6 verts, 6/6 avec
preuve rouge, 7/7 cas rouges d'amendement non vides.

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

**[A1] Les trois sur-revendications de la première exécution, à revérifier une
par une** :

1. `README.md` affirmait « `barrier_count` est strictement positif sur la
   fenêtre pilote (Pyrénées/Alpes) » sans qu'aucune barrière ne soit
   pyrénéenne. Vérifier que chaque massif nommé dans le `README.md` est appuyé
   par la donnée exportée (SC3, point 3-bis), et recalculer soi-même
   `massifs_revendiques_sans_appui_dans_la_donnee`.
2. `logs/v1_052_relief.log` décrivait « les massifs (Alpes, Pyrénées, Massif
   central) ressortent en teintes claires » alors que les Pyrénées étaient à
   `0,0`. Regarder la capture et confronter la description à ce qui est
   réellement visible. Une description qui ne concorde pas avec l'artefact est
   un échec, pas une approximation.
3. `deliverables/generator-log.md` déclarait ses compteurs « tous conformes aux
   SC ». Vérifier que le journal ne conclut pas au-delà de ce que
   `measure_g6_024.py` mesure réellement.

**[A1]** Vérifier aussi les deux incohérences de forme relevées :
`below_0_land_km2` doit être publié avec la même précision dans
`stats_g6.json`, dans le journal et dans le script de mesure ; la liste « non
livrés » du `README.md` ne doit pas ranger les villes à la fois sous `07` et
sous `07+`.

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

## SC7 — [A1] Couverture DEM complète, et rien de lu hors d'elle

C'est la condition ajoutée par l'amendement 001, et celle sur laquelle la
première exécution a échoué. **À traiter comme la plus importante de cette
itération, à égalité avec SC3.**

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tools/required_dem_tiles.py
```
puis lire `artifacts/dem_required_tiles_g6.json`,
`artifacts/dem_tile_availability_g6.json` et `stats_g6.json`.

**Reconstruction indépendante** :

1. **La liste des tuiles se dérive.** Vérifier que
   `pipeline/geo/tools/required_dem_tiles.py` **importe** les fonctions de
   génération de points de `steps/06_relief.py` au lieu d'en réimplémenter une
   deuxième version, et qu'il n'ouvre aucun raster
   (`grep -n "rasterio\|\.tif" pipeline/geo/tools/required_dem_tiles.py` ne doit
   rien ramener d'une lecture de pixel). Une seconde implémentation qui
   pourrait diverger de celle qui lit réellement les altitudes ne prouve rien.
2. **Les comptes.** Recalculer soi-même, depuis
   `artifacts/dem_required_tiles_g6.json` et `sources.lock` : requises,
   présentes, manquantes, excédentaires. Vérifier que
   `tuiles_manquantes = 0` et `tuiles_excedentaires_restantes = 0`.
   **[A2] Ne pas attendre 1 108, 934 ni 5** : ces trois valeurs de recoupement
   sont **retirées** par l'amendement 002, parce qu'elles ont été obtenues sous
   la règle d'attribution que D19 corrige. Vérifier à leur place l'identité du
   brief : `tuiles_ajoutees − tuiles_excedentaires_retirees =
   tuiles_requises − len(tuiles de l'instantané pré-édition)`, tous les termes
   recalculés par l'Évaluateur. Un Générateur qui aurait « retrouvé » 1 108
   après correction doit expliquer comment, et c'est suspect, pas rassurant.
2-bis. **[A2] La règle de domaine a bien mordu.** Vérifier que
   `tuiles_retirees_par_la_regle_de_domaine` contient
   `Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif` — la tuile inexistante que la
   deuxième exécution avait fabriquée — et que ce nom n'apparaît plus ni dans
   `sources.lock`, ni dans la liste requise, ni dans le cache. Lire
   `tuiles_ajoutees_par_la_regle_de_domaine` : pour chaque tuile ajoutée,
   vérifier dans `artifacts/dem_tile_availability_g6.json` qu'elle répond `200`.
   Vérifier `points_sur_ligne_de_degre` et sa répartition par famille : c'est la
   population que la règle réattribue, et elle doit être publiée, pas déduite.
2-ter. **[A2] Le point qui a déclenché l'amendement.** Vérifier que le journal
   publie nommément le nœud `(12,0°E ; 33,0°N)` de la cellule 9887 : la tuile qui
   le sert désormais (`Copernicus_DSM_COG_30_N32_00_E012_00_DEM.tif`), ses
   indices de pixel et la valeur brute lue. Vérifier soi-même que cette tuile
   figure bien dans le bloc `dem` et répond `200`. Si le journal se contente
   d'affirmer que le cas est réglé sans publier la lecture, la condition n'est
   pas satisfaite.
3. **Les lectures.** Vérifier `echantillons_hors_couverture_dem = 0`, et que
   son dénominateur est bien le total des lectures d'altitude (recoupement
   attendu 11 604 554 = 11 449 061 + 596 + 154 897). Vérifier que
   `couverture_grille`, `couverture_centroides` et `couverture_frontieres`
   égalent chacune leur dénominateur, et `cellules_non_mesurees = 0`.
4. **Le repli a disparu du code, il n'est pas désactivé.** Lire
   `steps/06_relief.py` :
   `grep -n "clamp\|nearest\|plus proche\|best_dist\|best_path" pipeline/geo/steps/06_relief.py`
   ne doit rien ramener. Un bornage gardé derrière un drapeau, une option ou
   une branche morte est disqualifiant : le brief exige une suppression.
5. **L'échec nomme ce qu'il faut.** Vérifier que le message d'erreur de lecture
   hors couverture contient la longitude, la latitude, l'identifiant de cellule
   ou d'arête, et le nom de la tuile nécessaire.
6. **La garde passe avant.** Vérifier dans le code que la comparaison
   couverture-requise / bloc `dem` s'exécute **avant** la première lecture
   d'altitude, pas après l'échantillonnage (règle n° 5).
7. **Le coût est mesuré, pas estimé.** Lire `volume_dem_telecharge_octets` et
   `duree_recuperation_dem_secondes` dans `deliverables/generator-log.md`. Ces
   deux nombres doivent venir d'une mesure ; l'ordre de grandeur annoncé par
   l'amendement (3,6 à 5,3 Go supplémentaires) est une extrapolation, pas une
   référence à laquelle comparer.

**Contre-preuves disqualifiantes** (copie hors dépôt) :

- retirer une tuile requise du bloc `dem` — la garde doit s'arrêter en la
  nommant, avant tout échantillonnage ;
- demander une altitude pour une coordonnée hors de toute tuile — la lecture
  doit échouer en nommant lon/lat/identifiant, jamais rendre `0,0` ;
- réintroduire le bornage de coordonnée — les compteurs de couverture doivent
  cesser d'égaler leurs dénominateurs, et `echantillons_hors_couverture_dem`
  doit devenir non nul ou la lecture échouer.

**[A2] Contre-preuves ajoutées** (copie hors dépôt) :

- rétablir l'ancienne règle d'attribution (tuile du nord pour une latitude
  entière) — les lectures de ligne de degré doivent échouer, et
  `lectures_hors_bornes_du_fichier` devenir non nul ; si tout reste vert et que
  des `0,0` réapparaissent, la condition n'est pas satisfaite ;
- retirer du bloc `dem` une tuile ajoutée par la règle de domaine — la garde de
  couverture doit s'arrêter en la nommant, avant tout échantillonnage.

**Résultat attendu** : couverture requise complète,
`echantillons_hors_couverture_dem = 0`, tuiles requises / présentes /
manquantes / excédentaires publiées et cohérentes, couverture des centroïdes,
de la grille et des frontières égale à son dénominateur,
`cellules_non_mesurees = 0`. **[A2]** `lectures_hors_bornes_du_fichier = 0`,
liste des tuiles re-dérivée et publiée avec son delta, aucune tuile fabriquée.
Aucune maille partielle, sous aucune forme.

---

## SC8 — [A2] Le journal dit ce qui s'est passé, et les preuves sont suivies

**Vérification** :
```
.venv/bin/python harness/queue/briefs/024-geo-relief-g6/deliverables/measure_g6_024.py
git ls-files pipeline/geo/artifacts/*g6* pipeline/geo/logs/*052* pipeline/geo/capture/*052*
git status --porcelain --ignored pipeline/geo/sources/dem_cache/
```

**Reconstruction indépendante** :

1. **Le journal décrit la bonne exécution.** Le journal livré avec la deuxième
   exécution annonçait « 179/179 tuiles vérifiées » et une date du 2026-08-20,
   alors que le bloc `dem` en comptait 1 108 et que les artefacts venaient d'une
   exécution ultérieure. Vérifier que chaque commande citée, son code de sortie
   et sa durée correspondent à l'exécution qui a produit les artefacts présents
   — recouper avec `logs/v1_052_qa.json` et les empreintes des artefacts.
2. **Le journal ne conclut pas.** `conclusions_de_recevabilite_dans_le_journal`
   doit valoir 0. La formule « tous conformes aux SC » du journal précédent, et
   toute variante, est un empiètement sur le rôle de l'Évaluateur : le
   producteur rapporte, il ne prononce pas.
3. **Les nombres du journal viennent de la mesure.** Relancer
   `measure_g6_024.py` et comparer chaque compteur au journal
   (`compteurs_du_journal_egaux_a_la_mesure` égale son dénominateur). Un nombre
   du journal qui ne se retrouve pas dans la sortie du script est saisi à la
   main : disqualifiant.
4. **Les preuves sont suivies.** Recalculer soi-même le dénominateur depuis
   `deliverables/manifest.json`, puis vérifier par `git ls-files` que chaque
   preuve déclarée y figure (`preuves_manquantes_dans_git = 0`). Les preuves qui
   vivent sous des répertoires ignorés doivent avoir été forcées au suivi
   (`git add -f`) **sans commit**. Vérifier en même temps que le cache DEM, lui,
   n'est **pas** suivi : `git ls-files pipeline/geo/sources/dem_cache/` vide.
5. **Le coût est mesuré.** `volume_dem_telecharge_octets`,
   `duree_recuperation_dem_secondes` et l'espace disque libre avant/après
   viennent d'une mesure, jamais d'une estimation reprise de l'amendement 001.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, retirer une preuve
de l'index git — `preuves_manquantes_dans_git` doit devenir non nul. Modifier un
compteur dans le journal sans toucher aux artefacts —
`compteurs_du_journal_egaux_a_la_mesure` doit cesser d'égaler son dénominateur.

**Résultat attendu** : un journal qui raconte l'exécution réelle, ne juge pas, et
dont chaque nombre se rejoue ; toutes les preuves déclarées suivies par git ; le
cache DEM ignoré.

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

**[A1] Ajoutés par l'amendement 001** :

- Une altitude rendue pour une coordonnée qu'aucune tuile ne contient, sous
  quelque forme que ce soit : bornage, repli sur la tuile la plus proche,
  lecture au bord, valeur par défaut, `0,0`.
- Du code de bornage ou de repli encore présent, même désactivé, même derrière
  un drapeau ou dans une branche morte.
- Un `nodata` de raster converti en altitude, compté comme échantillon valide,
  ou une valeur `nodata` écrite en dur dans le code.
- Un dénominateur de tuiles écrit en littéral — `179` ou tout autre — au lieu
  d'être dérivé de `len(dem.tiles)`.
- Une écriture dans `sources.lock` hors de son objet `dem`, ou une
  modification de `dem.licence` ou du texte d'attribution Copernicus.
- Une empreinte du bloc `dem` qui ne correspond pas au fichier du cache, ou
  saisie autrement que par recalcul.
- Plus d'une recette d'empreinte collective présente dans le dépôt, ou une
  recette essayée à l'exécution.
- La convention d'ouest fautive (`W001` traité comme `[−2, −1)`), ou l'absence
  de la comparaison nom-contre-raster sur toutes les tuiles.
- Une maille partielle : G6 restreint à l'emprise couverte, des cellules
  durablement déclarées non mesurées, ou une exigence de couverture abaissée
  pour faire passer le lot.
- Un nombre de recoupement du brief (1 108, 934, 5) ajusté en silence pour
  coïncider avec un résultat, sans reconstruction mécanique qui prouve l'écart.
- Un massif nommé dans le `README.md`, un journal ou une description de capture
  sans que la donnée exportée l'appuie.
- `pipeline/geo/constants.py`, `pipeline/geo/pipeline.py` ou
  `pipeline/geo/qa/checks.py` modifiés : l'amendement 001 n'accorde aucune
  dérogation sur ces trois fichiers.
- `rubrique_amendee_apres_revue` absent ou différent de 1 : le fait que cette
  rubrique ait été amendée après le verdict doit rester visible dans le
  verdict.

**[A2] Ajoutés par l'amendement 002** :

- Une tuile DEM **fabriquée** localement, sous quelque forme que ce soit :
  raster écrit par le dépôt, tuile remplie de zéros, fichier du cache qui ne
  répond pas `200` au sondage de disponibilité.
- Une empreinte inscrite dans `sources.lock` pour un fichier qui n'a pas été
  téléchargé du dépôt public.
- Du code de synthèse de tuile encore présent, même désactivé, même derrière une
  option (`--synthesize-missing` ou équivalent).
- Une lecture d'altitude dont les indices de pixel ne sont pas vérifiés avant la
  lecture, ou qui s'en remet au remplissage silencieux de `rasterio.sample()`
  pour signaler une absence.
- L'appartenance d'un point à une tuile déduite du **nom** de la tuile au lieu du
  domaine indexable lu dans le fichier ; ou la règle de D19 affirmée sans être
  prouvée tuile par tuile (`tuiles_regle_domaine_conforme` absent ou inférieur à
  son dénominateur).
- `lectures_hors_bornes_du_fichier` différent de 0, ou absent.
- `cellules_sans_littoral_avec_echantillon_a_zero` différent de 0 sans que
  chaque cellule concernée soit nommée et prouvée pixel par pixel.
- Une cellule entièrement nulle (`elev_min_m`, `elev_mean_m`, `elev_max_m`,
  `centroid_elev_m`, `roughness_m` et `slope_mean_deg` tous à `0,0`) publiée sans
  que ses lectures soient publiées — la cellule **1492** en particulier.
- Un `cellules_altitude_min_nulle` resté de l'ordre de 576 sur 596 : le défaut
  n'a pas été corrigé, quels que soient les contrôles verts par ailleurs.
- Les valeurs 1 108, 934 ou 5 réintroduites comme cibles à atteindre après
  correction (elles sont retirées), ou une nouvelle valeur ajustée pour
  coïncider avec elles.
- `registrement_dem_mesure` absent, ou une tolérance de comparaison de bornes
  écrite en dur au lieu d'être dérivée du registrement mesuré.
- `cas_rouges_amendement_non_vides` différent de 7.
- Un `deliverables/generator-log.md` décrivant une autre exécution que la
  dernière, contenant une conclusion de recevabilité, ou un compteur qui ne se
  retrouve pas dans la sortie de `measure_g6_024.py`.
- Une preuve déclarée au manifeste absente de `git ls-files`, ou une tuile DEM
  qui, elle, s'y trouverait.
