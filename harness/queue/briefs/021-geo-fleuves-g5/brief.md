# Brief 021 : les fleuves (G5) — segments navigables, arêtes enrichies, embouchures

**Authored**: 2026-08-14T21:15:00Z
**Author**: forge-planificateur

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude Code,
> invoqué en lecture seule par `forgepilot plan` (pilote ADR-0013), à partir de
> la tâche autoritaire `ROADMAP.md` (F1, « suite : fleuves, relief, climat,
> ressources » ; « Prochaines étapes » point 7). Le plan produit le
> 2026-08-14T21:07:00Z (`.forgepilot/runs/20260814T210700Z-planner/result.json`,
> non versionné) est la base de ce brief ; ses risques identifiés sont repris et
> tranchés ci-dessous, pas ignorés.

---

## Provenance

Ce brief est le **second lot atomique du jalon E1 — Fondations monde**, après
le brief 019 (adjacence maritime G4, fusionné). E1 n'est toujours pas clos :
restent les fleuves (ce lot), le relief, le climat et les ressources.

Le plan du 2026-08-14 a vérifié l'état réel du dépôt et trouvé que **G5 est
déjà entièrement pré-câblé** dans les fichiers partagés, exactement comme
G4 l'était avant le brief 019 :

- `pipeline/geo/constants.py` porte déjà toutes les constantes G5
  (`G5_NAV_SCALE_NAVIGABLE_MAX`, `G5_NAV_SCALE_NON_NAV_MIN`, `G5_RIVER_LAYER`,
  `G5_REGISTRY_CREATED`, `G5_NAMED_MAJOR_RIVERS`, `G5_INTERSECT_EPS_M`,
  `G5_SEA_ONLY_FRACTION`, `G5_MOUTH_SNAP_M`).
- `pipeline/geo/qa/checks.py` porte déjà `run_g5_green` et les six contrôles
  qu'il assemble : `Q1` (validité géométrie), `Q10` (déterminisme), `G5-A`
  (rattachement), `G5-B` (pas de fleuve en pleine mer), `G5-C` (voie fluviale
  implique fleuve navigable), `G5-D` (embouchure sur zone maritime adjacente).
- `pipeline/geo/pipeline.py` porte déjà `_load_rivers_module()`,
  `run_rivers_g5()` et la branche `args.source == "rivers"`. Le crochet est
  **déjà câblé** ; il attend un module `steps/05_rivers.py` qui n'existe pas.

**N'existe pas** : `pipeline/geo/steps/05_rivers.py`,
`pipeline/geo/tests/run_proof_g5.py`, `pipeline/geo/tests/test_qa_red_g5.py`,
tout artefact G5.

Ce lot est **neuf et autonome**, comme le brief 019 l'a établi pour G4. À
partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md` ›
Single Source of Instruction).

**Ce que ce lot ne fait pas :** le dépôt porte déjà du code pré-câblé pour deux
lots **ultérieurs et distincts**, tous deux hors de portée ici :

- **G5-bis** (`steps/05b_navigability_1400.py`, absent, hook
  `run_navigability_g5b` déjà câblé) — surcharges déclaratives de navigabilité
  (par exemple promouvoir un fleuve indéterminé en navigable sur preuve
  historique datée). C'est *après* G5, sur les sorties de G5.
- **G5-ter** (`steps/05c*.py`, absent, contrôles `g5cter_*` déjà câblés dans
  `qa/checks.py`) — fusion de la couche `ne_10m_rivers_europe`. **Cette couche
  n'est pas déclarée dans `sources.lock`** (vérifié : seule
  `ne_10m_rivers_lake_centerlines`, dans `10m_physical.zip`, y figure ; aucune
  entrée `ne_10m_rivers_europe` n'existe dans `sources.lock`'s
  `layer_coverage`). G5-ter est donc non seulement hors de portée mais
  **non exécutable en l'état** — un futur brief devra d'abord sourcer cette
  couche avant de pouvoir l'exécuter.

Ni l'un ni l'autre n'est traité ici. Ce lot ne livre que `steps/05_rivers.py`
et ce que `run_g5_green` vérifie.

---

## World-Terms Requirement

**Chaîne causale.**

Un fleuve n'est pas un trait décoratif sur la carte. Il fait trois choses
physiquement distinctes, et ce lot les distingue parce que le monde en aura
besoin plus tard :

1. **Il transporte.** Une péniche chargée de grain descend un fleuve
   navigable plus vite et moins cher qu'une charrette sur une route de terre —
   c'est un fait de monde que `sim/` devra un jour exploiter pour le commerce
   et la logistique. Un ruisseau saisonnier ne transporte rien de comparable.
2. **Il sépare.** Une cellule et sa voisine peuvent se toucher par la terre
   (arête `land-land` héritée de G4) tout en étant coupées par un fleuve : les
   deux rives ne communiquent pas comme si le fleuve n'existait pas. C'est
   pourquoi ce lot **enrichit** les arêtes `land-land` de G4, il ne les
   recrée pas.
3. **Il permet de franchir, ou pas.** Un fleuve navigable qui longe une
   frontière terrestre est un axe de circulation continu (une « artère
   fluviale »). Un fleuve qui traverse ponctuellement une frontière terrestre
   sans être navigable est un obstacle local (un « croisement ») : on le
   franchit à gué ou par un pont, on ne le remonte pas en bateau.

**Interdit** dans ce lot, comme dans le brief 019 : aucun barème, aucun
multiplicateur de commerce, aucun bonus de flotte. Ce lot établit ce qu'un
fleuve **est** et **permet géométriquement** — la valeur économique ou
militaire de cette permission est un lot futur de `sim/`.

**Rien n'est inventé au-delà de ce que Natural Earth encode.** `scalerank` est
un rang cartographique (importance visuelle sur une carte), pas un débit
hydrologique mesuré. Ce lot le déclare comme un **proxy**, exactement comme le
brief 019 a déclaré les noms de mer hérités comme un proxy — jamais comme une
source hydrologique savante.

---

## Vocabulaire (expliqué une fois, dérivé du code lu)

- **tronçon (segment)** : un enregistrement de `ne_10m_rivers_lake_centerlines`
  dans la fenêtre pilote, portant `segment_id`, `geometry` (LineString ou
  MultiLineString), `name`, `featurecla` (par exemple `"Lake Centerline"` pour
  un tronçon de lac), `scalerank` (source) et `navigability` (dérivé, voir D2).
- **navigabilité** : classification en trois classes dérivées de `scalerank`
  (D2) — `navigable`, `indeterminate`, `non_navigable`. Ce n'est **pas** un
  débit réel (constants.py le déclare explicitement : « pas de débit NE »).
- **rattachement (attachment)** : la liste des `cell_id` qu'un tronçon
  traverse réellement, vérifiée par `G5-A` avec une tolérance
  `G5_INTERSECT_EPS_M`.
- **arête enrichie** : une arête `land-land` de `adjacency_g4.json` (lue, pas
  modifiée en place — voir D7) à laquelle ce lot ajoute deux champs :
  `fluvial_artery` (booléen) et `artery_rivers` (liste de tronçons qui la
  traversent). C'est ce que `pipeline.py` appelle « enrichissement arêtes ».
- **artère fluviale (artery)**, **croisement (crossing)**, **mixte (both)** :
  les trois classes disjointes d'arêtes enrichies, tranchées en D3 — la seule
  vraie ambiguïté que le plan du 2026-08-14 a signalée comme non écrite en
  clair dans le code, et que ce brief tranche formellement.
- **embouchure (mouth)** : le point terminal en aval d'un tronçon de fleuve
  (pas un lac), avec la zone de mer adjacente qu'il devrait atteindre —
  vérifiée par `G5-D` via `sea_zone_adjacent_to_river_cells`.
- **fenêtre pilote** : déjà dérivée par `constants.py`
  (`PILOT_WINDOW_LONLAT`) ; ce brief ne la redéfinit pas.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture de ce brief :

- `pipeline/geo/artifacts/cells_g3.json`, `stats_g3.json` : committés,
  lecture seule.
- `pipeline/geo/artifacts/adjacency_g4.json`, `sea_zones_g4.json`,
  `stats_g4.json`, `MANIFEST_g4.json` : committés, lecture seule. **Ce lot ne
  les modifie pas en place** (D7).
- `pipeline/geo/constants.py` porte déjà, pour G5 :
  `G5_NAV_SCALE_NAVIGABLE_MAX = 5`, `G5_NAV_SCALE_NON_NAV_MIN = 9`,
  `G5_RIVER_LAYER = "ne_10m_rivers_lake_centerlines"`,
  `G5_REGISTRY_CREATED = "2026-07-26"`, `G5_NAMED_MAJOR_RIVERS` (9 noms :
  Seine, Loire, Rhône, Garonne, Rhin, Meuse, Escaut, Tamise, Severn),
  `G5_INTERSECT_EPS_M = 1.0`, `G5_SEA_ONLY_FRACTION = 0.95`,
  `G5_MOUTH_SNAP_M = 250.0`.
- `pipeline/geo/qa/checks.py` porte déjà (lignes ~888-1040) : `Q1`
  (`q1_river_geometry_validity(segments)`), `G5-A`
  (`g5a_attachments_match_geometry(segments, attachments, cells_xy, eps=1.0)`),
  `G5-B` (`g5b_no_river_in_open_sea(segments, land_xy, sea_xy,
  sea_only_fraction=0.95)`), `G5-C`
  (`g5c_artery_has_navigable_river(adjacency, segments)`), `G5-D`
  (`g5d_mouth_on_adjacent_sea(mouths)`), assemblés par `run_g5_green(*,
  segments, attachments, cells_xy, land_xy, sea_xy, adjacency, mouths,
  sha_pairs)`. Ces signatures **fixent** les contrats de données exacts que
  `steps/05_rivers.py` doit produire (D4-D6).
- `pipeline/geo/pipeline.py` (lignes ~983-986, ~1105-1118) porte déjà
  `run_rivers_g5()` qui appelle `rivers.run_rivers()` **sans argument**, et le
  contrat de retour exact via la ligne de résumé :
  `result['projection'].epsg`, `result['metrics']['segment_count']`,
  `result['metrics']['navigability_counts']`,
  `result['metrics']['artery_count']`, `result['metrics']['crossing_count']`,
  `result['metrics']['both_count']`, `result['metrics']['mouth_count']`,
  `result['captures']`, `result['shas']`.
- `pipeline/geo/sources.lock` déclare `ne_10m_rivers_lake_centerlines` dans
  `10m_physical.zip` (empreinte `a79cc39162f2…` du zip, licence domaine public
  Natural Earth) — **seule source requise par ce lot**.
- `pipeline/geo/steps/04_adjacency.py` : patron de module à suivre pour la
  structure (chargement dynamique optionnel de `03_cells.py`, export d'une
  fonction `run_*`).

**Ce lot n'est pas un portage octet-à-octet.** Comme pour G4, c'est une
**reconstruction** contre la barre qualité déjà écrite (`qa/checks.py`, les
constantes `G5_*`, le crochet de `pipeline.py`).

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit librement les noms de
fonctions et de variables internes, et l'organisation du code dans le
périmètre autorisé.

### D1 — Entrées exactes

Le nouveau module lit, en lecture seule :

| entrée | clés employées |
|---|---|
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry` |
| `pipeline/geo/artifacts/adjacency_g4.json` | `adjacency[]` filtré sur `kind == "land-land"` — jamais modifié en place (D7) |
| `pipeline/geo/artifacts/sea_zones_g4.json` | `zones[]` : `zone_id`, `geometry`, pour G5-D (adjacence embouchure↔zone) |
| `pipeline/geo/sources/10m_physical.zip` (couche `ne_10m_rivers_lake_centerlines`) | géométrie, `name`, `scalerank`, `featurecla` par tronçon, restreint à la fenêtre pilote |
| `pipeline/geo/constants.py` | toutes les bornes et graines G5, **lues**, jamais recopiées en littéral |
| terre corrigée / mer 1400 | ré-obtenues comme en G4 (D2 du brief 019) via `steps/02b_corrections_1400.py`, chargé dynamiquement, jamais modifié |

Comme pour G4 (brief 019, D2, non rediscuté ici) : la cohérence terre/cellules
est déjà validée par le lot précédent ; ce lot ne revérifie pas l'empreinte du
littoral, il réutilise la terre/mer déjà dérivées par les modules amont.

### D2 — Navigabilité des tronçons : trois classes dérivées de `scalerank`, jamais un débit

Chaque tronçon reçoit un champ `navigability` à trois valeurs, dérivé de
`scalerank` (source Natural Earth) contre les deux bornes déjà déclarées dans
`constants.py` :

- `scalerank <= G5_NAV_SCALE_NAVIGABLE_MAX` (5) → `"navigable"`.
- `scalerank >= G5_NAV_SCALE_NON_NAV_MIN` (9) → `"non_navigable"`.
- entre les deux (6, 7, 8) → `"indeterminate"`.

Preuve que cette bande intermédiaire est un concept réel du projet, pas une
invention de ce brief : le hook `run_navigability_g5b` (G5-bis, lot futur)
rapporte déjà un effet nommé `rivers_indeterminate_to_navigable`
(`pipeline.py` ligne ~1128) — un fleuve ne peut être « promu indéterminé →
navigable » par une surcharge historique que si la classe `indeterminate`
existe déjà en sortie de G5. Ce lot produit donc cette classe ; il ne
l'invente pas, il l'expose pour la première fois.

`metrics.navigability_counts` est un dictionnaire `{"navigable": n,
"indeterminate": n, "non_navigable": n}`, les trois comptes sommant à
`segment_count`.

### D3 — Artère / croisement / mixte : la classification des arêtes enrichies

**C'est la décision que le plan du 2026-08-14 a explicitement demandé de ne
pas laisser au Générateur.** Aucune ligne de code existante ne définit
`crossing_count` ni `both_count` — seul `pipeline.py` (lignes 1112-1113) exige
ces trois clés dans `metrics`, et seul `G5-C`
(`g5c_artery_has_navigable_river`, `qa/checks.py` lignes 974-1001) définit
opérationnellement `fluvial_artery`/`artery_rivers` sur une arête : si
`fluvial_artery` est vrai, `artery_rivers` doit être non vide et contenir au
moins un tronçon dont le `segment_id` est dans l'ensemble des tronçons
`navigable`, **et** au moins un tronçon dont le champ `navigability` embarqué
vaut `"navigable"` (double vérification de cohérence entre les deux
représentations).

Décision, dérivée de ces deux seuls faits de code et de la classification à
trois niveaux de D2 (jamais de seuil inventé) :

Pour chaque arête `land-land` de `adjacency_g4.json`, on calcule l'ensemble
des tronçons dont la géométrie intersecte la frontière partagée de l'arête
(tolérance `G5_INTERSECT_EPS_M`, projetés). Si cet ensemble est vide, l'arête
n'est pas enrichie (ni `fluvial_artery` ni `artery_rivers` ajoutés — elle
reste une arête `land-land` ordinaire de G4). Sinon, l'arête est classée
selon la navigabilité (D2) des tronçons qui la touchent :

| classe | condition | `fluvial_artery` | `artery_rivers` contient |
|---|---|---|---|
| **artery** | tous les tronçons touchant l'arête sont `navigable` | `true` | tous les tronçons touchants (tous navigables) |
| **crossing** | tous les tronçons touchant l'arête sont `indeterminate` et/ou `non_navigable` (aucun `navigable`) | absent ou `false` | absent (l'arête n'est pas une artère) |
| **both** | l'arête est touchée par au moins un tronçon `navigable` **et** au moins un tronçon `indeterminate` et/ou `non_navigable` | `true` | tous les tronçons touchants, navigables et non-navigables mélangés |

Ce découpage satisfait `G5-C` mécaniquement : `fluvial_artery` n'est vrai que
pour `artery` et `both`, et dans les deux cas `artery_rivers` contient au
moins un tronçon `navigable` — la condition exacte que `G5-C` vérifie.
`artery_count + crossing_count + both_count` est **égal** au nombre total
d'arêtes `land-land` touchées par au moins un tronçon (un nouveau compteur,
`aretes_terre_terre_avec_fleuve`, mesuré et rapporté). Les arêtes `land-land`
non touchées par aucun tronçon ne sont comptées dans aucune des trois classes.

Cette lecture est **la meilleure disponible depuis le code existant**, pas une
certitude absolue : aucune ligne ne l'énonce en clair. C'est précisément
pourquoi elle est tranchée ici, par écrit, avec sa preuve textuelle exacte
(citations ci-dessus), plutôt que laissée à l'appréciation du Générateur —
c'était le risque nommé par le plan. Si l'Évaluateur ou le propriétaire jugent
cette lecture fausse au vu du code réellement écrit par le Générateur,
c'est un motif d'escalade (Waivers), pas une correction silencieuse.

### D4 — Rattachement des tronçons aux cellules (contrat `G5-A`)

`attachments` est un dictionnaire `segment_id → [cell_id, …]` : pour chaque
tronçon, la liste des cellules de `cells_g3.json` que sa géométrie traverse
réellement (intersection non vide, longueur ≥ tolérance implicite du contrôle
`G5-A`, `eps=1.0` déjà fixé dans la signature de
`g5a_attachments_match_geometry`). Un tronçon peut être rattaché à plusieurs
cellules (il les traverse successivement) ou à aucune (hors fenêtre pilote
utile — mesuré, pas une erreur en soi).

### D5 — Aucun fleuve en pleine mer (contrat `G5-B`)

Un tronçon dont la fraction de longueur en mer (`sea_xy`) est
`>= G5_SEA_ONLY_FRACTION` (0.95) **et** dont la fraction en terre est
`< 1 - G5_SEA_ONLY_FRACTION` est une erreur de découpe, **sauf** si
`featurecla == "Lake Centerline"` (un tronçon de centre-ligne de lac n'est pas
« en pleine mer », il est dans un lac, hors de la terre ; `G5-B` l'exempte
déjà explicitement — ce lot ne change pas cette exemption). `land_xy` et
`sea_xy` sont les mêmes géométries terre/mer que celles dérivées pour G4 (pas
recalculées différemment).

### D6 — Embouchures (contrat `G5-D`)

Une embouchure est le point terminal aval d'un tronçon qui n'est **pas** de
`featurecla == "Lake Centerline"`, à moins de `G5_MOUTH_SNAP_M` (250 m,
projetés) de la mer (`sea_xy` ou frontière `land_xy`/`sea_xy`). Chaque
embouchure porte `segment_id`, `name`, `sea_zone_id` (la zone de
`sea_zones_g4.json` la plus proche du point terminal) et
`sea_zone_adjacent_to_river_cells` : vrai si cette zone de mer partage une
arête `land-sea` (de `adjacency_g4.json`) avec au moins une cellule que le
tronçon traverse (D4). Un tronçon peut n'avoir aucune embouchure mesurée dans
la fenêtre pilote (il sort du cadre avant d'atteindre la mer) — mesuré, pas
imposé.

### D7 — Sorties exactes, et `adjacency_g4.json` reste intouché

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `artifacts/rivers_g5.json` | les tronçons : `segment_id`, `name`, `geometry`, `scalerank`, `featurecla`, `navigability`, `attachments` (cellules traversées) |
| `artifacts/adjacency_g5.json` | **copie enrichie**, distincte de `adjacency_g4.json` : toutes les arêtes de G4 recopiées telles quelles, plus `fluvial_artery`/`artery_rivers` ajoutés sur les arêtes `land-land` concernées (D3). `adjacency_g4.json` n'est ni réécrit ni modifié en place — c'est un artefact d'un lot précédent déjà committé, en lecture seule (même principe que G4 envers G3, brief 019 D16) |
| `artifacts/mouths_g5.json` | les embouchures (D6) |
| `artifacts/stats_g5.json` | `segment_count`, `navigability_counts`, `artery_count`, `crossing_count`, `both_count`, `aretes_terre_terre_avec_fleuve`, `mouth_count`, comptes des 9 fleuves nommés de `G5_NAMED_MAJOR_RIVERS` trouvés/non trouvés dans la fenêtre pilote |
| `artifacts/MANIFEST_g5.json` | version, projection, `inputs` (empreintes calculées à l'exécution : `adjacency_g4.json`, `sea_zones_g4.json`, `cells_g3.json`, la couche source), `outputs` (empreintes des sorties) |
| `registry/river_registry.json` | registre des tronçons émis, date `G5_REGISTRY_CREATED` |
| `logs/v1_060_qa.json` | rapport : tableau `checks` (6 entrées, `passed` + `red_proof`) + `determinism.sha256` |
| `logs/v1_060_rivers.log` | journal lisible de la preuve |
| `capture/v1_060_rivers_window.png` | les tronçons classés (navigable/indéterminé/non-navigable) sur la fenêtre pilote |
| `capture/v1_060_artery_crossing_both.png` | zoom sur un secteur montrant les trois classes d'arêtes enrichies (D3) — captures **regardées et décrites** dans le journal (règle n° 11, comme le brief 019 l'exige pour ses propres captures) |
| `steps/05_rivers.py` | le nouveau module (exporte `run_rivers()`, sans argument — contrat déjà fixé par `pipeline.py`) |
| `tests/test_qa_red_g5.py` | cas rouges, un par contrôle (D9) |
| `tests/run_proof_g5.py` | script de preuve (D8) |
| `README.md` | mise à jour (SC6) |

**Deux couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json` :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md`
   publié.
2. `artifacts/adjacency_g4.json` (référence, non modifié) ↔
   `artifacts/adjacency_g5.json` (copie enrichie) — la preuve mécanique que
   l'enrichissement a produit un fichier réellement différent, pas une
   simple recopie.

### D8 — Déterminisme : deux passes, empreintes comparées

`tests/run_proof_g5.py` :

1. charge une fois la terre/mer 1400, les cellules G3, l'adjacence G4 et la
   couche source ;
2. exécute la dérivation **et l'export complet** deux fois ;
3. compare, empreinte par empreinte, les artefacts des deux passes (`Q10`) :
   chaque paire doit être égale et non vide ;
4. écrit `logs/v1_060_qa.json` et `logs/v1_060_rivers.log` ;
5. rend le code de sortie 0 si et seulement si les six contrôles de
   `run_g5_green` sont verts, chacun avec une preuve rouge non vide, et les
   deux passes identiques.

Aucune horloge murale, aucun horodatage courant dans un artefact.

### D9 — Preuve rouge d'abord

`tests/test_qa_red_g5.py` fournit **un cas rouge par contrôle** des six
assemblés par `run_g5_green` : `Q1`, `Q10`, `G5-A`, `G5-B`, `G5-C`, `G5-D`.
Chaque cas est une mutation locale explicite sur une copie en mémoire (par
exemple une géométrie de tronçon invalidée pour `Q1` ; une arête
`fluvial_artery=true` sans `artery_rivers` pour `G5-C` ; une embouchure dont
`sea_zone_adjacent_to_river_cells` est forcé à faux pour `G5-D`). Aucun cas ne
passe par une modification de `qa/checks.py`. Un `red_proof` vide vaut échec
du contrôle, même si le vert est vert.

### D10 — Bornes non modifiables ; G5-ter hors de portée constatée

**Interdiction ferme :** aucune valeur de `pipeline/geo/constants.py` n'est
modifiée par ce lot. Une borne inatteignable sur la fenêtre pilote réelle
s'escalade (Waivers), elle ne se déplace pas.

`G5C_EUROPE_LAYER`, `G5C_DEDUP_HAUSDORFF_M`, `G5C_DEDUP_COVERAGE_MIN`
(constants.py, section G5-ter) restent **non consommées** par ce lot : la
couche qu'elles présupposent (`ne_10m_rivers_europe`) n'est pas dans
`sources.lock`. Ce lot ne l'ajoute pas à `sources.lock` (sourcer une nouvelle
couche — téléchargement, licence, empreinte — est un brief dédié, hors
périmètre ici, sur le modèle de l'exclusion G3-ter du brief 019).

### D11 — Preuves committées, malgré `.gitignore`

Même mécanisme que les briefs 002/007a/019 : `pipeline/geo/.gitignore` exclut
`artifacts/`, `logs/`, `capture/`. `git add -f` sur chaque fichier de preuve
déclaré en D7. Décision enregistrée, jamais silencieuse.

### D12 — Périmètre de fichiers

**Autorisé (création ou modification) :**

- `pipeline/geo/steps/05_rivers.py` (nouveau) ;
- `pipeline/geo/tests/run_proof_g5.py`, `pipeline/geo/tests/test_qa_red_g5.py`
  (nouveaux) ;
- `pipeline/geo/README.md` (SC6) ;
- les artefacts, journaux, registre et captures listés en D7 ;
- `harness/queue/briefs/021-geo-fleuves-g5/deliverables/**` ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée).

**Interdit (lecture seule, ou hors périmètre) :** `pipeline/geo/constants.py` ;
`pipeline/geo/qa/checks.py` ; `pipeline/geo/pipeline.py` ;
`pipeline/geo/io_util.py` ; `pipeline/geo/projection.py` ;
`pipeline/geo/steps/02_coastline.py` ; `pipeline/geo/steps/02b_corrections_1400.py` ;
`pipeline/geo/steps/03_cells.py` ; `pipeline/geo/steps/04_adjacency.py` ;
`pipeline/geo/sources.lock` ; `pipeline/geo/sources/**` ;
tous les artefacts et registres G2/G3/G4 déjà committés (y compris
`adjacency_g4.json`, D7) ; `pipeline/geo/.gitignore` ;
`pipeline/geo/tests/*_g2*.py`, `*_g3*.py`, `*_g4*.py` ; tout fichier sous
`sim/` ou `unity/` ; `harness/*.py` ; `harness/pipeline/` ; `architecture/` ;
`docs/adr/**` ; `VISION.md` ; `ROADMAP.md` ; `HANDOFF.md` ; `.github/**` ;
les archives des briefs 001 à 020 ; `steps/05b_navigability_1400.py`,
`steps/05c*.py` (G5-bis/G5-ter, hors de portée, D10).

---

## Success Conditions

### SC1 — Les tronçons existent, valides, classés en trois classes de navigabilité mesurées

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_g5.py
```

- `troncons_valides` : `Q1` vert, tous les tronçons `LineString`/
  `MultiLineString`, géométrie valide.
- `navigability_counts` : les trois classes de D2 somment à `segment_count`,
  chacune mesurée et rapportée (aucun plancher imposé sur leur répartition).
- `fleuves_nommes_trouves` : parmi les 9 noms de `G5_NAMED_MAJOR_RIVERS`,
  le nombre effectivement présent dans la fenêtre pilote — mesuré et
  rapporté, contrôle à l'œil déclaré par `constants.py`, aucun plancher.

### SC2 — Rattachement correct (`G5-A`) et aucun fleuve en pleine mer (`G5-B`)

- `G5-A` vert : tout tronçon rattaché tombe dans les cellules déclarées,
  tolérance `G5_INTERSECT_EPS_M` lue de `constants.py`.
- `G5-B` vert : aucun tronçon (hors centre-lignes de lac) n'a une fraction de
  longueur en mer `>= G5_SEA_ONLY_FRACTION` avec une fraction en terre
  `< 1 - G5_SEA_ONLY_FRACTION`.

### SC3 — Les arêtes sont classées en trois classes disjointes, chacune mesurée sur le monde réel

- `aretes_terre_terre_avec_fleuve` est strictement positif.
- `artery_count`, `crossing_count`, `both_count` (D3) somment **exactement**
  à `aretes_terre_terre_avec_fleuve` — aucun chevauchement, aucune arête
  comptée deux fois.
- `artery_count` est strictement positif : au moins une arête `land-land`
  porte un fleuve navigable continu — la preuve que la classification a
  réellement mordu, pas seulement que le script s'est terminé.
- `G5-C` vert : sur `artifacts/adjacency_g5.json`, toute arête
  `fluvial_artery=true` porte au moins un tronçon dans `artery_rivers` dont
  la navigabilité est `navigable`.
- `adjacency_g4_inchange` vaut 1 : `git status --porcelain` sur
  `pipeline/geo/artifacts/adjacency_g4.json` est vide, **et**
  `artifacts/adjacency_g5.json` diffère de `artifacts/adjacency_g4.json`
  (couple `must_differ_from`, D7).

### SC4 — Les embouchures débouchent sur une zone maritime adjacente (`G5-D`)

- `embouchures_mesurees` est mesuré (peut être 0 si aucun tronçon n'atteint
  la mer dans la fenêtre pilote — fait rapporté, pas supposé positif a
  priori, mais `G5-D` doit rester vert dans tous les cas).
- `G5-D` vert : chaque embouchure a `sea_zone_adjacent_to_river_cells=true`.

### SC5 — Déterminisme sur deux passes, six contrôles verts, chacun mordant

- `paires_sha_determinisme_egales` égal au nombre total de paires du bloc
  `determinism.sha256` de `logs/v1_060_qa.json`, total strictement positif,
  aucune empreinte vide.
- `controles_g5_verts` vaut **6** sur 6.
- `controles_g5_avec_preuve_rouge_non_vide` vaut **6** sur 6 (D9).
- `code_sortie_run_proof_g5` vaut **0**.
- `constantes_g5_inchangees` vaut 1 : `git status --porcelain` sur
  `pipeline/geo/constants.py` vide.

### SC6 — Le contrat du crochet existant est satisfait ; preuves committées ; README sans sur-revendication

Depuis `pipeline/geo/` :

```
../../.venv/bin/python pipeline.py --source rivers
```

- La commande sort code 0 et affiche la ligne de résumé G5 (projection,
  segments, `navigability_counts`, `artery`/`crossing`/`both`, `mouths`).
  `pipeline/geo/pipeline.py` reste **inchangé** :
  `fichiers_partages_modifies` vaut 0 sur les neuf fichiers listés en D12
  (interdit), mesuré par `git status --porcelain`.
- `fichiers_preuve_suivis_par_git` égal au nombre de fichiers de preuve
  déclarés sous `pipeline/geo/` (D7), vérifié par `git ls-files`.
- `README.md` mis à jour : G5 — fleuves — désormais livré ; **restent non
  livrés** G5-bis (navigabilité, `05b`), G5-ter (fusion Europe, `05c`, non
  sourcée — D10), le relief et le climat (`06`), les ressources, les villes
  (`07`) et le reste déjà listé. Aucune sur-revendication : ce lot ne livre
  ni G5-bis ni G5-ter. Un instantané pré-édition est committé sous
  `deliverables/pre-edit/pipeline-geo-README.md.orig` (couple
  `must_differ_from`).
- La suite du harnais reste verte (aucune régression) :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_021` rapporté avec le nombre de tests collectés pour
  dénominateur.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Livrer G5-bis (surcharges de navigabilité), G5-ter (fusion Europe), le
   relief, le climat, les ressources — lots suivants du jalon E1, non
   sourcés ou non tranchés ici. Aucun fichier `05b*`, `05c*`, `06*`, `07*`,
   `08*`, `09*`, `10*` n'est créé.
2. Ajouter `ne_10m_rivers_europe` à `sources.lock` — c'est un brief dédié
   (sourcer une couche, pas seulement l'implémenter), hors périmètre ici.
3. Modifier `adjacency_g4.json`, `sea_zones_g4.json`, `cells_g3.json` ou tout
   autre artefact d'un lot précédent déjà committé. `adjacency_g5.json` est
   une **copie enrichie séparée** (D7).
4. Modifier `pipeline/geo/qa/checks.py`, `pipeline/geo/constants.py` ni
   `pipeline/geo/pipeline.py`.
5. Recopier une valeur hexadécimale d'empreinte dans un test, un document ou
   un commentaire (règle durement acquise n° 12).
6. Employer l'alias nu de l'interpréteur ni un chemin Windows ; sur cette
   machine l'interpréteur est `.venv/bin/python` (règle n° 1).
7. Committer, pousser, créer ou changer de branche. L'orchestrateur seul
   dépose.
8. Réinterpréter silencieusement la classification artère/croisement/mixte
   de D3 sans le documenter : si le Générateur trouve cette lecture
   incompatible avec ce qu'il constate en écrivant le code, c'est une
   escalade (Waivers), pas une réinterprétation tacite.
9. Rapporter un compteur depuis un échantillon vide sans le déclarer comme
   tel (règle n° 8 : la sentinelle du projet pour « non calculé » est `-1`,
   jamais `0` — un zéro mesuré, par exemple zéro embouchure trouvée, est
   légitime et se distingue d'un compteur non calculé).

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

| nom | source | dénominateur |
|---|---|---|
| `segment_count` | tronçons lus de la couche source dans la fenêtre pilote | fait mesuré, > 0 |
| `navigability_counts` | classification D2 de chaque tronçon | `segment_count` (les trois classes somment à ce total) |
| `aretes_terre_terre_avec_fleuve` | arêtes `land-land` de `adjacency_g4.json` touchées par ≥1 tronçon | arêtes `land-land` totales lues de `adjacency_g4.json` |
| `artery_count` / `crossing_count` / `both_count` | classification D3 | `aretes_terre_terre_avec_fleuve` (somme exacte) |
| `embouchures_mesurees` | embouchures dérivées (D6) | fait mesuré, peut être 0 |
| `fleuves_nommes_trouves` | noms de `G5_NAMED_MAJOR_RIVERS` présents dans la fenêtre | 9 (longueur du tuple lu de `constants.py`) |
| `paires_sha_determinisme_egales` | bloc `determinism.sha256` de `logs/v1_060_qa.json` | total de paires, > 0 |
| `controles_g5_verts` | tableau `checks` de `logs/v1_060_qa.json` | 6 |
| `controles_g5_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée | 6 |
| `code_sortie_run_proof_g5` | code de sortie de `tests/run_proof_g5.py` | 1 exécution ; doit valoir 0 |
| `constantes_g5_inchangees` | `git status --porcelain` sur `constants.py` | 1 ; doit valoir 1 |
| `fichiers_partages_modifies` | `git status --porcelain` sur les neuf fichiers interdits (D12) | 9 ; doit valoir 0 |
| `adjacency_g4_inchange` | `git status --porcelain` sur `adjacency_g4.json` | 1 ; doit valoir 1 |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves déclarées (D7) | nombre de preuves déclarées |
| `tests_harness_passed_021` | tests `PASSED` de `harness/tests/` | tests collectés (SKIP Linux/Unity acceptés et déclarés) |

Un script committé sous
`harness/queue/briefs/021-geo-fleuves-g5/deliverables/measure_g5_021.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des artefacts et constantes — jamais une valeur recopiée à la main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée » | `.venv/bin/python -c "import shapely, geopandas, pyproj; print('ok')"` depuis la racine | `ModuleNotFoundError` nommant le module |
| « `adjacency_g4.json` ou `sea_zones_g4.json` ne sont pas lisibles » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g4.json'))"` depuis la racine | `FileNotFoundError` ou équivalent |
| « la classification D3 (artère/croisement/mixte) est incompatible avec ce que produit la géométrie réelle » | le module `steps/05_rivers.py` écrit tel quel, plus la sortie de `../../.venv/bin/python pipeline.py --source rivers` | la sortie réelle montrant l'incohérence (par exemple `artery_count + crossing_count + both_count != aretes_terre_terre_avec_fleuve`) ; **si invoquée**, aucune SC3 n'est excusée — c'est un motif d'escalade vers le propriétaire, pas un contournement du contrôle |
| « aucun fleuve nommé de `G5_NAMED_MAJOR_RIVERS` n'est trouvé dans la fenêtre pilote » | sortie de `tests/run_proof_g5.py` montrant `fleuves_nommes_trouves = 0` | ce n'est **pas** un motif de blocage (aucun plancher n'est exigé, D-constants.py), seulement un constat inscrit dans le journal et le README |

---

## Registre de coût

Une ligne, sans `--audit-id` (ce brief naît de la roadmap, pas d'un audit
converti) :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/021-geo-fleuves-g5 \
  --event generator-run
```
