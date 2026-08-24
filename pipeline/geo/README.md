# pipeline/geo/

> **Archive (ADR-0019).** Ce répertoire n'est plus le produit quotidien.
> G6 est gelé (échec accepté). Plus de suite climat observé / consommation
> R1. `sim/` lit encore les cellules G3 déjà là. Ne pas relancer une
> preuve Europe ni un lot de sauvetage.

Ports VictoriaProject's `sandbox/geo/` map pipeline into ForgeHistory.
Licenses in `sources.lock` (Natural Earth public domain, GeoNames CC BY 4.0,
Copernicus DEM attribution-required) are carried over unchanged — do not redo
that legal work.

## Landed (brief 002)

Shared infrastructure every later step imports:

- `constants.py`, `io_util.py`, `projection.py`, `requirements.txt`, `sources.lock`
- `qa/checks.py` (shared QA module, ported wholesale)
- `legacy_game_data/` — read-only copies of VictoriaProject province
  coordinates/adjacency fixtures (no Unity project tree in this repository)
- Natural Earth `sources/10m_physical.zip`

G2 littoral-1400 cluster:

- `steps/02_coastline.py` — coastline from Natural Earth in the pilot window
- `steps/02b_corrections_1400.py` — declared, reversible 1400-era corrections
- proof scripts `tests/run_proof_g2.py` / `tests/run_proof_g2b.py` and their
  red-case companions

## Landed (brief 007, lot 007a, Amendment 007a-R3) — G3 cells, 13/14 checks green, one open finding

`pipeline.py` (top-level, byte-identical — hard runtime dependency of
`03_cells.py`'s `derive_adjacency()`, which dynamically loads it via
`importlib.util.spec_from_file_location` to reuse `stage_derive()`), plus:

- `steps/03_cells.py` — Voronoi/Poisson cell mesh, ported with exactly one
  marked path adjustment (`CITIES_JSON`/`CITY_COORDS_JSON` now resolve to
  `legacy_game_data/`, marked `# FORGEHISTORY-PATH-ADJUSTMENT`) **plus a
  genuine seeding/construction repair** (marked
  `# FORGEHISTORY-G3-REPAIR`, 196 lines across three amendments) — this is
  not merely a byte-identical port of a degenerate source; the mesh's
  seed-placement logic was diagnosed and repaired against the current
  (post-brief-002) coastline. See "G3 mesh status" below for the full
  before/after across all three amendments.
- `tests/run_proof_g3.py`, `tests/test_qa_red_g3.py` — **byte-identical to
  VictoriaProject, untouched**; the quality bar was not weakened
- `legacy_game_data/cities.json`, `legacy_game_data/city_coordinates.json`
  (read-only, SHA256-equal to VictoriaProject's own recorded
  `MANIFEST_g3.json`/`MANIFEST_g4.json` `inputs` target hashes) — the
  declared, non-optional input to G3's seed-density field `r(x)`

**Legacy game-data decisions recorded (per brief 007's table), all five
files named explicitly, none silently skipped:**

| file | decision | status this lot |
|---|---|---|
| `cities.json` | copy byte-identical | done, SHA target-matched |
| `city_coordinates.json` | copy byte-identical | done, SHA target-matched |
| `sea_zones.json` | copy byte-identical | **not this lot** — lot 007b |
| `province_adjacency.json` | reuse brief 002's copy, re-verified unchanged | re-verified this session, SHA unchanged since 002 |
| `province_coordinates.json` | reuse brief 002's copy, re-verified unchanged | re-verified this session, SHA unchanged since 002 |

### G3 mesh status (Amendment 007a-R, 007a-R2, 007a-R3)

Amendment 007a-R fixed real seeding defects (a coastline sliver counted as
a land mass, and Bridson running after rather than before urban anchors,
which starved low-density land masses of subdivision). Amendment 007a-R2
re-derived `G3_SEED_COUNT_MAX` from 400 to 600 against the current
coastline's real land-part structure, after a rigorous per-mass pigeonhole
proof showed 400 was 45+ cells short of even the theoretical zero-waste
minimum at the (then) 15,000 km2 area ceiling.

**Amendment 007a-R3 (current, owner Option 2) recalibrated
`G3_AREA_CEIL_KM2` from 15,000 to 40,000 km2** -- a real-world, country-
scale anchor (roughly the Netherlands' own area), chosen because a
further, stronger per-land-part pigeonhole proof showed even 600 seeds at
the old 15,000 km2 ceiling needed roughly 645-900 cells at real
Voronoi/Bridson overhead, well beyond the brief's own declared
~150-600-cell design grain. The owner chose to relax the per-cell area
ceiling specifically for genuinely low-interest, low-density periphery
(the Maghreb's Saharan-fringe masses) rather than double the whole map's
cell count. Cell size only, never cell shape -- `G3_COMPACTNESS_MIN`
(0.18) and `G3_AREA_MAX_MEDIAN_RATIO` (8.0) are untouched.

Current measured state (`cell_count=596`, within `[150,600]`, same
coastline, same master seed, six of the seven other bound constants
verified unchanged, `G3_SEED_COUNT_MAX=600` unchanged):

- `G3-A` through `G3-D`, `G3-H`, `Q1`-`Q4`, `Q10`, `G2b-B` -- all
  `passed: true`, non-empty `red_proof`.
- **`G3-E` (per-cell area ceiling) is now genuinely `passed: true`** --
  `max=37,217.8` km2, comfortably under the new 40,000 km2 ceiling, exactly
  the structural outcome the amendment's own derivation predicted. This
  was the primary problem Amendment 007a-R3 set out to fix, and it is
  fixed.
- `G3-F` (max/median area ratio) remains `passed: true` -- `ratio=3.838`
  against `ceil=8.0`.
- **`G3-G` (compactness floor 0.18) remains `passed: false`** -- 21 of 393
  non-island cells below the floor (singleton islands are exempt, per the
  check's own logic). This is an honest, currently unresolved gap, not a
  glossed-over one: a wide, reproducible seeding-repair experiment (7
  distinct configurations, real before/after numbers, see
  `deliverables/generator-log.md`'s "Lot 007a-R3" section) found that
  every attempt to close it via more seeding either made no improvement or
  made `G3-G` measurably worse (up to 29-49 violations), because the 21
  residual offenders concentrate in genuinely fractal coastal geography
  (Norwegian fjords, Scottish west-coast Highlands, Aegean islands) where
  one more seed frequently creates a new small sliver cell about as often
  as it fixes the offending one. Recorded as an open finding for the
  Planificateur -- not self-granted as a pass.
- Two-run determinism holds: 6/6 SHA pairs matched, non-empty.
- **Result: 13/14 checks green and red-proven** (`run_proof_g3.py` exits
  1).

`tests/run_proof_g3.py`, `tests/test_qa_red_g3.py`, `qa/checks.py` remain
byte-identical to VictoriaProject (SHA-verified, 3/3); the six remaining
frozen G3 acceptance-bound constants are unchanged in value (verified,
6/6); `G3_SEED_COUNT_MAX=600` unchanged; `G3_AREA_CEIL_KM2=40,000` is the
one deliberate, marked, derived change this amendment authorizes.

`game_unity`/`StreamingAssets` audit: `03_cells.py` (unmodified beyond the
marked path adjustment and the repair, neither of which touches the
`RADIUS_FIELD["sources"]` metadata list at lines ~108-109 or the docstring
at line ~179) carries the same 3 pre-existing string-literal hits as the
original port. Per Amendment 007a-R's Amended Success Condition 6, these
three lines plus `constants.py`'s two existing `FORBIDDEN_GAME_PATH_MARKERS`
literals are now a **permanent, unconditional exception** (independent of
whether a repair happens to touch them — it did not).
`game_unity_reference_remaining_count` = **0** after this five-literal
exclusion (raw hits = 5, all excluded). See
`harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/manifest.json`
for the full accounting.

## Livré (brief 019) — G4 : zones de mer et adjacence typée

*Section rédigée en français clair, conformément à `CLAUDE.md` › « Langue et
clarté ». Elle décrit ce qui existe et ce que cela lit.*

### D'où viennent les noms de mer, et ce qu'ils ne sont pas

Cette déclaration est écrite **avant** tout chiffre mesuré de ce lot.

- Les noms de mer sont **lus** de `legacy_game_data/sea_zones.json`, copie
  octet pour octet du fichier de données du jeu Unity
  (`unity/game_unity/Assets/StreamingAssets/data/sea_zones.json`, jamais
  modifié). L'égalité des deux fichiers est établie en calculant les deux
  empreintes SHA256 à l'exécution ; aucune valeur hexadécimale n'est recopiée
  nulle part.
- Ce sont des **données héritées du jeu**. Ce n'est ni une source savante, ni
  une reconstitution d'époque, ni un tracé de frontières maritimes de 1400.
  Aucun nom n'est inventé, traduit ni complété.
- Le tableau d'identifiants riverains que chaque nom hérité porte est un
  **proxy de localisation de nom**, et rien d'autre. Ce n'est jamais une clé
  spatiale : il n'entre dans aucun artefact exporté, il ne définit aucune
  appartenance, et la seule clé spatiale du projet reste la cellule
  (ADR-0003).
- **Règle d'attribution employée** : pour chaque nom hérité, un point
  d'ancrage est calculé comme la moyenne des coordonnées (lon, lat) des
  identifiants riverains qu'il déclare, ces coordonnées étant lues de
  `legacy_game_data/province_coordinates.json`, puis projetées. Chaque zone de
  mer prend le nom de l'ancrage **le plus proche** de sa géométrie.
- **Départage des égalités de distance** : le plus petit identifiant hérité
  gagne.
- Conséquences assumées : plusieurs zones peuvent porter le **même** nom (une
  mer nommée est plus grande qu'une zone), et un nom hérité peut n'être employé
  par **aucune** zone. Ces deux nombres sont mesurés et rapportés ; aucun
  plancher n'est exigé et l'attribution n'est pas ajustée pour employer tous
  les noms.

### Ce que ce lot ajoute

- `steps/04_adjacency.py` — dérive les zones de mer de 1400 et le graphe
  d'adjacence typé ; expose `run_adjacency(apply_topology_links_flag=...)`,
  que le crochet déjà câblé de `pipeline.py` appelle
  (`pipeline.py --source adjacency`). `pipeline.py`, `qa/checks.py`,
  `constants.py` et `steps/03_cells.py` sont lus, jamais modifiés.
- `tests/run_proof_g4.py` — preuve : les entrées sont chargées une fois, la
  dérivation et l'export complet tournent deux fois liens actifs, une
  troisième fois liens coupés ; les huit contrôles de `run_g4_green` sont
  joués, chacun avec son cas rouge.
- `tests/test_qa_red_g4.py` — un cas rouge par contrôle. Sept sont des
  mutations locales sur des copies en mémoire ; `G4-B` est le **cas naturel** :
  on coupe la déclaration historique, pas la donnée.
- `legacy_game_data/sea_zones.json` — la copie décrite ci-dessus.
- Artefacts : `artifacts/sea_zones_g4.json`, `adjacency_g4.json`,
  `topology_links_g4.json`, `stats_g4.json`, `adjacency_divergence_g4.json`,
  `MANIFEST_g4.json`, `registry/sea_zone_registry.json`.
- Journaux et captures : `logs/v1_050_qa.json`, `logs/v1_050_adjacency.log`,
  `logs/v1_050_g4b_links_on.txt`, `logs/v1_050_g4b_links_off.txt`,
  `capture/v1_050_sea_zones_window.png`,
  `capture/v1_050_zuiderzee_links_on.png`,
  `capture/v1_050_zuiderzee_links_off.png`.

### Ce que le graphe dit du monde

- **La mer est dérivée**, jamais saisie : eau = fenêtre pilote projetée moins
  la terre corrigée de 1400 ; la mer est la composante d'eau qui touche le bord
  de la fenêtre, plus les étendues que les corrections de 1400 reclassent en
  mer ouverte. Un lac reste un lac : il est exclu de la mer et compté.
- **Quatre natures d'arête.** `land-land` est **lu** de
  `artifacts/adjacency_g3.json` (la maille committée reste la source unique) ;
  `land-sea` et `sea-sea` sont dérivés d'une longueur de frontière partagée au
  moins égale à `LENGTH_EPS` ; `strait` relie deux terres **non contiguës**
  séparées d'au plus `G4_STRAIT_MAX_WIDTH_M`, toutes ces bornes étant lues de
  `constants.py`. Chaque arête `strait` porte sa largeur mesurée.
- **La littoralité est dérivée** : une cellule est littorale parce qu'elle
  porte au moins une arête `land-sea`. Elle n'est ni saisie, ni stockée sur la
  cellule. Retirer l'eau retire la littoralité.
- **La continuité historique est déclarée, pas dessinée.** Le Zuiderzee et la
  Lauwerszee sont enfermés dans la géométrie moderne (Afsluitdijk 1932,
  fermeture de 1969). Le trait de côte n'est pas percé : chaque correction
  `declare_topology_link` de `data/corrections_1400.json` produit une arête
  `sea-sea` marquée, qui porte l'identifiant de la correction, sa source, sa
  date et sa certitude. Sans ces déclarations, les deux bassins redeviennent
  injoignables depuis la mer extérieure — c'est exactement ce que montre le
  couple `logs/v1_050_g4b_links_on.txt` / `logs/v1_050_g4b_links_off.txt` et
  le couple de captures du Zuiderzee.
- **Aucun barème.** Ce lot ne dit ni gain ni malus : il dit ce que l'eau permet
  et ce qu'elle empêche.

### `artifacts/adjacency_divergence_g4.json` — comparaison QA, jamais une autorité

Ce fichier est la **seule** confrontation du graphe terre-terre dérivé au
graphe hérité du jeu (`legacy_game_data/province_adjacency.json`). Il porte
`"qa_only": true` dans son propre contenu. Il n'est lu que par
`tests/run_proof_g4.py`, aucun autre artefact ne le consomme, il ne fonde
aucune appartenance et il n'est jamais une autorité spatiale. C'est aussi le
seul artefact G4 où la sous-chaîne `province` a le droit d'apparaître : les six
autres en contiennent zéro, et ce zéro est mesuré, pas supposé.

Les trois nombres qu'il rend (arêtes héritées confirmées, contredites,
manquantes) sont des **constats**, pas des objectifs : aucun seuil ne leur est
appliqué et le graphe dérivé n'est pas ajusté pour ressembler davantage au
graphe hérité.

### État mesuré (`../../.venv/bin/python tests/run_proof_g4.py`, code de sortie 0)

- 40 zones de mer, dans la fourchette `[SEA_ZONE_COUNT_MIN,
  SEA_ZONE_COUNT_MAX]` lue de `constants.py` ; identifiants 5000 à 5039,
  attribués depuis `SEA_ZONE_ID_BASE` **en sautant** les identifiants de
  cellules déjà pris (la maille actuelle en émet de 1175 à 10466, une
  attribution naïve entrerait donc en collision). Collisions mesurées : 0.
- 5 composantes d'eau retenues comme mer, 5 portant au moins une zone, dont 2
  bassins enfermés ; 107 plans d'eau exclus parce que ce sont des lacs.
- 2 085 arêtes : 917 `land-land`, 437 `land-sea`, 63 `sea-sea`, 668 `strait`.
  Aucune arête ne porte encore l'identifiant fourre-tout de mer de G3.
- 372 cellules littorales sur 596 cellules lues, dérivées des arêtes
  `land-sea`.
- 551 détroits relient deux masses terrestres distinctes ; écart minimal mesuré
  297 m, seuil lu 45 000 m.
- 2 déclarations `declare_topology_link` lues, 2 appliquées. Liens actifs :
  aucun bassin enfermé injoignable. Liens coupés : 2 bassins injoignables
  (Zuiderzee et Lauwerszee).
- 40 zones nommées, 13 noms hérités employés sur 14 lus, 1 non employé, 0 nom
  hors de la liste attestée.
- Huit contrôles verts, chacun avec une preuve rouge non vide ; deux passes
  identiques en SHA256 sur 9 fichiers.

### Constats ouverts (non bloquants, aucune borne déplacée)

- **Bornes d'intention de surface et de compacité** : 24 zones sur 40 sortent
  de `G4_SEA_AREA_FLOOR_KM2` / `G4_SEA_AREA_CEIL_KM2` /
  `G4_SEA_COMPACTNESS_MIN`. Ces bornes ne sont pas bloquantes et n'entrent dans
  aucun des huit contrôles. Cause mesurée : la mer retenue fait environ
  5,1 millions de km² pour au plus 40 zones (borne d'acceptation lue), soit une
  surface moyenne très au-dessus du plafond d'intention. Aucune valeur de
  `constants.py` n'a été modifiée ; le constat est inscrit, pas maquillé.
  2 zones sont exemptées du plancher parce qu'elles constituent à elles seules
  un bassin enfermé entier.

## Livré (brief 020) — la terre déclarée par les cellules est la terre produite

*Section rédigée en français clair, conformément à `CLAUDE.md` › « Langue et
clarté ». Elle décrit ce qui existe et ce que cela lit.*

### Le constat d'empreinte du littoral ouvert par 019 est **fermé**

Le brief 019 avait mesuré que `artifacts/MANIFEST_g3.json` déclarait, sous
`inputs.coastline_1400`, une terre que la chaîne ne produisait plus : la
question « quelle terre a produit ces cellules ? » recevait deux réponses, celle
que la chaîne calcule et celle que le manifeste raconte. Ce constat est
désormais fermé, et voici comment.

- **Ce qui a été mesuré avant de toucher quoi que ce soit.** La terre du
  littoral vivant a été comparée à l'union des cellules committées, en
  projection EPSG:3035. La part de l'union des cellules qui sort de la terre est
  nulle, et la terre qu'aucune cellule ne couvre est un résidu très largement
  inférieur à `G3_AREA_EPS_M2`, **lue** de `constants.py`. La terre n'a donc pas
  bougé : ce sont les octets qui la sérialisent qui avaient changé. La surface
  mesurée coïncide avec le `land_area_km2` que porte l'artefact du littoral, ce
  qui écarte une mesure faite sur une géométrie vide.
- **Ce qui a été réparé.** Un seul champ : `inputs.coastline_1400` de
  `artifacts/MANIFEST_g3.json` reçoit l'empreinte du littoral vivant, calculée à
  l'exécution depuis le fichier lui-même — jamais recopiée d'un autre manifeste,
  ce qui ferait réussir la comparaison sans avoir jamais lu la terre. Le bloc
  `outputs` et le `fixed_timestamp` du manifeste ne sont pas touchés.
- **Ce qui n'a pas été rejoué.** La maille des cellules n'a pas été régénérée :
  `artifacts/cells_g3.json`, `adjacency_g3.json`, `stats_g3.json` et
  `registry/cell_registry.json` sont sans modification, donc les identifiants de
  cellule que `sim/` consomme sont ceux du fichier committé, par construction.
  Le graphe G4 n'a pas été rejoué non plus : ni semis de zones, ni recalcul
  d'arêtes, ni compteur de zones « amélioré ».
- **Ce que G4 relit.** `artifacts/MANIFEST_g4.json`
  (`coastline_1400_sha_declared_by_g3`, `coastline_1400_sha_equal`) et
  `artifacts/stats_g4.json` (`coastline_1400_sha_equals_g3_input`) relisent la
  déclaration réparée. Les deux drapeaux sont **dérivés** de la comparaison, pas
  posés à la main.

### Ce que ce lot ajoute

- `steps/03b_align_coastline_provenance.py` — l'alignement de la déclaration,
  lançable seul, idempotent : une seconde exécution ne change aucun octet. Il
  réécrit trois fichiers, dans un ordre contraint (`stats_g4.json` avant
  `MANIFEST_g4.json`, parce que le second déclare l'empreinte du premier).
- `tests/run_proof_coastline_provenance.py` — la garde durable. Elle recalcule
  l'empreinte du littoral vivant à chaque exécution, lit les déclarations depuis
  les manifestes du disque et ne porte aucune valeur attendue en dur. Elle sort
  au code 0 si tout concorde, 1 en nommant les sources en désaccord, 2 si une
  source manque du disque — un code d'absence n'est jamais un écart mesuré.
- `logs/v1_051_provenance.json`, `logs/v1_051_provenance_vert.txt` — le rapport
  et la sortie verte de cette garde sur le dépôt réparé.
- `logs/v1_051_provenance_rouge.txt` — la même garde sous sabotage de la
  déclaration d'entrée, monté dans une copie **hors du dépôt** : elle rougit.
  Le sabotage porte sur la déclaration, jamais sur le code de la garde.

Aucune valeur d'empreinte n'est citée ici, ni dans le code, ni dans les
journaux : les empreintes se comparent à l'exécution et se nomment par leur
source. Aucune valeur de `constants.py` n'a été modifiée. Le jalon E1 n'est pas
clos, et ce lot ne livre ni relief, ni climat, ni ressources, ni fleuves, ni
villes : il rend au monde une seule réponse à la question « quelle terre ? ».

## Livré (brief 021) — G5 fleuves : tronçons, arêtes enrichies, embouchures

*Section rédigée en français clair, conformément à `CLAUDE.md` › « Langue et
clarté ».*

### Ce que « artère fluviale » dit, et ce qu'il ne dit pas

Cette déclaration est écrite **avant** tout chiffre mesuré de ce lot
(amendement 001 du brief 021).

**Il permet d'embarquer, ou il faut le franchir.** Une arête terrestre touchée
par un fleuve **navigable** est une arête où une cargaison peut entrer dans le
réseau fluvial (`artery`) : le fleuve y est praticable en bateau. Une arête
touchée seulement par des fleuves non navigables est un obstacle local
(`crossing`) : on le franchit à gué ou par un pont. Une arête touchée par les
deux (`both`) porte les deux faits.

Ce que cette classification **ne dit pas** : elle ne dit pas que le fleuve
longe la frontière, ni qu'il constitue un corridor de transport continu le long
de cette arête. Mesuré sur le maillage actuel, un fleuve `artery` longe la
frontière partagée sur 3 % de sa longueur au maximum — il la touche, il ne la
suit pas. Un consommateur de `fluvial_artery` peut en conclure « on peut
embarquer ici » ; il ne peut **pas** en conclure « on circule le long de cette
frontière ».

La classification reste celle de D3 (navigabilité seule) ; aucun seuil
géométrique n'est introduit.

### Proxy `scalerank` et proxy `sea_zone_name`

- `navigability` est dérivée de `scalerank` Natural Earth : **proxy
  cartographique** (importance visuelle), jamais un débit hydrologique.
- `sea_zone_name` sur les embouchures est un **proxy hérité de G4** : une
  composante connexe entière porte une seule étiquette grossière (par exemple
  Adriatique + Ionienne + Tyrrhénienne sous « Mer Tyrrhenienne »). Ce n'est
  pas un fait hydrologique local ; le Pô et l'Ofanto peuvent partager ce nom
  sans se jeter dans la même mer réelle.

### Ce que ce lot livre

- `steps/05_rivers.py` — lit `ne_10m_rivers_lake_centerlines` (déclarée dans
  `sources.lock`, archive `10m_physical.zip`) dans la fenêtre pilote, classe
  chaque tronçon en trois navigabilités dérivées de `scalerank` (bornes lues
  de `constants.py`, jamais un débit), rattache chaque tronçon aux cellules
  qu'il traverse, enrichit une **copie** des arêtes `land-land` de
  `adjacency_g4.json` (artère / croisement / mixte selon D3), et dérive les
  embouchures (zone de mer la plus proche + booléen d'adjacence **calculé**).
- `tests/run_proof_g5.py`, `tests/test_qa_red_g5.py` — six contrôles verts
  (`Q1`, `Q10`, `G5-A`, `G5-B`, `G5-C`, `G5-D`), chacun avec une preuve rouge
  non vide ; deux passes déterministes.
- Artefacts : `artifacts/rivers_g5.json`, `adjacency_g5.json`,
  `mouths_g5.json`, `stats_g5.json`, `MANIFEST_g5.json` ;
  `registry/river_registry.json` ; journaux `logs/v1_060_*` ; captures
  `capture/v1_060_rivers_window.png` et
  `capture/v1_060_artery_crossing_both.png`.

`adjacency_g4.json` n'est **pas** modifié : `adjacency_g5.json` est une copie
enrichie distincte. Aucune valeur de `constants.py` n'a été touchée.
`pipeline.py` / `qa/checks.py` restent inchangés (crochet déjà câblé).

### État mesuré (`../../.venv/bin/python tests/run_proof_g5.py`, code 0)

Les comptes exacts vivent dans `artifacts/stats_g5.json` et
`logs/v1_060_qa.json` — ils se relisent, ils ne se recopient pas ici. La
classification artère / croisement / mixte (D3) partitionne exactement les
arêtes `land-land` touchées par au moins un tronçon ; `artery_count` est
strictement positif. Les neuf noms de `G5_NAMED_MAJOR_RIVERS` sont cherchés
dans la fenêtre (contrôle à l'œil, aucun plancher).

### Ce que ce lot ne livre pas

- **G5-bis** (`05b`, surcharges de navigabilité historiques) — lot suivant,
  sur les sorties de G5.
- **G5-ter** (`05c`, fusion `ne_10m_rivers_europe`) — **non sourcée** : cette
  couche n'est pas dans `sources.lock` ; un brief dédié devra d'abord la
  sourcer avant toute exécution.
- Relief et climat (`06`), ressources, villes (`07`) et le reste du jalon E1.

Le jalon E1 n'est pas clos. Ce lot établit ce qu'un fleuve **est** et ce qu'il
**permet géométriquement** — aucun barème commercial ou militaire.

## Livré (brief 024) — G6 relief : altitude, pente, rugosité, barrières et cols

*Section rédigée en français clair, conformément à `CLAUDE.md` › « Langue et
clarté ».*

### Ce que le relief dit, et ce qu'il ne dit pas

- **Altitude, pente, rugosité** : mesurées cellule par cellule depuis le MNT
  Copernicus DEM GLO-90 (tuiles COG déclarées dans `sources.lock`, cache local
  `sources/dem_cache/`, jamais committé). Échantillonnage sur grille régulière
  `G6_SAMPLE_STEP_DEG` ; échantillons hors `[-80 m, 4800 m]` exclus avant toute
  statistique.
- **Barrière (`relief_barrier`)** : une arête `land-land` dont le point de
  franchissement le long de la frontière partagée est **plus haut que les deux**
  centroïdes des cellules qu'elle sépare — pas un simple versant incliné.
- **Col (pass)** : un franchissement par barrière ; nommé s'il tombe à moins de
  20 km d'un des 9 cols de `G6_KNOWN_PASSES`, sinon identifiant neutre
  `g6_derived_<min>_<max>`.
- **Aucun barème** : pas de coût de franchissement, pas de malus de déplacement,
  pas de rendement agricole — faits géographiques mesurés seulement.

### Ce que ce lot livre

- `steps/06_relief.py` — lit `cells_g3.json`, `adjacency_g5.json` et le cache
  DEM vérifié ; produit `cells_relief_g6.json`, `adjacency_g6.json` (copie
  enrichie), `passes_g6.json`, `stats_g6.json`, `MANIFEST_g6.json` ;
  `registry/relief_registry.json`.
- `tools/fetch_dem_tiles.py` — téléchargement idempotent Copernicus (motif S3
  `<stem>/<stem>.tif`) avec vérification SHA256 par tuile et collective avant
  toute lecture d'altitude ; sondage HEAD, téléchargement des tuiles requises,
  régénération explicite du bloc `dem` de `sources.lock`.
- `tools/required_dem_tiles.py` — dérive la liste des tuiles 1°×1° requises
  (D15) sans ouvrir de raster.
- `tests/run_proof_g6.py`, `tests/test_qa_red_g6.py` — six contrôles verts
  (`Q10`, `G6-A` … `G6-E`), chacun avec une preuve rouge non vide ; deux passes
  déterministes.
- Journaux `logs/v1_052_*` ; captures `capture/v1_052_elevation_window.png` et
  `capture/v1_052_barriers_passes.png`.

`adjacency_g5.json` et `cells_g3.json` ne sont **pas** modifiés :
`adjacency_g6.json` est une copie enrichie distincte. Aucune valeur de
`constants.py` n'a été touchée. `pipeline.py` / `qa/checks.py` restent
inchangés (crochet déjà câblé).

### État mesuré

Les comptes exacts vivent dans `artifacts/stats_g6.json` et
`logs/v1_052_qa.json` — ils se relisent, ils ne se recopient pas ici.
`barrier_count` est strictement positif sur la fenêtre pilote ;
`pass_count == barrier_count` exactement.

### Cache et preuve rapide (brief 029)

Le cache historique reste `sources/dem_cache/`. Sur un VPS qui partage le
cache entre plusieurs worktrees, définir `FORGEHISTORY_DEM_CACHE_ROOT` vers
une racine hors du dépôt. Le chemin effectif est alors
`<racine>/<SHA256 complet de sources.lock>/` : une modification du lock ouvre
un nouvel espace au lieu de faire passer des tuiles anciennes pour courantes.
Chaque téléchargement est atomique et verrouillé par le système. Une tuile
incorrecte, absente ou hors lock rend la vérification rouge.

G6 groupe les points par tuile et lit une fenêtre Rasterio par groupe. La
première passe peut figer ses lots d'altitudes sous
`<cache>/measurements/g6/`; la clé lie `sources.lock`, `cells_g3.json`,
`adjacency_g5.json`, le code d'échantillonnage et le pas. La deuxième passe
rejoue ces mesures sans relire les pixels. Les vrais zéros sont stockés comme
des valeurs ; un masque de validité distinct représente nodata.

La sentinelle rapide crée ses minuscules rasters dans un dossier temporaire ;
elle ne lit ni ne télécharge le cache Europe :

```bash
../../.venv/bin/python -m pytest tests/test_g6_acceleration.py -q
```

La certification complète reste `../../.venv/bin/python tests/run_proof_g6.py`
et refuse explicitement de démarrer si le cache verrouillé n'est pas complet.

### Ce que ce lot ne livre pas

- **Climat et ressources** — lots suivants du jalon E1.
- **G7** villes, **G8** possession, **G9** LOD, **G10** textures d'identifiants,
  **A12** apparence/ombrage (consomme `cells_relief_g6.json`).
- **G5-bis / G5-ter** — voir section brief 021 ci-dessus.

## Not yet landed

- G5-bis / G5-ter (`05b` / `05c`), climate (`07+`)
- resources
- cities (`07`), ownership (`08`), LOD (`09`), id textures (`10`)
- whole-chain QA (`qa/run_all.py`, `qa/crs_coherence.py`)

Le brief 021 est le **second lot atomique** du jalon E1 (fondations monde),
après G4 (brief 019) et la provenance littoral (brief 020). Il livre les
fleuves G5, et rien d'autre.

## Livré (brief 025) — C1 déterminants physiques du climat : insolation et continentalité

Ce lot **n'est pas le climat** : il livre les deux déterminants physiques
mesurables sans source externe — l'insolation extraterrestre annuelle et les
durées de jour aux solstices (formule astronomique fixe, sans paramètre libre),
les deux distances à la mer et la zone de mer la plus proche, et les sauts au
littoral dérivés de l'adjacence committée.

**Ce lot ne livre pas** : température, précipitations, humidité, vent, saison de
culture, classification climatique (source absente du dépôt — la valeur
`climate` de `--source` reste réservée). Ni ressources, villes (G7), possession
(G8), LOD (G9), textures d'identifiants (G10), apparence (A12), G5-bis/G5-ter,
ni QA de chaîne complète (G11/G12). Le relief (G6) n'est pas décrit ici.

### Commandes

Depuis `pipeline/geo/` :

```bash
../../.venv/bin/python tests/run_proof_c1.py
../../.venv/bin/python pipeline.py --source climate_drivers
```

### Artefacts et preuves

- `artifacts/cells_climate_drivers_c1.json`, `stats_c1.json`, `MANIFEST_c1.json`
- `registry/climate_drivers_registry.json`
- `logs/v1_080_qa.json` (7 contrôles : Q10 + C1-A..F), `logs/v1_080_climate_drivers.log`
- `capture/v1_080_insolation_window.png`, `capture/v1_080_continentality_window.png`
- `steps/c1_climate_drivers.py`, `qa/checks_c1.py`, `tests/run_proof_c1.py`,
  `tests/test_qa_red_c1.py`

`constants.py` a reçu un bloc C1 en fin de fichier ; `pipeline.py` expose
`--source climate_drivers`. `qa/checks.py` n'a pas été modifié.

### État mesuré (`../../.venv/bin/python tests/run_proof_c1.py`, code 0)

Les comptes exacts vivent dans `artifacts/stats_c1.json` et
`logs/v1_080_qa.json`. Les captures montrent un dégradé nord-sud continu pour
l'insolation et un cœur continental (rouge foncé) nettement séparé des côtes
(jaune pâle) pour la distance centroïde → mer.

Le jalon E1 reste ouvert : après ce lot, la ligne « climat » exige encore une
source climatique choisie par le propriétaire.

## Livré (brief 026) — R1 gisements extractifs déclarés de 1400

Ce lot livre les **gisements extractifs déclarés** — présence, nature de ressource
et classe qualitative de richesse (`mineure`, `notable`, `majeure`) — et rien d'autre.

**Ce lot ne livre pas** : quantité, réserve, tonnage, rendement, intensité, ressource
agricole ou forestière (climat et sol non disponibles), climat proprement dit (C1 ne
livre que les déterminants physiques), villes (G7), possession (G8), LOD (G9), textures
d'identifiants (G10), apparence (A12), G5-bis/G5-ter, QA de chaîne complète (G11/G12).
Le relief (G6) n'est pas décrit ici.

La classe de richesse est un **nom** pris dans un vocabulaire fermé de trois valeurs,
jamais un nombre ni un barème — ni en donnée, ni sur la capture (forme de marqueur ou
libellé, jamais taille ni intensité).

### Commandes

Depuis `pipeline/geo/` :

```bash
../../.venv/bin/python tests/run_proof_r1.py
../../.venv/bin/python pipeline.py --source resources_1400
../../.venv/bin/python pipeline.py --source resources_1400 --no-corrections
```

La passe `--no-corrections` écrit dans un répertoire temporaire et **ne publie pas**
dans `artifacts/`.

### Artefacts et preuves

- `data/resources_1400.json` — déclarations (amorce provisoire, remplaçable sans code)
- `artifacts/resources_1400_r1.json`, `cells_resources_r1.json`, `stats_r1.json`,
  `MANIFEST_r1.json`
- `registry/resource_registry.json`
- `logs/v1_081_qa.json` (8 contrôles : Q10 + R1-A..G), journaux et sorties on/off
- `capture/v1_081_resources_window.png`
- `steps/r1_resources_1400.py`, `qa/checks_r1.py`, `tests/run_proof_r1.py`,
  `tests/test_qa_red_r1.py`

`constants.py` a reçu un bloc R1 en fin de fichier ; `pipeline.py` expose
`--source resources_1400`. `qa/checks.py` n'a pas été modifié.

ADR-0003 (`docs/adr/0003-single-spatial-primary-key.md`) unblocked writing
here; brief 002 landed the shared infra + G2 littoral-1400 cluster; brief
007 lot 007a lands the G3 cell-mesh code and its evidence (proof currently
failing per above, not silently marked green).
