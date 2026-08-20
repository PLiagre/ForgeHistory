# Brief 024 : le relief (G6) — altitude, pente, rugosité, barrières et cols

**Authored**: 2026-08-20T08:15:00Z
**Author**: forge-planificateur

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude
> Code (CTO), invoqué en session interactive **par Hermes, pilote du projet,
> sur décision du propriétaire** (pas via `forgepilot plan` cette fois, et
> pas invoqué directement par le propriétaire), à partir de la tâche
> autoritaire `hermes/requests/DEMANDE-20260820-g6-relief.md` (statut
> `HANDED_TO_CTO`) et de `ROADMAP.md` (F1, jalon E1, « Prochaines étapes »
> point 7 : « Poursuivre F1 avec G6 relief, climat et ressources »). Cette
> session n'a ni modifié `ROADMAP.md`, ni `hermes/**`, ni lancé Cursor, ni
> committé — conformément au mandat reçu.

---

## Provenance

Ce brief est le **troisième lot atomique** du jalon E1 — Fondations monde,
après le brief 019 (adjacence maritime G4, fusionné) et le brief 021
(fleuves G5, fusionné, PR #107). E1 n'est toujours pas clos : après ce lot
restent le climat et les ressources.

`harness/queue/geo-pipeline-port-plan.md` (brief 004, « topic only ») avait
déjà signalé, avant qu'aucun des deux ne soit sourcé, deux risques propres à
ce lot : (a) `06_relief.py` introduit une dépendance binaire volumineuse (179
tuiles Copernicus DEM, ~644 Mo) dont le mode de transport dans le dépôt
« doit être décidé, pas silencieusement supposé en `git add` » ; (b) le
script original (`sandbox/geo/steps/06_relief.py` sur la machine Windows du
propriétaire) n'est **pas accessible depuis cette machine Linux** — vérifié :
aucun fichier `06_relief.py`, aucun arbre `sandbox/geo` ni `VictoriaProject`
n'existe nulle part sur ce dépôt ni cette machine. Ce brief tranche (a) en
D2 et répond à (b) juste en dessous.

**Vérifié sur le dépôt au moment de l'écriture de ce brief : G6 est déjà
entièrement pré-câblé**, exactement comme G4 l'était avant le brief 019 et G5
avant le brief 021 :

- `pipeline/geo/constants.py` (lignes 357-390) porte déjà toutes les
  constantes G6 : `G6_DEM_NATIVE_DEG`, `G6_SAMPLE_STRIDE_PX`,
  `G6_SAMPLE_STEP_DEG`, `G6_EDGE_SAMPLE_STEP_M`, `G6_ELEV_DECIMALS`,
  `G6_SLOPE_DECIMALS`, `G6_ROUGH_DECIMALS`, `G6_SAMPLE_VALID_MIN_M` (−80),
  `G6_SAMPLE_VALID_MAX_M` (4800), `G6_ELEV_PLAUSIBLE_MIN_M`,
  `G6_ELEV_PLAUSIBLE_MAX_M`, `G6_KNOWN_PASS_MATCH_M` (20 000 m),
  `G6_REGISTRY_CREATED`, `G6_KNOWN_PASSES` (9 cols historiques nommés,
  Pyrénées/Alpes, `(id, nom, lon, lat)`), `G6_PIPELINE_VERSION =
  "1.7.0-g6-v1_052"`.
- `pipeline/geo/qa/checks.py` (lignes 1375-1495) porte déjà `run_g6_green`
  et les cinq contrôles qu'il assemble (plus `Q10` déterminisme) : `G6-A`
  (empreinte DEM vérifiée avant lecture), `G6-B` (chaque cellule terrestre
  échantillonnée), `G6-C` (altitudes plausibles), `G6-D` (toute arête
  barrière a un franchissement au-dessus des deux centroïdes), `G6-E`
  (maille inchangée — mêmes identifiants que G3). Ces signatures **fixent**
  les contrats de données exacts que `steps/06_relief.py` doit produire
  (D3-D7 ci-dessous).
- `pipeline/geo/pipeline.py` (lignes 944-1010, 1138-1152) porte déjà
  `_load_relief_module()`, `run_relief_g6()` et la branche
  `args.source == "relief"`, avec sa ligne de résumé exacte : `result['projection'].epsg`,
  `result['metrics']['cell_count']`, `result['metrics']['elev_distribution']['median']`,
  `result['metrics']['barrier_count']`, `result['metrics']['pass_count']`,
  `result['metrics']['below_0_land_km2']`, `result['captures']`,
  `result['shas']`. Le crochet est **déjà câblé** ; il attend un module
  `steps/06_relief.py` qui n'existe pas.
- `pipeline/geo/sources.lock` déclare déjà le bloc `dem` complet : 179
  tuiles nommées, chacune avec `bytes` et `sha256`, plus
  `collective_sha256`, `tile_count=179`, `total_bytes=644127181`, et la
  licence Copernicus (attribution obligatoire, texte déjà figé). C'est la
  **seule source de vérité** pour la liste des tuiles requises — ce lot ne
  la recalcule pas, il la lit.
- `pipeline/geo/qa/checks.py` (ligne 1304) et `constants.py` (ligne 170,
  bloc `A12_UNCHANGED_ARTIFACTS`) référencent déjà, par avance, le nom exact
  de l'artefact cellule que ce lot doit produire :
  **`artifacts/cells_relief_g6.json`** — un futur lot (G5-ter, A12) en fait
  déjà une empreinte figée à ne jamais changer sans le savoir. Ce nom n'est
  **pas** un choix de ce brief, il est imposé par du code déjà écrit.

**N'existe nulle part** : `pipeline/geo/steps/06_relief.py`,
`pipeline/geo/tests/run_proof_g6.py`, `pipeline/geo/tests/test_qa_red_g6.py`,
tout artefact G6, toute tuile Copernicus DEM sur le disque de cette machine
(`pipeline/geo/sources/` ne contient que `10m_physical.zip`) ou ailleurs sur
le dépôt.

Ce lot est **neuf et autonome**, comme les briefs 019 et 021 l'ont établi
pour G4 et G5. À partir d'ici, **ce `brief.md` est la SEULE instruction**
(voir `CLAUDE.md` › Single Source of Instruction).

**Ce que ce lot ne fait pas :** `registry/g6_density_refinement.json`
(committé, lu pour information) documente un raffinement de densité G3
*attendu* une fois le relief disponible (« cellules plus grandes en
montagne/forêt ») — **hors de portée ici** : ce lot ne touche ni au semis
G3, ni à `constants.py` (D12), ni à la maille (`G6-E` l'interdit
mécaniquement). Le climat, les ressources, les villes (G7), la possession
(G8), les LOD (G9), les textures d'identifiants (G10) et l'apparence/ombrage
(A12, qui **consomme** `cells_relief_g6.json` — biomes, hillshade) sont tous
des lots futurs, non traités ici.

---

## World-Terms Requirement

**Chaîne causale.**

Le relief n'est pas un décor. Il fait trois choses physiquement distinctes,
et ce lot les établit parce que le monde en aura besoin plus tard :

1. **Il détermine ce qu'on cultive et où on s'installe.** Une cellule de
   plaine basse retient l'eau et la terre arable ; une cellule de haute
   montagne a un sol mince, un hiver long, une saison de culture courte.
   Ce lot ne code aucune règle agricole — il fournit l'altitude, la pente et
   la rugosité mesurées, la seule donnée physique dont un futur système
   agricole aura besoin pour en dériver ses propres conséquences (interdit :
   « si altitude > 1500 m alors rendement −50 % » codé ici).
2. **Il sépare, plus fort qu'une simple frontière de cellule.** Deux
   cellules voisines par la terre (arête `land-land` héritée de G4/G5)
   peuvent être néanmoins coupées par une chaîne de montagnes : on ne les
   traverse pas comme un simple champ. C'est pourquoi ce lot **enrichit**
   les arêtes `land-land` de G5, il ne les recrée pas — même principe que G5
   envers G4.
3. **Il autorise un passage, à un endroit précis et à un coût.** Une arête
   barrière n'est pas infranchissable : elle a un point de franchissement
   mesuré (le col), plus haut que les deux versants, mais réel. Une armée,
   un marchand ou une famille qui migre passe **par ce point**, pas
   n'importe où sur la frontière. C'est un fait géographique, pas un droit
   de passage accordé par une règle de jeu.

**Interdit** dans ce lot, comme dans les briefs 019 et 021 : aucun barème,
aucun malus de déplacement, aucun coût de franchissement en unités de jeu.
Ce lot établit ce que le relief **est** et ce qu'il **permet ou empêche
géométriquement** — sa traduction en coût de marche, en rendement agricole
ou en positionnement défensif est un lot futur de `sim/`.

**Rien n'est inventé au-delà de ce que le MNT (modèle numérique de terrain)
Copernicus encode.** L'échantillonnage, les décimales, les plages de
validité et les 9 cols historiques nommés sont déjà déclarés dans
`constants.py` — ce lot les **lit**, il ne les choisit pas.

---

## Vocabulaire (expliqué une fois, dérivé du code lu)

- **MNT (modèle numérique de terrain)** : la grille d'altitudes Copernicus
  DEM GLO-90, résolution native ≈ 90 m (`G6_DEM_NATIVE_DEG`), distribuée en
  179 tuiles COG (Cloud-Optimized GeoTIFF) déclarées dans `sources.lock`.
- **échantillon** : une lecture ponctuelle d'altitude sur la grille
  régulière `G6_SAMPLE_STEP_DEG` (pas = 10 pixels natifs ≈ 900 m,
  `G6_SAMPLE_STRIDE_PX`) à l'intérieur du polygone d'une cellule.
- **échantillon valide** : un échantillon dont l'altitude tombe dans
  `[G6_SAMPLE_VALID_MIN_M, G6_SAMPLE_VALID_MAX_M]` = `[−80 m, 4800 m]`.
  Hors de cette plage, l'échantillon est un artefact du MNT (carrière
  moderne type Hambach ≈ −250 m) — **exclu avant tout calcul de moyenne**,
  jamais confondu avec un polder réel (≈ −7 m, qui reste dans la plage).
- **pente (slope)** : dérivée par gradient central sur la grille
  d'échantillons (dz/dx, dz/dy en mètres locaux projetés), en degrés.
- **rugosité (roughness)** : écart-type **population** des altitudes
  échantillonnées valides d'une cellule, en mètres.
- **centroïde** : le champ `centroid` déjà présent sur chaque cellule de
  `cells_g3.json` — ce lot n'en recalcule pas la position, il y lit une
  seule altitude MNT.
- **frontière partagée** : la ligne d'intersection entre les polygones
  `geometry` de deux cellules `land-land` voisines (re-dérivée depuis
  `cells_g3.json`, comme G5 a re-dérivé terre/mer depuis G2-bis — voir D1).
- **franchissement (crossing)** : le point de plus basse altitude
  échantillonné le long d'une frontière partagée, au pas
  `G6_EDGE_SAMPLE_STEP_M` (500 m).
- **barrière (`relief_barrier`)** : une arête `land-land` dont le
  franchissement est **plus haut que les deux** centroïdes des cellules
  qu'elle sépare — condition exacte vérifiée par `G6-D`. Un simple versant
  incliné (le franchissement est plus bas qu'au moins un centroïde) n'est
  **pas** une barrière : on descend pour passer d'un côté au moins.
- **col (pass)** : le franchissement d'une arête barrière, nommé s'il tombe
  à moins de `G6_KNOWN_PASS_MATCH_M` (20 km) d'un des 9 cols historiques de
  `G6_KNOWN_PASSES`, sinon porteur d'un identifiant neutre (D7).

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture de ce brief :

- `pipeline/geo/artifacts/cells_g3.json` : committé, lecture seule. Chaque
  cellule porte `cell_id`, `geometry` (Polygon), `centroid`, `area_km2` —
  toutes les cellules de ce fichier sont des cellules **terrestres** (la
  maille G3 ne couvre que la terre ; confirmé par le compte G4 : 372
  cellules littorales sur 596 cellules totales, aucune cellule
  exclusivement marine dans `cells_g3.json`).
- `pipeline/geo/artifacts/adjacency_g5.json` : committé, lecture seule.
  Chaque arête porte `a`, `b`, `kind`, `shared_length_m`, et pour les arêtes
  `land-land` déjà enrichies par G5 : `fluvial_artery`, `artery_rivers`.
  **Ce lot ne les modifie pas en place** (D9, même principe que G5 envers
  G4).
- `pipeline/geo/sources.lock`, bloc `dem` : 179 tuiles nommées
  (`Copernicus_DSM_COG_30_<lat>_00_<lon>_00_DEM.tif`), chacune avec `bytes`
  et `sha256`, plus `collective_sha256`, `tile_count=179`,
  `total_bytes=644127181`.
- **Accès réseau probé (lecture seule, aucune tuile téléchargée par ce
  brief)** : le compartiment public `copernicus-dem-90m.s3.amazonaws.com`
  répond, sans authentification, à une requête `HEAD` sur le motif de clé
  `<stem>/<stem>.tif` (répertoire nommé comme la tuile, puis le fichier),
  par exemple `Copernicus_DSM_COG_30_N42_00_E000_00_DEM/Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif`
  → `200 OK`. Le motif à plat (`<stem>.tif` sans répertoire) → `404`. Ceci
  est une **reconnaissance**, pas une preuve d'accès complet aux 179
  tuiles : le Générateur doit vérifier lui-même chaque tuile (D2), y
  compris la possibilité que le motif diffère pour une tuile particulière
  ou que l'accès soit throttlé sur un volume de 644 Mo.
- `pipeline/geo/qa/checks.py` (lignes 1375-1495) : signatures exactes citées
  ci-dessus (Provenance).
- `pipeline/geo/pipeline.py` : contrat de retour exact cité ci-dessus.
- `pipeline/geo/steps/05_rivers.py` : patron de module à suivre (chargement
  dynamique optionnel de `03_cells.py`, export d'une fonction `run_*` sans
  argument).

**Ce lot n'est pas un portage octet-à-octet** — il ne peut d'ailleurs pas
l'être : le script original `06_relief.py` n'est pas accessible depuis cette
machine (Provenance). Comme pour G4 et G5, c'est une **reconstruction**
contre la barre qualité déjà écrite (`qa/checks.py`, les constantes `G6_*`,
le crochet de `pipeline.py`).

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit librement les noms
de fonctions/variables internes et l'organisation du code dans le périmètre
autorisé.

### D1 — Entrées exactes

Le nouveau module lit, en lecture seule :

| entrée | usage |
|---|---|
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry`, `centroid`, `area_km2` — grille d'échantillonnage, centroïde, frontières partagées (re-dérivées par intersection de polygones, jamais stockées ailleurs) |
| `pipeline/geo/artifacts/adjacency_g5.json` | `adjacency[]` filtré sur `kind == "land-land"` — jamais modifié en place (D9) |
| `pipeline/geo/sources.lock`, bloc `dem` | 179 tuiles attendues (nom, `bytes`, `sha256`), `collective_sha256` — **lu**, jamais recopié en littéral |
| `pipeline/geo/constants.py` | toutes les bornes, pas d'échantillonnage et cols G6, **lus**, jamais recopiés en littéral |
| cache DEM local (D2) | 179 tuiles COG, vérifiées avant toute lecture d'altitude |

### D2 — Les tuiles DEM : cache hors dépôt, jamais committées ; vérification par tuile ET collective

**C'est la décision que le plan de portage (`geo-pipeline-port-plan.md`,
brief 004) a explicitement demandé de ne pas laisser au Générateur.**

Constat mesuré avant de trancher : le plus gros artefact déjà committé du
pipeline (`sources/10m_physical.zip`) pèse 52 Mo ; le bloc DEM en pèse
644 Mo, douze fois plus. Aucun mécanisme Git LFS n'existe dans ce dépôt
(`git check-attr` / `.gitattributes` vérifiés : aucune règle `filter=lfs`) —
`ROADMAP.md` réserve explicitement Git LFS au futur worker Unity Windows,
pas à ce pipeline Linux. Le disque de cette machine a 90 Go disponibles
(vérifié) : la place n'est pas le problème, la taille de l'historique Git
l'est.

**Décision : les 179 tuiles vont dans `pipeline/geo/sources/dem_cache/`, un
répertoire nouveau, ajouté à `.gitignore`, jamais committé.** Elles sont
**reproductibles** (même source publique, mêmes empreintes déclarées dans
`sources.lock`) — un clone frais les retélécharge et les revérifie, il ne
les hérite pas de l'historique Git. C'est cohérent avec l'exclusion déjà en
place de `.venv/`, `build/`, `artifacts/`, `logs/`, `capture/` (ces trois
derniers étant néanmoins forcés au commit par preuve individuelle, D10 —
la différence ici est le volume : 644 Mo de raster ne sont **jamais**
forcés au commit, aucune exception).

Chaque tuile est téléchargée (source publique Copernicus DEM, motif de clé
`<stem>/<stem>.tif` sur `copernicus-dem-90m.s3.amazonaws.com`, vérifié
accessible ci-dessus, aucune signature AWS requise) puis vérifiée par son
propre `sha256` déclaré dans `sources.lock`. `G6-A`
(`g6a_dem_fingerprint_verified`) est vert seulement si **les 179 empreintes
individuelles ET l'empreinte collective** (recalculée à partir des 179
tuiles présentes, jamais recopiée) correspondent exactement à
`sources.lock`. Une tuile manquante, corrompue, ou dont l'empreinte diverge
fait échouer `G6-A` avant toute lecture d'altitude — jamais un silence, une
tuile de secours ou une valeur inventée (règle n° 10).

Un script de récupération/vérification, committé sous
`pipeline/geo/tools/fetch_dem_tiles.py`, est **idempotent** : relancé sur un
cache déjà complet et vérifié, il ne retélécharge rien et sort en confirmant
les 179 empreintes. `steps/06_relief.py` appelle ce module (ou sa fonction
de vérification) avant tout échantillonnage — jamais un chemin qui suppose
silencieusement le cache déjà rempli.

### D3 — Grille d'échantillonnage par cellule (contrat `G6-B`/`G6-C`)

Pour chaque cellule de `cells_g3.json` :

1. Générer les points de la grille régulière lon/lat (pas
   `G6_SAMPLE_STEP_DEG`) qui tombent à l'intérieur du polygone `geometry`
   de la cellule.
2. Lire l'altitude MNT à chaque point.
3. Retenir uniquement les échantillons dans
   `[G6_SAMPLE_VALID_MIN_M, G6_SAMPLE_VALID_MAX_M]` (D « échantillon
   valide » du Vocabulaire) — les autres sont exclus **avant** tout calcul
   de statistique, et le compte d'exclusions est mesuré et rapporté
   (`echantillons_exclus_hors_plage`), jamais tu.
4. `sample_count` = nombre d'échantillons valides retenus. Si une cellule
   n'a **aucun** échantillon valide (cas non attendu sur la fenêtre pilote,
   mais possible en théorie sur une cellule minuscule), c'est une
   impossibilité au sens de la règle n° 9 : `sample_count = -1` (sentinelle,
   jamais `0` silencieux) et `G6-B` rouge nommant la cellule — pas une
   valeur inventée.
5. `elev_mean_m`, `elev_min_m`, `elev_max_m` : moyenne/min/max des
   échantillons valides, arrondis à `G6_ELEV_DECIMALS`.

### D4 — Altitude au centroïde (repli du contrat `G6-D`)

`centroid_elev_m` = une lecture MNT **unique**, au point `centroid` déjà
présent sur la cellule (pas une moyenne de grille, une valeur ponctuelle
distincte de `elev_mean_m`). C'est le champ que `g6d_barrier_above_both_cells`
lit en priorité (avec repli sur `elev_mean_m` si absent, `qa/checks.py`
ligne 1445) — ce lot le fournit toujours, le repli du contrôle ne doit
jamais être sollicité en pratique.

### D5 — Pente et rugosité : noms de champs tranchés ici

Aucun consommateur déjà écrit ne fixe le nom des champs pente/rugosité
(seuls `G6_SLOPE_DECIMALS`/`G6_ROUGH_DECIMALS` existent, sans nom de champ
associé) — c'est donc une décision de ce brief, comme D3 du brief 021 pour
artère/croisement/mixte :

- `slope_mean_deg` : moyenne, sur les échantillons valides de la cellule,
  de la pente locale calculée par gradient central sur la grille
  d'échantillonnage (dérivées `dz/dx`, `dz/dy` en mètres locaux projetés,
  convertie en degrés), arrondie à `G6_SLOPE_DECIMALS`. Méthode déjà
  déclarée dans le commentaire de `constants.py` ligne 359 (« Pente via
  gradient central sur cette grille ») — ce lot ne l'invente pas, il la
  nomme et l'implémente.
- `roughness_m` : écart-type **population** (diviseur `N`, pas `N−1`) des
  altitudes échantillonnées valides de la cellule, en mètres, arrondi à
  `G6_ROUGH_DECIMALS`. Méthode déjà déclarée au même endroit (« Rugosité =
  écart-type population des altitudes »).

### D6 — Barrières et franchissements (contrat `G6-D`)

Pour chaque arête `land-land` de `adjacency_g5.json` :

1. Calculer la frontière partagée réelle par intersection des polygones
   `geometry` des deux cellules `a`/`b` (pas de recours à
   `shared_length_m` seul, qui ne porte pas de géométrie).
2. Échantillonner l'altitude le long de cette frontière au pas
   `G6_EDGE_SAMPLE_STEP_M` (500 m projetés).
3. `crossing_elev_m` = altitude minimale trouvée le long de la frontière ;
   `crossing_lon`/`crossing_lat` = sa position.
4. `relief_barrier = true` si et seulement si `crossing_elev_m` est
   strictement supérieur aux **deux** `centroid_elev_m` des cellules `a` et
   `b` (D4) — exactement la condition que `G6-D` vérifie
   (`crossing > centroids des deux côtés`). Sinon `relief_barrier` est
   absent ou `false`, et l'arête reste une arête `land-land` ordinaire (pas
   de champs de relief ajoutés, même principe que G5 pour les arêtes sans
   fleuve).

Une arête `land-land` sans frontière partagée mesurable (intersection vide
malgré l'adjacence déclarée) est une incohérence entre G3/G5 et ce lot —
c'est un motif d'escalade (Waivers), pas un `relief_barrier=false` silencieux.

### D7 — Cols nommés vs dérivés, et l'invariant `pass_count == barrier_count`

Chaque arête `relief_barrier=true` produit **exactement un** enregistrement
de col dans `artifacts/passes_g6.json` — un franchissement mesuré par
barrière, jamais zéro, jamais deux (les deux cellules d'une même barrière
ne produisent pas chacune leur propre col).

- Si le point de franchissement (D6.3) tombe à moins de
  `G6_KNOWN_PASS_MATCH_M` (20 km) d'un des 9 cols de `G6_KNOWN_PASSES`, le
  col reçoit l'`id` et le `nom` du plus proche (départage par distance
  minimale, plus petit `id` si égalité — même règle de départage que G4 pour
  les noms de mer, brief 019).
- Sinon, le col reçoit un identifiant neutre dérivé, jamais un nom inventé :
  `pass_id = f"g6_derived_{min(a,b)}_{max(a,b)}"` (les deux cellules qu'il
  sépare, ordre canonique), `nom = null`.

**Invariant mesuré, mordant** : `pass_count` (dans `stats_g6.json`) est égal
à `barrier_count` **exactement** — c'est une condition de succès (SC3), pas
une simple observation. `passes_nommes_trouves` (parmi les 9 de
`G6_KNOWN_PASSES`) est un compte séparé, mesuré et rapporté, sans plancher
imposé — même posture que `fleuves_nommes_trouves` en G5.

### D8 — Terre sous le niveau de la mer (`below_0_land_km2`)

Somme de `area_km2` (lu de `cells_g3.json`, jamais recalculé) sur les
cellules dont `elev_mean_m < 0`. Grain cellule, cohérent avec `G6-C` qui
opère aussi au niveau cellule. Peut être `0.0` si aucune cellule n'est
concernée — mesuré et rapporté, `0.0` n'est alors pas confondu avec « non
calculé » (règle n° 8 : la sentinelle est `-1`, réservée à un échec de
calcul, jamais à un fait mesuré nul).

### D9 — Sorties exactes, et `adjacency_g5.json`/`cells_g3.json` restent intouchés

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `artifacts/cells_relief_g6.json` | par cellule (nom **imposé** par du code déjà écrit, Provenance) : `cell_id`, `sample_count`, `elev_mean_m`, `elev_min_m`, `elev_max_m`, `centroid_elev_m`, `slope_mean_deg`, `roughness_m` |
| `artifacts/adjacency_g6.json` | **copie enrichie**, distincte de `adjacency_g5.json` : toutes les arêtes de G5 recopiées telles quelles, plus `relief_barrier`/`crossing_elev_m`/`crossing_lon`/`crossing_lat`/`pass_id` ajoutés sur les arêtes `land-land` concernées (D6). `adjacency_g5.json` n'est ni réécrit ni modifié en place — même principe que D7 du brief 021 envers `adjacency_g4.json` |
| `artifacts/passes_g6.json` | les cols (D7) : `pass_id`, `nom` (nullable), `edge_a`, `edge_b`, `lon`, `lat`, `elev_m` |
| `artifacts/stats_g6.json` | `cell_count`, `elev_distribution` (au moins `median`, requis par `pipeline.py` ; `min`/`max`/`mean` recommandés), `barrier_count`, `pass_count`, `passes_nommes_trouves`, `below_0_land_km2`, `echantillons_exclus_hors_plage` |
| `artifacts/MANIFEST_g6.json` | version, projection, `inputs` (empreintes calculées à l'exécution : `adjacency_g5.json`, `cells_g3.json`, `sources.lock`), `outputs` (empreintes des sorties) |
| `registry/relief_registry.json` | registre des cellules de relief émises, date `G6_REGISTRY_CREATED` |
| `pipeline/geo/sources/dem_cache/` | 179 tuiles vérifiées (D2), **non committées** |
| `pipeline/geo/tools/fetch_dem_tiles.py` | script de récupération/vérification (D2), idempotent |
| `logs/v1_052_relief.log` | journal lisible de la preuve (tag `v1_052`, dérivé de `G6_PIPELINE_VERSION`, non colliding avec les tags déjà committés `v1_049`/`v1_050`/`v1_051`/`v1_060`) |
| `logs/v1_052_qa.json` | rapport : tableau `checks` (6 entrées, `passed` + `red_proof`) + `determinism.sha256` |
| `capture/v1_052_elevation_window.png` | altitude par cellule sur la fenêtre pilote (palette continue, pas un ombrage — le hillshade est A12, hors de portée) |
| `capture/v1_052_barriers_passes.png` | zoom sur un secteur montrant les arêtes barrières et leurs cols (nommés vs dérivés distingués visuellement) — capture **regardée et décrite** dans le journal (règle n° 11, comme les briefs 019/021 l'exigent pour leurs propres captures) |
| `steps/06_relief.py` | le nouveau module (exporte `run_relief()`, sans argument — contrat déjà fixé par `pipeline.py`) |
| `tests/test_qa_red_g6.py` | cas rouges, un par contrôle (D11) |
| `tests/run_proof_g6.py` | script de preuve (D10) |
| `README.md` | mise à jour (SC6) |

**Deux couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json` :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md`
   publié.
2. `artifacts/adjacency_g5.json` (référence, non modifié) ↔
   `artifacts/adjacency_g6.json` (copie enrichie) — la preuve mécanique que
   l'enrichissement a produit un fichier réellement différent.

### D10 — Déterminisme : deux passes, empreintes comparées

`tests/run_proof_g6.py` :

1. vérifie le cache DEM une fois (D2, `G6-A`) ;
2. charge une fois les cellules G3 et l'adjacence G5 ;
3. exécute la dérivation **et l'export complet** deux fois ;
4. compare, empreinte par empreinte, les artefacts des deux passes (`Q10`) :
   chaque paire doit être égale et non vide ;
5. écrit `logs/v1_052_qa.json` et `logs/v1_052_relief.log` ;
6. rend le code de sortie 0 si et seulement si les six contrôles de
   `run_g6_green` sont verts, chacun avec une preuve rouge non vide, et les
   deux passes identiques.

Aucune horloge murale, aucun horodatage courant dans un artefact. Le
téléchargement des tuiles (D2) n'est **pas** rejoué à chaque passe : une
fois le cache vérifié, les deux passes de dérivation le relisent tel quel
(seule l'échantillonnage/dérivation est rejouée deux fois, pas le réseau).

### D11 — Preuve rouge d'abord

`tests/test_qa_red_g6.py` fournit **un cas rouge par contrôle** des six
assemblés par `run_g6_green` : `Q10`, `G6-A`, `G6-B`, `G6-C`, `G6-D`, `G6-E`.
Chaque cas est une mutation locale explicite sur une copie en mémoire (par
exemple une empreinte de tuile falsifiée pour `G6-A` ; un `sample_count=0`
forcé pour `G6-B` ; une altitude hors plage pour `G6-C` ; une arête
`relief_barrier=true` avec `crossing_elev_m` sous un centroïde pour `G6-D` ;
un `cell_id` retiré de la maille de sortie pour `G6-E`). Aucun cas ne passe
par une modification de `qa/checks.py`. Un `red_proof` vide vaut échec du
contrôle, même si le vert est vert.

### D12 — Bornes non modifiables

**Interdiction ferme :** aucune valeur de `pipeline/geo/constants.py` n'est
modifiée par ce lot — y compris `.gitignore` du répertoire `pipeline/geo/`,
qui reçoit une seule ligne ajoutée (`sources/dem_cache/`), jamais une
réécriture. Une borne inatteignable sur la fenêtre pilote réelle s'escalade
(Waivers), elle ne se déplace pas.

### D13 — Périmètre de fichiers

**Autorisé (création ou modification) :**

- `pipeline/geo/steps/06_relief.py` (nouveau) ;
- `pipeline/geo/tools/fetch_dem_tiles.py` (nouveau) ;
- `pipeline/geo/tests/run_proof_g6.py`, `pipeline/geo/tests/test_qa_red_g6.py`
  (nouveaux) ;
- `pipeline/geo/README.md` (SC6) ;
- `pipeline/geo/.gitignore` (une ligne ajoutée, D12) ;
- les artefacts, journaux, registre, captures et cache listés en D9 ;
- `harness/queue/briefs/024-geo-relief-g6/deliverables/**` ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée).

**Interdit (lecture seule, ou hors périmètre) :** `pipeline/geo/constants.py` ;
`pipeline/geo/qa/checks.py` ; `pipeline/geo/pipeline.py` ;
`pipeline/geo/io_util.py` ; `pipeline/geo/projection.py` ;
`pipeline/geo/steps/02_coastline.py` ; `pipeline/geo/steps/02b_corrections_1400.py` ;
`pipeline/geo/steps/03_cells.py` ; `pipeline/geo/steps/03b_align_coastline_provenance.py` ;
`pipeline/geo/steps/04_adjacency.py` ; `pipeline/geo/steps/05_rivers.py` ;
`pipeline/geo/sources.lock` ; `pipeline/geo/sources/10m_physical.zip` ;
tous les artefacts et registres G2/G2-bis/G3/G4/G5 déjà committés (y compris
`adjacency_g5.json`, `cells_g3.json`, D9) ; tout fichier sous `sim/` ou
`unity/` ; `harness/*.py` ; `harness/pipeline/` ; `architecture/` ;
`docs/adr/**` ; `VISION.md` ; `ROADMAP.md` ; `hermes/**` ; `HANDOFF.md` ;
`.github/**` ; les archives des briefs 001 à 023.

---

## Success Conditions

### SC1 — Le cache DEM est complet et vérifié avant toute lecture (`G6-A`)

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_g6.py
```

- `tuiles_verifiees` = 179 sur 179, chacune avec son `sha256` déclaré dans
  `sources.lock` égal à celui du fichier sur disque.
- `empreinte_collective_egale` = vraie : l'empreinte recalculée sur les 179
  tuiles présentes égale `dem.collective_sha256` de `sources.lock`.
- `G6-A` vert.

### SC2 — Toute cellule terrestre est échantillonnée, altitudes plausibles (`G6-B`, `G6-C`)

- `cellules_sans_echantillon` = 0 (ou la sentinelle `-1` documentée si une
  impossibilité réelle est rencontrée, D3.4 — pas un `0` qui masquerait un
  échec).
- `echantillons_exclus_hors_plage` mesuré et rapporté (peut être 0).
- `G6-B` et `G6-C` verts.

### SC3 — Barrières et cols cohérents, l'invariant tient (`G6-D`)

- `barrier_count` strictement positif : au moins une frontière réellement
  classée barrière sur la fenêtre pilote (Pyrénées et/ou Alpes) — la preuve
  que la classification a réellement mordu, pas seulement que le script
  s'est terminé.
- `pass_count == barrier_count` exactement (D7).
- `passes_nommes_trouves` mesuré, sans plancher imposé.
- `G6-D` vert : sur `artifacts/adjacency_g6.json`, toute arête
  `relief_barrier=true` porte un `crossing_elev_m` strictement supérieur
  aux deux `centroid_elev_m` des cellules qu'elle sépare.
- `adjacency_g5_inchange` vaut 1 : `git status --porcelain` sur
  `pipeline/geo/artifacts/adjacency_g5.json` est vide, **et**
  `artifacts/adjacency_g6.json` diffère de `artifacts/adjacency_g5.json`
  (couple `must_differ_from`, D9).

### SC4 — La maille est inchangée (`G6-E`)

- `G6-E` vert : les `cell_id` de `cells_relief_g6.json` sont exactement
  ceux de `cells_g3.json`, même nombre, mêmes identifiants.

### SC5 — Déterminisme sur deux passes, six contrôles verts, chacun mordant

- `paires_sha_determinisme_egales` égal au nombre total de paires du bloc
  `determinism.sha256` de `logs/v1_052_qa.json`, total strictement positif,
  aucune empreinte vide.
- `controles_g6_verts` vaut **6** sur 6.
- `controles_g6_avec_preuve_rouge_non_vide` vaut **6** sur 6 (D11).
- `code_sortie_run_proof_g6` vaut **0**.
- `constantes_g6_inchangees` vaut 1 : `git status --porcelain` sur
  `pipeline/geo/constants.py` vide.

### SC6 — Le contrat du crochet existant est satisfait ; preuves committées ; DEM non committée ; README sans sur-revendication

Depuis `pipeline/geo/` :

```
../../.venv/bin/python pipeline.py --source relief
```

- La commande sort code 0 et affiche la ligne de résumé G6 (projection,
  `cell_count`, `elev_med`, `barriers`/`passes`, `below_0_km2`).
  `pipeline/geo/pipeline.py` reste **inchangé** :
  `fichiers_partages_modifies` vaut 0 sur les onze fichiers listés en D13
  (interdit), mesuré par `git status --porcelain`.
- `fichiers_preuve_suivis_par_git` égal au nombre de fichiers de preuve
  déclarés sous `pipeline/geo/` en D9 (hors `sources/dem_cache/`, jamais
  suivi par git), vérifié par `git ls-files`.
- `dem_cache_non_suivi` vaut 1 : `git status --porcelain --ignored
  pipeline/geo/sources/dem_cache/` montre le répertoire comme **ignoré**,
  jamais comme non suivi ordinaire ni suivi.
- `README.md` mis à jour : G6 — relief — désormais livré ; **restent non
  livrés** le climat, les ressources, les villes (G7), la possession (G8),
  les LOD (G9), les textures d'identifiants (G10), l'apparence/ombrage
  (A12), G5-bis/G5-ter (déjà non livrés, non redécrits en détail — pointer
  vers la section brief 021 existante). Aucune sur-revendication. Un
  instantané pré-édition est committé sous
  `deliverables/pre-edit/pipeline-geo-README.md.orig` (couple
  `must_differ_from`).
- La suite du harnais reste verte (aucune régression) :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_024` rapporté avec le nombre de tests collectés
  pour dénominateur. **`pytest` n'est actuellement installé dans aucun venv
  de cette machine** (vérifié à l'écriture de ce brief — voir la table des
  Waivers) : c'est un outillage de test du harnais, absent de tout fichier
  de dépendances du dépôt. Le Générateur l'installe (`.venv/bin/pip install
  pytest`) comme étape de provisionnement normale avant de lancer cette
  commande — ce n'est pas une modification produit et ce n'est pas soumis à
  D13. Si cette installation échoue réellement, le waiver dédié
  s'applique : `tests_harness_passed_024 = -1` (sentinelle, jamais un `0`
  ou un `PASS` supposé), et le fait est consigné dans
  `deliverables/generator-log.md` — SC6 est alors acceptée sur ses autres
  points, cette seule ligne restant non vérifiée et déclarée comme telle.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Livrer le climat, les ressources, les villes (G7), la possession (G8,
   qui reste en plus en conflit ouvert avec ADR-0003 — voir
   `geo-pipeline-port-plan.md`), les LOD (G9), les textures d'identifiants
   (G10), l'apparence/ombrage (A12) — lots suivants du jalon E1 ou hors
   jalon, non traités ici. Aucun fichier `07*`, `08*`, `09*`, `10*`, `a12*`
   n'est créé.
2. Modifier `adjacency_g5.json`, `cells_g3.json` ou tout autre artefact
   d'un lot précédent déjà committé. `adjacency_g6.json` est une **copie
   enrichie séparée** (D9).
3. Modifier `pipeline/geo/qa/checks.py`, `pipeline/geo/constants.py` ni
   `pipeline/geo/pipeline.py`.
4. Committer une seule tuile Copernicus DEM dans Git (D2) — le cache est
   exclusivement local, reproductible, jamais versionné.
5. Recopier une valeur hexadécimale d'empreinte (SHA256, ETag) dans un
   test, un document ou un commentaire (règle durement acquise n° 12).
   L'ETag S3 observé lors de la reconnaissance réseau de ce brief n'est en
   particulier **jamais** une preuve d'intégrité — seul le `sha256` de
   `sources.lock`, recalculé sur le fichier réellement téléchargé, fait foi.
6. Employer l'alias nu de l'interpréteur ni un chemin Windows ; sur cette
   machine l'interpréteur est `.venv/bin/python` (règle n° 1).
7. Committer, pousser, créer ou changer de branche. L'orchestrateur seul
   dépose.
8. Réinterpréter silencieusement une des décisions D2-D8 sans le
   documenter : si le Générateur trouve une lecture incompatible avec ce
   qu'il constate en écrivant le code, c'est une escalade (Waivers), pas
   une réinterprétation tacite.
9. Rapporter un compteur depuis un échantillon vide ou un calcul manqué
   sans le déclarer comme tel (règle n° 8 : la sentinelle du projet pour
   « non calculé » est `-1`, jamais `0` — un zéro mesuré, par exemple
   `below_0_land_km2 = 0.0`, est légitime et se distingue d'un compteur non
   calculé).
10. Traiter `registry/g6_density_refinement.json` comme une instruction à
    exécuter — c'est une note pour un lot G3 **futur**, pas pour celui-ci
    (Provenance).

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

| nom | source | dénominateur |
|---|---|---|
| `tuiles_verifiees` | tuiles du cache dont le SHA256 égale `sources.lock` | 179 (longueur du bloc `dem.tiles`) |
| `empreinte_collective_egale` | empreinte recalculée sur les 179 tuiles vs `dem.collective_sha256` | booléen, doit être vrai |
| `cellules_sans_echantillon` | cellules de `cells_relief_g6.json` avec `sample_count <= 0` | cellules totales lues de `cells_g3.json`, doit être 0 (ou `-1` documenté) |
| `echantillons_exclus_hors_plage` | échantillons hors `[G6_SAMPLE_VALID_MIN_M, G6_SAMPLE_VALID_MAX_M]` | échantillons bruts générés par la grille, fait mesuré |
| `barrier_count` | arêtes `land-land` avec `relief_barrier=true` | arêtes `land-land` totales lues de `adjacency_g5.json` |
| `pass_count` | enregistrements de `artifacts/passes_g6.json` | doit égaler `barrier_count` exactement |
| `passes_nommes_trouves` | cols dérivés appariés à `G6_KNOWN_PASSES` | 9 (longueur du tuple lu de `constants.py`) |
| `below_0_land_km2` | somme `area_km2` sur cellules `elev_mean_m < 0` | fait mesuré, peut être `0.0` |
| `paires_sha_determinisme_egales` | bloc `determinism.sha256` de `logs/v1_052_qa.json` | total de paires, > 0 |
| `controles_g6_verts` | tableau `checks` de `logs/v1_052_qa.json` | 6 |
| `controles_g6_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée | 6 |
| `code_sortie_run_proof_g6` | code de sortie de `tests/run_proof_g6.py` | 1 exécution ; doit valoir 0 |
| `constantes_g6_inchangees` | `git status --porcelain` sur `constants.py` | 1 ; doit valoir 1 |
| `fichiers_partages_modifies` | `git status --porcelain` sur les onze fichiers interdits (D13) | 11 ; doit valoir 0 |
| `adjacency_g5_inchange` | `git status --porcelain` sur `adjacency_g5.json` | 1 ; doit valoir 1 |
| `dem_cache_non_suivi` | `git status --porcelain --ignored` sur `sources/dem_cache/` | 1 ; doit valoir 1 (ignoré) |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves déclarées (D9, hors cache DEM) | nombre de preuves déclarées |
| `tests_harness_passed_024` | tests `PASSED` de `harness/tests/` | tests collectés (SKIP Linux/Unity acceptés et déclarés) ; sentinelle `-1` si `pytest` n'a pu être provisionné (Waivers) — jamais `0` |

Un script committé sous
`harness/queue/briefs/024-geo-relief-g6/deliverables/measure_g6_024.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des artefacts et constantes — jamais une valeur recopiée à la main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée » | `.venv/bin/python -c "import shapely, geopandas, pyproj, rasterio; print('ok')"` depuis la racine | `ModuleNotFoundError` nommant le module — **vérifié à l'écriture de ce brief** : sur cette machine, `.venv/` est un venv Python 3.12 nu (créé par `python3 -m venv`, aucun paquet de `pipeline/geo/requirements.txt` installé), la commande échoue réellement sur `shapely` en premier. `pipeline/geo/requirements.txt` déclare déjà les huit paquets requis (dont `rasterio>=1.3`, la lecture COG) ; la provision normale est `.venv/bin/pip install -r pipeline/geo/requirements.txt` — un Générateur qui l'exécute d'abord n'a pas besoin d'invoquer ce waiver. Le waiver ne s'applique que si cette installation elle-même échoue (réseau, dépôt PyPI inaccessible), pas au simple constat initial d'absence |
| « `pytest` n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest` — **vérifié à l'écriture de ce brief**, code de sortie 1. `pytest` n'est déclaré dans **aucun** fichier de dépendances du dépôt (`pipeline/geo/requirements.txt` est de portée géo uniquement ; aucun `requirements*.txt` n'existe à la racine) alors que `harness/tests/*.py` est écrit dans le style de découverte `pytest` (fonctions `def test_*` hors classe), donc non exécutable par `unittest` sans réécriture. C'est un **outillage de test du harnais**, pas du code produit : le Générateur peut l'installer (`.venv/bin/pip install pytest`) sans que cela touche un fichier protégé de D13 ni constitue une modification produit. Si l'installation échoue (réseau), le waiver s'applique et `tests_harness_passed_024` est rapporté avec la sentinelle `-1` (non calculé), jamais un `0` ou un `PASS` supposé — SC6 reste alors non entièrement vérifiée et c'est un fait à consigner dans `deliverables/generator-log.md`, pas une case cochée en silence |
| « une ou plusieurs tuiles Copernicus DEM ne sont pas accessibles au motif de clé vérifié par ce brief (`<stem>/<stem>.tif`, `copernicus-dem-90m.s3.amazonaws.com`) » | la commande de téléchargement/`HEAD` réelle utilisée, avec la réponse HTTP complète loggée | code HTTP non-`200` ou erreur réseau, **par tuile nommée** — pas une affirmation générale ; si invoquée pour une partie seulement des 179 tuiles, `G6-A` reste rouge et le lot escalade, il ne livre pas une maille partiellement échantillonnée |
| « `adjacency_g5.json` ou `cells_g3.json` ne sont pas lisibles » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g5.json'))"` depuis la racine | `FileNotFoundError` ou équivalent |
| « la dérivation barrière/col (D6-D7) est incompatible avec ce que produit la géométrie réelle » | le module `steps/06_relief.py` écrit tel quel, plus la sortie de `../../.venv/bin/python pipeline.py --source relief` | la sortie réelle montrant l'incohérence (par exemple `pass_count != barrier_count`) ; **si invoquée**, aucune SC3 n'est excusée — c'est un motif d'escalade vers le propriétaire, pas un contournement du contrôle |
| « aucune arête `land-land` n'est classée barrière sur la fenêtre pilote » | sortie de `tests/run_proof_g6.py` montrant `barrier_count = 0` | c'est un motif de blocage (contrairement à `fleuves_nommes_trouves` en G5) : les Pyrénées et les Alpes sont dans la fenêtre pilote (cols de `G6_KNOWN_PASSES`, tous en zone Pyrénées/Alpes) — un `barrier_count` nul signale un défaut réel de dérivation, pas un fait de monde plausible, et bloque SC3 |

---

## Registre de coût

Une ligne, sans `--audit-id` (ce brief naît de la roadmap et de la demande
`DEMANDE-20260820-g6-relief.md`, pas d'un audit converti) :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/024-geo-relief-g6 \
  --event generator-run
```
