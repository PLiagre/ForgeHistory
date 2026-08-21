# Brief 024 : le relief (G6) — altitude, pente, rugosité, barrières et cols

**Authored**: 2026-08-20T08:15:00Z
**Author**: forge-planificateur
**Amendé le**: 2026-08-21T17:10:00Z (amendement 001, voir ci-dessous)

> **AMENDEMENT 001 — couverture DEM complète (2026-08-21).** La première
> exécution de ce lot a été relue et refusée (verdict `FAIL`,
> `.forgepilot/runs/20260820T220349Z-reviewer/result.json`) : les 179 tuiles
> déclarées ne couvraient qu'une partie de la carte, et le code fabriquait des
> altitudes de `0,0 m` hors de cette emprise, d'où de fausses barrières en
> plaine et aucune barrière pyrénéenne. Sur décision du propriétaire
> (`hermes/requests/DEMANDE-20260821-couverture-dem-complete-g6.md`), ce brief
> est amendé. La décision, les mesures qui la fondent et les risques sont
> consignés dans
> `harness/queue/briefs/024-geo-relief-g6/amendement-001-couverture-dem-complete.md` —
> document de décision, **jamais une instruction** : toute instruction vit ici,
> dans ce fichier (`CLAUDE.md` › Single Source of Instruction).
>
> Les passages amendés portent la mention **[A1]**. Ce qui ne la porte pas
> reste en vigueur inchangé.

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
- `pipeline/geo/sources.lock` déclare un bloc `dem` : tuiles nommées, chacune
  avec `bytes` et `sha256`, plus `collective_sha256`, `tile_count`,
  `total_bytes`, et la licence Copernicus (attribution obligatoire, texte déjà
  figé).
  **[A1]** Ce bloc s'est révélé **incomplet** : il ne couvre que W007→E008 et
  N42→N55, alors que les lectures de G6 demandent 1 108 tuiles. Ce lot le
  **régénère** désormais (D14), au lieu de le lire comme un acquis. Le reste du
  fichier `sources.lock` demeure intouchable.
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
  tuiles COG (Cloud-Optimized GeoTIFF) de 1°×1° déclarées dans `sources.lock`.
- **[A1] tuile requise** : une tuile 1°×1° qui contient au moins un des points
  que G6 lit réellement (grille, centroïde ou frontière). La liste se **dérive**
  (D15), elle ne se déclare pas.
- **[A1] `nodata`** : la valeur qu'un fichier raster réserve pour dire « ici, je
  n'ai pas de donnée ». Ce n'est pas une altitude, et surtout pas une altitude
  de zéro (D17).
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
- `pipeline/geo/sources.lock`, bloc `dem` : tuiles nommées
  (`Copernicus_DSM_COG_30_<lat>_00_<lon>_00_DEM.tif`), chacune avec `bytes`
  et `sha256`, plus `collective_sha256`, `tile_count`, `total_bytes`.
  **[A1]** Ce bloc est régénéré par ce lot (D14) ; le reste du fichier ne l'est
  pas.
- **Accès réseau probé (lecture seule, aucune tuile téléchargée par ce
  brief)** : le compartiment public `copernicus-dem-90m.s3.amazonaws.com`
  répond, sans authentification, à une requête `HEAD` sur le motif de clé
  `<stem>/<stem>.tif` (répertoire nommé comme la tuile, puis le fichier),
  par exemple `Copernicus_DSM_COG_30_N42_00_E000_00_DEM/Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif`
  → `200 OK`. Le motif à plat (`<stem>.tif` sans répertoire) → `404`. Ceci
  est une **reconnaissance**, pas une preuve d'accès complet : le Générateur
  doit vérifier lui-même chaque tuile (D2), y compris la possibilité que le
  motif diffère pour une tuile particulière, que l'accès soit ralenti sur un
  gros volume, ou **[A1]** qu'une tuile requise n'existe tout simplement pas
  côté fournisseur (D16).
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

> **[A1] Ordre de lecture.** Les décisions ajoutées par l'amendement 001
> (D14 à D18) sont placées **avant** D12 et D13, qui ferment la section en
> disant ce qui reste interdit. Aucune décision n'a été retirée ni renumérotée :
> D1 à D11 restent en vigueur, amendées seulement là où le marqueur **[A1]**
> l'indique.

### D1 — Entrées exactes

Le nouveau module lit, en lecture seule :

| entrée | usage |
|---|---|
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry`, `centroid`, `area_km2` — grille d'échantillonnage, centroïde, frontières partagées (re-dérivées par intersection de polygones, jamais stockées ailleurs) |
| `pipeline/geo/artifacts/adjacency_g5.json` | `adjacency[]` filtré sur `kind == "land-land"` — jamais modifié en place (D9) |
| `pipeline/geo/sources.lock`, bloc `dem` | tuiles attendues (nom, `bytes`, `sha256`), `collective_sha256` — **lu**, jamais recopié en littéral. **[A1]** Seul bloc de ce fichier que le lot a le droit de réécrire (D14) |
| `pipeline/geo/constants.py` | toutes les bornes, pas d'échantillonnage et cols G6, **lus**, jamais recopiés en littéral |
| cache DEM local (D2) | **[A1]** toutes les tuiles requises, vérifiées avant toute lecture d'altitude |

### D2 — Les tuiles DEM : cache hors dépôt, jamais committées ; vérification par tuile ET collective

> **[A1] Amendement 001.** Les mentions de « 179 tuiles » et de « 644 Mo »
> ci-dessous décrivent l'état **initial** du bloc `dem`, qui s'est révélé
> incomplet. Le nombre de tuiles est désormais **dérivé** (D15) et vaut
> 1 108 ; le volume attendu est de l'ordre de 4 à 6 Go au total. Le principe
> de D2 — cache local hors dépôt, jamais committé, vérifié tuile par tuile
> **et** collectivement avant toute lecture — reste intégralement en vigueur
> et s'applique à toutes les tuiles requises.

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

**Décision : les tuiles vont dans `pipeline/geo/sources/dem_cache/`, un
répertoire nouveau, ajouté à `.gitignore`, jamais committé.** Elles sont
**reproductibles** (même source publique, mêmes empreintes déclarées dans
`sources.lock`) — un clone frais les retélécharge et les revérifie, il ne
les hérite pas de l'historique Git. C'est cohérent avec l'exclusion déjà en
place de `.venv/`, `build/`, `artifacts/`, `logs/`, `capture/` (ces trois
derniers étant néanmoins forcés au commit par preuve individuelle, D10 —
la différence ici est le volume : plusieurs gigaoctets de raster ne sont
**jamais** forcés au commit, aucune exception).

Chaque tuile est téléchargée (source publique Copernicus DEM, motif de clé
`<stem>/<stem>.tif` sur `copernicus-dem-90m.s3.amazonaws.com`, vérifié
accessible ci-dessus, aucune signature AWS requise) puis vérifiée par son
propre `sha256` déclaré dans `sources.lock`. `G6-A`
(`g6a_dem_fingerprint_verified`) est vert seulement si **toutes les empreintes
individuelles ET l'empreinte collective** (recalculée à partir des tuiles
présentes, jamais recopiée) correspondent exactement à `sources.lock`. Une
tuile manquante, corrompue, ou dont l'empreinte diverge fait échouer `G6-A`
avant toute lecture d'altitude — jamais un silence, une tuile de secours ou une
valeur inventée (règle n° 10).

**[A1]** `G6-A` est vert seulement si, **en plus**, la garde de couverture de
D16 est verte : la liste dérivée des tuiles requises est entièrement incluse
dans le bloc `dem` de `sources.lock`. Le contrat de
`g6a_dem_fingerprint_verified` reçoit un booléen calculé par le module de
relief — les nouvelles gardes alimentent ce booléen et `qa/checks.py` n'est pas
modifié d'une ligne (D12).

Un script de récupération/vérification, committé sous
`pipeline/geo/tools/fetch_dem_tiles.py`, est **idempotent** : relancé sur un
cache déjà complet et vérifié, il ne retélécharge rien et sort en confirmant
toutes les empreintes. `steps/06_relief.py` appelle ce module (ou sa fonction
de vérification) avant tout échantillonnage — jamais un chemin qui suppose
silencieusement le cache déjà rempli.

### D3 — Grille d'échantillonnage par cellule (contrat `G6-B`/`G6-C`)

Pour chaque cellule de `cells_g3.json` :

1. Générer les points de la grille régulière lon/lat (pas
   `G6_SAMPLE_STEP_DEG`) qui tombent à l'intérieur du polygone `geometry`
   de la cellule.
2. Lire l'altitude MNT à chaque point.
3. **[A1]** Écarter d'abord les échantillons `nodata` (D17) : ils ne sont pas
   des altitudes et n'entrent dans aucune statistique. Leur compte est mesuré
   et rapporté (`echantillons_nodata_raster`).
   Retenir ensuite uniquement les échantillons dans
   `[G6_SAMPLE_VALID_MIN_M, G6_SAMPLE_VALID_MAX_M]` (D « échantillon
   valide » du Vocabulaire) — les autres sont exclus **avant** tout calcul
   de statistique, et le compte d'exclusions est mesuré et rapporté
   (`echantillons_exclus_hors_plage`), jamais tu.
4. `sample_count` = nombre d'échantillons valides retenus. Si une cellule
   n'a **aucun** échantillon valide (cas non attendu sur la fenêtre pilote,
   mais possible en théorie sur une cellule minuscule), c'est une
   impossibilité au sens de la règle n° 9 : `sample_count = -1` (sentinelle,
   jamais `0` silencieux) et `G6-B` rouge nommant la cellule — pas une
   valeur inventée. **[A1]** Cela vaut aussi lorsque la cause est le
   `nodata` : une cellule entièrement sans donnée est déclarée sans donnée,
   jamais ramenée à `0,0 m`.
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
| `artifacts/stats_g6.json` | `cell_count`, `elev_distribution` (au moins `median`, requis par `pipeline.py` ; `min`/`max`/`mean` recommandés), `barrier_count`, `pass_count`, `passes_nommes_trouves`, `below_0_land_km2`, `echantillons_exclus_hors_plage`. **[A1]** plus : `echantillons_hors_couverture_dem`, `echantillons_nodata_raster`, `points_lus_grille`, `points_lus_centroides`, `points_lus_frontieres`, `cellules_non_mesurees` |
| `artifacts/MANIFEST_g6.json` | version, projection, `inputs` (empreintes calculées à l'exécution : `adjacency_g5.json`, `cells_g3.json`, `sources.lock`), `outputs` (empreintes des sorties) |
| **[A1]** `artifacts/dem_required_tiles_g6.json` | liste dérivée des tuiles requises et ses comptes (D15) — committé |
| **[A1]** `artifacts/dem_tile_availability_g6.json` | sondage de disponibilité par tuile requise, code HTTP (D16) — committé |
| `registry/relief_registry.json` | registre des cellules de relief émises, date `G6_REGISTRY_CREATED` |
| `pipeline/geo/sources/dem_cache/` | **[A1]** toutes les tuiles requises, vérifiées (D2), **non committées** |
| `pipeline/geo/tools/fetch_dem_tiles.py` | script de récupération/vérification (D2), idempotent. **[A1]** porte aussi le sondage de disponibilité (D16) et la régénération du bloc `dem` (D14) |
| **[A1]** `pipeline/geo/tools/required_dem_tiles.py` | dérivation de la liste des tuiles requises (D15), sans lecture de raster |
| **[A1]** `pipeline/geo/sources.lock` | bloc `dem` seul, régénéré (D14) |
| `logs/v1_052_relief.log` | journal lisible de la preuve (tag `v1_052`, dérivé de `G6_PIPELINE_VERSION`, non colliding avec les tags déjà committés `v1_049`/`v1_050`/`v1_051`/`v1_060`) |
| `logs/v1_052_qa.json` | rapport : tableau `checks` (6 entrées, `passed` + `red_proof`) + `determinism.sha256` |
| `capture/v1_052_elevation_window.png` | altitude par cellule sur la fenêtre pilote (palette continue, pas un ombrage — le hillshade est A12, hors de portée) |
| `capture/v1_052_barriers_passes.png` | zoom sur un secteur montrant les arêtes barrières et leurs cols (nommés vs dérivés distingués visuellement) — capture **regardée et décrite** dans le journal (règle n° 11, comme les briefs 019/021 l'exigent pour leurs propres captures) |
| `steps/06_relief.py` | le nouveau module (exporte `run_relief()`, sans argument — contrat déjà fixé par `pipeline.py`) |
| `tests/test_qa_red_g6.py` | cas rouges, un par contrôle (D11) |
| `tests/run_proof_g6.py` | script de preuve (D10) |
| `README.md` | mise à jour (SC6) |

**[A1] Trois couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json` :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md`
   publié.
2. `artifacts/adjacency_g5.json` (référence, non modifié) ↔
   `artifacts/adjacency_g6.json` (copie enrichie) — la preuve mécanique que
   l'enrichissement a produit un fichier réellement différent.
3. **[A1]** `deliverables/pre-edit/pipeline-geo-sources.lock.orig` ↔ le
   `sources.lock` publié — la preuve mécanique que le bloc `dem` a réellement
   été régénéré (D14).

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

> **[A1] Quatre cas rouges supplémentaires**, dans le même fichier, pour les
> gardes ajoutées par l'amendement. Ils sont comptés à part
> (`cas_rouges_amendement_non_vides` = 4 sur 4), afin de ne pas troubler le
> contrat existant « un cas rouge par contrôle de `run_g6_green` » :
>
> 1. **Couverture** : retirer une tuile requise d'une copie en mémoire du bloc
>    `dem` — la garde de D16 doit s'arrêter **avant** la première lecture
>    d'altitude, en nommant la tuile.
> 2. **Hors couverture** : demander une altitude pour une coordonnée
>    qu'aucune tuile ne contient — la lecture doit échouer en nommant
>    longitude, latitude et identifiant de cellule ou d'arête, et ne jamais
>    rendre `0,0`.
> 3. **`nodata`** : un échantillon marqué sans donnée ne doit ni devenir
>    `0,0`, ni compter comme échantillon valide ; il doit incrémenter
>    `echantillons_nodata_raster`.
> 4. **Convention d'ouest** : une tuile `W001` dont les bornes seraient
>    calculées comme `[−2, −1)` doit faire rougir la comparaison
>    nom-contre-raster de D16.
>
> Aucun de ces cas ne passe par une modification de `qa/checks.py`.

`tests/test_qa_red_g6.py` fournit **un cas rouge par contrôle** des six
assemblés par `run_g6_green` : `Q10`, `G6-A`, `G6-B`, `G6-C`, `G6-D`, `G6-E`.
Chaque cas est une mutation locale explicite sur une copie en mémoire (par
exemple une empreinte de tuile falsifiée pour `G6-A` ; un `sample_count=0`
forcé pour `G6-B` ; une altitude hors plage pour `G6-C` ; une arête
`relief_barrier=true` avec `crossing_elev_m` sous un centroïde pour `G6-D` ;
un `cell_id` retiré de la maille de sortie pour `G6-E`). Aucun cas ne passe
par une modification de `qa/checks.py`. Un `red_proof` vide vaut échec du
contrôle, même si le vert est vert.

### D14 — [A1] `sources.lock` : le bloc `dem` devient inscriptible, et lui seul

L'interdiction d'écrire dans `pipeline/geo/sources.lock` est **levée pour le
seul objet de premier niveau `dem`**.

**Ce qui peut changer, dans `dem`** : `tiles` (la liste des tuiles, avec pour
chacune `bytes` et `sha256`), `tile_count`, `total_bytes`,
`collective_sha256`, et un nouveau champ `collective_recipe` (D18).

**Ce qui ne change pas, sous aucun prétexte** :

- les autres objets de premier niveau — `files`, `geonames_cities500`,
  `layer_coverage`, `licence`, `source_set` — octet pour octet identiques ;
- le sous-objet `dem.licence`, mot pour mot : l'attribution Copernicus est une
  obligation légale, pas un détail de format.

**Comment le bloc est régénéré** : par le script committé
`pipeline/geo/tools/fetch_dem_tiles.py`, dans un mode dédié, à partir des
fichiers réellement présents dans le cache local. Chaque `sha256` et chaque
`bytes` est **recalculé sur le fichier**. **Aucune valeur hexadécimale n'est
saisie ni recopiée à la main**, nulle part — ni dans le lock, ni dans un test,
ni dans un commentaire, ni dans un journal (règle durement acquise n° 12).

Un instantané pré-édition est committé sous
`deliverables/pre-edit/pipeline-geo-sources.lock.orig`, avec un couple
`must_differ_from` contre le `sources.lock` publié (D9).

### D15 — [A1] La liste des tuiles requises se dérive, elle ne se déclare pas

Un outil neuf, committé sous `pipeline/geo/tools/required_dem_tiles.py`, dérive
la liste des tuiles 1°×1° réellement nécessaires.

1. Il **appelle les mêmes fonctions de génération de points** que
   `steps/06_relief.py` — grille dans le polygone (D3.1), centroïde (D4),
   densification de frontière partagée (D6.2). Une seconde implémentation qui
   pourrait diverger est interdite : le module de relief expose ces fonctions,
   l'outil les importe.
2. Il **n'ouvre aucun fichier raster**. Il ne lit que `cells_g3.json`,
   `adjacency_g5.json` et `constants.py`.
3. Pour chaque point, il déduit le nom de tuile par la convention de D16.
4. Il écrit `pipeline/geo/artifacts/dem_required_tiles_g6.json` : la liste
   triée des tuiles requises, les comptes (requises, présentes dans le lock,
   manquantes, excédentaires), les comptes de points par famille, et les bornes
   réelles de l'emprise échantillonnée.

**Valeurs attendues** (reconstruites indépendamment par le propriétaire, voir
l'amendement 001 § 2) :

| grandeur | valeur attendue |
|---|---|
| tuiles requises | 1 108 |
| tuiles du lock initial réellement utiles | 174 |
| tuiles à ajouter | 934 |
| tuiles excédentaires à retirer | 5 |
| points de grille | 11 449 061 |
| points de centroïde | 596 |
| points de frontière | 154 897 |
| total des lectures d'altitude | 11 604 554 |
| longitude minimale / maximale | −10,475 / 34,819 304 810 285 41 |
| latitude minimale / maximale | 29,704 867 323 841 14 / 61,558 333 333 333 |

Ces nombres sont une **valeur de recoupement**, pas un seuil à faire coïncider.
Le compteur, lui, se dérive toujours de la sortie du script (règle n° 3). Si la
reconstruction mécanique donne un autre nombre, c'est une **escalade** (Waivers)
accompagnée de la sortie qui le prouve : aucun nombre de ce brief ne s'ajuste en
silence, et aucun résultat ne s'arrondit vers l'attendu.

### D16 — [A1] Convention des bornes de tuile, prouvée contre le raster

Le nom d'une tuile Copernicus désigne son **coin sud-ouest signé** ; la tuile
couvre un degré vers le nord et vers l'est à partir de ce coin. Donc, sans
exception ni cas particulier :

| jeton du nom | intervalle couvert |
|---|---|
| `E000` | longitudes `[0, 1)` |
| `E034` | longitudes `[34, 35)` |
| `W001` | longitudes `[−1, 0)` |
| `W011` | longitudes `[−11, −10)` |
| `N29` | latitudes `[29, 30)` |
| `S001` (non employé sur cette fenêtre, énoncé pour ne pas être deviné) | latitudes `[−1, 0)` |

Le code livré appliquait cette règle correctement à l'est et au nord, et
faussement à l'ouest (`W001` traité comme `[−2, −1)`), soit un degré de décalage
sur tout l'ouest de la carte.

**La convention n'est pas seulement écrite, elle est prouvée** : pour **chaque**
tuile du cache, les bornes déduites du nom sont comparées aux bornes réelles
lues dans les métadonnées du fichier COG (lecture d'en-tête, aucun pixel). Le
compteur `tuiles_bornes_nom_vs_raster_egales` a pour dénominateur la longueur
lue du bloc de tuiles, et doit l'égaler. Une convention qu'on affirme est une
convention qu'on peut se tromper à relire ; une convention qu'on confronte au
raster ne ment pas.

**Garde de couverture, avant toute lecture d'altitude.** Le module de relief
compare la liste dérivée (D15) aux clés du bloc `dem` **avant le premier
échantillonnage**, et s'arrête en nommant les tuiles manquantes si l'inclusion
n'est pas totale. Une garde placée après l'effet qu'elle doit empêcher ne
protège rien (règle n° 5) : celle-ci passe avant.

**Sondage de disponibilité, avant tout téléchargement de masse.** Avant de
récupérer le moindre gigaoctet, le script de récupération sonde par requête
d'en-tête (`HEAD`) la disponibilité de **toutes** les tuiles requises et écrit
`pipeline/geo/artifacts/dem_tile_availability_g6.json` (par tuile : le code
HTTP obtenu). Le compteur `tuiles_requises_absentes_du_depot_public` doit valoir
`0` ; s'il est non nul, le lot **s'arrête et escalade** en nommant les tuiles,
sans avoir transféré des heures de données pour rien.

Une tuile requise absente du dépôt public n'autorise **ni** un `0,0`, **ni** un
repli, **ni** une emprise réduite, **ni** une maille partielle. Elle autorise une
escalade vers le propriétaire, et rien d'autre.

### D17 — [A1] Hors couverture, et `nodata` : deux échecs distincts, jamais un zéro

**Le bornage et le repli disparaissent du code.** `clamp_lonlat` et la recherche
de « la tuile la plus proche » sont **supprimés**, pas neutralisés par un
drapeau ni gardés derrière une option. Une coordonnée qu'aucune tuile ne
contient fait **échouer la lecture**, avec un message qui nomme :

- la longitude et la latitude exactes ;
- l'identifiant de cellule (`cell_id`) ou d'arête (`a`-`b`) pour lequel la
  lecture était faite ;
- le nom de la tuile qui aurait été nécessaire.

Jamais un `0,0`. Jamais une valeur de secours. Jamais un silence. Le compteur
`echantillons_hors_couverture_dem` vaut `0`, avec pour dénominateur le total des
lectures d'altitude.

**Le `nodata` du raster n'est pas une altitude.** Un pixel marqué sans donnée —
soit parce que sa valeur égale la valeur `nodata` **déclarée par le fichier**,
soit parce que le masque du raster l'exclut — est écarté de toute statistique et
compté dans `echantillons_nodata_raster`. Il n'est jamais converti en `0,0`, ni
compté comme échantillon valide.

La valeur `nodata` est **lue dans le fichier**, jamais écrite en dur dans le
code : un `-32767` saisi à la main serait exactement le genre de constante
recopiée que la règle n° 12 proscrit. Si un fichier ne déclare aucune valeur
`nodata`, le masque du raster fait seul autorité, et le fait est compté dans
`tuiles_sans_valeur_nodata_declaree` (fait mesuré, peut valoir `0`).

**Un `0,0 m` lu sur un pixel valide reste une mesure.** C'est le niveau de la
mer selon le géoïde de référence du produit, et il est conservé tel quel. La
distinction entre « zéro mesuré » et « pas de donnée » est la règle n° 8
appliquée au raster.

### D18 — [A1] Une seule recette d'empreinte collective, figée

Le code livré essayait quatre recettes successives et retenait celle qui
retombait sur la valeur attendue. C'est une méthode de découverte acceptable une
fois ; ce n'est pas une méthode de production, et elle transforme un contrôle
d'intégrité en recherche de correspondance.

**Recette canonique, désormais la seule** : empreinte SHA256 de la
concaténation, triée par nom de tuile, de `nom_de_tuile` immédiatement suivi de
`sha256_de_la_tuile` (chaîne hexadécimale), le tout encodé en ASCII. C'est la
recette déjà démontrée sur le bloc initial ; elle est ici **figée**, pas
redécouverte.

- Son **nom** est inscrit dans `sources.lock` sous `dem.collective_recipe`. Un
  nom, jamais une valeur (règle n° 12).
- La fonction qui essayait plusieurs recettes est **supprimée** du dépôt. Le
  compteur `recettes_collectives_essayees` vaut `1`, dénominateur `1`.
- Une empreinte collective qui ne correspond pas est un **échec** de `G6-A`, pas
  le début d'une recherche.

### D12 — Bornes non modifiables

**Interdiction ferme :** aucune valeur de `pipeline/geo/constants.py` n'est
modifiée par ce lot — y compris `.gitignore` du répertoire `pipeline/geo/`,
qui reçoit une seule ligne ajoutée (`sources/dem_cache/`), jamais une
réécriture. Une borne inatteignable sur la fenêtre pilote réelle s'escalade
(Waivers), elle ne se déplace pas.

**[A1]** L'amendement 001 ne change rien à D12 et n'accorde aucune dérogation :
`pipeline/geo/constants.py`, `pipeline/geo/pipeline.py` et
`pipeline/geo/qa/checks.py` restent **interdits en écriture**. C'est réalisable
sans compromis, parce que `g6a_dem_fingerprint_verified` reçoit déjà un booléen
calculé par le module de relief : les gardes de D16 et D17 alimentent ce
booléen. Un Générateur qui se croirait obligé de toucher à la barre qualité doit
escalader, pas modifier.

### D13 — Périmètre de fichiers

**Autorisé (création ou modification) :**

- `pipeline/geo/steps/06_relief.py` (nouveau) ;
- `pipeline/geo/tools/fetch_dem_tiles.py` (nouveau) ;
- **[A1]** `pipeline/geo/tools/required_dem_tiles.py` (nouveau, D15) ;
- **[A1]** `pipeline/geo/sources.lock`, **objet `dem` uniquement** (D14) —
  tout autre objet de premier niveau reste octet pour octet identique, et
  `dem.licence` reste mot pour mot identique ;
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
**[A1]** `pipeline/geo/sources.lock` **hors de son objet `dem`** (le bloc `dem`
est désormais autorisé, D14 — tout le reste du fichier reste interdit) ;
`pipeline/geo/sources/10m_physical.zip` ;
tous les artefacts et registres G2/G2-bis/G3/G4/G5 déjà committés (y compris
`adjacency_g5.json`, `cells_g3.json`, D9) ; tout fichier sous `sim/` ou
`unity/` ; `harness/*.py` ; `harness/pipeline/` ; `architecture/` ;
`docs/adr/**` ; `VISION.md` ; `ROADMAP.md` ; `hermes/**` ; `HANDOFF.md` ;
`.github/**` ; les archives des briefs 001 à 023.

---

## Success Conditions

### SC1 — [A1] Le cache DEM est complet, couvrant et vérifié avant toute lecture (`G6-A`)

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_g6.py
```

- `tuiles_verifiees` égale son dénominateur, et **ce dénominateur est la
  longueur lue du bloc de tuiles de `sources.lock`** — jamais un nombre écrit
  dans ce brief. Chaque tuile a son `sha256` déclaré égal à celui recalculé sur
  le fichier présent sur disque.
- `empreinte_collective_egale` = vraie : l'empreinte recalculée sur les tuiles
  présentes, par la seule recette canonique de D18, égale
  `dem.collective_sha256` de `sources.lock`.
- `recettes_collectives_essayees` = 1 sur 1 : aucune recette de repli n'existe
  plus dans le dépôt.
- `tuiles_bornes_nom_vs_raster_egales` égale son dénominateur (D16) : pour
  chaque tuile, les bornes déduites du nom sont celles que le fichier déclare.
- `tuiles_requises_absentes_du_depot_public` = 0 (D16).
- `blocs_sources_lock_hors_dem_inchanges` égale son dénominateur, mesuré sur
  l'instantané pré-édition : aucun autre objet de premier niveau n'a bougé, et
  `dem.licence` est mot pour mot identique (D14).
- `sha256_saisis_a_la_main` = 0 : chaque `sha256` du bloc `dem` publié est égal
  à l'empreinte recalculée sur le fichier correspondant du cache (D14).
- `G6-A` vert.

### SC2 — Toute cellule terrestre est échantillonnée, altitudes plausibles (`G6-B`, `G6-C`)

- `cellules_sans_echantillon` = 0 (ou la sentinelle `-1` documentée si une
  impossibilité réelle est rencontrée, D3.4 — pas un `0` qui masquerait un
  échec).
- `echantillons_exclus_hors_plage` mesuré et rapporté (peut être 0).
- **[A1]** `echantillons_nodata_raster` mesuré et rapporté séparément, avec
  pour dénominateur le total des lectures d'altitude (peut être 0 ; un `0`
  mesuré n'est pas un `0` supposé, règle n° 8).
- **[A1]** `tuiles_sans_valeur_nodata_declaree` mesuré et rapporté (D17).
- `G6-B` et `G6-C` verts.

### SC3 — Barrières et cols cohérents, l'invariant tient (`G6-D`)

- `barrier_count` strictement positif : au moins une frontière réellement
  classée barrière sur la fenêtre pilote — la preuve que la classification a
  réellement mordu, pas seulement que le script s'est terminé.
- `pass_count == barrier_count` exactement (D7).
- `passes_nommes_trouves` mesuré, sans plancher imposé.
- **[A1] Localisation publiée, jamais revendiquée.** La première exécution a
  produit des « barrières » à 14,5 m de franchissement en plaine anglaise,
  parce que les deux centroïdes valaient `0,0` par fabrication. Deux exigences
  en découlent :
  - `barrieres_par_zone_nommee` : pour chaque zone de `A12_RELIEF_ZONES` lue de
    `constants.py`, le nombre de franchissements exportés qui y tombent — un
    fait mesuré, publié dans le journal, sans seuil imposé.
  - `zones_hautes_sous_une_zone_basse` = 0, dénominateur
    `len(A12_RELIEF_MUST_BE_HIGH)`. Pour chaque zone déclarée haute, le plus
    grand `elev_max_m` parmi les cellules dont le polygone rencontre la boîte
    de la zone doit dépasser celui de **chaque** zone déclarée basse. Bornes et
    noms de zones sont **lus** de `constants.py`, jamais recopiés (règle n° 2).
    Ce lot n'implémente pas A12 : il emprunte des boîtes déjà déclarées comme
    recoupement mécanique sur les altitudes exportées. Sous le défaut corrigé
    ici, ce compteur aurait rougi immédiatement, les Pyrénées étant à `0,0`.
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
- **[A1]** `cas_rouges_amendement_non_vides` vaut **4** sur 4 (D11, cas ajoutés
  par l'amendement).
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
- **[A1] Sur-revendications à corriger, nommément.** La première exécution en a
  produit trois, relevées par la relecture :
  1. le `README.md` affirmait « `barrier_count` est strictement positif sur la
     fenêtre pilote (Pyrénées/Alpes) » alors qu'aucune barrière n'était
     pyrénéenne ;
  2. `logs/v1_052_relief.log` décrivait la capture d'altitude en affirmant que
     « les massifs (Alpes, Pyrénées, Massif central) ressortent en teintes
     claires », ce que la donnée exportée ne montrait pas ;
  3. `deliverables/generator-log.md` déclarait ses vingt compteurs « tous
     conformes aux SC », conclusion que ses propres mesures n'établissaient
     pas.
  Le compteur `massifs_revendiques_sans_appui_dans_la_donnee` vaut `0`, de
  dénominateur le nombre de zones de `A12_RELIEF_ZONES` que le `README.md` et
  le journal nomment effectivement : un massif ne peut être nommé que si la
  donnée exportée le montre (SC3). Une capture se **regarde** avant d'être
  décrite (règle n° 11) ; une description qui ne concorde pas avec l'artefact
  est un échec, pas une approximation.
- **[A1]** Corriger aussi les deux incohérences de forme relevées :
  `stats_g6.json` doit publier `below_0_land_km2` avec la même précision que
  celle que le journal et le script de mesure en lisent ; la liste « non
  livrés » du `README.md` ne doit pas ranger les villes à la fois sous `07` et
  sous `07+`.
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

### SC7 — [A1] La couverture DEM est complète, et rien n'est lu hors d'elle

C'est **la** condition que l'amendement 001 ajoute, et celle sur laquelle la
première exécution a échoué.

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tools/required_dem_tiles.py
```

**Couverture des tuiles** (dénominateurs dérivés, jamais écrits en dur) :

- `tuiles_requises` : dérivé de `artifacts/dem_required_tiles_g6.json`, valeur
  de recoupement attendue **1 108** (D15).
- `tuiles_presentes_dans_le_lock` : `len(dem.tiles)` lu de `sources.lock`, doit
  égaler `tuiles_requises`.
- `tuiles_manquantes` = **0**, dénominateur `tuiles_requises`.
- `tuiles_ajoutees` : dérivé en comparant le bloc publié à l'instantané
  pré-édition, valeur de recoupement attendue **934**.
- `tuiles_excedentaires_retirees` : même méthode, valeur de recoupement
  attendue **5**.
- `tuiles_excedentaires_restantes` = **0** : aucune tuile inutile ne demeure
  dans le bloc.

**Couverture des lectures** (dénominateurs dérivés du même script) :

- `echantillons_hors_couverture_dem` = **0**, dénominateur le total des
  lectures d'altitude (valeur de recoupement attendue **11 604 554**).
- `couverture_centroides` : centroïdes lus dans une tuile qui les contient,
  sur 596 — doit égaler son dénominateur.
- `couverture_grille` : points de grille lus dans une tuile qui les contient,
  sur 11 449 061 — doit égaler son dénominateur.
- `couverture_frontieres` : points de frontière lus dans une tuile qui les
  contient, sur 154 897 — doit égaler son dénominateur.
- `cellules_non_mesurees` = **0**, dénominateur le nombre de cellules lues de
  `cells_g3.json`.

**Aucune maille partielle.** Le lot n'a le droit ni de restreindre G6 à
l'emprise couverte, ni de marquer durablement des cellules comme non mesurées,
ni de réintroduire un bornage ou un repli. Si l'une de ces conditions n'est pas
atteignable, le lot **escalade** (Waivers) : il ne livre pas.

**Coût réel publié, jamais estimé après coup** : `volume_dem_telecharge_octets`
et `duree_recuperation_dem_secondes` sont mesurés et consignés dans
`deliverables/generator-log.md`, avec l'espace disque libre constaté avant et
après. L'ordre de grandeur annoncé par l'amendement (3,6 à 5,3 Go
supplémentaires, 85 Go libres) est une extrapolation à partir des tuiles
européennes déjà connues, pas une prévision : c'est la mesure qui fait foi.

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
   `pipeline/geo/pipeline.py`. **[A1]** L'amendement 001 ne lève rien de cette
   interdiction et n'accorde aucune dérogation (D12).
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
11. **[A1]** Rendre une altitude pour une coordonnée qu'aucune tuile ne
    contient — quelle qu'en soit la forme : bornage de coordonnée, repli sur
    la tuile la plus proche, lecture au bord, valeur par défaut, `0,0`. Le code
    qui faisait cela est supprimé, pas désactivé (D17).
12. **[A1]** Convertir un `nodata` de raster en altitude, ni le compter comme
    échantillon valide, ni écrire une valeur `nodata` en dur dans le code
    (D17).
13. **[A1]** Essayer plusieurs recettes d'empreinte collective en production.
    Une seule recette existe désormais, figée et nommée (D18) ; la fonction
    d'essai est supprimée du dépôt.
14. **[A1]** Écrire un dénominateur `179` où que ce soit. Tout dénominateur de
    tuiles se dérive de la longueur lue du bloc de tuiles de `sources.lock`.
15. **[A1]** Écrire dans `sources.lock` ailleurs que dans son objet `dem`, ni
    modifier `dem.licence` (D14).
16. **[A1]** Livrer une maille partielle : restreindre G6 à l'emprise couverte,
    marquer durablement des cellules comme non mesurées, ou baisser une
    exigence de couverture pour faire passer le lot. Une tuile requise
    introuvable côté fournisseur est une escalade, jamais un contournement
    (D16).
17. **[A1]** Ajuster en silence un nombre de ce brief pour le faire coïncider
    avec un résultat obtenu. Un écart entre une valeur de recoupement (D15) et
    une reconstruction mécanique se **déclare** et s'escalade, preuve à
    l'appui.
18. **[A1]** Nommer un massif dans le `README.md`, un journal ou une
    description de capture sans que la donnée exportée l'appuie (SC3, SC6).
19. **[A1]** Émettre un appel d'outil par tuile pour la récupération : 1 108
    appels épuiseraient le budget d'exécution avant toute mesure. La
    récupération est une commande longue (D16).
20. **[A1]** Committer, pousser, ouvrir une nouvelle branche ou une nouvelle
    demande de fusion. L'itération se rejoue dans **le worktree agent existant
    et la demande de fusion existante**.

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

**[A1] Règle générale des dénominateurs.** Aucun dénominateur de tuiles n'est
un nombre écrit dans ce brief. Tout dénominateur de tuiles est
`len(dem.tiles)`, lu de `sources.lock` à l'exécution. Les nombres attendus
(1 108, 934, 5, 596, 11 449 061, 154 897, 11 604 554) sont des **valeurs de
recoupement** : le compteur se dérive, et un écart s'escalade au lieu de se
laisser arrondir (D15, règles n° 2 et n° 3).

| nom | source | dénominateur |
|---|---|---|
| `tuiles_verifiees` | tuiles du cache dont le SHA256 recalculé égale `sources.lock` | **[A1]** `len(dem.tiles)` lu de `sources.lock` ; doit l'égaler |
| `empreinte_collective_egale` | empreinte recalculée par la seule recette canonique (D18) vs `dem.collective_sha256` | booléen, doit être vrai |
| **[A1]** `tuiles_requises` | `artifacts/dem_required_tiles_g6.json`, dérivé (D15) | lui-même ; recoupement attendu 1 108 |
| **[A1]** `tuiles_presentes_dans_le_lock` | `len(dem.tiles)` | `tuiles_requises` ; doit l'égaler |
| **[A1]** `tuiles_manquantes` | requises absentes du bloc `dem` | `tuiles_requises` ; doit valoir 0 |
| **[A1]** `tuiles_ajoutees` | bloc publié moins instantané pré-édition | `tuiles_requises` ; recoupement attendu 934 |
| **[A1]** `tuiles_excedentaires_retirees` | instantané pré-édition moins bloc publié | tuiles de l'instantané pré-édition ; recoupement attendu 5 |
| **[A1]** `tuiles_excedentaires_restantes` | tuiles du bloc non requises | `len(dem.tiles)` ; doit valoir 0 |
| **[A1]** `tuiles_requises_absentes_du_depot_public` | `artifacts/dem_tile_availability_g6.json` (D16) | `tuiles_requises` ; doit valoir 0 |
| **[A1]** `tuiles_bornes_nom_vs_raster_egales` | bornes du nom vs métadonnées du COG (D16) | `len(dem.tiles)` ; doit l'égaler |
| **[A1]** `recettes_collectives_essayees` | recettes d'empreinte collective présentes dans le dépôt (D18) | 1 ; doit valoir 1 |
| **[A1]** `sha256_saisis_a_la_main` | `sha256` du bloc `dem` publié différant de l'empreinte recalculée sur le fichier | `len(dem.tiles)` ; doit valoir 0 |
| **[A1]** `blocs_sources_lock_hors_dem_inchanges` | objets de premier niveau hors `dem`, comparés à l'instantané pré-édition | leur nombre lu de l'instantané ; doit l'égaler |
| **[A1]** `dem_licence_inchangee` | `dem.licence` comparé à l'instantané pré-édition | 1 ; doit valoir 1 |
| **[A1]** `echantillons_hors_couverture_dem` | lectures pour lesquelles aucune tuile ne contient le point (D17) | total des lectures d'altitude ; doit valoir 0 ; recoupement attendu du dénominateur 11 604 554 |
| **[A1]** `couverture_centroides` | centroïdes lus dans une tuile qui les contient | centroïdes lus ; recoupement attendu 596 ; doit égaler son dénominateur |
| **[A1]** `couverture_grille` | points de grille lus dans une tuile qui les contient | points de grille générés ; recoupement attendu 11 449 061 ; doit égaler son dénominateur |
| **[A1]** `couverture_frontieres` | points de frontière lus dans une tuile qui les contient | points de frontière générés ; recoupement attendu 154 897 ; doit égaler son dénominateur |
| **[A1]** `cellules_non_mesurees` | cellules sans altitude mesurée réelle | cellules lues de `cells_g3.json` ; doit valoir 0 |
| **[A1]** `echantillons_nodata_raster` | échantillons `nodata` ou masqués (D17) | total des lectures d'altitude ; fait mesuré, un 0 mesuré est légitime |
| **[A1]** `tuiles_sans_valeur_nodata_declaree` | tuiles dont le fichier ne déclare pas de `nodata` | `len(dem.tiles)` ; fait mesuré |
| **[A1]** `barrieres_par_zone_nommee` | franchissements exportés tombant dans chaque boîte de `A12_RELIEF_ZONES` (SC3) | `barrier_count` ; fait mesuré, publié, sans seuil |
| **[A1]** `zones_hautes_sous_une_zone_basse` | `elev_max_m` par zone de `A12_RELIEF_ZONES` (SC3) | `len(A12_RELIEF_MUST_BE_HIGH)` lu de `constants.py` ; doit valoir 0 |
| **[A1]** `massifs_revendiques_sans_appui_dans_la_donnee` | zones nommées par le `README.md` et les journaux vs données exportées (SC6) | zones effectivement nommées ; doit valoir 0 |
| **[A1]** `volume_dem_telecharge_octets` | octets réellement transférés | fait mesuré, publié |
| **[A1]** `duree_recuperation_dem_secondes` | durée réelle de la récupération | fait mesuré, publié |
| **[A1]** `cas_rouges_amendement_non_vides` | cas rouges des quatre gardes ajoutées (D11) | 4 ; doit valoir 4 |
| **[A1]** `rubrique_amendee_apres_revue` | 1 si `eval-rubric.md` a été amendée après le verdict de relecture | 1 ; doit valoir 1 (voir plus bas) |
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
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves déclarées (D9, hors cache DEM) | nombre de preuves déclarées — **[A1]** dérivé de `deliverables/manifest.json`, jamais un nombre écrit ici ; il augmente des deux artefacts ajoutés (D9) |
| `tests_harness_passed_024` | tests `PASSED` de `harness/tests/` | tests collectés (SKIP Linux/Unity acceptés et déclarés) ; sentinelle `-1` si `pytest` n'a pu être provisionné (Waivers) — jamais `0` |

Un script committé sous
`harness/queue/briefs/024-geo-relief-g6/deliverables/measure_g6_024.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des artefacts et constantes — jamais une valeur recopiée à la main.

**[A1] Un mot sur `rubrique_amendee_apres_revue`.** Le contrôle mécanique
`rubric_predates_deliverables` compare la date déclarée en tête de
`eval-rubric.md` à la date des livrables ; il existe pour prouver qu'une grille
d'évaluation n'a pas été écrite après avoir vu les résultats. **Pour cette
itération, il ne prouve pas cela** : la rubrique a été amendée le 2026-08-21,
après le verdict de relecture du 2026-08-20, en connaissance des résultats — sur
décision du propriétaire, ce qui est légitime, mais ce n'est pas ce que le
contrôle affirme. Les dates d'origine ont été conservées parce que ce sont des
faits ; la date d'amendement est portée séparément en tête des deux documents.
Le compteur porte ce fait jusque dans le verdict, avec les deux dates. Un
contrôle vert dont on sait qu'il ne mesure pas ce qu'il prétend se déclare, il
ne s'encaisse pas.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée » | `.venv/bin/python -c "import shapely, geopandas, pyproj, rasterio; print('ok')"` depuis la racine | `ModuleNotFoundError` nommant le module — **vérifié à l'écriture de ce brief** : sur cette machine, `.venv/` est un venv Python 3.12 nu (créé par `python3 -m venv`, aucun paquet de `pipeline/geo/requirements.txt` installé), la commande échoue réellement sur `shapely` en premier. `pipeline/geo/requirements.txt` déclare déjà les huit paquets requis (dont `rasterio>=1.3`, la lecture COG) ; la provision normale est `.venv/bin/pip install -r pipeline/geo/requirements.txt` — un Générateur qui l'exécute d'abord n'a pas besoin d'invoquer ce waiver. Le waiver ne s'applique que si cette installation elle-même échoue (réseau, dépôt PyPI inaccessible), pas au simple constat initial d'absence |
| « `pytest` n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest` — **vérifié à l'écriture de ce brief**, code de sortie 1. `pytest` n'est déclaré dans **aucun** fichier de dépendances du dépôt (`pipeline/geo/requirements.txt` est de portée géo uniquement ; aucun `requirements*.txt` n'existe à la racine) alors que `harness/tests/*.py` est écrit dans le style de découverte `pytest` (fonctions `def test_*` hors classe), donc non exécutable par `unittest` sans réécriture. C'est un **outillage de test du harnais**, pas du code produit : le Générateur peut l'installer (`.venv/bin/pip install pytest`) sans que cela touche un fichier protégé de D13 ni constitue une modification produit. Si l'installation échoue (réseau), le waiver s'applique et `tests_harness_passed_024` est rapporté avec la sentinelle `-1` (non calculé), jamais un `0` ou un `PASS` supposé — SC6 reste alors non entièrement vérifiée et c'est un fait à consigner dans `deliverables/generator-log.md`, pas une case cochée en silence |
| « une ou plusieurs tuiles Copernicus DEM ne sont pas accessibles au motif de clé vérifié par ce brief (`<stem>/<stem>.tif`, `copernicus-dem-90m.s3.amazonaws.com`) » | la commande de téléchargement/`HEAD` réelle utilisée, avec la réponse HTTP complète loggée | code HTTP non-`200` ou erreur réseau, **par tuile nommée** — pas une affirmation générale ; si invoquée pour une partie seulement des tuiles requises, `G6-A` reste rouge et le lot escalade, il ne livre pas une maille partiellement échantillonnée |
| **[A1]** « une tuile requise n'existe pas dans le dépôt public Copernicus » | `artifacts/dem_tile_availability_g6.json` produit par le sondage préalable (D16), plus la requête `HEAD` et sa réponse complète pour chaque tuile concernée | code HTTP `404` **par tuile nommée**. Le lot **s'arrête et escalade vers le propriétaire** avant tout téléchargement de masse. Ce waiver n'excuse aucune condition : il n'autorise ni un `0,0`, ni un repli, ni une emprise réduite, ni une maille partielle (D16, Non-Goal 16) |
| **[A1]** « la récupération des tuiles a échoué en cours de route (réseau coupé, débit limité par le fournisseur) » | la commande de récupération réelle et son erreur complète, plus le nombre de tuiles vérifiées au moment de l'arrêt | erreur réseau ou HTTP nommant la tuile en cours. La récupération étant idempotente (D2), **la reprise est la conduite attendue** ; l'abandon avec un cache incomplet ne l'est pas. Si la reprise échoue durablement, escalade avec la durée et le volume réellement obtenus |
| **[A1]** « l'espace disque est insuffisant pour le cache complet » | `df -h` avant et après, plus `volume_dem_telecharge_octets` au moment de l'arrêt | message d'écriture impossible nommant le chemin. Mesuré à l'écriture de cet amendement : 85 Go libres pour un besoin supplémentaire estimé entre 3,6 et 5,3 Go — ce waiver ne devrait pas s'appliquer, et une invocation sans ces deux sorties est une abdication (règle n° 9) |
| **[A1]** « la reconstruction mécanique du nombre de tuiles requises diverge de la valeur de recoupement du brief (1 108 / 934 / 5) » | `../../.venv/bin/python tools/required_dem_tiles.py` depuis `pipeline/geo/`, sa sortie complète et `artifacts/dem_required_tiles_g6.json` | la sortie réelle montrant l'écart et sa cause. **Escalade obligatoire vers le Planificateur** : le brief n'est pas réécrit par le Générateur, et le résultat n'est pas arrondi vers l'attendu (Non-Goal 17) |
| « `adjacency_g5.json` ou `cells_g3.json` ne sont pas lisibles » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g5.json'))"` depuis la racine | `FileNotFoundError` ou équivalent |
| « la dérivation barrière/col (D6-D7) est incompatible avec ce que produit la géométrie réelle » | le module `steps/06_relief.py` écrit tel quel, plus la sortie de `../../.venv/bin/python pipeline.py --source relief` | la sortie réelle montrant l'incohérence (par exemple `pass_count != barrier_count`) ; **si invoquée**, aucune SC3 n'est excusée — c'est un motif d'escalade vers le propriétaire, pas un contournement du contrôle |
| « aucune arête `land-land` n'est classée barrière sur la fenêtre pilote » | sortie de `tests/run_proof_g6.py` montrant `barrier_count = 0` | c'est un motif de blocage (contrairement à `fleuves_nommes_trouves` en G5) : les Pyrénées et les Alpes sont dans la fenêtre pilote (cols de `G6_KNOWN_PASSES`, tous en zone Pyrénées/Alpes) — un `barrier_count` nul signale un défaut réel de dérivation, pas un fait de monde plausible, et bloque SC3 |

---

## [A1] Cohérence décision → condition → compteur → rubrique

Table de contrôle de l'amendement 001. Chaque décision ajoutée a au moins une
condition de succès, au moins un compteur dérivé et une section de rubrique qui
dit comment la contredire.

| décision | condition | compteurs | rubrique |
|---|---|---|---|
| D14 — bloc `dem` inscriptible, lui seul | SC1 | `blocs_sources_lock_hors_dem_inchanges`, `dem_licence_inchangee`, `sha256_saisis_a_la_main` | SC1 |
| D15 — liste des tuiles dérivée | SC7 | `tuiles_requises`, `tuiles_ajoutees`, `tuiles_excedentaires_retirees`, `tuiles_excedentaires_restantes` | SC7 |
| D16 — convention d'ouest, garde, sondage | SC1, SC7 | `tuiles_bornes_nom_vs_raster_egales`, `tuiles_manquantes`, `tuiles_requises_absentes_du_depot_public` | SC1, SC7 |
| D17 — hors couverture et `nodata` | SC2, SC7 | `echantillons_hors_couverture_dem`, `echantillons_nodata_raster`, `tuiles_sans_valeur_nodata_declaree`, `couverture_grille`, `couverture_centroides`, `couverture_frontieres`, `cellules_non_mesurees` | SC2, SC7 |
| D18 — recette collective figée | SC1 | `recettes_collectives_essayees`, `empreinte_collective_egale` | SC1 |
| D11 (étendu) — quatre cas rouges | SC5 | `cas_rouges_amendement_non_vides` | SC5 |
| D12 (confirmé) — barre qualité intouchée | SC5, SC6 | `constantes_g6_inchangees`, `fichiers_partages_modifies` | SC5, SC6 |
| SC3 (étendu) — localisation publiée | SC3 | `barrieres_par_zone_nommee`, `zones_hautes_sous_une_zone_basse` | SC3 |
| SC6 (étendu) — plus de sur-revendication | SC6 | `massifs_revendiques_sans_appui_dans_la_donnee` | SC6 |
| honnêteté du harnais | SC5 | `rubrique_amendee_apres_revue` | SC5 |

## [A1] Cadre de l'itération

Ce lot se rejoue **dans le worktree agent existant et la demande de fusion
existante**, par régénération complète suivie d'une nouvelle relecture. Aucune
nouvelle branche, aucune nouvelle demande de fusion, aucune fusion. Le
Générateur ne commit pas, ne pousse pas et ne prononce pas la recevabilité de
son propre travail : l'orchestrateur seul dépose (Non-Goal 7 et 20).

La régénération est **complète** : tous les artefacts, journaux, registres et
captures du lot sont reproduits à partir de la donnée corrigée. Aucun artefact
de la première exécution n'est conservé tel quel, aucun compteur n'est repris
d'un journal précédent.

## Registre de coût

Une ligne, sans `--audit-id` (ce brief naît de la roadmap et de la demande
`DEMANDE-20260820-g6-relief.md`, pas d'un audit converti) :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/024-geo-relief-g6 \
  --event generator-run
```
