# pipeline/geo/

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
- **Empreinte du littoral de 1400** : l'empreinte de
  `artifacts/coastline_1400.json` régénéré ici est **égale** à celle que
  `artifacts/MANIFEST_g2b.json` déclare comme sortie de l'étape qui le produit,
  et **différente** de celle que `artifacts/MANIFEST_g3.json` déclare comme
  entrée des cellules. Les deux comparaisons sont calculées à l'exécution.
  L'artefact que G3 avait sous la main n'est pas suivi par git et n'existe plus
  dans le dépôt. Aucun artefact G3 n'a été touché ; le constat est escaladé.

## Not yet landed

- rivers (`05` / `05b` / `05c`), relief and climate / Copernicus DEM (`06`)
- resources
- cities (`07`), ownership (`08`), LOD (`09`), id textures (`10`)
- whole-chain QA (`qa/run_all.py`, `qa/crs_coherence.py`)

Le brief 019 est le **premier lot** du jalon E1 (fondations monde) : il livre
l'adjacence maritime, et rien d'autre. Ni le relief, ni le climat, ni les
ressources, ni les fleuves ne sont livrés, et le jalon E1 n'est pas clos. La
mer n'est pas « simulée » : ce lot établit quelle eau touche quelle côte et
quelle eau communique avec quelle eau.

ADR-0003 (`docs/adr/0003-single-spatial-primary-key.md`) unblocked writing
here; brief 002 landed the shared infra + G2 littoral-1400 cluster; brief
007 lot 007a lands the G3 cell-mesh code and its evidence (proof currently
failing per above, not silently marked green).
